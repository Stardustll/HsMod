using HarmonyLib;
using System;
using System.Collections.Generic;
using static HsMod.PluginConfig;

namespace HsMod
{
    public partial class Patcher
    {
        public class PatchRankedCollectionUnlock
        {
            private static HashSet<int> cachedCardBackIds;
            private static HashSet<int> cachedCoinIds;

            private static bool IsEnabled()
            {
                return isRankedCollectionUnlockEnable.Value;
            }

            private static HashSet<int> GetLocalCardBackIds()
            {
                if (cachedCardBackIds == null || cachedCardBackIds.Count == 0)
                {
                    HashSet<int> ids = new HashSet<int>();
                    foreach (var record in GameDbf.CardBack.GetRecords())
                    {
                        if (record != null && record.Enabled)
                            ids.Add(record.ID);
                    }
                    if (ids.Count > 0)
                        cachedCardBackIds = ids;
                    return ids;
                }
                return cachedCardBackIds;
            }

            private static HashSet<int> GetLocalCoinIds()
            {
                if (cachedCoinIds == null || cachedCoinIds.Count == 0)
                {
                    HashSet<int> ids = new HashSet<int>();
                    foreach (var record in GameDbf.CosmeticCoin.GetRecords())
                    {
                        if (record != null && record.Enabled)
                            ids.Add(record.ID);
                    }
                    if (ids.Count > 0)
                        cachedCoinIds = ids;
                    return ids;
                }
                return cachedCoinIds;
            }

            private static bool IsBattlegroundsHeroSkin(CollectibleCard card)
            {
                try
                {
                    CollectionManager manager = CollectionManager.Get();
                    return manager != null && manager.IsBattlegroundsHeroSkinCard(card.CardDbId);
                }
                catch
                {
                    return false;
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CardBackManager), "GetCardBacksOwned")]
            public static void PatchGetCardBacksOwned(ref HashSet<int> __result)
            {
                if (!IsEnabled())
                    return;
                HashSet<int> ids = GetLocalCardBackIds();
                if (ids.Count > 0)
                {
                    if (__result == null)
                        __result = new HashSet<int>();
                    __result.UnionWith(ids);
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CardBackManager), "IsCardBackOwned")]
            public static void PatchIsCardBackOwned(int cardBackID, ref bool __result)
            {
                if (!IsEnabled() || __result)
                    return;
                __result = GetLocalCardBackIds().Contains(cardBackID);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CardBackManager), "GetNumCardBacksOwned")]
            public static void PatchGetNumCardBacksOwned(CardBackManager __instance, ref int __result)
            {
                if (!IsEnabled() || __instance == null)
                    return;
                int count = __instance.GetCardBackIds(true).Count;
                if (count > 0)
                    __result = count;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CosmeticCoinManager), "GetCoinsOwned")]
            public static void PatchGetCoinsOwned(ref HashSet<int> __result)
            {
                if (!IsEnabled())
                    return;
                HashSet<int> ids = GetLocalCoinIds();
                if (ids.Count > 0)
                {
                    if (__result == null)
                        __result = new HashSet<int>();
                    __result.UnionWith(ids);
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CollectibleCard), "get_OwnedCount")]
            public static void PatchCollectibleCardOwnedCount(CollectibleCard __instance, ref int __result)
            {
                if (!IsEnabled() || __instance == null || !__instance.IsHeroSkin || IsBattlegroundsHeroSkin(__instance))
                    return;
                __result = Math.Max(1, __result);
            }
        }
    }
}
