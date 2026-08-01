using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static HsMod.PluginConfig;

namespace HsMod
{
    public class WebApi
    {

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

            // Platform-specific settings for command execution
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

                // Set up asynchronous reading of output and error streams
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                        tcs.TrySetResult(true); // Mark as complete when output ends
                    else
                        outputBuilder.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                        tcs.TrySetResult(true); // Mark as complete when error output ends
                    else
                        outputBuilder.AppendLine(e.Data);
                };

                // Start the process and begin reading output and error
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Create a task to wait for the process to exit
                var processTask = Task.Run(() =>
                {
                    process.WaitForExit();
                    tcs.TrySetResult(true);
                });

                // Wait for the process to complete or timeout after 5 seconds
                var completedTask = await Task.WhenAny(processTask, Task.Delay(TimeSpan.FromSeconds(5)));
                if (completedTask == processTask)
                {
                    // Process completed within timeout
                    return outputBuilder.ToString();
                }
                else
                {
                    // Timeout occurred
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.Kill(); // Ensure the process is terminated on timeout
                        }
                        catch
                        {
                            // Ignore any exceptions if the process is already terminated
                        }
                    }
                    return string.Empty; // Return empty string on timeout
                }
            }
        }

        public static int UpdateHsSkinsCfg(string content, out string res)
        {
            res = string.Empty;

            try
            {
                File.WriteAllText(Path.Combine(BepInEx.Paths.ConfigPath, "HsSkins.cfg"), content);
                LoadSkinsConfigFromFile();
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
                key = key.Substring(0, key.Length - 5); // remove .name
                if (key.Equals("isWebshellEnable"))
                {
                    res = "not allow.";
                    return 403;
                }
                var configKeyProp = typeof(PluginConfig).GetField(key, BindingFlags.Public | BindingFlags.Static);
                if (configKeyProp == null)
                {
                    res = "key not found.";
                    return 501;

                }
                var configEntry = (ConfigEntryBase)configKeyProp.GetValue(null);
                var converter = TomlTypeConverter.GetConverter(configEntry.SettingType);
                if (converter != null)
                {
                    configEntry.SetSerializedValue(value);
                    res = configEntry.GetSerializedValue();
                    return 200;
                }
            }
            return 500;
        }

        public static string GetAllConfigMetadata(string lang = null)
        {
            // Use specified language or fall back to plugin default
            string targetLang = string.IsNullOrEmpty(lang) ? pluginInitLanague.Value : lang;

            // Load language file for the target language
            Dictionary<string, string> langDict = null;
            Dictionary<string, string> fallbackDict = null;

            try
            {
                string langJson = FileManager.ReadEmbeddedFile($"./Languages/{targetLang}.json");
                if (!string.IsNullOrEmpty(langJson))
                {
                    langDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(langJson);
                }
            }
            catch { }

            // Always load enUS as fallback
            try
            {
                string enUSJson = FileManager.ReadEmbeddedFile("./Languages/enUS.json");
                if (!string.IsNullOrEmpty(enUSJson))
                {
                    fallbackDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(enUSJson);
                }
            }
            catch { }

            // Helper function to get localized value
            Func<string, string, string> getLangValue = (key, defaultValue) =>
            {
                if (langDict != null && langDict.TryGetValue(key, out var val))
                    return val;
                if (fallbackDict != null && fallbackDict.TryGetValue(key, out var fallbackVal))
                    return fallbackVal;
                return defaultValue;
            };

            var configList = new List<Dictionary<string, object>>();
            var fields = typeof(PluginConfig).GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (!field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition() != typeof(ConfigEntry<>))
                    continue;

                var configEntry = field.GetValue(null) as ConfigEntryBase;
                if (configEntry == null)
                    continue;

                // Mark internal/advanced configs
                string fieldName = field.Name;
                bool isAdvanced = (fieldName == "pluginInitLanague" || fieldName == "isEulaRead" || fieldName == "isDynamicFpsEnable");

                // Get localized name, label, description
                string localizedName = getLangValue($"{fieldName}.name", configEntry.Definition.Key);
                string localizedLabel = getLangValue($"{fieldName}.label", configEntry.Definition.Section);
                string localizedDesc = getLangValue($"{fieldName}.description", configEntry.Description?.Description ?? "");

                var configItem = new Dictionary<string, object>
                {
                    ["key"] = fieldName,
                    ["name"] = localizedName,
                    ["label"] = localizedLabel,
                    ["description"] = localizedDesc,
                    ["type"] = GetConfigType(configEntry.SettingType),
                    ["value"] = configEntry.GetSerializedValue(),
                    ["isAdvanced"] = isAdvanced
                };

                // Handle enum types
                if (configEntry.SettingType.IsEnum)
                {
                    configItem["enumValues"] = Enum.GetNames(configEntry.SettingType);
                }

                // Handle AcceptableValueRange
                if (configEntry.Description?.AcceptableValues != null)
                {
                    var acceptableValues = configEntry.Description.AcceptableValues;
                    var acceptableType = acceptableValues.GetType();

                    if (acceptableType.IsGenericType)
                    {
                        var minProp = acceptableType.GetProperty("MinValue");
                        var maxProp = acceptableType.GetProperty("MaxValue");

                        if (minProp != null && maxProp != null)
                        {
                            configItem["min"] = minProp.GetValue(acceptableValues);
                            configItem["max"] = maxProp.GetValue(acceptableValues);
                        }
                    }
                }

                // Handle KeyboardShortcut type
                if (configEntry.SettingType == typeof(KeyboardShortcut))
                {
                    configItem["keyCodes"] = Enum.GetNames(typeof(KeyCode));
                }

                configList.Add(configItem);
            }

            // Group by label
            var grouped = configList
                .GroupBy(c => c["label"].ToString())
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new Dictionary<string, object>
            {
                ["language"] = pluginInitLanague.Value,
                ["groups"] = grouped
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(result);
        }

        private static string GetConfigType(Type type)
        {
            if (type == typeof(bool)) return "bool";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(float)) return "float";
            if (type == typeof(string)) return "string";
            if (type == typeof(KeyboardShortcut)) return "keyboard";
            if (type.IsEnum) return "enum";
            return "string";
        }

        //GET /api/skins：皮肤列表 JSON（type 缺省返回全部组）
        //SkinPanel.GetList 内部访问 GameDbf/DefLoader 等 Unity 数据，必须在主线程执行
        public static string GetSkinListJson(string type = null)
        {
            string result = null;
            Exception error = null;
            using (ManualResetEventSlim evt = new ManualResetEventSlim(false))
            {
                ModSettingsUI.RunOnMainThread(() =>
                {
                    try { result = BuildSkinListJson(type); }
                    catch (Exception ex) { error = ex; }
                    finally { evt.Set(); }
                });
                if (!evt.Wait(10000))
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, object> { ["success"] = false, ["error"] = "timeout" });
                }
            }
            if (error != null)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"WebApi.GetSkinListJson: {error.Message} \n{error.StackTrace}");
                return Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, object> { ["success"] = false, ["error"] = error.Message });
            }
            return result;
        }

        //实际数据构建：经主线程调度后执行（BuildSkinListJson 只跑在主线程）
        private static string BuildSkinListJson(string type)
        {
            Dictionary<string, List<Dictionary<string, object>>> groups = new Dictionary<string, List<Dictionary<string, object>>>();
            string[] allTypes = { "cardBack", "coin", "board", "bgsBoard", "bgsFinisher", "hero", "bgsHero", "bob", "pet" };
            string[] wanted = string.IsNullOrEmpty(type) ? allTypes : new[] { type };
            foreach (string t in wanted)
            {
                SkinPanel.SkinType? st = SkinPanel.ParseType(t);
                if (st == null) continue;
                List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
                bool heroMapping = st.Value == SkinPanel.SkinType.Hero || st.Value == SkinPanel.SkinType.BgsHero;
                foreach (SkinPanel.SkinItem item in SkinPanel.GetList(st.Value))
                {
                    Dictionary<string, object> d = new Dictionary<string, object>
                    {
                        ["id"] = item.Id,
                        ["name"] = item.Name
                    };
                    if (item.ClassId >= 0) d["heroClass"] = SkinPanel.GetClassName(item.ClassId);
                    if (item.PetId >= 0) d["petId"] = item.PetId;
                    if (heroMapping && SkinPanel.HasMapping(item.Id))
                        d["mapping"] = SkinPanel.GetMappingTargets(item.Id);    //已映射的目标列表
                    items.Add(d);
                }
                groups[t] = items;
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["success"] = true,
                ["groups"] = groups,
                ["configNames"] = new Dictionary<string, string>()
            });
        }

        //POST /api/skins：映射保存/删除 或 直接设置皮肤
        //body: {action:"save"|"delete"|"set", type, src, targets?[], id?}
        public static int HandleSkinAction(string body, out string res)
        {
            res = string.Empty;
            try
            {
                var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
                if (json == null || !json.TryGetValue("action", out object actionObj))
                {
                    res = "invalid request";
                    return 400;
                }
                string action = actionObj.ToString();
                string type = json.TryGetValue("type", out object typeObj) ? typeObj.ToString() : "";
                SkinPanel.SkinType? st = SkinPanel.ParseType(type);
                if (st == null)
                {
                    res = "unknown skin type";
                    return 400;
                }
                if (action == "save" || action == "delete")
                {
                    if (!json.TryGetValue("src", out object srcObj) || !int.TryParse(srcObj.ToString(), out int src) || src <= 0)
                    {
                        res = "invalid src";
                        return 400;
                    }
                    List<int> targets = new List<int>();
                    if (action == "save")
                    {
                        if (json.TryGetValue("targets", out object targetsObj) && targetsObj is Newtonsoft.Json.Linq.JArray arr)
                        {
                            foreach (var token in arr)
                                if (int.TryParse(token.ToString(), out int tid) && tid > 0) targets.Add(tid);
                        }
                        if (targets.Count == 0)
                        {
                            res = "no targets";
                            return 400;
                        }
                        SkinPanel.SetMapping(src, targets);
                    }
                    else
                    {
                        SkinPanel.SetMapping(src, null);
                    }
                    SkinPanel.SaveMapping();
                    res = action == "save" ? "saved" : "deleted";
                    return 200;
                }
                if (action == "set")
                {
                    if (!json.TryGetValue("id", out object idObj) || !int.TryParse(idObj.ToString(), out int id))
                    {
                        res = "invalid id";
                        return 400;
                    }
                    SkinPanel.SetValue(st.Value, id > 0 ? id : -1);    //id<=0 表示取消设置
                    res = "set";
                    return 200;
                }
                res = "unknown action";
                return 400;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"WebApi.HandleSkinAction: {ex.Message} \n{ex.StackTrace}");
                res = ex.Message;
                return 500;
            }
        }

    }
}
