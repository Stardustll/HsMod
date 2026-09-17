using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HsMod
{
    public partial class Patcher
    {
        //英雄形态：本地切换英雄的"变身后"外观（血量触发/表情触发/异画死亡之翼轰炸）
        //参考自上游 DLL 的 PatchHeroForms，参数表按本仓库类库调整
        public class PatchHeroForms
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(EmoteHandler), "DetermineAvailableEmotes")]
            public static void PatchDetermineAvailableEmotes(EmoteHandler __instance, List<EmoteOption> ___m_availableEmotes)
            {
                if (!LocalHeroForms.HasLocalCosmeticHero() || ___m_availableEmotes == null)
                {
                    return;
                }
                //把第 4 号表情位（默认是"抱歉"）替换成本地形态绑定的表情
                int index = __instance.m_DefaultEmotes.FindIndex(delegate (EmoteOption item)
                {
                    return item != null && item.m_EmoteType == EmoteType.SORRY;
                });
                if (index < 0 || index >= ___m_availableEmotes.Count)
                {
                    return;
                }
                EmoteType localEmote = LocalHeroForms.GetLocalCosmeticEmote();
                EmoteOption replacement = localEmote == EmoteType.SORRY
                    ? __instance.m_DefaultEmotes[index]
                    : __instance.m_EmoteOverrides.Find(delegate (EmoteOption item)
                    {
                        return item != null && item.m_EmoteType == localEmote;
                    });
                if (replacement == null)
                {
                    return;
                }
                if (___m_availableEmotes[index] == replacement)
                {
                    replacement.UpdateEmoteType();
                    return;
                }
                ___m_availableEmotes[index].gameObject.SetActive(false);
                ___m_availableEmotes[index] = replacement;
                TransformUtil.CopyWorld(replacement, __instance.m_DefaultEmotes[index]);
                replacement.gameObject.SetActive(true);
                replacement.UpdateEmoteType();
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(EmoteHandler), "ShowEmotes")]
            public static void PatchShowEmotes(EmoteHandler __instance)
            {
                if (LocalHeroForms.HasLocalCosmeticHero() && !__instance.AreEmotesActive())
                {
                    __instance.ChangeAvailableEmotes();
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(InputManager), "DoNetworkResponse")]
            public static bool PatchCosmeticAction(Entity entity, GAME_TAG desiredKeyword, ref bool __result)
            {
                if (desiredKeyword != GAME_TAG.COSMETIC_ACTION)
                {
                    return true;
                }
                return !LocalHeroForms.HandleCosmeticAction(entity, out __result);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Card), "OnTagChanged")]
            public static void PatchObserveHealth(TagDelta change)
            {
                //TagDelta.tag 是 int：45=HEALTH 44=DAMAGE
                if (change.tag == 45 || change.tag == 44)
                {
                    LocalHeroForms.ObserveHealth();
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Player), "OnTagChanged")]
            public static void PatchObserveCorpses(Player __instance, TagDelta change)
            {
                if (change.tag == 2186)    //尸体数
                {
                    LocalHeroForms.ObserveCorpses(__instance, change);
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Actor), "LoadSpell")]
            public static void PatchHeroSpellSource(Actor __instance, SpellType spellType, Spell __result)
            {
                Card heroSpellSource = LocalHeroForms.GetHeroSpellSource(__instance, spellType, __result);
                if (heroSpellSource != null)
                {
                    SpellUtils.SetupSpell(__result, heroSpellSource);
                }
            }
        }
    }
}
