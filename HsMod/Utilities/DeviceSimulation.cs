using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using Blizzard.Proto;
using Blizzard.Telemetry;
using Blizzard.Telemetry.WTCG.Client;
using PegasusShared;

namespace HsMod
{


	internal static class DeviceSimulation
	{
		private sealed class DeviceIdentity
		{
			internal string Native;

			internal string Simulated;
		}

		private struct SystemSpec
		{
			internal string OperatingSystem;

			internal string OsVersionShort;

			internal string ProcessorType;

			internal int ProcessorCount;

			internal int ProcessorFrequencyMhz;

			internal int SystemMemoryMb;

			internal int GraphicsDeviceType;

			internal string GraphicsDeviceName;

			internal string GraphicsDeviceVendor;

			internal string GraphicsDeviceVersion;

			internal bool SupportsGeometryShaders;
		}

		private sealed class DeviceEnumValues<T> : AcceptableValueBase where T : struct
		{
			private readonly T fallback;

			internal DeviceEnumValues(T fallback)
				: base(typeof(T))
			{
				this.fallback = fallback;
			}

			public override bool IsValid(object value)
			{
				if (value is T)
				{
					return Enum.IsDefined(typeof(T), value);
				}
				return false;
			}

			public override object Clamp(object value)
			{
				if (!((AcceptableValueBase)this).IsValid(value))
				{
					return fallback;
				}
				return value;
			}

			public override string ToDescriptionString()
			{
				return "# Acceptable values: " + string.Join(", ", Enum.GetNames(typeof(T)));
			}
		}

		private sealed class DeviceModelValues : AcceptableValueBase
		{
			internal DeviceModelValues()
				: base(typeof(string))
			{
			}

			public override bool IsValid(object value)
			{
				return IsValidModel(value as string);
			}

			public override object Clamp(object value)
			{
				if (!((AcceptableValueBase)this).IsValid(value))
				{
					return "HsMod";
				}
				return value;
			}

			public override string ToDescriptionString()
			{
				return "# Non-empty device model; control characters are not allowed.";
			}
		}

		internal const string DefaultModel = "HsMod";

		private static readonly ConditionalWeakTable<Platform, DeviceIdentity> SimulatedPlatforms = new ConditionalWeakTable<Platform, DeviceIdentity>();

		private static string lastWarning;

		private const int SystemFamilyOther = 1;

		private const int SystemFamilyMac = 2;

		private const int SystemFamilyWindows = 3;

		private const int SystemDeviceHandheld = 2;

		private const int SystemGraphicsMetal = 17;

		private const int SystemGraphicsVulkan = 22;

		private const int SystemBatteryFull = 5;


		internal static OSCategory ResolveReportedOperatingSystem(OSCategory nativeOs)
		{
			if (!TryResolve(out var os, out var _, out var _))
			{
				return nativeOs;
			}
			return os;
		}

		internal static DeviceInfo ProjectTelemetryDeviceInfo(DeviceInfo native)
		{
			if (native == null)
			{
				return null;
			}
			if (!TryResolve(out var os, out var screen, out var model))
			{
				return native;
			}
			DeviceInfo val = new DeviceInfo();
			using (MemoryStream memoryStream = new MemoryStream())
			{
				native.Serialize((Stream)memoryStream);
				memoryStream.Position = 0L;
				val.Deserialize((Stream)memoryStream);
			}
			val.ConnectionType_ = native.ConnectionType_;
			val.HasConnectionType_ = native.HasConnectionType_;
			val.Os = (DeviceInfo.OSCategory)os;
			val.Screen = (DeviceInfo.ScreenCategory)screen;
			val.Model = model;
			return val;
		}

