using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HsMod
{
    public static class PluginConfig
    {
        public static bool isDebug = false;

        public static ConfigEntry<bool> isPluginEnable;
        public static ConfigEntry<string> pluginInitLanague;
        public static ConfigEntry<Locale> pluginLanague;
        public static ConfigEntry<bool> isFakeOpenEnable;
        public static ConfigEntry<Utils.ConfigTemplate> configTemplate;
        public static ConfigEntry<bool> isTimeGearEnable;
        public static ConfigEntry<bool> isShortcutsEnable;
        public static ConfigEntry<int> targetFrameRate;
        public static ConfigEntry<bool> isDynamicFpsEnable;
        public static ConfigEntry<bool> isEulaRead;

        public static ConfigEntry<float> timeGear;
        public static ConfigEntry<int> receiveEnemyEmoteLimit;

        public static ConfigEntry<bool> isIGMMessageShow;
        public static ConfigEntry<bool> isOnApplicationFocus;
        public static ConfigEntry<bool> isAutoExit;
        //public static ConfigEntry<bool> isAutoRestart;
        public static ConfigEntry<bool> isAlertPopupShow;
        public static ConfigEntry<Utils.AlertPopupResponse> responseAlertPopup;
        public static ConfigEntry<bool> isRewardToastShow;
        public static ConfigEntry<bool> isAutoOpenBoxesRewardEnable;
        public static ConfigEntry<bool> isFullnameShow;
        public static ConfigEntry<bool> isBlockStreamerMode;
        public static ConfigEntry<bool> isOpponentRankInGameShow;
        public static ConfigEntry<bool> isSkipHeroIntro;
        public static ConfigEntry<bool> isThinkEmotesEnable;
        public static ConfigEntry<bool> isExtendedBMEnable;
        public static ConfigEntry<bool> isQuickPackOpeningEnable;
        public static ConfigEntry<bool> isAutoPackOpeningEnable;
        public static ConfigEntry<bool> isShowCardLargeCount;
        public static ConfigEntry<bool> isAutoRefundCardDisenchantEnable;
        public static ConfigEntry<bool> isShowCollectionCardIdEnable;
        public static ConfigEntry<bool> isShowRetireForever;
        public static ConfigEntry<bool> isIdleKickEnable;
        public static ConfigEntry<bool> isBypassDeckShareCodeCheckEnable;

        //public static ConfigEntry<Utils.QuickMode> quickModeState;
        public static ConfigEntry<bool> isQuickModeEnable;
        public static ConfigEntry<bool> isCardTrackerEnable;
        public static ConfigEntry<bool> isCardRevealedEnable;
        public static ConfigEntry<bool> isMoveEnemyCardsEnable;
        public static ConfigEntry<bool> isAutoReportEnable;

        public static ConfigEntry<bool> isAutoRecvMercenaryRewardEnable;
        public static ConfigEntry<bool> isMercenaryBattleZoom;
        public static ConfigEntry<Utils.CardState> mercenaryDiamondCardState;
        public static ConfigEntry<Utils.CardState> randomMercenarySkinEnable;

        public static ConfigEntry<bool> isShutUpBobEnable;
        public static ConfigEntry<bool> isBgsGoldenEnable;
        public static ConfigEntry<bool> isBgsSeasonTicketUnlock;
        public static ConfigEntry<bool> isBgsUnlockCollectionEnable;
        public static ConfigEntry<bool> isBgRankEnable;
        public static ConfigEntry<bool> isBgSessionStatsEnable;
        public static ConfigEntry<bool> isBgAutoSquelchEnable;


        public static ConfigEntry<bool> isPatchAssetLoader;
        public static ConfigEntry<bool> shieldMainBoxLuckyDraw;
        // 卡牌原画导出
        public static ConfigEntry<bool> SaveCardTextures;
        public static ConfigEntry<bool> isOpponentGoldenCardShow;
        public static ConfigEntry<bool> isSignatureCardStateEnable;
        public static ConfigEntry<Utils.CardState> goldenCardState;
        public static ConfigEntry<Utils.CardState> maxCardState;
        public static ConfigEntry<bool> signatureFirst;
        public static ConfigEntry<bool> previewCardPlaySounds;
        public static ConfigEntry<bool> checkCollDeckValidForMode;
        public static ConfigEntry<bool> oldSignatureSave;
        public static ConfigEntry<KeyboardShortcut> keyTimeGearUp;
        public static ConfigEntry<KeyboardShortcut> keyTimeGearDown;
        public static ConfigEntry<KeyboardShortcut> keyTimeGearDefault;
        public static ConfigEntry<KeyboardShortcut> keyTimeGearMax;
        public static ConfigEntry<KeyboardShortcut> keySimulateDisconnect;
        public static ConfigEntry<KeyboardShortcut> keyCopyBattleTag;
        public static ConfigEntry<KeyboardShortcut> keyCopySelectBattleTag;
        public static ConfigEntry<KeyboardShortcut> keyConcede;
        public static ConfigEntry<KeyboardShortcut> keyContinueMulligan;
        public static ConfigEntry<KeyboardShortcut> keySquelch;
        public static ConfigEntry<KeyboardShortcut> keySoundMute;
        public static ConfigEntry<KeyboardShortcut> keyShutUpBob;
        public static ConfigEntry<KeyboardShortcut> keyRefund;
        public static ConfigEntry<KeyboardShortcut> keyReadNewCards;
        //public static ConfigEntry<KeyboardShortcut> keyRuin;    //毁灭吧赶紧的
        public static ConfigEntry<KeyboardShortcut> keyZeroDollarShopping;
        public static ConfigEntry<KeyboardShortcut> keyShowFPS;

        public static ConfigEntry<KeyboardShortcut> keyBgsRefresh;
        public static ConfigEntry<KeyboardShortcut> keyBgsFreeze;
        public static ConfigEntry<KeyboardShortcut> keyBgsUpgrade;
        public static ConfigEntry<KeyboardShortcut> keyBgsHeroPower;
        public static ConfigEntry<KeyboardShortcut> keyBgsTeammateBoard;

        public static ConfigEntry<KeyboardShortcut> keyEmoteGreetings;
        public static ConfigEntry<KeyboardShortcut> keyEmoteWellPlayed;
        public static ConfigEntry<KeyboardShortcut> keyEmoteThanks;
        public static ConfigEntry<KeyboardShortcut> keyEmoteWow;
        public static ConfigEntry<KeyboardShortcut> keyEmoteOops;
        public static ConfigEntry<KeyboardShortcut> keyEmoteThreaten;

        public static ConfigEntry<int> skinCoin;
        public static ConfigEntry<int> skinCardBack;
        public static ConfigEntry<int> skinBoard;
        public static ConfigEntry<int> skinBgsBoard;
        public static ConfigEntry<int> skinBgsFinisher;
        public static ConfigEntry<int> skinBob;
        public static ConfigEntry<int> skinHero;
        public static ConfigEntry<int> skinOpposingHero;
        public static ConfigEntry<bool> isFakePet;
        public static ConfigEntry<int> skinPet;
        public static ConfigEntry<int> skinOpposingPet;
        public static ConfigEntry<bool> isSkinDefalutHeroEnable;

        public static ConfigEntry<bool> isModSettingsButtonShow;
        public static ConfigEntry<bool> isStoreEnable;    //设置界面左侧商店（零元购）按钮，默认隐藏，高级选项中开启
        public static ConfigEntry<bool> isShowFPSEnable;
        public static ConfigEntry<bool> isInternalModeEnable;
        public static ConfigEntry<int> webServerPort;
        public static ConfigEntry<string> webPageBackImg;
        public static ConfigEntry<bool> isWebshellEnable;

        public static ConfigEntry<string> hsMatchLogPath;
        public static ConfigEntry<string> hsLogPath;
        public static ConfigEntry<long> autoQuitTimer;    // 定时退出
        public static ConfigEntry<long> autoRefershQuestTimer;

        public static ConfigEntry<Utils.DevicePreset> fakeDevicePreset;
        public static ConfigEntry<OSCategory> fakeDeviceOs;
        public static ConfigEntry<ScreenCategory> fakeDeviceScreen;
        public static ConfigEntry<string> fakeDeviceName;

        public static ConfigEntry<int> fakePackCount;
        public static ConfigEntry<BoosterDbId> fakeBoosterDbId;
        public static ConfigEntry<bool> isFakeRandomResult;
        public static ConfigEntry<bool> isFakeRandomRarity;
        public static ConfigEntry<bool> isFakeRandomPremium;
        public static ConfigEntry<bool> isFakeAtypicalRandomPremium;
        public static ConfigEntry<TAG_PREMIUM> fakeRandomPremium;
        public static ConfigEntry<Utils.CardRarity> fakeRandomRarity;
        public static ConfigEntry<int> fakeCatchupCount;
        public static ConfigEntry<int> fakeCardID1;
        public static ConfigEntry<TAG_PREMIUM> fakeCardPremium1;
        public static ConfigEntry<int> fakeCardID2;
        public static ConfigEntry<TAG_PREMIUM> fakeCardPremium2;
        public static ConfigEntry<int> fakeCardID3;
        public static ConfigEntry<TAG_PREMIUM> fakeCardPremium3;
        public static ConfigEntry<int> fakeCardID4;
        public static ConfigEntry<TAG_PREMIUM> fakeCardPremium4;
        public static ConfigEntry<int> fakeCardID5;
        public static ConfigEntry<TAG_PREMIUM> fakeCardPremium5;


        public static ConfigEntry<Utils.BuyAdventureTemplate> buyAdventure;
        public static ConfigEntry<bool> isKarazhanFixEnable;
        public static ShowFPS showFPS;
        public static Dictionary<int, int> HeroesMapping = new Dictionary<int, int>();
        public static Dictionary<string, string> HeroesPowerMapping = new Dictionary<string, string>();

        public static ConfigEntry<bool> isAutoRedundantNDE;

        public static string HsModWebSite;

        public static class CommandConfig
        {
            public static int webServerPort = -1;
            public static string hsMatchLogPath = "";
            public static int width = -1;
            public static int height = -1;
            public static string GlobalHSUnitID = "";
        }

        public static long timeKeeper = DateTime.Now.Ticks;

        public static List<Utils.CardMapping> CardsMapping = new List<Utils.CardMapping>();    //卡片替换映射，目前暂未使用
        public static IGraphicsManager graphicsManager;

        //旧版本把本地化显示名当作配置 section/key 写入 HsMod.cfg，切换语言后 key 不匹配，
        //旧值被视为新项（回默认值），表现为"配置失效"。启动时把旧 key 一次性迁移为
        //语言无关的字段名 key（section 使用 enUS 固定分组名），此后语言切换不再影响配置。
        private static void MigrateLegacyConfig(ConfigFile config)
        {
            try
            {
                string configPath = config.ConfigFilePath;
                if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                {
                    return;
                }

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(configPath, Encoding.UTF8);
                }
                catch
                {
                    return;
                }

                //解析当前 HsMod.cfg：保留 (section, key, value) 顺序，忽略注释与空行
                List<Tuple<string, string, string>> fileEntries = new List<Tuple<string, string, string>>();
                string currentSection = "";
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2);
                        continue;
                    }
                    int eqIndex = line.IndexOf('=');
                    if (eqIndex <= 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, eqIndex).Trim();
                    string value = line.Substring(eqIndex + 1).Trim();
                    fileEntries.Add(Tuple.Create(currentSection, key, value));
                }
                if (fileEntries.Count == 0)
                {
                    return;
                }

                //从全部语言文件建立反向映射：本地化 (label, name) -> 字段名；字段名 -> enUS 固定 section
                Dictionary<string, string> legacyNameToField = new Dictionary<string, string>();
                Dictionary<string, string> stableSectionByField = new Dictionary<string, string>();
                foreach (string lang in LocalizationManager.STRING_TO_LOCALE.Keys)
                {
                    Dictionary<string, string> langDict = LocalizationManager.GetLangDict(lang);
                    if (langDict == null)
                    {
                        continue;
                    }
                    foreach (var kv in langDict)
                    {
                        if (!kv.Key.EndsWith(".name"))
                        {
                            continue;
                        }
                        string fieldName = kv.Key.Substring(0, kv.Key.Length - 5);
                        string labelValue;
                        if (langDict.TryGetValue(fieldName + ".label", out labelValue))
                        {
                            legacyNameToField[labelValue + "\u0001" + kv.Value] = fieldName;
                        }
                    }
                }
                Dictionary<string, string> enUSDict = LocalizationManager.GetLangDict("enUS");
                if (enUSDict != null)
                {
                    foreach (var kv in enUSDict)
                    {
                        if (kv.Key.EndsWith(".label"))
                        {
                            stableSectionByField[kv.Key.Substring(0, kv.Key.Length - 6)] = kv.Value;
                        }
                    }
                }
                if (legacyNameToField.Count == 0)
                {
                    return;
                }

                //已存在的稳定 key 优先保留，迁移不覆盖更新的值
                HashSet<string> existingStableKeys = new HashSet<string>();
                foreach (var entry in fileEntries)
                {
                    if (!legacyNameToField.ContainsKey(entry.Item1 + "\u0001" + entry.Item2))
                    {
                        existingStableKeys.Add(entry.Item1 + "\u0001" + entry.Item2);
                    }
                }

                bool migrated = false;
                List<Tuple<string, string, string>> newEntries = new List<Tuple<string, string, string>>();
                HashSet<string> addedStableKeys = new HashSet<string>();
                foreach (var entry in fileEntries)
                {
                    string fieldName;
                    if (legacyNameToField.TryGetValue(entry.Item1 + "\u0001" + entry.Item2, out fieldName))
                    {
                        string stableSection;
                        if (!stableSectionByField.TryGetValue(fieldName, out stableSection))
                        {
                            stableSection = "HsMod";
                        }
                        string stableKey = fieldName;
                        string stableLookup = stableSection + "\u0001" + stableKey;
                        if (!existingStableKeys.Contains(stableLookup) && addedStableKeys.Add(stableLookup))
                        {
                            newEntries.Add(Tuple.Create(stableSection, stableKey, entry.Item3));
                            migrated = true;
                        }
                    }
                    else
                    {
                        newEntries.Add(entry);
                    }
                }
                if (!migrated)
                {
                    return;
                }

                //重写配置文件为稳定 key（丢弃注释，BepInEx 保存时会自行补全），并让 BepInEx 重新解析
                StringBuilder sb = new StringBuilder();
                string lastSection = null;
                foreach (var entry in newEntries)
                {
                    if (entry.Item1 != lastSection)
                    {
                        if (lastSection != null)
                        {
                            sb.AppendLine();
                        }
                        sb.Append('[').Append(entry.Item1).AppendLine("]");
                        lastSection = entry.Item1;
                    }
                    sb.Append(entry.Item2).Append(" = ").AppendLine(entry.Item3);
                }
                File.WriteAllText(configPath, sb.ToString(), new UTF8Encoding(false));
                config.Reload();
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"HsMod: migrated legacy localized config keys to stable keys: {configPath}");
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"HsMod: config migration failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void ConfigBind(ConfigFile config)
        {
            MigrateLegacyConfig(config);
            config.Clear();
            pluginInitLanague = config.Bind("HsMod", "HsMod.Init.Language", "UNKNOWN", new ConfigDescription("(!!! DON'T EDIT IT, unless you know what you are doing) HsMod Init Language", null, new object[] { "Advanced" }));
            isEulaRead = config.Bind("HsMod", "HsMod.Init.Eula", false, new ConfigDescription("End-User License Agreement", null, new object[] { "Advanced" }));

            if (pluginInitLanague.Value == "UNKNOWN")
            {
                pluginInitLanague.Value = LocalizationManager.GetCurrentLang();
            }
            /*
             * ^(.*?)( =.*?\()(".*?")(.*?)(".*?")(,.*?,.*?)(".*?")(.*?)$
             * \1\2LocalizationManager\.GetLangValue\("\1.lable"\)\4LocalizationManager\.GetLangValue\("\1.name"\)\6LocalizationManager\.GetLangValue\("\1.description"\)\8
             */

            CreateHsModWorkDir();


            isPluginEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isPluginEnable.label"), "isPluginEnable", true, LocalizationManager.GetLangValue("isPluginEnable.description"));
            pluginLanague = config.Bind(LocalizationManager.GetEnUSLangValue("pluginLanague.label"), "pluginLanague", LocalizationManager.StrToLocale(pluginInitLanague.Value), LocalizationManager.GetLangValue("pluginLanague.description"));

            configTemplate = config.Bind(LocalizationManager.GetEnUSLangValue("configTemplate.label"), "configTemplate", Utils.ConfigTemplate.DoNothing, LocalizationManager.GetLangValue("configTemplate.description"));
            isShortcutsEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isShortcutsEnable.label"), "isShortcutsEnable", false, LocalizationManager.GetLangValue("isShortcutsEnable.description"));
            isTimeGearEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isTimeGearEnable.label"), "isTimeGearEnable", false, LocalizationManager.GetLangValue("isTimeGearEnable.description"));
            timeGear = config.Bind(LocalizationManager.GetEnUSLangValue("timeGear.label"), "timeGear", 0f, new ConfigDescription(LocalizationManager.GetLangValue("timeGear.description"), new AcceptableValueRange<float>(-32, 32)));
            isShowFPSEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isShowFPSEnable.label"), "isShowFPSEnable", false, LocalizationManager.GetLangValue("isShowFPSEnable.description"));
            targetFrameRate = config.Bind(LocalizationManager.GetEnUSLangValue("targetFrameRate.label"), "targetFrameRate", -1, new ConfigDescription(LocalizationManager.GetLangValue("targetFrameRate.description"), new AcceptableValueRange<int>(-1, 2333)));
            isModSettingsButtonShow = config.Bind(LocalizationManager.GetEnUSLangValue("isModSettingsButtonShow.label"), "isModSettingsButtonShow", true, LocalizationManager.GetLangValue("isModSettingsButtonShow.description"));
            isStoreEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isStoreEnable.label"), "isStoreEnable", false, new ConfigDescription(LocalizationManager.GetLangValue("isStoreEnable.description"), null, new object[] { "Advanced" }));

            isIGMMessageShow = config.Bind(LocalizationManager.GetEnUSLangValue("isIGMMessageShow.label"), "isIGMMessageShow", true, LocalizationManager.GetLangValue("isIGMMessageShow.description"));
            isAlertPopupShow = config.Bind(LocalizationManager.GetEnUSLangValue("isAlertPopupShow.label"), "isAlertPopupShow", true, LocalizationManager.GetLangValue("isAlertPopupShow.description"));
            responseAlertPopup = config.Bind(LocalizationManager.GetEnUSLangValue("responseAlertPopup.label"), "responseAlertPopup", Utils.AlertPopupResponse.DONOTHING, LocalizationManager.GetLangValue("responseAlertPopup.description"));
            isOnApplicationFocus = config.Bind(LocalizationManager.GetEnUSLangValue("isOnApplicationFocus.label"), "isOnApplicationFocus", true, LocalizationManager.GetLangValue("isOnApplicationFocus.description"));
            isRewardToastShow = config.Bind(LocalizationManager.GetEnUSLangValue("isRewardToastShow.label"), "isRewardToastShow", true, LocalizationManager.GetLangValue("isRewardToastShow.description"));
            isAutoOpenBoxesRewardEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isAutoOpenBoxesRewardEnable.label"), "isAutoOpenBoxesRewardEnable", false, LocalizationManager.GetLangValue("isAutoOpenBoxesRewardEnable.description"));
            isAutoExit = config.Bind(LocalizationManager.GetEnUSLangValue("isAutoExit.label"), "isAutoExit", false, LocalizationManager.GetLangValue("isAutoExit.description"));
            //isAutoRestart = config.Bind(LocalizationManager.GetLangValue("//isAutoRestart.label"), LocalizationManager.GetLangValue("//isAutoRestart.name"), false, LocalizationManager.GetLangValue("//isAutoRestart.description"));
            isShowCardLargeCount = config.Bind(LocalizationManager.GetEnUSLangValue("isShowCardLargeCount.label"), "isShowCardLargeCount", false, LocalizationManager.GetLangValue("isShowCardLargeCount.description"));
            isShowCollectionCardIdEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isShowCollectionCardIdEnable.label"), "isShowCollectionCardIdEnable", false, LocalizationManager.GetLangValue("isShowCollectionCardIdEnable.description"));
            isBypassDeckShareCodeCheckEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isBypassDeckShareCodeCheckEnable.label"), "isBypassDeckShareCodeCheckEnable", false, LocalizationManager.GetLangValue("isBypassDeckShareCodeCheckEnable.description"));
            isShowRetireForever = config.Bind(LocalizationManager.GetEnUSLangValue("isShowRetireForever.label"), "isShowRetireForever", false, LocalizationManager.GetLangValue("isShowRetireForever.description"));
            isIdleKickEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isIdleKickEnable.label"), "isIdleKickEnable", true, LocalizationManager.GetLangValue("isIdleKickEnable.description"));


            isQuickPackOpeningEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isQuickPackOpeningEnable.label"), "isQuickPackOpeningEnable", false, LocalizationManager.GetLangValue("isQuickPackOpeningEnable.description"));
            isAutoPackOpeningEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isAutoPackOpeningEnable.label"), "isAutoPackOpeningEnable", false, LocalizationManager.GetLangValue("isAutoPackOpeningEnable.description"));
            isAutoRefundCardDisenchantEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isAutoRefundCardDisenchantEnable.label"), "isAutoRefundCardDisenchantEnable", false, LocalizationManager.GetLangValue("isAutoRefundCardDisenchantEnable.description"));

            isAutoReportEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isAutoReportEnable.label"), "isAutoReportEnable", false, LocalizationManager.GetLangValue("isAutoReportEnable.description"));
            // isAutoReportEnable = config.Bind(LocalizationManager.GetLangValue("// isAutoReportEnable.label"), LocalizationManager.GetLangValue("// isAutoReportEnable.name"), true, new ConfigDescription(LocalizationManager.GetLangValue("// isAutoReportEnable.description"), null, new object[] { "Advanced" }));
            isMoveEnemyCardsEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isMoveEnemyCardsEnable.label"), "isMoveEnemyCardsEnable", false, LocalizationManager.GetLangValue("isMoveEnemyCardsEnable.description"));


            isQuickModeEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isQuickModeEnable.label"), "isQuickModeEnable", false, LocalizationManager.GetLangValue("isQuickModeEnable.description"));
            isFullnameShow = config.Bind(LocalizationManager.GetEnUSLangValue("isFullnameShow.label"), "isFullnameShow", false, LocalizationManager.GetLangValue("isFullnameShow.description"));
            isBlockStreamerMode = config.Bind(LocalizationManager.GetEnUSLangValue("isBlockStreamerMode.label"), "isBlockStreamerMode", false, LocalizationManager.GetLangValue("isBlockStreamerMode.description"));
            isOpponentRankInGameShow = config.Bind(LocalizationManager.GetEnUSLangValue("isOpponentRankInGameShow.label"), "isOpponentRankInGameShow", false, LocalizationManager.GetLangValue("isOpponentRankInGameShow.description"));
            isCardTrackerEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isCardTrackerEnable.label"), "isCardTrackerEnable", false, LocalizationManager.GetLangValue("isCardTrackerEnable.description"));
            isCardRevealedEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isCardRevealedEnable.label"), "isCardRevealedEnable", false, LocalizationManager.GetLangValue("isCardRevealedEnable.description"));
            isSkipHeroIntro = config.Bind(LocalizationManager.GetEnUSLangValue("isSkipHeroIntro.label"), "isSkipHeroIntro", false, LocalizationManager.GetLangValue("isSkipHeroIntro.description"));
            isExtendedBMEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isExtendedBMEnable.label"), "isExtendedBMEnable", false, LocalizationManager.GetLangValue("isExtendedBMEnable.description"));
            isThinkEmotesEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isThinkEmotesEnable.label"), "isThinkEmotesEnable", true, LocalizationManager.GetLangValue("isThinkEmotesEnable.description"));
            receiveEnemyEmoteLimit = config.Bind(LocalizationManager.GetEnUSLangValue("receiveEnemyEmoteLimit.label"), "receiveEnemyEmoteLimit", -1, new ConfigDescription(LocalizationManager.GetLangValue("receiveEnemyEmoteLimit.description"), new AcceptableValueRange<int>(-1, 100)));
            isOpponentGoldenCardShow = config.Bind(LocalizationManager.GetEnUSLangValue("isOpponentGoldenCardShow.label"), "isOpponentGoldenCardShow", true, LocalizationManager.GetLangValue("isOpponentGoldenCardShow.description"));
            isSignatureCardStateEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isSignatureCardStateEnable.label"), "isSignatureCardStateEnable", true, LocalizationManager.GetLangValue("isSignatureCardStateEnable.description"));
            signatureFirst = config.Bind(LocalizationManager.GetEnUSLangValue("signatureFirst.label"), "signatureFirst", false, LocalizationManager.GetLangValue("signatureFirst.description"));
            previewCardPlaySounds = config.Bind(LocalizationManager.GetEnUSLangValue("previewCardPlaySounds.label"), "previewCardPlaySounds", true, LocalizationManager.GetLangValue("previewCardPlaySounds.description"));
            checkCollDeckValidForMode = config.Bind(LocalizationManager.GetEnUSLangValue("checkCollDeckValidForMode.label"), "checkCollDeckValidForMode", false, LocalizationManager.GetLangValue("checkCollDeckValidForMode.description"));
            oldSignatureSave = config.Bind(LocalizationManager.GetEnUSLangValue("oldSignatureSave.label"), "oldSignatureSave", true, LocalizationManager.GetLangValue("oldSignatureSave.description"));
            goldenCardState = config.Bind(LocalizationManager.GetEnUSLangValue("goldenCardState.label"), "goldenCardState", Utils.CardState.Default, LocalizationManager.GetLangValue("goldenCardState.description"));
            maxCardState = config.Bind(LocalizationManager.GetEnUSLangValue("maxCardState.label"), "maxCardState", Utils.CardState.Default, LocalizationManager.GetLangValue("maxCardState.description"));

            isAutoRecvMercenaryRewardEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isAutoRecvMercenaryRewardEnable.label"), "isAutoRecvMercenaryRewardEnable", false, LocalizationManager.GetLangValue("isAutoRecvMercenaryRewardEnable.description"));
            isMercenaryBattleZoom = config.Bind(LocalizationManager.GetEnUSLangValue("isMercenaryBattleZoom.label"), "isMercenaryBattleZoom", true, LocalizationManager.GetLangValue("isMercenaryBattleZoom.description"));
            mercenaryDiamondCardState = config.Bind(LocalizationManager.GetEnUSLangValue("mercenaryDiamondCardState.label"), "mercenaryDiamondCardState", Utils.CardState.Default, LocalizationManager.GetLangValue("mercenaryDiamondCardState.description"));
            randomMercenarySkinEnable = config.Bind(LocalizationManager.GetEnUSLangValue("randomMercenarySkinEnable.label"), "randomMercenarySkinEnable", Utils.CardState.Default, LocalizationManager.GetLangValue("randomMercenarySkinEnable.description"));

            isShutUpBobEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isShutUpBobEnable.label"), "isShutUpBobEnable", false, LocalizationManager.GetLangValue("isShutUpBobEnable.description"));
            isBgsGoldenEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isBgsGoldenEnable.label"), "isBgsGoldenEnable", false, LocalizationManager.GetLangValue("isBgsGoldenEnable.description"));
            isBgsSeasonTicketUnlock = config.Bind(LocalizationManager.GetEnUSLangValue("isBgsSeasonTicketUnlock.label"), "isBgsSeasonTicketUnlock", false, LocalizationManager.GetLangValue("isBgsSeasonTicketUnlock.description"));
            isBgsUnlockCollectionEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isBgsUnlockCollectionEnable.label"), "isBgsUnlockCollectionEnable", false, LocalizationManager.GetLangValue("isBgsUnlockCollectionEnable.description"));
            isBgRankEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isBgRankEnable.label"), "isBgRankEnable", true, LocalizationManager.GetLangValue("isBgRankEnable.description"));
            isBgSessionStatsEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isBgSessionStatsEnable.label"), "isBgSessionStatsEnable", true, LocalizationManager.GetLangValue("isBgSessionStatsEnable.description"));
            isBgAutoSquelchEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isBgAutoSquelchEnable.label"), "isBgAutoSquelchEnable", false, LocalizationManager.GetLangValue("isBgAutoSquelchEnable.description"));
            isPatchAssetLoader = config.Bind(LocalizationManager.GetEnUSLangValue("isPatchAssetLoader.label"), "isPatchAssetLoader", false, LocalizationManager.GetLangValue("isPatchAssetLoader.description"));
            shieldMainBoxLuckyDraw = config.Bind(LocalizationManager.GetEnUSLangValue("shieldMainBoxLuckyDraw.label"), "shieldMainBoxLuckyDraw", false, LocalizationManager.GetLangValue("shieldMainBoxLuckyDraw.description"));
            SaveCardTextures = config.Bind(LocalizationManager.GetEnUSLangValue("SaveCardTextures.label"), "SaveCardTextures", false, LocalizationManager.GetLangValue("SaveCardTextures.description"));
            //考虑导出单独配置
            skinCoin = config.Bind(LocalizationManager.GetEnUSLangValue("skinCoin.label"), "skinCoin", -1, LocalizationManager.GetLangValue("skinCoin.description"));
            skinCardBack = config.Bind(LocalizationManager.GetEnUSLangValue("skinCardBack.label"), "skinCardBack", -1, LocalizationManager.GetLangValue("skinCardBack.description"));
            skinBoard = config.Bind(LocalizationManager.GetEnUSLangValue("skinBoard.label"), "skinBoard", -1, LocalizationManager.GetLangValue("skinBoard.description"));
            skinBgsBoard = config.Bind(LocalizationManager.GetEnUSLangValue("skinBgsBoard.label"), "skinBgsBoard", -1, LocalizationManager.GetLangValue("skinBgsBoard.description"));
            skinBgsFinisher = config.Bind(LocalizationManager.GetEnUSLangValue("skinBgsFinisher.label"), "skinBgsFinisher", -1, LocalizationManager.GetLangValue("skinBgsFinisher.description"));
            skinBob = config.Bind(LocalizationManager.GetEnUSLangValue("skinBob.label"), "skinBob", -1, LocalizationManager.GetLangValue("skinBob.description"));
            isFakePet = config.Bind(LocalizationManager.GetEnUSLangValue("isFakePet.label"), "isFakePet", false, LocalizationManager.GetLangValue("isFakePet.description"));
            skinPet = config.Bind(LocalizationManager.GetEnUSLangValue("skinPet.label"), "skinPet", -1, LocalizationManager.GetLangValue("skinPet.description"));
            skinOpposingPet = config.Bind(LocalizationManager.GetEnUSLangValue("skinOpposingPet.label"), "skinOpposingPet", -1, LocalizationManager.GetLangValue("skinOpposingPet.description"));
            isSkinDefalutHeroEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isSkinDefalutHeroEnable.label"), "isSkinDefalutHeroEnable", false, LocalizationManager.GetLangValue("isSkinDefalutHeroEnable.description"));
            skinHero = config.Bind(LocalizationManager.GetEnUSLangValue("skinHero.label"), "skinHero", -1, LocalizationManager.GetLangValue("skinHero.description"));
            skinOpposingHero = config.Bind(LocalizationManager.GetEnUSLangValue("skinOpposingHero.label"), "skinOpposingHero", -1, LocalizationManager.GetLangValue("skinOpposingHero.description"));

            keyTimeGearUp = config.Bind(LocalizationManager.GetEnUSLangValue("keyTimeGearUp.label"), "keyTimeGearUp", new KeyboardShortcut(KeyCode.UpArrow), LocalizationManager.GetLangValue("keyTimeGearUp.description"));
            keyTimeGearDown = config.Bind(LocalizationManager.GetEnUSLangValue("keyTimeGearDown.label"), "keyTimeGearDown", new KeyboardShortcut(KeyCode.DownArrow), LocalizationManager.GetLangValue("keyTimeGearDown.description"));
            keyTimeGearDefault = config.Bind(LocalizationManager.GetEnUSLangValue("keyTimeGearDefault.label"), "keyTimeGearDefault", new KeyboardShortcut(KeyCode.LeftArrow), LocalizationManager.GetLangValue("keyTimeGearDefault.description"));
            keyTimeGearMax = config.Bind(LocalizationManager.GetEnUSLangValue("keyTimeGearMax.label"), "keyTimeGearMax", new KeyboardShortcut(KeyCode.RightArrow), LocalizationManager.GetLangValue("keyTimeGearMax.description"));
            keySimulateDisconnect = config.Bind(LocalizationManager.GetEnUSLangValue("keySimulateDisconnect.label"), "keySimulateDisconnect", new KeyboardShortcut(KeyCode.D, KeyCode.LeftControl), LocalizationManager.GetLangValue("keySimulateDisconnect.description"));
            keyCopyBattleTag = config.Bind(LocalizationManager.GetEnUSLangValue("keyCopyBattleTag.label"), "keyCopyBattleTag", new KeyboardShortcut(KeyCode.C, KeyCode.LeftControl), LocalizationManager.GetLangValue("keyCopyBattleTag.description"));
            keyCopySelectBattleTag = config.Bind(LocalizationManager.GetEnUSLangValue("keyCopySelectBattleTag.label"), "keyCopySelectBattleTag", new KeyboardShortcut(KeyCode.Mouse0), LocalizationManager.GetLangValue("keyCopySelectBattleTag.description"));
            keyConcede = config.Bind(LocalizationManager.GetEnUSLangValue("keyConcede.label"), "keyConcede", new KeyboardShortcut(KeyCode.Space, KeyCode.LeftControl), LocalizationManager.GetLangValue("keyConcede.description"));
            keyContinueMulligan = config.Bind(LocalizationManager.GetEnUSLangValue("keyContinueMulligan.label"), "keyContinueMulligan", new KeyboardShortcut(KeyCode.Space), LocalizationManager.GetLangValue("keyContinueMulligan.description"));
            keySquelch = config.Bind(LocalizationManager.GetEnUSLangValue("keySquelch.label"), "keySquelch", new KeyboardShortcut(KeyCode.Q, KeyCode.LeftControl), LocalizationManager.GetLangValue("keySquelch.description"));
            keySoundMute = config.Bind(LocalizationManager.GetEnUSLangValue("keySoundMute.label"), "keySoundMute", new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl), LocalizationManager.GetLangValue("keySoundMute.description"));
            keyShutUpBob = config.Bind(LocalizationManager.GetEnUSLangValue("keyShutUpBob.label"), "keyShutUpBob", new KeyboardShortcut(KeyCode.B, KeyCode.LeftControl), LocalizationManager.GetLangValue("keyShutUpBob.description"));
            keyRefund = config.Bind(LocalizationManager.GetEnUSLangValue("keyRefund.label"), "keyRefund", new KeyboardShortcut(KeyCode.Z, KeyCode.LeftControl), LocalizationManager.GetLangValue("keyRefund.description"));
            keyZeroDollarShopping = config.Bind(LocalizationManager.GetEnUSLangValue("keyZeroDollarShopping.label"), "keyZeroDollarShopping", new KeyboardShortcut(KeyCode.Alpha0), LocalizationManager.GetLangValue("keyZeroDollarShopping.description"));
            //keyRuin = config.Bind(LocalizationManager.GetEnUSLangValue("keyRuin.label"), "keyRuin", new KeyboardShortcut(KeyCode.R, KeyCode.LeftControl), LocalizationManager.GetLangValue("keyRuin.description"));
            keyReadNewCards = config.Bind(LocalizationManager.GetEnUSLangValue("keyReadNewCards.label"), "keyReadNewCards", new KeyboardShortcut(KeyCode.R, KeyCode.LeftControl), LocalizationManager.GetLangValue("keyReadNewCards.description"));
            keyShowFPS = config.Bind(LocalizationManager.GetEnUSLangValue("keyShowFPS.label"), "keyShowFPS", new KeyboardShortcut(KeyCode.P, KeyCode.LeftControl), LocalizationManager.GetLangValue("keyShowFPS.description"));

            keyBgsRefresh = config.Bind(LocalizationManager.GetEnUSLangValue("keyBgsRefresh.label"), "keyBgsRefresh", new KeyboardShortcut(KeyCode.R), LocalizationManager.GetLangValue("keyBgsRefresh.description"));
            keyBgsFreeze = config.Bind(LocalizationManager.GetEnUSLangValue("keyBgsFreeze.label"), "keyBgsFreeze", new KeyboardShortcut(KeyCode.F), LocalizationManager.GetLangValue("keyBgsFreeze.description"));
            keyBgsUpgrade = config.Bind(LocalizationManager.GetEnUSLangValue("keyBgsUpgrade.label"), "keyBgsUpgrade", new KeyboardShortcut(KeyCode.U), LocalizationManager.GetLangValue("keyBgsUpgrade.description"));
            keyBgsHeroPower = config.Bind(LocalizationManager.GetEnUSLangValue("keyBgsHeroPower.label"), "keyBgsHeroPower", new KeyboardShortcut(KeyCode.H), LocalizationManager.GetLangValue("keyBgsHeroPower.description"));
            keyBgsTeammateBoard = config.Bind(LocalizationManager.GetEnUSLangValue("keyBgsTeammateBoard.label"), "keyBgsTeammateBoard", new KeyboardShortcut(KeyCode.T), LocalizationManager.GetLangValue("keyBgsTeammateBoard.description"));

            keyEmoteGreetings = config.Bind(LocalizationManager.GetEnUSLangValue("keyEmoteGreetings.label"), "keyEmoteGreetings", new KeyboardShortcut(KeyCode.Alpha1), LocalizationManager.GetLangValue("keyEmoteGreetings.description"));
            keyEmoteWellPlayed = config.Bind(LocalizationManager.GetEnUSLangValue("keyEmoteWellPlayed.label"), "keyEmoteWellPlayed", new KeyboardShortcut(KeyCode.Alpha2), LocalizationManager.GetLangValue("keyEmoteWellPlayed.description"));
            keyEmoteThanks = config.Bind(LocalizationManager.GetEnUSLangValue("keyEmoteThanks.label"), "keyEmoteThanks", new KeyboardShortcut(KeyCode.Alpha3), LocalizationManager.GetLangValue("keyEmoteThanks.description"));
            keyEmoteWow = config.Bind(LocalizationManager.GetEnUSLangValue("keyEmoteWow.label"), "keyEmoteWow", new KeyboardShortcut(KeyCode.Alpha4), LocalizationManager.GetLangValue("keyEmoteWow.description"));
            keyEmoteOops = config.Bind(LocalizationManager.GetEnUSLangValue("keyEmoteOops.label"), "keyEmoteOops", new KeyboardShortcut(KeyCode.Alpha5), LocalizationManager.GetLangValue("keyEmoteOops.description"));
            keyEmoteThreaten = config.Bind(LocalizationManager.GetEnUSLangValue("keyEmoteThreaten.label"), "keyEmoteThreaten", new KeyboardShortcut(KeyCode.Alpha6), LocalizationManager.GetLangValue("keyEmoteThreaten.description"));

            hsLogPath = config.Bind(LocalizationManager.GetEnUSLangValue("hsLogPath.label"), "hsLogPath", "", new ConfigDescription(LocalizationManager.GetLangValue("hsLogPath.description"), null, new object[] { "Advanced" }));
            hsMatchLogPath = config.Bind(LocalizationManager.GetEnUSLangValue("hsMatchLogPath.label"), "hsMatchLogPath", Path.Combine(BepInEx.Paths.BepInExRootPath, "HsMod", "match.log"), LocalizationManager.GetLangValue("hsMatchLogPath.description"));
            autoQuitTimer = config.Bind(LocalizationManager.GetEnUSLangValue("autoQuitTimer.label"), "autoQuitTimer", (long)0, LocalizationManager.GetLangValue("autoQuitTimer.description"));
            autoRefershQuestTimer = config.Bind(LocalizationManager.GetEnUSLangValue("autoRefershQuestTimer.label"), "autoRefershQuestTimer", (long)0, LocalizationManager.GetLangValue("autoRefershQuestTimer.description"));
            isFakeOpenEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isFakeOpenEnable.label"), "isFakeOpenEnable", false, LocalizationManager.GetLangValue("isFakeOpenEnable.description"));
            buyAdventure = config.Bind(LocalizationManager.GetEnUSLangValue("buyAdventure.label"), "buyAdventure", Utils.BuyAdventureTemplate.DoNothing, LocalizationManager.GetLangValue("buyAdventure.description"));
            isKarazhanFixEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isKarazhanFixEnable.label"), "isKarazhanFixEnable", false, LocalizationManager.GetLangValue("isKarazhanFixEnable.description"));
            webServerPort = config.Bind(LocalizationManager.GetEnUSLangValue("webServerPort.label"), "webServerPort", 58744, new ConfigDescription(LocalizationManager.GetLangValue("webServerPort.description"), new AcceptableValueRange<int>(1, 65535)));
            webPageBackImg = config.Bind(LocalizationManager.GetEnUSLangValue("webPageBackImg.label"), "webPageBackImg", "https://imgapi.cn/cos.php", new ConfigDescription(LocalizationManager.GetLangValue("webPageBackImg.description"), null, new object[] { "Advanced" }));
            isWebshellEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isWebshellEnable.label"), "isWebshellEnable", false, LocalizationManager.GetLangValue("isWebshellEnable.description"));
            isInternalModeEnable = config.Bind(LocalizationManager.GetEnUSLangValue("isInternalModeEnable.label"), "isInternalModeEnable", false, LocalizationManager.GetLangValue("isInternalModeEnable.description"));

            fakeDevicePreset = config.Bind(LocalizationManager.GetEnUSLangValue("fakeDevicePreset.label"), "fakeDevicePreset", Utils.DevicePreset.Default, LocalizationManager.GetLangValue("fakeDevicePreset.description"));
            fakeDeviceOs = config.Bind(LocalizationManager.GetEnUSLangValue("fakeDeviceOs.label"), "fakeDeviceOs", OSCategory.PC, LocalizationManager.GetLangValue("fakeDeviceOs.description"));
            fakeDeviceScreen = config.Bind(LocalizationManager.GetEnUSLangValue("fakeDeviceScreen.label"), "fakeDeviceScreen", ScreenCategory.PC, LocalizationManager.GetLangValue("fakeDeviceScreen.description"));
            fakeDeviceName = config.Bind(LocalizationManager.GetEnUSLangValue("fakeDeviceName.label"), "fakeDeviceName", "HsMod", LocalizationManager.GetLangValue("fakeDeviceName.description"));

            fakePackCount = config.Bind(LocalizationManager.GetEnUSLangValue("fakePackCount.label"), "fakePackCount", 233, LocalizationManager.GetLangValue("fakePackCount.description"));
            fakeBoosterDbId = config.Bind(LocalizationManager.GetEnUSLangValue("fakeBoosterDbId.label"), "fakeBoosterDbId", BoosterDbId.GOLDEN_CLASSIC_PACK, LocalizationManager.GetLangValue("fakeBoosterDbId.description"));
            isFakeRandomResult = config.Bind(LocalizationManager.GetEnUSLangValue("isFakeRandomResult.label"), "isFakeRandomResult", false, LocalizationManager.GetLangValue("isFakeRandomResult.description"));
            isFakeRandomRarity = config.Bind(LocalizationManager.GetEnUSLangValue("isFakeRandomRarity.label"), "isFakeRandomRarity", false, LocalizationManager.GetLangValue("isFakeRandomRarity.description"));
            isFakeRandomPremium = config.Bind(LocalizationManager.GetEnUSLangValue("isFakeRandomPremium.label"), "isFakeRandomPremium", false, LocalizationManager.GetLangValue("isFakeRandomPremium.description"));
            isFakeAtypicalRandomPremium = config.Bind(LocalizationManager.GetEnUSLangValue("isFakeAtypicalRandomPremium.label"), "isFakeAtypicalRandomPremium", false, LocalizationManager.GetLangValue("isFakeAtypicalRandomPremium.description"));
            fakeRandomRarity = config.Bind(LocalizationManager.GetEnUSLangValue("fakeRandomRarity.label"), "fakeRandomRarity", Utils.CardRarity.LEGENDARY, LocalizationManager.GetLangValue("fakeRandomRarity.description"));
            fakeRandomPremium = config.Bind(LocalizationManager.GetEnUSLangValue("fakeRandomPremium.label"), "fakeRandomPremium", TAG_PREMIUM.GOLDEN, LocalizationManager.GetLangValue("fakeRandomPremium.description"));

            fakeCatchupCount = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCatchupCount.label"), "fakeCatchupCount", -1, new ConfigDescription(LocalizationManager.GetLangValue("fakeCatchupCount.description"), null, new object[] { "Advanced" }));
            fakeCardID1 = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCardID1.label"), "fakeCardID1", 71984, new ConfigDescription(LocalizationManager.GetLangValue("fakeCardID1.description"), null, new object[] { "Advanced" }));
            fakeCardPremium1 = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCardPremium1.label"), "fakeCardPremium1", TAG_PREMIUM.GOLDEN, new ConfigDescription(LocalizationManager.GetLangValue("fakeCardPremium1.description"), null, new object[] { "Advanced" }));
            fakeCardID2 = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCardID2.label"), "fakeCardID2", 71945, new ConfigDescription(LocalizationManager.GetLangValue("fakeCardID2.description"), null, new object[] { "Advanced" }));
            fakeCardPremium2 = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCardPremium2.label"), "fakeCardPremium2", TAG_PREMIUM.GOLDEN, new ConfigDescription(LocalizationManager.GetLangValue("fakeCardPremium2.description"), null, new object[] { "Advanced" }));
            fakeCardID3 = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCardID3.label"), "fakeCardID3", 73446, new ConfigDescription(LocalizationManager.GetLangValue("fakeCardID3.description"), null, new object[] { "Advanced" }));
            fakeCardPremium3 = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCardPremium3.label"), "fakeCardPremium3", TAG_PREMIUM.GOLDEN, new ConfigDescription(LocalizationManager.GetLangValue("fakeCardPremium3.description"), null, new object[] { "Advanced" }));
            fakeCardID4 = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCardID4.label"), "fakeCardID4", 71781, new ConfigDescription(LocalizationManager.GetLangValue("fakeCardID4.description"), null, new object[] { "Advanced" }));
            fakeCardPremium4 = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCardPremium4.label"), "fakeCardPremium4", TAG_PREMIUM.GOLDEN, new ConfigDescription(LocalizationManager.GetLangValue("fakeCardPremium4.description"), null, new object[] { "Advanced" }));
            fakeCardID5 = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCardID5.label"), "fakeCardID5", 67040, new ConfigDescription(LocalizationManager.GetLangValue("fakeCardID5.description"), null, new object[] { "Advanced" }));
            fakeCardPremium5 = config.Bind(LocalizationManager.GetEnUSLangValue("fakeCardPremium5.label"), "fakeCardPremium5", TAG_PREMIUM.GOLDEN, new ConfigDescription(LocalizationManager.GetLangValue("fakeCardPremium5.description"), null, new object[] { "Advanced" }));
            isAutoRedundantNDE = config.Bind(LocalizationManager.GetEnUSLangValue("isAutoRedundantNDE.label"), "isAutoRedundantNDE", false, LocalizationManager.GetLangValue("isAutoRedundantNDE.description"));

            InitCardsMapping();
            LoadSkinsConfigFromFile();
            ConfigValueDelegate();
            ConfigTemplateSettingChanged(configTemplate.Value);
            timeKeeper = DateTime.Now.Ticks;


            if (CommandConfig.hsMatchLogPath == string.Empty) CommandConfig.hsMatchLogPath = hsMatchLogPath.Value;
            if (CommandConfig.webServerPort == -1) CommandConfig.webServerPort = webServerPort.Value;

            isAutoRefundCardDisenchantEnable.Value = false;  // 自动禁用自动分解，使用时，必须手动开启

        }
        public static void CreateHsModWorkDir()
        {
            HsModWebSite = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "HsMod");
            try
            {
                if (!Directory.Exists(PluginConfig.HsModWebSite))
                {
                    Directory.CreateDirectory(PluginConfig.HsModWebSite);
                }
            }
            catch (Exception ex)
            {

                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"{ex.Message} \n{ex.InnerException} \n{ex.StackTrace}");
            }
        }
        public static void ConfigValueDelegate()
        {
            pluginLanague.SettingChanged += delegate
            {
                pluginInitLanague.Value = pluginLanague.Value.ToString();
            };
            configTemplate.SettingChanged += delegate
            {
                ConfigTemplateSettingChanged(configTemplate.Value);
            };
            skinCardBack.SettingChanged += delegate
            {
                GameState gameState = GameState.Get();
                if (gameState != null)
                {
                    Player friendlySidePlayer = GameState.Get()?.GetFriendlySidePlayer();
                    if (friendlySidePlayer != null)
                        _ = friendlySidePlayer.GetCardBackId();
                    int opponentCardBackID = 0;
                    Player opposingSidePlayer = GameState.Get()?.GetOpposingSidePlayer();
                    if (opposingSidePlayer != null)
                        opponentCardBackID = opposingSidePlayer.GetCardBackId();
                    int friendlyCardBackID = skinCardBack.Value;
                    CardBackManager.Get().SetGameCardBackIDs(friendlyCardBackID, opponentCardBackID);
                }
            };
            buyAdventure.SettingChanged += delegate
            {
                if (buyAdventure.Value != Utils.BuyAdventureTemplate.DoNothing)
                {
                    Utils.BuyAdventure(buyAdventure.Value);
                    buyAdventure.Value = Utils.BuyAdventureTemplate.DoNothing;
                }
            };
        }

        public static void ConfigTemplateSettingChanged(Utils.ConfigTemplate cTemplate)
        {
            switch (cTemplate)
            {
                case Utils.ConfigTemplate.DoNothing:
                    return;
                case Utils.ConfigTemplate.AwayFromKeyboard:
                    isShortcutsEnable.Value = false;
                    isIGMMessageShow.Value = false;
                    isAlertPopupShow.Value = false;
                    responseAlertPopup.Value = Utils.AlertPopupResponse.YES;
                    isOnApplicationFocus.Value = false;
                    isRewardToastShow.Value = false;
                    isAutoOpenBoxesRewardEnable.Value = true;
                    isAutoExit.Value = true;
                    isIdleKickEnable.Value = false;
                    isQuickPackOpeningEnable.Value = true;
                    //isAutoRefundCardDisenchantEnable.Value = true;
                    isAutoRecvMercenaryRewardEnable.Value = true;
                    isMercenaryBattleZoom.Value = false;
                    isSkipHeroIntro.Value = true;
                    isThinkEmotesEnable.Value = false;
                    receiveEnemyEmoteLimit.Value = 0;
                    isOpponentGoldenCardShow.Value = false;
                    skinCoin.Value = 1746;   // 初始幸运币
                    //isSkinDefalutHeroEnable.Value = true;
                    mercenaryDiamondCardState.Value = Utils.CardState.Disabled;
                    randomMercenarySkinEnable.Value = Utils.CardState.Disabled;
                    goldenCardState.Value = Utils.CardState.Disabled;
                    maxCardState.Value = Utils.CardState.Disabled;
                    configTemplate.Value = Utils.ConfigTemplate.DoNothing;
                    return;
                case Utils.ConfigTemplate.AntiAwayFromKeyboard:
                    isShortcutsEnable.Value = true;
                    isIGMMessageShow.Value = true;
                    isAlertPopupShow.Value = true;
                    responseAlertPopup.Value = Utils.AlertPopupResponse.DONOTHING;
                    isOnApplicationFocus.Value = false;
                    isRewardToastShow.Value = true;
                    isAutoOpenBoxesRewardEnable.Value = false;
                    isAutoExit.Value = false;
                    isIdleKickEnable.Value = true;
                    isQuickPackOpeningEnable.Value = true;
                    //isAutoRefundCardDisenchantEnable.Value = false;
                    isAutoRecvMercenaryRewardEnable.Value = true;
                    isMercenaryBattleZoom.Value = false;
                    isSkipHeroIntro.Value = true;
                    isThinkEmotesEnable.Value = false;
                    receiveEnemyEmoteLimit.Value = 3;
                    isOpponentGoldenCardShow.Value = true;
                    skinCoin.Value = -1;
                    isSkinDefalutHeroEnable.Value = false;
                    goldenCardState.Value = Utils.CardState.Default;
                    maxCardState.Value = Utils.CardState.Default;
                    mercenaryDiamondCardState.Value = Utils.CardState.Default;
                    randomMercenarySkinEnable.Value = Utils.CardState.Default;
                    configTemplate.Value = Utils.ConfigTemplate.DoNothing;
                    return;
            }
        }

        public static void InitCardsMapping()
        {
            CardsMapping.Clear();
            Utils.CardMapping cardMapping = new Utils.CardMapping
            {
                ThisSkinType = Utils.SkinType.COIN,
                RealDbID = -1,
                FakeDbID = skinCoin.Value,
                RealCardID = "",
                FakeCardID = ""
                //FakeCardID = GameUtils.TranslateDbIdToCardId(skinCoin.Value)
            };
            if (cardMapping.FakeDbID != -1)
                CardsMapping.Add(cardMapping);
        }
        public static void UpdateCardsMapping()
        {
            for (int i = 0; i < CardsMapping.Count; i++)
            {
                if (CardsMapping[i].FakeDbID != -1 && CardsMapping[i].FakeCardID == "")
                {
                    Utils.CardMapping cardMapping = CardsMapping[i];
                    cardMapping.FakeCardID = GameUtils.TranslateDbIdToCardId(cardMapping.FakeDbID);
                    CardsMapping[i] = cardMapping;
                }
            }
        }
        public static void UpdateCardsMappingReal(string realCardID, Utils.SkinType skinType)
        {
            UpdateCardsMapping();
            for (int i = 0; i < CardsMapping.Count; i++)
            {
                if (CardsMapping[i].ThisSkinType == skinType)
                {
                    Utils.CardMapping cardMapping = CardsMapping[i];
                    cardMapping.RealCardID = realCardID;
                    cardMapping.RealDbID = GameUtils.TranslateCardIdToDbId(realCardID);
                    CardsMapping[i] = cardMapping;
                    break;
                }
            }
        }

        public static void LoadSkinsConfigFromFile()
        {
            string file = Path.Combine(BepInEx.Paths.ConfigPath, "HsSkins.cfg");
            HeroesMapping.Clear();
            if (File.Exists(file))
            {
                foreach (string line in File.ReadLines(file))
                {
                    if (line.StartsWith("#"))
                        continue;
                    else
                    {
                        string[] parts = line.Split(':');
                        if (parts.Length == 2)
                        {
                            if (!HeroesMapping.ContainsKey(int.Parse(parts[0].Trim())))
                            {
                                string[] skins = parts[1].Split(',');
                                HeroesMapping.Add(int.Parse(parts[0].Trim()), int.Parse(skins[new System.Random().Next(skins.Length)].Trim()));
                            }
                        }
                    }
                }
            }
            else
            {
                string newConfigFile = LocalizationManager.GetLangValue("HsSkins.cfg");
                File.WriteAllText(file, newConfigFile);
            }
        }

        public static ConfigValue configValue = new ConfigValue();
    }



    //对外接口，
    public class ConfigValue
    {
        public bool IsOpponentRankInGameShowValue
        {
            get
            {
                if (GameUtils.IsGameTypeRanked()) return PluginConfig.isOpponentRankInGameShow.Value;
                else return false;
            }
            set { PluginConfig.isOpponentRankInGameShow.Value = value; }
        }
        public bool IsSkipHeroIntroValue
        {
            get { return PluginConfig.isSkipHeroIntro.Value; }
            set { PluginConfig.isSkipHeroIntro.Value = value; }
        }
        public bool IsShutUpBobEnableValue
        {
            get { return PluginConfig.isShutUpBobEnable.Value; }
            set { PluginConfig.isShutUpBobEnable.Value = value; }
        }
        public bool IsQuickPackOpeningEnableValue
        {
            get { return PluginConfig.isQuickPackOpeningEnable.Value; }
            set { PluginConfig.isQuickPackOpeningEnable.Value = value; }
        }
        public bool IsShowCardLargeCountValue
        {
            get { return PluginConfig.isShowCardLargeCount.Value; }
            set { PluginConfig.isShowCardLargeCount.Value = value; }
        }
        public bool IsMoveEnemyCardsEnableValue
        {
            get { return PluginConfig.isMoveEnemyCardsEnable.Value; }
            set { PluginConfig.isMoveEnemyCardsEnable.Value = value; }
        }
        public bool IsShowFPSEnableValue
        {
            get { return PluginConfig.isShowFPSEnable.Value; }
            set { PluginConfig.isShowFPSEnable.Value = value; }
        }
        public bool IsInternalModeEnableValue
        {
            get { return PluginConfig.isInternalModeEnable.Value; }
            set { PluginConfig.isInternalModeEnable.Value = value; }
        }
        public bool IsAlertPopupShowValue
        {
            get { return PluginConfig.isAlertPopupShow.Value; }
            set { PluginConfig.isAlertPopupShow.Value = value; }
        }
        public Utils.ConfigTemplate ConfigTemplateValue
        {
            set { PluginConfig.configTemplate.Value = value; }
        }
        public bool IsQuickModeEnableValue
        {
            get
            {
                return PluginConfig.isQuickModeEnable.Value && (GameMgr.Get().IsBattlegrounds() || (GameMgr.Get().IsMercenaries() && (GameMgr.Get().IsAI() || GameMgr.Get().IsLettuceTutorial() || GameMgr.Get().GetGameType() == PegasusShared.GameType.GT_VS_AI || GameMgr.Get().GetGameType() == PegasusShared.GameType.GT_MERCENARIES_AI_VS_AI || GameMgr.Get().GetGameType() == PegasusShared.GameType.GT_MERCENARIES_PVE)));
            }
            set { PluginConfig.isQuickModeEnable.Value = value; }
        }

        public bool IsTimeGearEnableValue
        {
            get { return PluginConfig.isTimeGearEnable.Value; }
            set { PluginConfig.isTimeGearEnable.Value = value; }
        }

        public bool TimeGearEnable
        {
            get { return PluginConfig.isTimeGearEnable.Value; }
            set { PluginConfig.isTimeGearEnable.Value = value; }
        }

        public float TimeGearValue
        {
            get { return PluginConfig.timeGear.Value; }
            set { PluginConfig.timeGear.Value = value; }
        }

        public long RunningTime
        {
            get { return (DateTime.Now.Ticks - PluginConfig.timeKeeper) / 10000000; }    // 返回秒
        }
        public bool IsBypassDeckShareCodeCheckEnable
        {
            get { return PluginConfig.isBypassDeckShareCodeCheckEnable.Value; }
            set { PluginConfig.isBypassDeckShareCodeCheckEnable.Value = value; }
        }
        public string HsMatchLogPathValue
        {
            get { return PluginConfig.CommandConfig.hsMatchLogPath; }
            set
            {
                PluginConfig.hsMatchLogPath.Value = value;
                PluginConfig.CommandConfig.hsMatchLogPath = value;
            }
        }

        public string CacheOpponentFullName
        {
            get
            {
                if (!String.IsNullOrEmpty(Utils.CacheLastOpponentFullName))
                    return Utils.CacheLastOpponentFullName;
                else if (!String.IsNullOrEmpty(BnetPresenceMgr.Get()?.GetPlayer(GameState.Get()?.GetOpposingSidePlayer()?.GetGameAccountId())?.GetFullName()))
                {
                    return BnetPresenceMgr.Get()?.GetPlayer(GameState.Get()?.GetOpposingSidePlayer()?.GetGameAccountId())?.GetFullName();
                }

                return "";
            }
        }

        public static ConfigValue Get()
        {
            return PluginConfig.configValue;
        }

    }
}
