using Blizzard.T5.Core.Time;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using static HsMod.PluginConfig;

namespace HsMod
{
    //public struct AllPatch
    //{
    //    Harmony mHarmony;
    //    Type mType;
    //}


    public static class PatchManager
    {
        public static List<Harmony> AllHarmony = new List<Harmony>();    //保存补丁信息，方便定向卸载。
        public static List<string> AllHarmonyName = new List<string>();

        public static void LoadPatch(Type loadType)
        {
            try
            {
                Harmony harmony;
                int harmonyCount;
                harmony = Harmony.CreateAndPatchAll(loadType);
                //var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Patcher");
                harmonyCount = harmony.GetPatchedMethods().Count();
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"{loadType.Name} => Patched {harmonyCount} methods");
                AllHarmony.Add(harmony);
                AllHarmonyName.Add(loadType.Name);
            }
            catch (Exception ex)
            {
                if (loadType == typeof(Patcher.PatchAntiCheat))
                {
                    if ((Environment.OSVersion.Platform == PlatformID.MacOSX) || (Environment.OSVersion.Platform == PlatformID.Unix))
                    {
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "Skip Mac.");
                        return;
                    }
                }
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"{loadType.Name} => {ex.Message} \n{ex.InnerException}");
                //单个补丁类失败（多为游戏更新后目标方法改名/移除）不应导致整个插件退出：
                //跳过该类，其余补丁继续加载，避免白屏闪退
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"{loadType.Name} patch skipped (目标方法可能已随游戏更新变更)");
            }
        }

        public static bool UnPatch(string name)
        {
            for (int i = 0; i < AllHarmonyName.Count; i++)
            {
                if (AllHarmonyName[i] == name)
                {
                    AllHarmony[i].UnpatchSelf();
                    AllHarmonyName.Remove(AllHarmonyName[i]);
                    AllHarmony.Remove(AllHarmony[i]);
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Unpatched {name}!");
                    return true;
                }
            }
            return false;
        }

        //委托绑定
        public static void PatchSettingDelegate()
        {
            isPluginEnable.SettingChanged += delegate
            {
                if (isPluginEnable.Value)
                {
                    PatchAll();
                }
                else
                {
                    UnPatchAll();
                }
            };
            isTimeGearEnable.SettingChanged += delegate
            {
                TimeScaleMgr.Get().Update();
            };
            timeGear.SettingChanged += delegate
            {
                TimeScaleMgr.Get().Update();
            };

            isBlockStreamerMode.SettingChanged += delegate
            {
                //开启时立即关闭已保存的主播模式
                if (isBlockStreamerMode.Value)
                {
                    try { Options.Get()?.SetBool(Option.STREAMER_MODE, false); } catch { }
                }
            };
            isShowCardLargeCount.SettingChanged += delegate
            {
                if (isShowCardLargeCount.Value)
                {
                    LoadPatch(typeof(Patcher.PatchRealtimeCardNum));
                }
                else
                {
                    UnPatch("PatchRealtimeCardNum");
                }
            };
            isBypassDeckShareCodeCheckEnable.SettingChanged += delegate
            {
                if (isBypassDeckShareCodeCheckEnable.Value)
                {
                    LoadPatch(typeof(Patcher.PatchDeckShareCode));
                }
                else
                {
                    UnPatch("PatchDeckShareCode");
                }
            };
            isIdleKickEnable.SettingChanged += delegate
            {
                //Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"SetShouldCheckForInactivity: {isIdleKickEnable.Value}");
                InactivePlayerKicker.Get().SetShouldCheckForInactivity(isIdleKickEnable.Value);
            };
            targetFrameRate.SettingChanged += delegate
            {
                graphicsManager = Blizzard.T5.Services.ServiceManager.Get<IGraphicsManager>();
                if (targetFrameRate.Value > 0 && Options.Get().GetInt(Option.GFX_TARGET_FRAME_RATE, 0) != targetFrameRate.Value)
                    graphicsManager.UpdateTargetFramerate(targetFrameRate.Value, false);
                else if (targetFrameRate.Value <= 0)
                {
                    graphicsManager.UpdateTargetFramerate(30, true);
                }
            };
            //isDynamicFpsEnable.SettingChanged += delegate
            //{
            //    graphicsManager = Blizzard.T5.Services.ServiceManager.Get<IGraphicsManager>();
            //    if (targetFrameRate.Value > 0 && Options.Get().GetInt(Option.GFX_TARGET_FRAME_RATE) != targetFrameRate.Value)
            //        graphicsManager.UpdateTargetFramerate(targetFrameRate.Value, isDynamicFpsEnable.Value);
            //    else if (targetFrameRate.Value <= 0)
            //    {
            //        graphicsManager.UpdateTargetFramerate(30, true);
            //    }
            //};
            isAutoRecvMercenaryRewardEnable.SettingChanged += delegate
            {
                if (isAutoRecvMercenaryRewardEnable.Value)
                {
                    LoadPatch(typeof(Patcher.PatchMercenariesReward));
                }
                else
                {
                    UnPatch("PatchMercenariesReward");
                }
            };
            isAutoOpenBoxesRewardEnable.SettingChanged += delegate
            {
                if (isAutoOpenBoxesRewardEnable.Value)
                {
                    LoadPatch(typeof(Patcher.PatchBoxesReward));
                }
                else
                {
                    UnPatch("PatchBoxesReward");
                }
            };
            isMoveEnemyCardsEnable.SettingChanged += delegate
            {
                if (isMoveEnemyCardsEnable.Value)
                {
                    LoadPatch(typeof(Patcher.PatchDeathOb));
                }
                else
                {
                    UnPatch("PatchDeathOb");
                }
                SpectatorHandLayout.SetPatched(isMoveEnemyCardsEnable.Value);    //开关切换后让对手手牌布局立即生效
            };
            isHeroFormsEnable.SettingChanged += delegate
            {
                if (isHeroFormsEnable.Value)
                {
                    LoadPatch(typeof(Patcher.PatchHeroForms));
                }
                else
                {
                    UnPatch("PatchHeroForms");
                }
            };
            isCollectionUnlockEnable.SettingChanged += delegate
            {
                if (isCollectionUnlockEnable.Value)
                {
                    LoadPatch(typeof(Patcher.PatchCollectionUnlock));
                }
                else
                {
                    UnPatch("PatchCollectionUnlock");
                }
            };
            isFakeOpenEnable.SettingChanged += delegate
            {
                if (isFakeOpenEnable.Value)
                {
                    LoadPatch(typeof(Patcher.PatchFakePackOpening));
                }
                else
                {
                    UnPatch("PatchFakePackOpening");
                }
            };
            isKarazhanFixEnable.SettingChanged += delegate
            {
                if (isKarazhanFixEnable.Value)
                {
                    LoadPatch(typeof(Patcher.PatchKarazhan));
                }
                else
                {
                    UnPatch("PatchKarazhan");
                }
            };
        }

        public static void PatchAll()
        {
            LoadPatch(typeof(Patcher));
            LoadPatch(typeof(Patcher.PatchAntiCheat));
            LoadPatch(typeof(Patcher.PatchMisc));
            LoadPatch(typeof(Patcher.PatchEmote));
            LoadPatch(typeof(Patcher.PatchIGMMessage));
            LoadPatch(typeof(Patcher.PatchMercenaries));
            LoadPatch(typeof(Patcher.PatchHearthstone));
            LoadPatch(typeof(Patcher.PatchLogArchive));
            LoadPatch(typeof(Patcher.PatchBattlegrounds));
            LoadPatch(typeof(Patcher.PatchBgsUnlockCollection));
            LoadPatch(typeof(Patcher.PatchFavorite));
            LoadPatch(typeof(Patcher.PatchFakeDevice));
            LoadPatch(typeof(Patcher.PatchPlatformSettings));
            LoadPatch(typeof(Patcher.PatchDevOptioins));
            LoadPatch(typeof(Patcher.PatchGameMenu));
            LoadPatch(typeof(Patcher.PatchBgRank));
            if (isShowCardLargeCount.Value)
            {
                LoadPatch(typeof(Patcher.PatchRealtimeCardNum));
            }
            if (isBypassDeckShareCodeCheckEnable.Value)
            {
                LoadPatch(typeof(Patcher.PatchDeckShareCode));
            }
            if (isMoveEnemyCardsEnable.Value)
            {
                LoadPatch(typeof(Patcher.PatchDeathOb));
                SpectatorHandLayout.SetPatched(true);
            }
            if (isCollectionUnlockEnable.Value)
            {
                LoadPatch(typeof(Patcher.PatchCollectionUnlock));
            }
            if (isHeroFormsEnable.Value)
            {
                LoadPatch(typeof(Patcher.PatchHeroForms));
            }
            if (isAutoRecvMercenaryRewardEnable.Value)
            {
                LoadPatch(typeof(Patcher.PatchMercenariesReward));
            }
            if (isAutoOpenBoxesRewardEnable.Value)
            {
                LoadPatch(typeof(Patcher.PatchBoxesReward));
            }
            if (isFakeOpenEnable.Value)
            {
                LoadPatch(typeof(Patcher.PatchFakePackOpening));
            }
            if (isKarazhanFixEnable.Value)
            {
                LoadPatch(typeof(Patcher.PatchKarazhan));
            }
            TimeScaleMgr.Get().Update();

        }
        public static void UnPatchAll()
        {
            SpectatorHandLayout.SetPatched(false);
            for (int i = 0; i < AllHarmony.Count; i++)
            {
                AllHarmony[i].UnpatchSelf();
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Unpatched {AllHarmonyName[i]}!");
            }
            AllHarmony.Clear();
            AllHarmonyName.Clear();
        }
        public static void RePatchAll()
        {
            UnPatchAll();
            PatchAll();
        }
    }

    //前置Patch为harmony补丁，后置Patch为反射
}
