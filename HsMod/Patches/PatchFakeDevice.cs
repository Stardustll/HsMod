using HarmonyLib;
using PegasusShared;

namespace HsMod
{
    public partial class Patcher
    {
        //设备伪装：把平台信息投影为预设机型。
        //注：旧版的 ConnectAPI.RequestPurchaseMethod 补丁点已在新类库中移除，
        //    购买身份还原（DeviceSimulation.PreservePurchaseIdentity）暂无挂载点，方法保留备用。
        public class PatchFakeDevice
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(Network), "GetPlatformBuilder")]
            public static void PatchGetPlatformBuilder(ref Platform __result)
            {
                DeviceSimulation.Apply(__result);
            }
        }
    }
}