		private static bool TryResolveSystemSpec(Utils.DevicePreset preset, out SystemSpec spec)
		{
			spec = default(SystemSpec);
			switch (preset)
			{
			case Utils.DevicePreset.iPad:
				spec.OperatingSystem = "iOS 26.0.1";
				spec.OsVersionShort = "26.0.1";
				spec.ProcessorType = "Apple M5";
				spec.ProcessorCount = 10;
				spec.ProcessorFrequencyMhz = 4400;
				spec.SystemMemoryMb = 12288;
				spec.GraphicsDeviceType = 17;
				spec.GraphicsDeviceName = "Apple M5";
				spec.GraphicsDeviceVendor = "Apple";
				spec.GraphicsDeviceVersion = "Metal";
				spec.SupportsGeometryShaders = false;
				return true;
			case Utils.DevicePreset.iPhone:
				spec.OperatingSystem = "iOS 26.0";
				spec.OsVersionShort = "26.0";
				spec.ProcessorType = "Apple A19 Pro";
				spec.ProcessorCount = 6;
				spec.ProcessorFrequencyMhz = 4260;
				spec.SystemMemoryMb = 12288;
				spec.GraphicsDeviceType = 17;
				spec.GraphicsDeviceName = "Apple A19 Pro";
				spec.GraphicsDeviceVendor = "Apple";
				spec.GraphicsDeviceVersion = "Metal";
				spec.SupportsGeometryShaders = false;
				return true;
			case Utils.DevicePreset.Phone:
				spec.OperatingSystem = "Android OS 16 / API-36";
				spec.OsVersionShort = "16";
				spec.ProcessorType = "Snapdragon 8 Elite Gen 5";
				spec.ProcessorCount = 8;
				spec.ProcessorFrequencyMhz = 4740;
				spec.SystemMemoryMb = 12288;
				spec.GraphicsDeviceType = 22;
				spec.GraphicsDeviceName = "Adreno (TM) 840";
				spec.GraphicsDeviceVendor = "Qualcomm";
				spec.GraphicsDeviceVersion = "Vulkan";
				spec.SupportsGeometryShaders = true;
				return true;
			case Utils.DevicePreset.Tablet:
				spec.OperatingSystem = "Android OS 16 / API-36";
				spec.OsVersionShort = "16";
				spec.ProcessorType = "Dimensity 9400+";
				spec.ProcessorCount = 8;
				spec.ProcessorFrequencyMhz = 3630;
				spec.SystemMemoryMb = 12288;
				spec.GraphicsDeviceType = 22;
				spec.GraphicsDeviceName = "Immortalis-G925";
				spec.GraphicsDeviceVendor = "ARM";
				spec.GraphicsDeviceVersion = "Vulkan";
				spec.SupportsGeometryShaders = true;
				return true;
			case Utils.DevicePreset.HuaweiPhone:
				spec.OperatingSystem = "Android OS 12 / API-31";
				spec.OsVersionShort = "12";
				spec.ProcessorType = "Kirin 9020";
				spec.ProcessorCount = 12;
				spec.ProcessorFrequencyMhz = 2500;
				spec.SystemMemoryMb = 16384;
				spec.GraphicsDeviceType = 22;
				spec.GraphicsDeviceName = "Maleoon 920";
				spec.GraphicsDeviceVendor = "HiSilicon";
				spec.GraphicsDeviceVersion = "Vulkan";
				spec.SupportsGeometryShaders = true;
				return true;
			default:
				return false;
			}
		}

		internal static UnitySystemInfo ProjectUnitySystemInfo(UnitySystemInfo native)
		{
			if (native == null)
			{
				return null;
			}
			Utils.DevicePreset preset = PluginConfig.fakeDevicePreset?.Value ?? Utils.DevicePreset.Default;
			if (!TryResolve(preset, out var os, out var screen, out var model))
			{
				return native;
			}
			UnitySystemInfo val = new UnitySystemInfo();
			using (MemoryStream memoryStream = new MemoryStream())
			{
				native.Serialize((Stream)memoryStream);
				memoryStream.Position = 0L;
				val.Deserialize((Stream)memoryStream);
			}
			val.BatteryStatus = native.BatteryStatus;
			val.HasBatteryStatus = native.HasBatteryStatus;
			val.CopyTextureSupport = native.CopyTextureSupport;
			val.HasCopyTextureSupport = native.HasCopyTextureSupport;
			val.DeviceType = native.DeviceType;
			val.HasDeviceType = native.HasDeviceType;
			val.GraphicsDeviceType = native.GraphicsDeviceType;
			val.HasGraphicsDeviceType = native.HasGraphicsDeviceType;
			val.NpotSupport = native.NpotSupport;
			val.HasNpotSupport = native.HasNpotSupport;
			val.OperatingSystemFamily = native.OperatingSystemFamily;
			val.HasOperatingSystemFamily = native.HasOperatingSystemFamily;
			val.RenderingThreadingMode = native.RenderingThreadingMode;
			val.HasRenderingThreadingMode = native.HasRenderingThreadingMode;
			bool num = (int)os == 3 || (int)os == 4;
			bool flag = TryResolveSystemSpec(preset, out var spec);
			if (flag)
			{
				val.OperatingSystem = spec.OperatingSystem;
			}
			else if ((int)os == 3)
			{
				val.OperatingSystem = "iOS 18.6";
			}
			else if ((int)os == 4)
			{
				val.OperatingSystem = "Android OS 16 / API-36";
			}
			else if ((int)os == 2)
			{
				val.OperatingSystem = "macOS 15.5";
			}
			val.OperatingSystemFamily = (UnitySystemInfo.OperatingSystemFamilyEnum)OsFamilyValue(os);
			if ((int)screen != 4)
			{
				val.DeviceType = (UnitySystemInfo.DeviceTypeEnum)2;
			}
			val.DeviceModel = model;
			val.DeviceName = model;
			if (!string.IsNullOrEmpty(native.DeviceUniqueIdentifier))
			{
				val.DeviceUniqueIdentifier = GetUniqueDeviceId(native.DeviceUniqueIdentifier, os, screen, model);
			}
			if (num)
			{
				val.SupportsVibration = true;
				val.SupportsGyroscope = true;
				val.SupportsAccelerometer = true;
				val.SupportsLocationService = true;
				val.BatteryLevel = 1f;
				val.BatteryStatus = (UnitySystemInfo.BatteryStatusEnum)5;
			}
			if (flag)
			{
				val.ProcessorType = spec.ProcessorType;
				val.ProcessorCount = spec.ProcessorCount;
				val.ProcessorFrequency = spec.ProcessorFrequencyMhz;
				val.SystemMemorySize = spec.SystemMemoryMb;
				val.GraphicsDeviceType = (UnitySystemInfo.GraphicsDeviceTypeEnum)spec.GraphicsDeviceType;
				val.GraphicsDeviceName = spec.GraphicsDeviceName;
				val.GraphicsDeviceVendor = spec.GraphicsDeviceVendor;
				val.GraphicsDeviceVersion = spec.GraphicsDeviceVersion;
				val.GraphicsMemorySize = spec.SystemMemoryMb;
				val.GraphicsShaderLevel = 50;
				val.GraphicsUVStartsAtTop = true;
				val.UsesLoadStoreActions = true;
				val.UsesReversedZBuffer = true;
				val.SupportsGeometryShaders = spec.SupportsGeometryShaders;
				val.SupportsRayTracing = false;
			}
			return val;
		}

