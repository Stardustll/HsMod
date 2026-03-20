using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static HsMod.PluginConfig;

namespace HsMod
{
    public static class WebServer
    {
        public static HttpListener httpListener = new HttpListener
        {
            AuthenticationSchemes = AuthenticationSchemes.Anonymous
        };

        public static Task listenerTask;
        public static string shellCommand;
        public static bool shellCommandLock;

        // 皮肤图片缓存: dbfid -> OSS image URL
        private static ConcurrentDictionary<string, string> skinImageCache = new ConcurrentDictionary<string, string>();
        private static volatile bool skinImageCacheLoaded = false;
        private const string FBIGAME_OSS = "https://fbigame.oss-cn-beijing.aliyuncs.com/";
        private const string FBIGAME_PAGE = "https://fbigame.com/card";
        private const string FBIGAME_SEARCH = "https://fbigame.com/card/search";
        private static string SkinImageCacheFile => Path.Combine(BepInEx.Paths.ConfigPath, "HsSkinImages.json");
        public static bool pluginConfigLock;
        public static bool updateLock;

        public static void Restart()
        {
            try
            {
                httpListener.Stop();
                listenerTask = null;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex);
            }
            finally
            {
                try
                {
                    httpListener.Prefixes.Remove($"http://+:{CommandConfig.webServerPort}/");
                    Start();
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex);
                }
            }
        }


        public static void Start()
        {
            httpListener.Prefixes.Add($"http://+:{CommandConfig.webServerPort}/");
            httpListener.Start();
            listenerTask = Task.Run(ListenAsync);
        }

        private static async Task ListenAsync()
        {
            while (httpListener.IsListening)
            {
                var context = await httpListener.GetContextAsync(); // Fully asynchronous, non-blocking
                _ = Task.Run(() => ProcessRequestAsync(context));   // Start processing each request in a separate task
            }
        }

        private static async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            context.Response.StatusCode = 200;
            string rawUrLower = request.RawUrl.ToLower();

            Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"{request.RemoteEndPoint.ToString()} => {request.RawUrl}");
            Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"{DateTime.Now.ToString("yyyy/MM/dd_HH:mm:ss")} {request.Url}");

            if (rawUrLower == "/webshell" && request.HttpMethod == "POST" && !shellCommandLock)
            {
                shellCommandLock = true;

                string output = string.Empty;
                try
                {
                    // Read the JSON from the request body
                    using (var reader = new StreamReader(request.InputStream))
                    {
                        string requestBody = await reader.ReadToEndAsync();
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"POST: {requestBody}");
                        // Parse JSON and get the "command" field
                        var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody);

                        if (json != null && json.TryGetValue("command", out string command))
                        {
                            // Execute the command asynchronously
                            output = await WebApi.RunShellCommandAsync(command);
                        }
                        else
                        {
                            context.Response.StatusCode = 400; // Bad Request
                            output = "Invalid request: 'command' field is required.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500; // Internal Server Error
                    output = $"Error executing command: {ex.Message}";
                }
                finally
                {
                    shellCommandLock = false;
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync(output);
                    }
                }
            }
            else if (rawUrLower == "/config" && request.HttpMethod == "POST" && !pluginConfigLock)
            {
                pluginConfigLock = true;

                string output = string.Empty;
                try
                {
                    // Read the JSON from the request body
                    using (var reader = new StreamReader(request.InputStream))
                    {
                        string requestBody = await reader.ReadToEndAsync();
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"POST: {requestBody}");
                        // Parse JSON and get the "key" field
                        var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody);

                        if (json != null && json.TryGetValue("key", out string key) && json.TryGetValue("value", out string value))
                        {
                            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                            {

                                context.Response.StatusCode = 400; // Bad Request
                                output = "Invalid request: key or value not found.";
                            }
                            else
                            {
                                string realConfigKey = key.EndsWith(".name") ? key : LocalizationManager.GetLangKey(key);
                                if (!realConfigKey.EndsWith(".name"))
                                {
                                    context.Response.StatusCode = 400; // Bad Request
                                    output = "Invalid request: key not found.";
                                }
                                else
                                {
                                    context.Response.StatusCode = WebApi.RunPluginConfigAsync(realConfigKey, value, out string newValue);
                                    output = newValue;
                                }
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = 400; // Bad Request
                            output = "Invalid request: 'key' and 'value' field is required.";
                        }

                    }
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500; // Internal Server Error
                    output = $"Error config: {ex.Message}";
                }
                finally
                {
                    pluginConfigLock = false;
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync($"{{\"status\":{context.Response.StatusCode},\"output\":\"{output}\"}}");
                    }
                }
            }
            else if (rawUrLower == "/update" && request.HttpMethod == "POST" && !updateLock)
            {
                updateLock = true;

                string output = string.Empty;
                try
                {
                    // Read the JSON from the request body
                    using (var reader = new StreamReader(request.InputStream))
                    {
                        string requestBody = await reader.ReadToEndAsync();
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"POST: {requestBody}");
                        // Parse JSON and get the "key" field
                        var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(requestBody);

                        if (json != null && json.TryGetValue("key", out string key) && json.TryGetValue("value", out string value))
                        {
                            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                            {

                                context.Response.StatusCode = 400; // Bad Request
                                output = "Invalid request: key or value not found.";
                            }
                            else
                            {
                                if (key.ToLower().Equals("hsskins.cfg") || key.ToLower().Equals("hsskins"))
                                {
                                    context.Response.StatusCode = WebApi.UpdateHsSkinsCfg(value, out string newValue);
                                    output = newValue;
                                }
                                else
                                {
                                    context.Response.StatusCode = 400; // Bad Request
                                    output = "Invalid request: 'key' not support.";
                                }
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = 400; // Bad Request
                            output = "Invalid request: 'key' and 'value' field is required.";
                        }

                    }
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500; // Internal Server Error
                    output = $"Error update: {ex.Message}";
                }
                finally
                {
                    updateLock = false;
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync($"{{\"status\":{context.Response.StatusCode},\"output\":\"{output}\"}}");
                    }
                }
            }
            else if (rawUrLower == "/skinimages")
            {
                context.Response.ContentType = "application/json; charset=UTF-8";
                try
                {
                    string json = await GetSkinImagesJson();
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync(json);
                    }
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500;
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync($"{{\"error\":\"{ex.Message}\"}}");
                    }
                }
            }
            else if (rawUrLower == "/skinimages/refresh")
            {
                context.Response.ContentType = "application/json; charset=UTF-8";
                try
                {
                    skinImageCache.Clear();
                    skinImageCacheLoaded = false;
                    await FetchSkinImagesFromFbigame();
                    skinImageCacheLoaded = true;
                    SaveSkinImageCacheToFile();
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(skinImageCache);
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync(json);
                    }
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500;
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync($"{{\"error\":\"{ex.Message}\"}}");
                    }
                }
            }
            else
            {
                context.Response.ContentType = DetermineContentType(rawUrLower);

                string preUrl = DetermineFilePath(rawUrLower);
                if (VaildFilePath(preUrl))   // 优先查找本地文件
                {
                    context.Response.ContentType = GetMimeType(Path.GetExtension(preUrl));
                    var file = await File.ReadAllBytesAsync(preUrl);
                    await context.Response.OutputStream.WriteAsync(file, 0, file.Length);
                }
                else if (rawUrLower == "/safeimg")
                {
                    //var safeimg = Convert.FromBase64String(WebPage.SafeImg);
                    //context.Response.OutputStream.Write(safeimg, 0, safeimg.Length);
                }
                else
                {
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync(Route(rawUrLower).ToString());
                    }
                }
            }
            context.Response.OutputStream.Close();
        }


        private static string DetermineContentType(string rawUrl)
        {
            if (rawUrl.EndsWith(".js"))
                return "text/javascript; charset=UTF-8";
            if (rawUrl.EndsWith(".jpg") || rawUrl.EndsWith(".jpeg") || rawUrl == "/safeimg")
                return "image/jpeg";
            if (rawUrl.EndsWith(".txt") || rawUrl.EndsWith(".log") || rawUrl.EndsWith(".cfg"))
                return "text/plain; charset=UTF-8";
            return "text/html; charset=UTF-8";
        }

        private static string DetermineFilePath(string rawUrl)
        {
            string preUrl = rawUrl.Substring(1);
            switch (preUrl)
            {
                case "hslog": preUrl = hsLogPath.Value; break;
                // case "beplog": preUrl = "BepInEx/LogOutput.log"; break;
                default: preUrl = Path.Combine(PluginConfig.HsModWebSite, rawUrl.Substring(1)); break;
            }
            return preUrl;
        }
        private static bool VaildFilePath(string rawUrl)
        {
            if (File.Exists(rawUrl))
            {
                string rawUrlFilePath = Path.GetFullPath(rawUrl);
                string websitePath = Path.GetFullPath(PluginConfig.HsModWebSite);
                //string gameRootPath = Path.GetFullPath(BepInEx.Paths.GameRootPath);
                //string bepinexRootPath = Path.GetFullPath(BepInEx.Paths.BepInExRootPath);
                if (rawUrlFilePath.StartsWith(websitePath))
                {
                    return true;
                }
                return false;
            }
            return false;
        }

        /// <summary>
        /// 从 fbigame.com 按职业前缀搜索英雄皮肤图片映射 (dbfid -> OSS image URL)
        /// </summary>
        private static async Task FetchSkinImagesFromFbigame()
        {
            try
            {
                var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer(), UseCookies = true };
                using (var client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    // 1. GET page to obtain CSRF token
                    var pageResp = await client.GetAsync(FBIGAME_PAGE);
                    string pageHtml = await pageResp.Content.ReadAsStringAsync();

                    string csrf = "";
                    var csrfMatch = System.Text.RegularExpressions.Regex.Match(pageHtml, @"csrf-token""\s*content=""([^""]+)""");
                    if (csrfMatch.Success) csrf = csrfMatch.Groups[1].Value;

                    string xsrf = "";
                    var allCookies = handler.CookieContainer.GetCookies(new Uri(FBIGAME_PAGE));
                    foreach (System.Net.Cookie ck in allCookies)
                    {
                        if (ck.Name == "XSRF-TOKEN") { xsrf = ck.Value; break; }
                    }

                    if (string.IsNullOrEmpty(csrf)) return;

                    // 2. 按职业前缀搜索: HERO_01 ~ HERO_12
                    string[] prefixes = { "HERO_01", "HERO_02", "HERO_03", "HERO_04", "HERO_05",
                        "HERO_06", "HERO_07", "HERO_08", "HERO_09", "HERO_10", "HERO_11", "HERO_12" };

                    foreach (string prefix in prefixes)
                    {
                        try { await SearchFbigameHeroes(client, csrf, xsrf, prefix, 1); }
                        catch { }
                    }
                }
                Utils.MyLogger(BepInEx.Logging.LogLevel.Info, $"SkinImageCache loaded: {skinImageCache.Count} entries");
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"FetchSkinImages error: {ex.Message}");
            }
        }

        private static async Task SearchFbigameHeroes(HttpClient client, string csrf, string xsrf, string query, int page)
        {
            var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                searchQuery = query, page = page, mode = "list",
                zilliaxSearch = 0, sideboardType = 0, touristClassId = 0
            });
            var req = new HttpRequestMessage(HttpMethod.Post, FBIGAME_SEARCH);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            req.Headers.Add("X-CSRF-TOKEN", csrf);
            if (!string.IsNullOrEmpty(xsrf)) req.Headers.Add("X-XSRF-TOKEN", xsrf);
            req.Headers.Add("Referer", FBIGAME_PAGE);
            req.Headers.Add("Accept", "application/json");

            var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return;

            string json = await resp.Content.ReadAsStringAsync();
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            var dataArr = root["data"] as Newtonsoft.Json.Linq.JArray;
            if (dataArr == null) return;

            foreach (Newtonsoft.Json.Linq.JObject card in dataArr)
            {
                int cardType = card.ContainsKey("card_type") ? (int)card["card_type"] : 0;
                if (cardType != 3) continue;

                string dbfid = card.ContainsKey("dbfid") ? card["dbfid"].ToString() : "";
                if (string.IsNullOrEmpty(dbfid)) continue;

                string imgNormal = "";
                string tile = "";
                var imgObj = card["image"] as Newtonsoft.Json.Linq.JObject;
                if (imgObj != null && imgObj.ContainsKey("image_normal"))
                    imgNormal = imgObj["image_normal"].ToString();
                var tileObj = card["tile"] as Newtonsoft.Json.Linq.JObject;
                if (tileObj != null && tileObj.ContainsKey("image"))
                    tile = tileObj["image"].ToString();

                string imgPath = !string.IsNullOrEmpty(imgNormal) ? imgNormal : tile;
                if (!string.IsNullOrEmpty(imgPath))
                    skinImageCache[dbfid] = FBIGAME_OSS + imgPath + "?x-oss-process=style/hearthstone-image";
            }

            int lastPage = root.ContainsKey("last_page") ? (int)root["last_page"] : 1;
            if (page < lastPage)
                await SearchFbigameHeroes(client, csrf, xsrf, query, page + 1);
        }

        /// <summary>
        /// 获取皮肤图片JSON映射 (dbfid -> imageUrl)
        /// 优先从本地缓存文件加载，无缓存时从 fbigame 获取并保存
        /// </summary>
        private static async Task<string> GetSkinImagesJson()
        {
            if (!skinImageCacheLoaded)
            {
                // 1. 优先从本地文件加载
                if (LoadSkinImageCacheFromFile())
                {
                    skinImageCacheLoaded = true;
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Info, $"SkinImageCache loaded from file: {skinImageCache.Count} entries");
                }
                else
                {
                    // 2. 本地无缓存，从 fbigame 获取
                    await FetchSkinImagesFromFbigame();
                    skinImageCacheLoaded = true;
                    // 3. 保存到本地
                    SaveSkinImageCacheToFile();
                }
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject(skinImageCache);
        }

        /// <summary>
        /// 从本地文件加载皮肤图片缓存
        /// </summary>
        private static bool LoadSkinImageCacheFromFile()
        {
            try
            {
                if (!File.Exists(SkinImageCacheFile)) return false;
                string json = File.ReadAllText(SkinImageCacheFile);
                var dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (dict == null || dict.Count == 0) return false;
                foreach (var kv in dict)
                    skinImageCache[kv.Key] = kv.Value;
                return true;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"LoadSkinImageCache error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将皮肤图片缓存保存到本地文件
        /// </summary>
        private static void SaveSkinImageCacheToFile()
        {
            try
            {
                if (skinImageCache.Count == 0) return;
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(skinImageCache);
                File.WriteAllText(SkinImageCacheFile, json);
                Utils.MyLogger(BepInEx.Logging.LogLevel.Info, $"SkinImageCache saved to file: {skinImageCache.Count} entries");
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"SaveSkinImageCache error: {ex.Message}");
            }
        }

        public static StringBuilder Route(string url = "")
        {
            switch (url)
            {
                case "/info":
                    return WebPage.InfoPage();
                case "/collection":
                    return WebPage.CollectionPage();
                case "/pack":
                    return WebPage.PackPage();
                case "/skins":
                    return WebPage.SkinsPage();
                case "/lettuce":
                    return WebPage.MercenariesLettucePage();
                case "/mercenaries":
                    return WebPage.MercenariesPage();
                case "/matchlog":
                    return WebPage.MatchLogPage();
                case "/alive":
                    return WebPage.AlivePage();
                case "/bepinex.min.log":
                    return WebPage.BepInExLogPage(666);
                case "/bepinex.log":
                    return WebPage.BepInExLogPage();
                case "/hsmod.cfg":
                    return WebPage.HsModCfgPage("HsMod.cfg");
                case "/hsskins.cfg":
                    return WebPage.HsModCfgPage("HsSkins.cfg");
                case "":
                case "/":
                case "/home":
                    return WebPage.HomePage();
                case "/about":
                    return WebPage.AboutPage();
                case "/shell":
                    return WebPage.ShellPage();
                case "/jquery.min.js":
                    return new StringBuilder(FileManager.ReadEmbeddedFile("./WebResources/jquery.min.js"));
                default:
                    return new StringBuilder();
            }
        }

        static string GetMimeType(string extension)
        {
            var mimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".cfg", "text/plain; charset=UTF-8" },
                { ".txt", "text/plain; charset=UTF-8" },
                { ".log", "text/plain; charset=UTF-8" },
                { ".html", "text/html; charset=UTF-8" },
                { ".htm", "text/html; charset=UTF-8" },
                { ".css", "text/css; charset=UTF-8" },
                { ".js", "application/javascript; charset=UTF-8" },
                { ".json", "application/json; charset=UTF-8" },
                { ".xml", "application/xml; charset=UTF-8" },
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".png", "image/png" },
                { ".gif", "image/gif" },
                { ".svg", "image/svg+xml" },
                { ".bmp", "image/bmp" },
                { ".mp3", "audio/mpeg" },
                { ".wav", "audio/wav" },
                { ".mp4", "video/mp4" },
                { ".pdf", "application/pdf" },
                { ".zip", "application/zip" },
                { ".woff", "font/woff" },
                { ".woff2", "font/woff2" },
                { ".ttf", "font/ttf" },
                { ".eot", "application/vnd.ms-fontobject" },
                { ".otf", "font/otf" },
            };

            return mimeTypes.TryGetValue(extension, out var mimeType) ? mimeType : "application/octet-stream";
        }

    }
}
