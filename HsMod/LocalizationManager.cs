using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using static HsMod.PluginConfig;

namespace HsMod
{
    public class LocalizationManager
    {
        public static string GetCurrentLang()
        {
            //优先使用炉石客户端自身的语言：Unity的CurrentCulture在部分环境下不可靠
            //（可能返回Invariant或与系统区域不符），导致语言"偶尔检测错误"
            string res = null;
            string gameLocale = "N/A";
            try
            {
                Locale locale = Localization.GetLocale();
                gameLocale = locale.ToString();
                if (locale != Locale.UNKNOWN && STRING_TO_LOCALE.ContainsKey(gameLocale))
                    res = gameLocale;
            }
            catch { }

            string cultureCode = "";
            if (string.IsNullOrEmpty(res))
            {
                try
                {
                    cultureCode = System.Globalization.CultureInfo.CurrentCulture.Name.Replace("-", "");
                    if (STRING_TO_LOCALE.ContainsKey(cultureCode))
                        res = cultureCode;
                }
                catch { }
            }

            //都取不到有效值时返回UNKNOWN：不持久化错误结果，下次启动可重新检测
            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"CurrentCulture: {cultureCode}, Hearthstone {gameLocale}, detected {res ?? "UNKNOWN"}, HsMod {pluginInitLanague.Value}.");
            return string.IsNullOrEmpty(res) ? "UNKNOWN" : res;
        }

        public static string GetLangFileContext(string lang)
        {
            //string localName = GetCurrentLang();
            string fileName = $"./Languages/{lang}.json";
            string context = FileManager.ReadEmbeddedFile(fileName);
            if (String.IsNullOrEmpty(context))
            {
                fileName = $"./Languages/enUS.json";
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"HsMod languages file not found or empty, now using {fileName}");
            }
            context = FileManager.ReadEmbeddedFile(fileName);
            return context;
        }

        public static string CacheCurrentLangJson = "";
        public static string CacheEnUSLangJson = "";
        public static Dictionary<string, string> CacheCurrentLangJsonObj = null;
        public static Dictionary<string, string> CacheEnUSLangJsonObj = null;
        private static string cachedCurrentLangName;

        //语言切换（pluginLanague/pluginInitLanague 变化）后必须重建当前语言缓存，否则显示名反查会失败
        private static void EnsureCurrentLangCache()
        {
            string currentLang = string.IsNullOrEmpty(pluginInitLanague.Value) ? "enUS" : pluginInitLanague.Value;
            if (cachedCurrentLangName != currentLang || CacheCurrentLangJsonObj == null)
            {
                cachedCurrentLangName = currentLang;
                CacheCurrentLangJson = GetLangFileContext(currentLang);
                CacheCurrentLangJsonObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(CacheCurrentLangJson);
            }
        }

        private static void EnsureEnUSLangCache()
        {
            if (CacheEnUSLangJsonObj == null)
            {
                CacheEnUSLangJson = GetLangFileContext("enUS");
                CacheEnUSLangJsonObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(CacheEnUSLangJson);
            }
        }

        public static string GetLangValue(string lang_key)
        {
            EnsureCurrentLangCache();

            string res;
            if (CacheCurrentLangJsonObj.TryGetValue(lang_key, out res))
            {
                return res;
            }
            else
            {
                //Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Languages key '{lang_key}' not found.");
                EnsureEnUSLangCache();
                if (CacheEnUSLangJsonObj.TryGetValue(lang_key, out res))
                {
                    return res;
                }
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"Languages key '{lang_key}' not found.");
                return "VALUE_NOT_FOUND";
            }
        }

        //enUS 固定值，用于配置文件中语言无关的 section（如 "Global"、"Shortcut"），不受当前语言影响
        public static string GetEnUSLangValue(string lang_key)
        {
            EnsureEnUSLangCache();

            string res;
            if (CacheEnUSLangJsonObj.TryGetValue(lang_key, out res))
            {
                return res;
            }
            return lang_key;
        }

        //读取指定语言文件的原始字典；读取或解析失败时返回 null
        public static Dictionary<string, string> GetLangDict(string lang)
        {
            try
            {
                string json = FileManager.ReadEmbeddedFile($"./Languages/{lang}.json");
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch
            {
                return null;
            }
        }

        public static string GetLangKey(string lang_value)
        {
            EnsureCurrentLangCache();

            foreach (var key in CacheCurrentLangJsonObj)
            {
                if (key.Value == lang_value && key.Key.EndsWith(".name"))
                {
                    return key.Key;
                }
            }
            EnsureEnUSLangCache();

            foreach (var key in CacheEnUSLangJsonObj)
            {
                if (key.Value == lang_value && key.Key.EndsWith(".name"))
                {
                    return key.Key;
                }
            }

            Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"Languages value '{lang_value}' not found.");
            return "KEY_NOT_FOUND";

        }

        public static Locale StrToLocale(string lang)
        {
            STRING_TO_LOCALE.TryGetValue(lang, out var value);
            return value;
        }
        public static readonly Dictionary<string, Locale> STRING_TO_LOCALE = new Dictionary<string, Locale>
    {
        {
            "enUS",
            Locale.enUS
        },
        {
            "enGB",
            Locale.enGB
        },
        {
            "frFR",
            Locale.frFR
        },
        {
            "deDE",
            Locale.deDE
        },
        {
            "koKR",
            Locale.koKR
        },
        {
            "esES",
            Locale.esES
        },
        {
            "esMX",
            Locale.esMX
        },
        {
            "ruRU",
            Locale.ruRU
        },
        {
            "zhTW",
            Locale.zhTW
        },
        {
            "zhCN",
            Locale.zhCN
        },
        {
            "itIT",
            Locale.itIT
        },
        {
            "ptBR",
            Locale.ptBR
        },
        {
            "plPL",
            Locale.plPL
        },
        {
            "jaJP",
            Locale.jaJP
        },
        {
            "thTH",
            Locale.thTH
        }
    };

    }


}