		private static int OsFamilyValue(OSCategory os)
		{
			if ((int)os == 2)
			{
				return 2;
			}
			if ((int)os == 1)
			{
				return 3;
			}
			return 1;
		}

		internal static void BindSettings(ConfigFile config)
		{
			PluginConfig.fakeDevicePreset = Bind(config, "fakeDevicePreset", Utils.DevicePreset.Default, (AcceptableValueBase)(object)new DeviceEnumValues<Utils.DevicePreset>(Utils.DevicePreset.Default));
			PluginConfig.fakeDeviceOs = Bind<OSCategory>(config, "fakeDeviceOs", (OSCategory)1, (AcceptableValueBase)(object)new DeviceEnumValues<OSCategory>((OSCategory)1));
			PluginConfig.fakeDeviceScreen = Bind<ScreenCategory>(config, "fakeDeviceScreen", (ScreenCategory)4, (AcceptableValueBase)(object)new DeviceEnumValues<ScreenCategory>((ScreenCategory)4));
			PluginConfig.fakeDeviceName = Bind(config, "fakeDeviceName", "HsMod", (AcceptableValueBase)(object)new DeviceModelValues());
		}

		private static ConfigEntry<T> Bind<T>(ConfigFile config, string key, T value, AcceptableValueBase acceptable)
		{
			return config.Bind<T>(LocalizationManager.GetLangValue(key + ".label"), LocalizationManager.GetLangValue(key + ".name"), value, new ConfigDescription(LocalizationManager.GetLangValue(key + ".description"), acceptable, Array.Empty<object>()));
		}

		internal static bool IsCustomSetting(string key)
		{
			if (!(key == "fakeDeviceOs") && !(key == "fakeDeviceScreen"))
			{
				return key == "fakeDeviceName";
			}
			return true;
		}

		internal static bool CanEdit(string key)
		{
			if (IsCustomSetting(key))
			{
				ConfigEntry<Utils.DevicePreset> fakeDevicePreset = PluginConfig.fakeDevicePreset;
				if (fakeDevicePreset == null)
				{
					return false;
				}
				return fakeDevicePreset.Value == Utils.DevicePreset.Custom;
			}
			return true;
		}

