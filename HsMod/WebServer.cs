using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
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

        // 皮肤图片缓存: 原画导出结果
        private static string SkinImageCacheDirectory => Path.Combine(BepInEx.Paths.ConfigPath, "HsSkinImages");
        private static readonly string[] SkinImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
        private static readonly string[] CardArtTextureMethodCandidates =
        {
            "GetStaticPortraitTexture",
            "TryGetPortraitTexture",
            "GetPreferredActorPortraitTexture",
            "GetPortraitTextureHandle"
        };
        private static readonly string[] CardArtAssetReferenceMethodCandidates =
        {
            "GetSignaturePortraitRef",
            "GetPremiumPortraitRef",
            "GetPortraitRef"
        };
        public static bool pluginConfigLock;
        public static bool updateLock;

        private sealed class CardArtExportResult
        {
            public string FilePath;
            public string ErrorMessage;
        }

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
            string rawUrLower = (request.Url != null ? request.Url.AbsolutePath : request.RawUrl).ToLowerInvariant();

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
            else if (rawUrLower == "/skinimage")
            {
                await HandleSkinImageRequestAsync(context);
            }
            else if (rawUrLower == "/cardart")
            {
                await HandleCardArtRequestAsync(context);
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
            if (rawUrl.EndsWith(".webp"))
                return "image/webp";
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

        private static async Task HandleSkinImageRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            string dbfId = (request.QueryString["id"] ?? request.QueryString["dbfid"] ?? string.Empty).Trim();
            string cardStringId = (request.QueryString["cardStringId"] ?? request.QueryString["cardid"] ?? request.QueryString["card_id"] ?? string.Empty).Trim();
            TAG_PREMIUM premium = ParsePremium(request.QueryString["premium"] ?? request.QueryString["quality"]);

            if (string.IsNullOrEmpty(dbfId) && string.IsNullOrEmpty(cardStringId))
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "text/plain; charset=UTF-8";
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    await writer.WriteAsync("Missing image id.");
                }
                return;
            }

            string localPath = FindLocalSkinImagePath(dbfId, cardStringId, premium);
            if (!string.IsNullOrEmpty(localPath))
            {
                await WriteSkinImageFileAsync(context, localPath);
                return;
            }

            CardArtExportResult exportResult = await TryExportCardArtAsync(dbfId, cardStringId, premium);
            if (exportResult != null && !string.IsNullOrEmpty(exportResult.FilePath) && File.Exists(exportResult.FilePath))
            {
                await WriteSkinImageFileAsync(context, exportResult.FilePath);
                return;
            }

            context.Response.StatusCode = 404;
            context.Response.ContentType = "text/plain; charset=UTF-8";
            using (var writer = new StreamWriter(context.Response.OutputStream))
            {
                await writer.WriteAsync(string.IsNullOrEmpty(exportResult?.ErrorMessage) ? "Skin image not found." : exportResult.ErrorMessage);
            }
        }

        private static async Task HandleCardArtRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            string dbfId = (request.QueryString["id"] ?? request.QueryString["dbfid"] ?? string.Empty).Trim();
            string cardStringId = (request.QueryString["cardStringId"] ?? request.QueryString["cardid"] ?? request.QueryString["card_id"] ?? string.Empty).Trim();
            TAG_PREMIUM premium = ParsePremium(request.QueryString["premium"] ?? request.QueryString["quality"]);

            if (string.IsNullOrEmpty(dbfId) && string.IsNullOrEmpty(cardStringId))
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "text/plain; charset=UTF-8";
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    await writer.WriteAsync("Missing card id.");
                }
                return;
            }

            string localPath = FindLocalCardArtPath(dbfId, cardStringId, premium);
            if (!string.IsNullOrEmpty(localPath))
            {
                await WriteSkinImageFileAsync(context, localPath);
                return;
            }

            CardArtExportResult exportResult = await TryExportCardArtAsync(dbfId, cardStringId, premium);
            if (exportResult != null && !string.IsNullOrEmpty(exportResult.FilePath) && File.Exists(exportResult.FilePath))
            {
                await WriteSkinImageFileAsync(context, exportResult.FilePath);
                return;
            }

            context.Response.StatusCode = 404;
            context.Response.ContentType = "text/plain; charset=UTF-8";
            using (var writer = new StreamWriter(context.Response.OutputStream))
            {
                await writer.WriteAsync(string.IsNullOrEmpty(exportResult?.ErrorMessage) ? "Card art not found." : exportResult.ErrorMessage);
            }
        }

        private static async Task<CardArtExportResult> TryExportCardArtAsync(string dbfId, string cardStringId, TAG_PREMIUM premium)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() => TryExportCardArtOnMainThread(dbfId, cardStringId, premium));
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"ExportCardArtAsync error: {ex.Message}");
                return new CardArtExportResult
                {
                    ErrorMessage = ex.Message
                };
            }
        }

        private static CardArtExportResult TryExportCardArtOnMainThread(string dbfId, string cardStringId, TAG_PREMIUM premium)
        {
            var result = new CardArtExportResult();
            try
            {
                string resolvedCardId = ResolveCardStringId(dbfId, cardStringId);
                if (string.IsNullOrEmpty(resolvedCardId))
                {
                    result.ErrorMessage = "Unable to resolve card id.";
                    return result;
                }

                string existingPath = FindLocalCardArtPath(dbfId, resolvedCardId, premium);
                if (!string.IsNullOrEmpty(existingPath))
                {
                    result.FilePath = existingPath;
                    return result;
                }

                string cachePath = Path.Combine(SkinImageCacheDirectory, BuildCardArtCacheKey(dbfId, resolvedCardId, premium) + ".png");
                Texture texture = TryResolveCardArtTexture(resolvedCardId, premium, out string textureError);
                if (texture == null)
                {
                    result.ErrorMessage = textureError;
                    return result;
                }

                if (!Utils.TryWriteTextureToPng(texture, cachePath))
                {
                    result.ErrorMessage = "Failed to encode portrait texture.";
                    return result;
                }

                result.FilePath = cachePath;
                return result;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"ExportCardArt error: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private static TAG_PREMIUM ParsePremium(string rawPremium)
        {
            if (string.IsNullOrWhiteSpace(rawPremium))
                return TAG_PREMIUM.NORMAL;

            string normalized = rawPremium.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "golden":
                case "gold":
                case "1":
                    return TAG_PREMIUM.GOLDEN;
                case "diamond":
                case "2":
                    return TAG_PREMIUM.DIAMOND;
                case "signature":
                case "3":
                    return TAG_PREMIUM.SIGNATURE;
                default:
                    return TAG_PREMIUM.NORMAL;
            }
        }

        private static string ResolveCardStringId(string dbfId, string cardStringId)
        {
            if (!string.IsNullOrWhiteSpace(cardStringId))
                return cardStringId.Trim();

            if (int.TryParse(dbfId, out int dbId))
            {
                try
                {
                    return GameUtils.TranslateDbIdToCardId(dbId);
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"ResolveCardStringId error: {ex.Message}");
                }
            }

            return string.Empty;
        }

        private static string FindLocalCardArtPath(string dbfId, string cardStringId, TAG_PREMIUM premium)
        {
            return FindLocalSkinImagePath(dbfId, cardStringId, premium);
        }

        private static string BuildCardArtCacheKey(string dbfId, string cardStringId, TAG_PREMIUM premium)
        {
            string baseKey = !string.IsNullOrWhiteSpace(cardStringId) ? cardStringId.Trim() : dbfId.Trim();
            if (string.IsNullOrEmpty(baseKey))
                baseKey = "unknown";

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                baseKey = baseKey.Replace(invalidChar, '_');

            if (premium != TAG_PREMIUM.NORMAL)
                baseKey = baseKey + "-" + premium.ToString().ToUpperInvariant();

            return baseKey;
        }

        private static string FindExportedCardTextureDownloadPath(string cardStringId, TAG_PREMIUM premium)
        {
            if (string.IsNullOrWhiteSpace(cardStringId))
                return string.Empty;

            string directory = Utils.GetCardTexturesDownloadDirectory();
            if (!Directory.Exists(directory))
                return string.Empty;

            string[] files = Directory.GetFiles(directory, "*-" + cardStringId.Trim() + ".png");
            if (files == null || files.Length == 0)
                return string.Empty;

            if (premium == TAG_PREMIUM.SIGNATURE)
            {
                string signatureFile = files.FirstOrDefault(file =>
                    Path.GetFileNameWithoutExtension(file).IndexOf("-SIGNATURE-", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(signatureFile))
                    return signatureFile;
            }

            string normalFile = files.FirstOrDefault(file =>
                Path.GetFileNameWithoutExtension(file).IndexOf("-SIGNATURE-", StringComparison.OrdinalIgnoreCase) < 0);
            return !string.IsNullOrEmpty(normalFile) ? normalFile : files[0];
        }

        private static Texture TryResolveCardArtTexture(string cardStringId, TAG_PREMIUM premium, out string errorMessage)
        {
            errorMessage = string.Empty;
            var defLoader = DefLoader.Get();
            if (defLoader == null)
            {
                errorMessage = "DefLoader not ready.";
                return null;
            }

            object disposableCardDef = null;
            try
            {
                disposableCardDef = defLoader.GetCardDef(cardStringId, premium);
                object cardDef = ExtractCardDefInstance(disposableCardDef);
                Texture texture = TryResolveTextureFromObject(cardDef, premium);
                if (texture != null)
                    return texture;

                EntityDef entityDef = null;
                try
                {
                    entityDef = defLoader.GetEntityDef(cardStringId);
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"GetEntityDef error: {ex.Message}");
                }

                texture = TryResolveTextureFromPortraitAssetReference(entityDef, premium);
                if (texture != null)
                    return texture;

                errorMessage = "Portrait methods returned no texture.";
                return null;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"TryResolveCardArtTexture error: {ex.Message}");
                return null;
            }
            finally
            {
                DisposeIfNeeded(disposableCardDef);
            }
        }

        private static object ExtractCardDefInstance(object disposableCardDef)
        {
            if (disposableCardDef == null)
                return null;

            PropertyInfo cardDefProperty = disposableCardDef.GetType().GetProperty("CardDef", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (cardDefProperty != null)
                return cardDefProperty.GetValue(disposableCardDef, null);

            return disposableCardDef;
        }

        private static Texture TryResolveTextureFromObject(object target, TAG_PREMIUM premium)
        {
            if (target == null)
                return null;

            Type targetType = target.GetType();
            foreach (string methodName in CardArtTextureMethodCandidates)
            {
                foreach (MethodInfo method in targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                        continue;

                    if (TryInvokeTextureMethod(target, method, premium, out Texture texture) && texture != null)
                        return texture;
                }
            }

            foreach (PropertyInfo property in targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!property.CanRead)
                    continue;
                if (property.GetIndexParameters().Length > 0)
                    continue;
                if (!property.Name.Contains("Portrait") && !string.Equals(property.Name, "Texture2D", StringComparison.Ordinal))
                    continue;

                Texture texture = TryExtractTexture(property.GetValue(target, null));
                if (texture != null)
                    return texture;
            }

            foreach (FieldInfo field in targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!field.Name.Contains("Portrait") && !string.Equals(field.Name, "Texture2D", StringComparison.Ordinal))
                    continue;

                Texture texture = TryExtractTexture(field.GetValue(target));
                if (texture != null)
                    return texture;
            }

            return null;
        }

        private static bool TryInvokeTextureMethod(object target, MethodInfo method, TAG_PREMIUM premium, out Texture texture)
        {
            texture = null;
            object[] arguments = BuildInvocationArguments(method, premium);
            if (arguments == null)
                return false;

            object returnValue = method.Invoke(target, arguments);
            texture = TryExtractTexture(returnValue);
            if (texture != null)
                return true;

            ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!parameters[i].ParameterType.IsByRef && !parameters[i].IsOut)
                    continue;

                texture = TryExtractTexture(arguments[i]);
                if (texture != null)
                    return true;
            }

            return false;
        }

        private static Texture TryResolveTextureFromPortraitAssetReference(object entityDef, TAG_PREMIUM premium)
        {
            if (entityDef == null)
                return null;

            foreach (string methodName in ResolveCardArtAssetReferenceMethodOrder(premium))
            {
                foreach (MethodInfo method in entityDef.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                        continue;

                    object[] arguments = BuildInvocationArguments(method, premium);
                    if (arguments == null)
                        continue;

                    object assetReference = method.Invoke(entityDef, arguments);
                    Texture texture = TryLoadTextureFromAssetReference(assetReference);
                    if (texture != null)
                        return texture;
                }
            }

            return null;
        }

        private static IEnumerable<string> ResolveCardArtAssetReferenceMethodOrder(TAG_PREMIUM premium)
        {
            if (premium == TAG_PREMIUM.SIGNATURE)
            {
                return new[]
                {
                    CardArtAssetReferenceMethodCandidates[0],
                    CardArtAssetReferenceMethodCandidates[1],
                    CardArtAssetReferenceMethodCandidates[2]
                };
            }

            if (premium == TAG_PREMIUM.GOLDEN || premium == TAG_PREMIUM.DIAMOND)
            {
                return new[]
                {
                    CardArtAssetReferenceMethodCandidates[1],
                    CardArtAssetReferenceMethodCandidates[2],
                    CardArtAssetReferenceMethodCandidates[0]
                };
            }

            return new[]
            {
                CardArtAssetReferenceMethodCandidates[2],
                CardArtAssetReferenceMethodCandidates[1],
                CardArtAssetReferenceMethodCandidates[0]
            };
        }

        private static object[] BuildInvocationArguments(MethodInfo method, TAG_PREMIUM premium)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0)
                return Array.Empty<object>();

            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!TryBuildInvocationArgument(parameters[i], premium, out object argument))
                    return null;
                args[i] = argument;
            }
            return args;
        }

        private static bool TryBuildInvocationArgument(ParameterInfo parameter, TAG_PREMIUM premium, out object argument)
        {
            Type parameterType = parameter.ParameterType;
            Type actualType = parameterType.IsByRef ? parameterType.GetElementType() : parameterType;
            argument = null;

            if (actualType == null)
                return false;

            if (actualType == typeof(TAG_PREMIUM))
            {
                argument = premium;
                return true;
            }

            if (actualType == typeof(string))
            {
                argument = string.Empty;
                return true;
            }

            if (actualType == typeof(bool))
            {
                argument = false;
                return true;
            }

            if (actualType.IsEnum)
            {
                argument = GetDefaultEnumValue(actualType);
                return true;
            }

            if (!actualType.IsValueType)
            {
                argument = null;
                return true;
            }

            try
            {
                argument = Activator.CreateInstance(actualType);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object GetDefaultEnumValue(Type enumType)
        {
            Array values = Enum.GetValues(enumType);
            if (values.Length <= 0)
                return Activator.CreateInstance(enumType);

            foreach (var value in values)
            {
                string name = value.ToString();
                if (string.Equals(name, "None", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Normal", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }

            return values.GetValue(0);
        }

        private static Texture TryExtractTexture(object value)
        {
            if (value == null)
                return null;

            if (value is Texture texture)
                return texture;

            Type valueType = value.GetType();
            PropertyInfo assetProperty = valueType.GetProperty("Asset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (assetProperty != null)
            {
                Texture assetTexture = TryExtractTexture(assetProperty.GetValue(value, null));
                if (assetTexture != null)
                    return assetTexture;
            }

            foreach (PropertyInfo property in valueType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!property.CanRead)
                    continue;
                if (property.GetIndexParameters().Length > 0)
                    continue;
                if (!typeof(Texture).IsAssignableFrom(property.PropertyType))
                    continue;

                Texture propertyTexture = property.GetValue(value, null) as Texture;
                if (propertyTexture != null)
                    return propertyTexture;
            }

            foreach (FieldInfo field in valueType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(Texture).IsAssignableFrom(field.FieldType))
                    continue;

                Texture fieldTexture = field.GetValue(value) as Texture;
                if (fieldTexture != null)
                    return fieldTexture;
            }

            return null;
        }

        private static Texture TryLoadTextureFromAssetReference(object assetReference)
        {
            if (assetReference == null)
                return null;

            try
            {
                object assetLoader = AssetLoader.Get();
                if (assetLoader == null)
                    return null;

                MethodInfo loadAssetMethod = assetLoader.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(method => method.Name == "LoadAsset" && method.IsGenericMethodDefinition && method.GetParameters().Length == 2);

                if (loadAssetMethod == null)
                    return null;

                ParameterInfo[] parameters = loadAssetMethod.GetParameters();
                object loadingOptions = parameters[1].ParameterType.IsEnum
                    ? Activator.CreateInstance(parameters[1].ParameterType)
                    : null;
                object assetHandle = loadAssetMethod.MakeGenericMethod(typeof(Texture2D))
                    .Invoke(assetLoader, new[] { assetReference, loadingOptions });

                return TryExtractTexture(assetHandle);
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"TryLoadTextureFromAssetReference error: {ex.Message}");
                return null;
            }
        }

        private static void DisposeIfNeeded(object value)
        {
            if (value is IDisposable disposable)
                disposable.Dispose();
        }

        private static async Task WriteSkinImageFileAsync(HttpListenerContext context, string filePath)
        {
            context.Response.ContentType = GetMimeType(Path.GetExtension(filePath));
            byte[] file = await File.ReadAllBytesAsync(filePath);
            await context.Response.OutputStream.WriteAsync(file, 0, file.Length);
        }

        private static string FindLocalSkinImagePath(string dbfId, string cardStringId, TAG_PREMIUM premium)
        {
            string resolvedCardId = ResolveCardStringId(dbfId, cardStringId);
            foreach (string cacheKey in GetSkinImageCacheKeys(dbfId, resolvedCardId, premium))
            {
                foreach (string extension in SkinImageExtensions)
                {
                    string filePath = Path.Combine(SkinImageCacheDirectory, cacheKey + extension);
                    if (File.Exists(filePath))
                        return filePath;
                }
            }

            if (!string.IsNullOrEmpty(resolvedCardId))
            {
                string exportedPath = FindExportedCardTextureDownloadPath(resolvedCardId, premium);
                if (!string.IsNullOrEmpty(exportedPath))
                    return exportedPath;
            }

            return string.Empty;
        }

        private static IEnumerable<string> GetSkinImageCacheKeys(string dbfId, string cardStringId, TAG_PREMIUM premium)
        {
            var keys = new List<string>();
            string primaryKey = BuildCardArtCacheKey(dbfId, cardStringId, premium);
            if (!string.IsNullOrEmpty(primaryKey))
                keys.Add(primaryKey);

            if (premium != TAG_PREMIUM.NORMAL)
            {
                string fallbackKey = BuildCardArtCacheKey(dbfId, cardStringId, TAG_PREMIUM.NORMAL);
                if (!string.IsNullOrEmpty(fallbackKey) && !keys.Contains(fallbackKey))
                    keys.Add(fallbackKey);
            }

            return keys;
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
                { ".webp", "image/webp" },
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
