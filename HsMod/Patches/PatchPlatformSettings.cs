using System;
using HarmonyLib;
using HearthstoneTelemetry;

namespace HsMod
{
    public partial class Patcher
    {
        //遥测设备/系统指纹投影：让上报的设备信息与"设备伪装"选择的机型一致
        //任一步异常都保留原生信息，不影响游戏
        public class PatchPlatformSettings
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(TelemetryClient), "GetDeviceInfo")]
            public static void PatchGetDeviceInfo(ref Blizzard.Telemetry.WTCG.Client.DeviceInfo __result)
            {
                try
                {
                    __result = DeviceSimulation.ProjectTelemetryDeviceInfo(__result);
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "遥测设备模拟失败，保留原生设备信息: " + ex.GetType().Name);
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(TelemetryClient), "SendSystemDetail")]
            public static void PatchSendSystemDetail(ref Blizzard.Telemetry.WTCG.Client.UnitySystemInfo info)
            {
                try
                {
                    info = DeviceSimulation.ProjectUnitySystemInfo(info);
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "系统指纹投影失败，保留原生系统信息: " + ex.GetType().Name);
                }
            }
        }
    }
}