		internal static bool IsValidModel(string model)
		{
			if (string.IsNullOrWhiteSpace(model))
			{
				return false;
			}
			for (int i = 0; i < model.Length; i++)
			{
				if (char.IsControl(model[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool TryResolve(out OSCategory os, out ScreenCategory screen, out string model)
		{
			return TryResolve(PluginConfig.fakeDevicePreset?.Value ?? Utils.DevicePreset.Default, out os, out screen, out model);
		}

		private static bool TryResolve(Utils.DevicePreset preset, out OSCategory os, out ScreenCategory screen, out string model)
		{
			os = (OSCategory)1;
			screen = (ScreenCategory)4;
			model = null;
			switch (preset)
			{
			case Utils.DevicePreset.iPad:
				os = (OSCategory)3;
				screen = (ScreenCategory)3;
				model = "iPad17,4";
				return true;
			case Utils.DevicePreset.iPhone:
				os = (OSCategory)3;
				screen = (ScreenCategory)1;
				model = "iPhone18,2";
				return true;
			case Utils.DevicePreset.Phone:
				os = (OSCategory)4;
				screen = (ScreenCategory)1;
				model = "samsung SM-S948B";
				return true;
			case Utils.DevicePreset.Tablet:
				os = (OSCategory)4;
				screen = (ScreenCategory)3;
				model = "samsung SM-X930";
				return true;
			case Utils.DevicePreset.HuaweiPhone:
				os = (OSCategory)4;
				screen = (ScreenCategory)1;
				model = "HUAWEI LMU-LX9";
				return true;
			case Utils.DevicePreset.Custom:
				if (PluginConfig.fakeDeviceOs == null || PluginConfig.fakeDeviceScreen == null || PluginConfig.fakeDeviceName == null)
				{
					return false;
				}
				os = (OSCategory)(int)PluginConfig.fakeDeviceOs.Value;
				screen = (ScreenCategory)(int)PluginConfig.fakeDeviceScreen.Value;
				model = PluginConfig.fakeDeviceName.Value;
				if (Enum.IsDefined(typeof(OSCategory), os) && Enum.IsDefined(typeof(ScreenCategory), screen))
				{
					return IsValidModel(model);
				}
				return false;
			default:
				return false;
			}
		}

		internal static void Apply(Platform platform)
		{
			if (platform == null || PluginConfig.fakeDevicePreset == null || PluginConfig.fakeDevicePreset.Value == Utils.DevicePreset.Default)
			{
				lastWarning = null;
				return;
			}
			if (!TryResolve(out var os, out var screen, out var model))
			{
				Warn("设备模拟配置无效，保留原生设备信息。");
				return;
			}
			string uniqueDeviceIdentifier = platform.UniqueDeviceIdentifier;
			if (string.IsNullOrEmpty(uniqueDeviceIdentifier))
			{
				Warn("原生设备标识尚未就绪，保留原生设备信息。");
				return;
			}
			try
			{
				string uniqueDeviceId = GetUniqueDeviceId(uniqueDeviceIdentifier, os, screen, model);
				SimulatedPlatforms.Remove(platform);
				SimulatedPlatforms.Add(platform, new DeviceIdentity
				{
					Native = uniqueDeviceIdentifier,
					Simulated = uniqueDeviceId
				});
				platform.Os = (int)os;
				platform.Screen = (int)screen;
				platform.Name = model;
				platform.UniqueDeviceIdentifier = uniqueDeviceId;
				lastWarning = null;
			}
			catch (Exception ex)
			{
				Warn("设备模拟失败，保留原生设备信息: " + ex.GetType().Name);
			}
		}

		internal static void PreservePurchaseIdentity(Platform platform, string deviceId)
		{
			if (platform != null && SimulatedPlatforms.TryGetValue(platform, out var value) && deviceId == value.Native && platform.UniqueDeviceIdentifier == value.Simulated)
			{
				platform.UniqueDeviceIdentifier = value.Native;
				SimulatedPlatforms.Remove(platform);
			}
		}

		private static string GetUniqueDeviceId(string nativeId, OSCategory os, ScreenCategory screen, string model)
		{
			string s = (((int)os == 4) ? "HsModeD_" : "HsModeD") + nativeId + os.ToString() + screen.ToString() + model;
			byte[] bytes = Encoding.Default.GetBytes(s);
			if ((int)os == 1)
			{
				return Crypto.SHA1.Calc(bytes);
			}
			byte[] array;
			using (MD5 md5 = MD5.Create())
			{
				array = md5.ComputeHash(bytes);
			}
			StringBuilder stringBuilder = new StringBuilder(32);
			byte[] array2 = array;
			foreach (byte b in array2)
			{
				stringBuilder.Append(b.ToString("x2"));
			}
			return ((int)os == 4) ? stringBuilder.ToString() : new Guid(stringBuilder.ToString()).ToString("D").ToUpperInvariant();
		}

		private static void Warn(string message)
		{
			if (!(message == lastWarning))
			{
				lastWarning = message;
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, message);
			}
		}
	}
}
