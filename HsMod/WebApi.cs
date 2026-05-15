using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HsMod.PluginConfig;

namespace HsMod
{
    public class WebApi
    {
        private sealed class SkinCatalogItem
        {
            public string type { get; set; }
            public string configKey { get; set; }
            public string configName { get; set; }
            public int id { get; set; }
            public string cardStringId { get; set; }
            public string name { get; set; }
            public string heroClass { get; set; }
            public string heroType { get; set; }
            public int petId { get; set; }
            public int cardId { get; set; }
            public bool enabled { get; set; }
            public bool owned { get; set; }
            public bool isDefault { get; set; }
            public string imageType { get; set; }
            public string detailsTexture { get; set; }
            public string detailsMovie { get; set; }
            public string prefab { get; set; }
            public string description { get; set; }
        }

        private sealed class SkinCatalogResponse
        {
            public bool success { get; set; }
            public string type { get; set; }
            public Dictionary<string, List<SkinCatalogItem>> groups { get; set; }
            public Dictionary<string, string> configKeys { get; set; }
            public Dictionary<string, string> configNames { get; set; }
            public string cacheKey { get; set; }
            public string generatedAt { get; set; }
            public int cacheTtlSeconds { get; set; }
            public long elapsedMs { get; set; }
            public string message { get; set; }
        }

        private sealed class PackCatalogItem
        {
            public int id { get; set; }
            public string name { get; set; }
            public int opened { get; set; }
            public int remaining { get; set; }
            public bool isCatchupPack { get; set; }
            public string packOpeningPrefab { get; set; }
            public bool nativeMassOpenable { get; set; }
            public int nativeMassPackLimit { get; set; }
        }

        private sealed class PackCatalogResponse
        {
            public bool success { get; set; }
            public int totalOpened { get; set; }
            public int totalRemaining { get; set; }
            public bool nativeMassPackOpeningEnabled { get; set; }
            public bool nativeMassCatchupPackOpeningEnabled { get; set; }
            public int nativeMassHooverChunkSize { get; set; }
            public List<PackCatalogItem> packs { get; set; }
            public string cacheKey { get; set; }
            public string generatedAt { get; set; }
            public int cacheTtlSeconds { get; set; }
            public long elapsedMs { get; set; }
            public string message { get; set; }
        }

        private sealed class ConfigCatalogItem
        {
            public string fieldName { get; set; }
            public string key { get; set; }
            public string section { get; set; }
            public string group { get; set; }
            public string value { get; set; }
            public string defaultValue { get; set; }
            public string settingType { get; set; }
            public string description { get; set; }
            public string acceptableValues { get; set; }
            public string[] options { get; set; }
            public string[] tags { get; set; }
            public bool writable { get; set; }
            public bool hidden { get; set; }
        }

        private sealed class ConfigCatalogResponse
        {
            public bool success { get; set; }
            public Dictionary<string, string> values { get; set; }
            public Dictionary<string, string> fieldNames { get; set; }
            public List<ConfigCatalogItem> items { get; set; }
            public string cacheKey { get; set; }
            public string generatedAt { get; set; }
            public int cacheTtlSeconds { get; set; }
            public long elapsedMs { get; set; }
            public string message { get; set; }
        }

        private sealed class ApiCacheEntry
        {
            public string json { get; set; }
            public DateTime expiresAtUtc { get; set; }
        }

        private sealed class CacheBuildResult
        {
            public string json { get; set; }
            public bool cacheable { get; set; }
        }

        private static readonly Dictionary<string, string> SkinConfigKeyByType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "hero", "skinHero" },
            { "opposingHero", "skinOpposingHero" },
            { "cardBack", "skinCardBack" },
            { "coin", "skinCoin" },
            { "board", "skinBoard" },
            { "bgsBoard", "skinBgsBoard" },
            { "bgsFinisher", "skinBgsFinisher" },
            { "bob", "skinBob" },
            { "pet", "skinPet" },
            { "opposingPet", "skinOpposingPet" }
        };

        private static readonly Dictionary<string, string> SkinConfigNameByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "skinHero", "英雄" },
            { "skinOpposingHero", "对手英雄" },
            { "skinCardBack", "卡背" },
            { "skinCoin", "硬币" },
            { "skinBoard", "对战面板" },
            { "skinBgsBoard", "酒馆战斗面板" },
            { "skinBgsFinisher", "酒馆击杀特效" },
            { "skinBob", "鲍勃" },
            { "skinPet", "宠物" },
            { "skinOpposingPet", "对手宠物" }
        };

        private static readonly object ApiCacheLock = new object();
        private static readonly Dictionary<string, ApiCacheEntry> ApiCache = new Dictionary<string, ApiCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan SkinCatalogCacheTtl = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan PackCatalogCacheTtl = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan ConfigCatalogCacheTtl = TimeSpan.FromSeconds(2);

        public static async Task<string> RunShellCommandAsync(string command)
        {
            if (!isWebshellEnable.Value)
            {
                return string.Empty;
            }

            var processInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if ((Environment.OSVersion.Platform == PlatformID.MacOSX) || (Environment.OSVersion.Platform == PlatformID.Unix))
            {
                processInfo.FileName = "/bin/sh";
                processInfo.Arguments = $"-c \"{command}\"";
            }
            else
            {
                processInfo.FileName = "cmd.exe";
                processInfo.Arguments = "/C chcp 65001 & " + command;
            }

            using (var process = new Process { StartInfo = processInfo })
            {
                var outputBuilder = new StringBuilder();
                var tcs = new TaskCompletionSource<bool>();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                        tcs.TrySetResult(true);
                    else
                        outputBuilder.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                        tcs.TrySetResult(true);
                    else
                        outputBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var processTask = Task.Run(() =>
                {
                    process.WaitForExit();
                    tcs.TrySetResult(true);
                });

                var completedTask = await Task.WhenAny(processTask, Task.Delay(TimeSpan.FromSeconds(5)));
                if (completedTask == processTask)
                {
                    return outputBuilder.ToString();
                }

                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                }
                return string.Empty;
            }
        }

        public static int UpdateHsSkinsCfg(string content, out string res)
        {
            res = string.Empty;

            try
            {
                string sanitizedContent = SanitizeHsSkinsCfg(content);
                File.WriteAllText(Path.Combine(BepInEx.Paths.ConfigPath, "HsSkins.cfg"), sanitizedContent);
                LoadSkinsConfigFromFile();
                InvalidateApiCache("skins");
                res = WebPage.HsModCfgPage("HsSkins.cfg").ToString();
                return 200;
            }
            catch (Exception ex)
            {
                res = ex.Message;
                return 500;
            }
        }

        public static int RunPluginConfigAsync(string key, string value, out string res)
        {
            res = string.Empty;

            if (!string.IsNullOrEmpty(key) && (key.Length > 5))
            {
                key = key.Substring(0, key.Length - 5);
                if (key.Equals("isWebshellEnable"))
                {
                    res = "not allow.";
                    return 403;
                }
                var configKeyProp = typeof(PluginConfig).GetField(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (configKeyProp == null)
                {
                    res = "key not found.";
                    return 501;
                }
                var configEntry = (ConfigEntryBase)configKeyProp.GetValue(null);
                var converter = TomlTypeConverter.GetConverter(configEntry.SettingType);
                if (converter != null)
                {
                    if (TryNormalizeSkinConfigValue(key, value, out string normalizedValue, out string validationMessage))
                    {
                        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
                        {
                            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Skin config {key} fallback: {value} => {normalizedValue}. {validationMessage}");
                        }
                        value = normalizedValue;
                    }

                    configEntry.SetSerializedValue(value);
                    res = configEntry.GetSerializedValue();
                    InvalidateApiCache("config");
                    InvalidateApiCache("skins");
                    if (key.IndexOf("Pack", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("Booster", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        InvalidateApiCache("packs");
                    }
                    return 200;
                }
            }
            return 500;
        }

        public static string GetSkinCatalogJson(string type = null, bool forceRefresh = false)
        {
            string normalizedType = string.IsNullOrWhiteSpace(type) ? string.Empty : type.Trim();
            string cacheKey = "skins:" + normalizedType.ToLowerInvariant();
            return GetCachedCatalogJson(cacheKey, SkinCatalogCacheTtl, forceRefresh, () => BuildSkinCatalogJson(normalizedType, cacheKey, (int)SkinCatalogCacheTtl.TotalSeconds));
        }

        private static CacheBuildResult BuildSkinCatalogJson(string type, string cacheKey, int ttlSeconds)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var response = new SkinCatalogResponse
            {
                success = true,
                type = type ?? string.Empty,
                groups = new Dictionary<string, List<SkinCatalogItem>>(StringComparer.OrdinalIgnoreCase),
                configKeys = new Dictionary<string, string>(SkinConfigKeyByType, StringComparer.OrdinalIgnoreCase),
                configNames = new Dictionary<string, string>(SkinConfigNameByKey, StringComparer.OrdinalIgnoreCase),
                cacheKey = cacheKey,
                generatedAt = DateTime.UtcNow.ToString("O"),
                cacheTtlSeconds = ttlSeconds
            };

            try
            {
                AddSkinGroup(response.groups, "pet", BuildPetItems("pet", "skinPet"));
                AddSkinGroup(response.groups, "opposingPet", BuildPetItems("opposingPet", "skinOpposingPet"));
                AddSkinGroup(response.groups, "coin", BuildCoinItems());
                AddSkinGroup(response.groups, "cardBack", BuildCardBackItems());
                AddSkinGroup(response.groups, "board", BuildBoardItems());
                AddSkinGroup(response.groups, "bgsBoard", BuildBgsBoardItems());
                AddSkinGroup(response.groups, "bgsFinisher", BuildBgsFinisherItems());

                List<SkinCatalogItem> allHeroes = BuildHeroItems();
                AddSkinGroup(response.groups, "hero", allHeroes.Where(item => item.heroType == "对战英雄").ToList());
                AddSkinGroup(response.groups, "opposingHero", allHeroes.Where(item => item.heroType == "对战英雄").Select(CloneAsOpposingHero).ToList());
                AddSkinGroup(response.groups, "bgsHero", allHeroes.Where(item => item.heroType == "酒馆英雄").ToList());
                AddSkinGroup(response.groups, "bob", allHeroes.Where(item => item.heroType == "酒馆鲍勃").ToList());

                if (!string.IsNullOrWhiteSpace(type))
                {
                    var filteredGroups = new Dictionary<string, List<SkinCatalogItem>>(StringComparer.OrdinalIgnoreCase);
                    if (response.groups.TryGetValue(type, out List<SkinCatalogItem> items))
                    {
                        filteredGroups[type] = items;
                    }
                    response.groups = filteredGroups;
                }
            }
            catch (Exception ex)
            {
                response.success = false;
                response.message = ex.Message;
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex);
            }

            stopwatch.Stop();
            response.elapsedMs = stopwatch.ElapsedMilliseconds;
            return new CacheBuildResult
            {
                json = Newtonsoft.Json.JsonConvert.SerializeObject(response),
                cacheable = response.success
            };
        }

        public static string GetPackCatalogJson(bool forceRefresh = false)
        {
            const string cacheKey = "packs";
            return GetCachedCatalogJson(cacheKey, PackCatalogCacheTtl, forceRefresh, () => BuildPackCatalogJson(cacheKey, (int)PackCatalogCacheTtl.TotalSeconds));
        }

        private static CacheBuildResult BuildPackCatalogJson(string cacheKey, int ttlSeconds)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var response = new PackCatalogResponse
            {
                success = true,
                packs = new List<PackCatalogItem>(),
                cacheKey = cacheKey,
                generatedAt = DateTime.UtcNow.ToString("O"),
                cacheTtlSeconds = ttlSeconds
            };

            try
            {
                PackOpening packOpening = SafeGet(() => PackOpening.Get(), null);
                if (packOpening != null)
                {
                    SafeTry(() => response.nativeMassPackOpeningEnabled = packOpening.MassPackOpeningEnabled());
                    SafeTry(() => response.nativeMassCatchupPackOpeningEnabled = packOpening.MassCatchupPackOpeningEnabled());
                    SafeTry(() => response.nativeMassHooverChunkSize = packOpening.MassPackOpeningHooverChunkSize());
                }

                foreach (var booster in GameDbf.Booster.GetRecords().OrderBy(record => record.ID).ToList())
                {
                    if (booster == null)
                    {
                        continue;
                    }

                    int boosterId = (int)booster.ID;
                    int opened = SafeGet(() => BoosterPackUtils.GetBoosterOpenedCount(boosterId), 0);
                    int remaining = SafeGet(() => BoosterPackUtils.GetBoosterCount(boosterId), 0);
                    int nativeLimit = packOpening != null ? SafeGet(() => packOpening.MassPackOpeningPackLimit(boosterId), 0) : 0;
                    bool isCatchupPack = SafeGet(() => booster.IsCatchupPack, false);

                    response.totalOpened += opened;
                    response.totalRemaining += remaining;
                    response.packs.Add(new PackCatalogItem
                    {
                        id = boosterId,
                        name = ResolveBoosterName(booster),
                        opened = opened,
                        remaining = remaining,
                        isCatchupPack = isCatchupPack,
                        packOpeningPrefab = SafeGet(() => booster.PackOpeningPrefab, string.Empty),
                        nativeMassPackLimit = nativeLimit,
                        nativeMassOpenable = remaining > 1 && nativeLimit > 1 && (response.nativeMassPackOpeningEnabled || (isCatchupPack && response.nativeMassCatchupPackOpeningEnabled))
                    });
                }
            }
            catch (Exception ex)
            {
                response.success = false;
                response.message = ex.Message;
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex);
            }

            stopwatch.Stop();
            response.elapsedMs = stopwatch.ElapsedMilliseconds;
            return new CacheBuildResult
            {
                json = Newtonsoft.Json.JsonConvert.SerializeObject(response),
                cacheable = response.success
            };
        }

        public static string GetConfigCatalogJson(bool forceRefresh = false)
        {
            const string cacheKey = "config";
            return GetCachedCatalogJson(cacheKey, ConfigCatalogCacheTtl, forceRefresh, () => BuildConfigCatalogJson(cacheKey, (int)ConfigCatalogCacheTtl.TotalSeconds));
        }

        private static CacheBuildResult BuildConfigCatalogJson(string cacheKey, int ttlSeconds)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var response = new ConfigCatalogResponse
            {
                success = true,
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                fieldNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                items = new List<ConfigCatalogItem>(),
                cacheKey = cacheKey,
                generatedAt = DateTime.UtcNow.ToString("O"),
                cacheTtlSeconds = ttlSeconds
            };

            try
            {
                var fields = typeof(PluginConfig).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .Where(field => typeof(ConfigEntryBase).IsAssignableFrom(field.FieldType));

                foreach (var field in fields)
                {
                    ConfigEntryBase entry = SafeGet(() => field.GetValue(null) as ConfigEntryBase, null);
                    if (entry == null || entry.Definition == null)
                    {
                        continue;
                    }

                    ConfigCatalogItem item = BuildConfigCatalogItem(field.Name, entry);
                    response.items.Add(item);

                    if (!string.IsNullOrEmpty(item.key))
                    {
                        response.values[item.key] = item.value ?? string.Empty;
                        response.fieldNames[item.key] = item.fieldName;
                    }
                }
            }
            catch (Exception ex)
            {
                response.success = false;
                response.message = ex.Message;
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex);
            }

            stopwatch.Stop();
            response.elapsedMs = stopwatch.ElapsedMilliseconds;
            return new CacheBuildResult
            {
                json = Newtonsoft.Json.JsonConvert.SerializeObject(response),
                cacheable = response.success
            };
        }

        private static void AddSkinGroup(Dictionary<string, List<SkinCatalogItem>> groups, string key, List<SkinCatalogItem> items)
        {
            groups[key] = items ?? new List<SkinCatalogItem>();
        }

        private static string GetCachedCatalogJson(string cacheKey, TimeSpan ttl, bool forceRefresh, Func<CacheBuildResult> factory)
        {
            if (!forceRefresh)
            {
                lock (ApiCacheLock)
                {
                    if (ApiCache.TryGetValue(cacheKey, out ApiCacheEntry entry) && entry.expiresAtUtc > DateTime.UtcNow)
                    {
                        return entry.json;
                    }
                }
            }

            CacheBuildResult result = factory();
            if (result != null && result.cacheable && !string.IsNullOrEmpty(result.json))
            {
                lock (ApiCacheLock)
                {
                    ApiCache[cacheKey] = new ApiCacheEntry
                    {
                        json = result.json,
                        expiresAtUtc = DateTime.UtcNow.Add(ttl)
                    };
                }
            }

            return result != null ? (result.json ?? string.Empty) : string.Empty;
        }

        public static void InvalidateApiCache(string prefix = null)
        {
            lock (ApiCacheLock)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    ApiCache.Clear();
                    return;
                }

                string normalizedPrefix = prefix.Trim();
                foreach (string key in ApiCache.Keys.Where(key => key.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    ApiCache.Remove(key);
                }
            }
        }

        private static ConfigCatalogItem BuildConfigCatalogItem(string fieldName, ConfigEntryBase entry)
        {
            Type settingType = entry.SettingType;
            string key = SafeGet(() => entry.Definition.Key, fieldName);
            string section = SafeGet(() => entry.Definition.Section, string.Empty);
            string description = SafeGet(() => entry.Description != null ? entry.Description.Description : string.Empty, string.Empty);
            string acceptableValues = SafeGet(() => entry.Description != null && entry.Description.AcceptableValues != null ? entry.Description.AcceptableValues.ToDescriptionString() : string.Empty, string.Empty);
            string[] tags = SafeGet(() => entry.Description != null && entry.Description.Tags != null
                ? entry.Description.Tags.Where(tag => tag != null).Select(tag => tag.ToString()).Where(tag => !string.IsNullOrEmpty(tag)).ToArray()
                : new string[0], new string[0]);

            return new ConfigCatalogItem
            {
                fieldName = fieldName,
                key = key,
                section = section,
                group = ResolveConfigGroup(fieldName, key, section, description, settingType),
                value = SafeGet(() => entry.GetSerializedValue(), string.Empty),
                defaultValue = SafeSerializeConfigValue(entry.DefaultValue, settingType),
                settingType = settingType != null ? settingType.Name : string.Empty,
                description = description,
                acceptableValues = acceptableValues,
                options = ResolveConfigOptions(settingType),
                tags = tags,
                writable = !string.Equals(fieldName, "isWebshellEnable", StringComparison.Ordinal),
                hidden = key != null && key.StartsWith("HsMod.Init.", StringComparison.OrdinalIgnoreCase)
            };
        }

        private static string ResolveConfigGroup(string fieldName, string key, string section, string description, Type settingType)
        {
            string text = string.Join(" ", new[]
            {
                fieldName ?? string.Empty,
                key ?? string.Empty,
                section ?? string.Empty,
                description ?? string.Empty,
                settingType != null ? settingType.Name : string.Empty
            });

            if (text.Contains("快捷键") || text.Contains("KeyboardShortcut") || text.Contains("KeyCode"))
                return "shortcut";
            if (text.Contains("pid") || text.Contains("语言") || text.Contains("插件") || text.Contains("端口") || text.Contains("Webshell") || text.Contains("内部模式") || text.Contains("日志") || text.Contains("HsMod"))
                return "base";
            if (text.Contains("自动") || text.Contains("掉线") || text.Contains("举报") || text.Contains("冒险") || text.Contains("开包") || text.Contains("分解") || text.Contains("领奖") || text.Contains("结算"))
                return "automation";
            if (text.Contains("显示") || text.Contains("FPS") || text.Contains("帧率") || text.Contains("观战") || text.Contains("消息") || text.Contains("弹出") || text.Contains("焦点"))
                return "display";
            if (text.Contains("齿轮") || text.Contains("战斗") || text.Contains("表情") || text.Contains("英雄介绍") || text.Contains("沉默") || text.Contains("投降") || text.Contains("回合"))
                return "control";
            if (text.Contains("设备") || text.Contains("模拟") || text.Contains("随机") || text.Contains("类型") || text.Contains("品质") || text.Contains("稀有度") || text.Contains("Catchup") || text.Contains("Booster") || text.Contains("Premium") || text.Contains("Rarity"))
                return "simulation";
            if (text.Contains("皮肤") || text.Contains("硬币") || text.Contains("卡背") || text.Contains("面板") || text.Contains("鲍勃") || text.Contains("英雄") || text.Contains("对手") || text.Contains("特效") || text.Contains("镀金") || text.Contains("Cosmetic") || text.Contains("Skin"))
                return "cosmetic";

            return "base";
        }

        private static string SafeSerializeConfigValue(object value, Type settingType)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return SafeGet(() => TomlTypeConverter.ConvertToString(value, settingType), value.ToString());
        }

        private static string[] ResolveConfigOptions(Type settingType)
        {
            if (settingType == null)
            {
                return new string[0];
            }

            if (settingType == typeof(bool))
            {
                return new[] { "true", "false" };
            }

            if (settingType.IsEnum)
            {
                return Enum.GetNames(settingType);
            }

            return new string[0];
        }

        private static List<SkinCatalogItem> BuildPetItems(string type, string configKey)
        {
            return GameDbf.PetVariant.GetRecords()
                .Where(record => record != null)
                .OrderBy(record => record.ID)
                .Select(record =>
                {
                    string cardStringId = SafeTranslateDbIdToCardId(record.CardId);
                    return new SkinCatalogItem
                    {
                        type = type,
                        configKey = configKey,
                        configName = GetSkinConfigName(configKey),
                        id = record.ID,
                        petId = record.PetId,
                        cardId = record.CardId,
                        cardStringId = cardStringId,
                        name = SafeGet(() => record.Name.GetString(), string.Empty),
                        description = SafeGet(() => record.CollectionDescription.GetString(), string.Empty),
                        enabled = SafeGet(() => record.Enabled, true),
                        owned = SafeGet(() => PetsManager.Get() != null && PetsManager.Get().IsPetVariantOwned(record.ID), false),
                        imageType = type,
                        detailsTexture = SafeGet(() => record.PetIcon, string.Empty)
                    };
                }).ToList();
        }

        private static List<SkinCatalogItem> BuildCoinItems()
        {
            return GameDbf.CosmeticCoin.GetRecords()
                .Where(record => record != null)
                .OrderBy(record => record.ID)
                .Select(record =>
                {
                    string cardStringId = SafeTranslateDbIdToCardId(record.CardId);
                    return new SkinCatalogItem
                    {
                        type = "coin",
                        configKey = "skinCoin",
                        configName = GetSkinConfigName("skinCoin"),
                        id = record.CardId,
                        cardId = record.CardId,
                        cardStringId = cardStringId,
                        name = SafeGet(() => record.Name.GetString(), string.Empty),
                        enabled = SafeGet(() => record.Enabled, true),
                        owned = !string.IsNullOrEmpty(cardStringId) && SafeGet(() => CosmeticCoinManager.Get() != null && CosmeticCoinManager.Get().IsOwnedCoinCard(cardStringId), false),
                        imageType = "coin"
                    };
                }).ToList();
        }

        private static List<SkinCatalogItem> BuildCardBackItems()
        {
            return GameDbf.CardBack.GetRecords()
                .Where(record => record != null)
                .OrderBy(record => record.ID)
                .Select(record => new SkinCatalogItem
                {
                    type = "cardBack",
                    configKey = "skinCardBack",
                    configName = GetSkinConfigName("skinCardBack"),
                    id = record.ID,
                    name = SafeGet(() => record.Name.GetString(), string.Empty),
                    description = SafeGet(() => record.Description.GetString(), string.Empty),
                    enabled = SafeGet(() => record.Enabled, true),
                    owned = SafeGet(() => CardBackManager.Get() != null && CardBackManager.Get().IsCardBackOwned(record.ID), false),
                    isDefault = SafeGet(() => record.IsRandomCardBack, false),
                    imageType = "cardBack",
                    prefab = SafeGet(() => record.PrefabName, string.Empty)
                }).ToList();
        }

        private static List<SkinCatalogItem> BuildBoardItems()
        {
            return GameDbf.Board.GetRecords()
                .Where(record => record != null)
                .OrderBy(record => record.ID)
                .Select(record => new SkinCatalogItem
                {
                    type = "board",
                    configKey = "skinBoard",
                    configName = GetSkinConfigName("skinBoard"),
                    id = record.ID,
                    name = SafeGet(() => record.NoteDesc, string.Empty),
                    imageType = "board",
                    prefab = SafeGet(() => record.Prefab, string.Empty)
                }).ToList();
        }

        private static List<SkinCatalogItem> BuildBgsBoardItems()
        {
            return GameDbf.BattlegroundsBoardSkin.GetRecords()
                .Where(record => record != null)
                .OrderBy(record => record.ID)
                .Select(record => new SkinCatalogItem
                {
                    type = "bgsBoard",
                    configKey = "skinBgsBoard",
                    configName = GetSkinConfigName("skinBgsBoard"),
                    id = record.ID,
                    name = SafeGet(() => record.CollectionName.GetString(), string.Empty),
                    description = SafeGet(() => record.Description.GetString(), string.Empty),
                    enabled = true,
                    imageType = "bgsBoard",
                    detailsTexture = SafeGet(() => record.DetailsTexture, string.Empty),
                    detailsMovie = SafeGet(() => record.DetailsMovie, string.Empty),
                    prefab = SafeGet(() => record.FullTavernBoardPrefab, string.Empty)
                }).ToList();
        }

        private static List<SkinCatalogItem> BuildBgsFinisherItems()
        {
            return GameDbf.BattlegroundsFinisher.GetRecords()
                .Where(record => record != null)
                .OrderBy(record => record.ID)
                .Select(record => new SkinCatalogItem
                {
                    type = "bgsFinisher",
                    configKey = "skinBgsFinisher",
                    configName = GetSkinConfigName("skinBgsFinisher"),
                    id = record.ID,
                    name = SafeGet(() => record.CollectionName.GetString(), string.Empty),
                    description = SafeGet(() => record.Description.GetString(), string.Empty),
                    enabled = SafeGet(() => record.Enabled, true),
                    isDefault = SafeGet(() => record.IsDefault, false),
                    imageType = "bgsFinisher",
                    detailsTexture = SafeGet(() => record.DetailsTexture, string.Empty),
                    detailsMovie = SafeGet(() => record.DetailsMovie, string.Empty),
                    prefab = SafeGet(() => record.GameplaySettings, string.Empty)
                }).ToList();
        }

        private static List<SkinCatalogItem> BuildHeroItems()
        {
            return GameDbf.CardHero.GetRecords()
                .Where(record => record != null)
                .OrderBy(record => record.HeroType)
                .ThenBy(record => record.CardId)
                .Select(record =>
                {
                    string cardStringId = SafeTranslateDbIdToCardId(record.CardId);
                    string className = string.Empty;
                    SafeTry(() =>
                    {
                        EntityDef entityDef = DefLoader.Get()?.GetEntityDef(record.CardId);
                        if (entityDef != null)
                            className = GameStrings.GetClassName(entityDef.GetClass());
                    });
                    string heroType = ConvertHeroType(record.HeroType);
                    string configKey = heroType == "酒馆鲍勃" ? "skinBob" : "skinHero";
                    return new SkinCatalogItem
                    {
                        type = heroType == "酒馆英雄" ? "bgsHero" : (heroType == "酒馆鲍勃" ? "bob" : "hero"),
                        configKey = configKey,
                        configName = GetSkinConfigName(configKey),
                        id = record.CardId,
                        cardId = record.CardId,
                        cardStringId = cardStringId,
                        name = SafeGet(() => GameDbf.Card.GetRecord(record.CardId).Name.GetString(), cardStringId),
                        heroClass = className,
                        heroType = heroType,
                        owned = !string.IsNullOrEmpty(cardStringId) && SafeGet(() => HeroSkinUtils.IsHeroSkinOwned(cardStringId), false),
                        imageType = "hero",
                        detailsTexture = SafeGet(() => record.StoreBackgroundTexture, string.Empty)
                    };
                }).ToList();
        }

        private static SkinCatalogItem CloneAsOpposingHero(SkinCatalogItem source)
        {
            return new SkinCatalogItem
            {
                type = "opposingHero",
                configKey = "skinOpposingHero",
                configName = GetSkinConfigName("skinOpposingHero"),
                id = source.id,
                cardId = source.cardId,
                cardStringId = source.cardStringId,
                name = source.name,
                heroClass = source.heroClass,
                heroType = source.heroType,
                owned = source.owned,
                enabled = source.enabled,
                imageType = source.imageType,
                detailsTexture = source.detailsTexture,
                description = source.description
            };
        }

        private static string ConvertHeroType(Assets.CardHero.HeroType heroType)
        {
            switch (heroType)
            {
                case Assets.CardHero.HeroType.BATTLEGROUNDS_HERO:
                    return "酒馆英雄";
                case Assets.CardHero.HeroType.BATTLEGROUNDS_GUIDE:
                    return "酒馆鲍勃";
                default:
                    return "对战英雄";
            }
        }

        private static string GetSkinConfigName(string configKey)
        {
            return SkinConfigNameByKey.TryGetValue(configKey, out string configName) ? configName : configKey;
        }

        private static string SafeTranslateDbIdToCardId(int dbId)
        {
            return SafeGet(() => GameUtils.TranslateDbIdToCardId(dbId, false), string.Empty);
        }

        private static string ResolveBoosterName(BoosterDbfRecord booster)
        {
            string name = SafeGet(() => booster.Name.GetString(), string.Empty);
            if (!string.IsNullOrEmpty(name))
                return name;

            name = Enum.GetName(typeof(BoosterDbId), booster.ID);
            return string.IsNullOrEmpty(name) ? "未知" : name;
        }

        private static bool TryNormalizeSkinConfigValue(string key, string value, out string normalizedValue, out string message)
        {
            normalizedValue = value;
            message = string.Empty;
            if (!SkinConfigNameByKey.ContainsKey(key))
            {
                return false;
            }

            if (!int.TryParse(value, out int id))
            {
                normalizedValue = "-1";
                message = "value is not an integer";
                return true;
            }

            if (IsValidSkinConfigValue(key, id))
            {
                normalizedValue = id.ToString();
                return true;
            }

            normalizedValue = "-1";
            message = $"invalid {key} id {id}";
            return true;
        }

        private static bool IsValidSkinConfigValue(string key, int id)
        {
            if (id == -1)
                return true;

            switch (key)
            {
                case "skinPet":
                case "skinOpposingPet":
                    return id == 0 || GameDbf.PetVariant.GetRecord(id) != null;
                case "skinCoin":
                    return GameDbf.CosmeticCoin.GetRecords().Any(record => record != null && record.CardId == id);
                case "skinCardBack":
                    return GameDbf.CardBack.GetRecord(id) != null;
                case "skinBoard":
                    return GameDbf.Board.GetRecord(id) != null;
                case "skinBgsBoard":
                    return id == 0 || GameDbf.BattlegroundsBoardSkin.GetRecord(id) != null;
                case "skinBgsFinisher":
                    return id == 0 || GameDbf.BattlegroundsFinisher.GetRecord(id) != null;
                case "skinBob":
                    return IsHeroSkinId(id, out Assets.CardHero.HeroType bobType) && bobType == Assets.CardHero.HeroType.BATTLEGROUNDS_GUIDE;
                case "skinHero":
                case "skinOpposingHero":
                    return IsHeroSkinId(id, out _);
                default:
                    return true;
            }
        }

        private static bool IsHeroSkinId(int id, out Assets.CardHero.HeroType heroType)
        {
            heroType = Assets.CardHero.HeroType.UNKNOWN;
            CardHeroDbfRecord record = GameDbf.CardHero.GetRecords().FirstOrDefault(item => item != null && item.CardId == id);
            if (record != null)
            {
                heroType = record.HeroType;
                return true;
            }

            try
            {
                EntityDef entityDef = DefLoader.Get()?.GetEntityDef(id);
                if (entityDef != null && entityDef.GetCardType() == TAG_CARDTYPE.HERO)
                {
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static string SanitizeHsSkinsCfg(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content ?? string.Empty;
            }

            var builder = new StringBuilder();
            foreach (string rawLine in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                {
                    builder.AppendLine(rawLine);
                    continue;
                }

                string[] parts = line.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out int sourceId) || !IsHeroSkinId(sourceId, out _))
                {
                    builder.AppendLine("# invalid: " + rawLine);
                    continue;
                }

                var validTargets = new List<string>();
                foreach (string rawTarget in parts[1].Split(','))
                {
                    string targetText = rawTarget.Trim();
                    if (int.TryParse(targetText, out int targetId) && IsHeroSkinId(targetId, out _))
                    {
                        validTargets.Add(targetId.ToString());
                    }
                }

                if (validTargets.Count == 0)
                {
                    builder.AppendLine("# invalid: " + rawLine);
                    continue;
                }

                builder.AppendLine(sourceId + ":" + string.Join(",", validTargets));
            }

            return builder.ToString();
        }

        private static T SafeGet<T>(Func<T> getter, T fallback)
        {
            try
            {
                return getter();
            }
            catch
            {
                return fallback;
            }
        }

        private static void SafeTry(Action action)
        {
            try
            {
                action();
            }
            catch
            {
            }
        }
    }
}
