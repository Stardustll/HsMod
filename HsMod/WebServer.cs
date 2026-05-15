using Blizzard.T5.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
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
        private static readonly Type[] AssetLoadTypeCandidates =
        {
            typeof(Texture2D),
            typeof(Sprite),
            typeof(Material),
            typeof(GameObject)
        };
        private static readonly string[] AssetReferenceMemberHints = { "Asset", "Prefab", "Frame", "Texture", "Sprite", "Material", "Preview", "Thumbnail", "Icon" };
        private static readonly string[] PetMemberHints = { "Pet", "Icon", "Portrait", "Texture", "Sprite", "Material", "Prefab", "Asset", "Card" };
        private static readonly string[] CardBackMemberHints = { "CardBack", "Texture", "Material", "Frame", "Prefab", "Asset", "Highlight", "Back", "Image", "Portrait" };
        private static readonly string[] BattlegroundsBoardMemberHints = { "Board", "Texture", "Prefab", "Asset", "Preview", "Thumbnail", "FullBoard", "FullTavern", "Layout", "Details", "Movie", "Image" };
        private static readonly string[] BattlegroundsFinisherMemberHints = { "Finisher", "Texture", "Prefab", "Asset", "Preview", "Thumbnail", "Material", "Effect", "Details", "Movie", "Image", "Gameplay" };
        public static bool pluginConfigLock;
        public static bool updateLock;

        private static bool IsTruthyQuery(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("y", StringComparison.OrdinalIgnoreCase)
                || value.Equals("force", StringComparison.OrdinalIgnoreCase);
        }

        private enum SkinImageKind
        {
            Card,
            Pet,
            CardBack,
            BgsBoard,
            BgsFinisher,
            Board
        }

        private delegate Texture SkinTextureResolver(int dbfId, out string errorMessage);

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
                    context.Response.ContentType = "application/json; charset=UTF-8";
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new { status = context.Response.StatusCode, output = output }));
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
                    context.Response.ContentType = "application/json; charset=UTF-8";
                    using (var writer = new StreamWriter(context.Response.OutputStream))
                    {
                        await writer.WriteLineAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new { status = context.Response.StatusCode, output = output }));
                    }
                }
            }
            else if (rawUrLower == "/api/skins" && request.HttpMethod == "GET")
            {
                context.Response.ContentType = "application/json; charset=UTF-8";
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    await writer.WriteAsync(WebApi.GetSkinCatalogJson(request.QueryString["type"], IsTruthyQuery(request.QueryString["refresh"])));
                }
            }
            else if (rawUrLower == "/api/config" && request.HttpMethod == "GET")
            {
                context.Response.ContentType = "application/json; charset=UTF-8";
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    await writer.WriteAsync(WebApi.GetConfigCatalogJson(IsTruthyQuery(request.QueryString["refresh"])));
                }
            }
            else if (rawUrLower == "/api/packs" && request.HttpMethod == "GET")
            {
                context.Response.ContentType = "application/json; charset=UTF-8";
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    await writer.WriteAsync(WebApi.GetPackCatalogJson(IsTruthyQuery(request.QueryString["refresh"])));
                }
            }
            else if (rawUrLower == "/api/cache/clear" && request.HttpMethod == "POST")
            {
                WebApi.InvalidateApiCache(request.QueryString["prefix"]);
                context.Response.ContentType = "application/json; charset=UTF-8";
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    await writer.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, output = "cache cleared" }));
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
            SkinImageKind kind = ParseSkinImageKind(request.QueryString["type"] ?? request.QueryString["skinType"] ?? request.QueryString["kind"]);

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

            string localPath = FindLocalSkinImagePath(dbfId, cardStringId, premium, kind);
            if (!string.IsNullOrEmpty(localPath))
            {
                await WriteSkinImageFileAsync(context, localPath);
                return;
            }

            CardArtExportResult exportResult = await TryExportSkinImageAsync(dbfId, cardStringId, premium, kind);
            if (exportResult != null && !string.IsNullOrEmpty(exportResult.FilePath) && File.Exists(exportResult.FilePath))
            {
                await WriteSkinImageFileAsync(context, exportResult.FilePath);
                return;
            }

            context.Response.StatusCode = 404;
            context.Response.ContentType = "text/plain; charset=UTF-8";
            Utils.MyLogger(
                BepInEx.Logging.LogLevel.Warning,
                $"Skin image not found type={kind} id={dbfId} card={cardStringId} premium={premium}: {exportResult?.ErrorMessage}");
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
            return await TryExportSkinImageAsync(dbfId, cardStringId, premium, SkinImageKind.Card);
        }

        private static async Task<CardArtExportResult> TryExportSkinImageAsync(string dbfId, string cardStringId, TAG_PREMIUM premium, SkinImageKind kind)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() => TryExportSkinImageOnMainThread(dbfId, cardStringId, premium, kind));
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"ExportSkinImageAsync error: {ex.Message}");
                return new CardArtExportResult
                {
                    ErrorMessage = ex.Message
                };
            }
        }

        private static CardArtExportResult TryExportSkinImageOnMainThread(string dbfId, string cardStringId, TAG_PREMIUM premium, SkinImageKind kind)
        {
            switch (kind)
            {
                case SkinImageKind.Card:
                    return TryExportCardArtOnMainThread(dbfId, cardStringId, premium);
                case SkinImageKind.Pet:
                    return TryExportPetImageOnMainThread(dbfId, cardStringId, premium);
                case SkinImageKind.CardBack:
                    return TryExportDbfSkinImageOnMainThread(dbfId, cardStringId, premium, kind, TryResolveCardBackTexture);
                case SkinImageKind.BgsBoard:
                    return TryExportDbfSkinImageOnMainThread(dbfId, cardStringId, premium, kind, TryResolveBattlegroundsBoardTexture);
                case SkinImageKind.BgsFinisher:
                    return TryExportDbfSkinImageOnMainThread(dbfId, cardStringId, premium, kind, TryResolveBattlegroundsFinisherTexture);
                case SkinImageKind.Board:
                    return new CardArtExportResult
                    {
                        ErrorMessage = "Board preview export is not supported."
                    };
                default:
                    return TryExportCardArtOnMainThread(dbfId, cardStringId, premium);
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

        private static CardArtExportResult TryExportPetImageOnMainThread(string dbfId, string cardStringId, TAG_PREMIUM premium)
        {
            var result = new CardArtExportResult();
            try
            {
                string resolvedCardId = ResolvePetCardStringId(dbfId, cardStringId);
                if (string.IsNullOrEmpty(resolvedCardId))
                {
                    result.ErrorMessage = "Unable to resolve pet card id.";
                    return result;
                }

                string existingPath = FindLocalSkinImagePath(dbfId, resolvedCardId, premium, SkinImageKind.Pet);
                if (!string.IsNullOrEmpty(existingPath))
                {
                    result.FilePath = existingPath;
                    return result;
                }

                string cachePath = Path.Combine(SkinImageCacheDirectory, BuildSkinImageCacheKey(SkinImageKind.Pet, dbfId, resolvedCardId, premium) + ".png");
                Texture texture = TryResolvePetTexture(dbfId, resolvedCardId, premium, out string textureError);
                if (texture == null)
                {
                    result.ErrorMessage = textureError;
                    return result;
                }

                if (!Utils.TryWriteTextureToPng(texture, cachePath))
                {
                    result.ErrorMessage = "Failed to encode pet texture.";
                    return result;
                }

                result.FilePath = cachePath;
                return result;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"ExportPetImage error: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private static CardArtExportResult TryExportDbfSkinImageOnMainThread(string dbfId, string cardStringId, TAG_PREMIUM premium, SkinImageKind kind, SkinTextureResolver resolver)
        {
            var result = new CardArtExportResult();
            try
            {
                if (!int.TryParse(dbfId, out int parsedId))
                {
                    result.ErrorMessage = "Invalid skin id.";
                    return result;
                }

                string existingPath = FindLocalSkinImagePath(dbfId, cardStringId, premium, kind);
                if (!string.IsNullOrEmpty(existingPath))
                {
                    result.FilePath = existingPath;
                    return result;
                }

                string cachePath = Path.Combine(SkinImageCacheDirectory, BuildSkinImageCacheKey(kind, dbfId, cardStringId, premium) + ".png");
                Texture texture = resolver(parsedId, out string textureError);
                if (texture == null)
                {
                    result.ErrorMessage = textureError;
                    return result;
                }

                if (!Utils.TryWriteTextureToPng(texture, cachePath))
                {
                    result.ErrorMessage = "Failed to encode skin image texture.";
                    return result;
                }

                result.FilePath = cachePath;
                return result;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"ExportDbfSkinImage error: {ex.Message}");
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

        private static SkinImageKind ParseSkinImageKind(string rawKind)
        {
            if (string.IsNullOrWhiteSpace(rawKind))
                return SkinImageKind.Card;

            string normalized = rawKind.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "pet":
                case "opposingpet":
                    return SkinImageKind.Pet;
                case "cardback":
                    return SkinImageKind.CardBack;
                case "bgsboard":
                case "battlegroundboard":
                case "battlegroundsboard":
                    return SkinImageKind.BgsBoard;
                case "bgsfinisher":
                case "battlegroundfinisher":
                case "battlegroundsfinisher":
                case "finisher":
                    return SkinImageKind.BgsFinisher;
                case "board":
                    return SkinImageKind.Board;
                default:
                    return SkinImageKind.Card;
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

        private static string ResolvePetCardStringId(string dbfId, string cardStringId)
        {
            if (!string.IsNullOrWhiteSpace(cardStringId))
                return cardStringId.Trim();

            if (!int.TryParse(dbfId, out int variantId))
                return string.Empty;

            try
            {
                PetsManager petsManager = PetsManager.Get();
                if (petsManager != null)
                {
                    int cardDbId;
                    if (petsManager.TryGetCardIdFromPetVariantId(variantId, out cardDbId))
                        return GameUtils.TranslateDbIdToCardId(cardDbId, false) ?? string.Empty;
                }

                PetVariantDbfRecord record = GameDbf.PetVariant.GetRecord(variantId);
                if (record != null)
                    return GameUtils.TranslateDbIdToCardId(record.CardId, false) ?? string.Empty;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"ResolvePetCardStringId error: {ex.Message}");
            }

            return string.Empty;
        }

        private static string FindLocalCardArtPath(string dbfId, string cardStringId, TAG_PREMIUM premium)
        {
            return FindLocalSkinImagePath(dbfId, cardStringId, premium, SkinImageKind.Card);
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

        private static Texture TryResolvePetTexture(string dbfId, string cardStringId, TAG_PREMIUM premium, out string errorMessage)
        {
            errorMessage = string.Empty;
            var targets = new List<object>();

            if (int.TryParse(dbfId, out int variantId))
            {
                try
                {
                    object petRecord = GameDbf.PetVariant.GetRecord(variantId);
                    if (petRecord != null)
                    {
                        targets.Add(petRecord);
                        Texture petIconTexture = TryLoadTextureFromNamedStringMembers(petRecord, "PetIcon", "Icon", "Texture", "Portrait", "Prefab");
                        if (petIconTexture != null)
                            return petIconTexture;
                    }
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"TryResolvePetTexture record error: {ex.Message}");
                }
            }

            Texture texture = TryResolveTextureFromSearchTargets(targets, PetMemberHints, premium, out errorMessage);
            if (texture != null)
                return texture;

            texture = TryResolveCardArtTexture(cardStringId, premium, out string cardArtError);
            if (texture != null)
                return texture;

            errorMessage = !string.IsNullOrEmpty(errorMessage)
                ? errorMessage + "; card fallback: " + cardArtError
                : cardArtError;
            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = "Pet texture not found.";
            return null;
        }

        private static Texture TryResolveCardBackTexture(int dbfId, out string errorMessage)
        {
            errorMessage = string.Empty;
            var targets = new List<object>();

            try
            {
                object cardBackManager = CardBackManager.Get();
                object cardBackDataMap = TryGetMemberValue(cardBackManager, "m_cardBackData");
                object cardBackData = TryGetIndexedValue(cardBackDataMap, dbfId);
                if (cardBackData != null)
                {
                    targets.Add(cardBackData);
                    Texture cardBackDataTexture = TryLoadTextureFromNamedStringMembers(cardBackData, "PrefabName", "Prefab", "Texture", "CardBack");
                    if (cardBackDataTexture != null)
                        return cardBackDataTexture;
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"TryResolveCardBackTexture manager error: {ex.Message}");
            }

            try
            {
                object cardBackRecord = GameDbf.CardBack.GetRecord(dbfId);
                if (cardBackRecord != null)
                {
                    targets.Add(cardBackRecord);
                    Texture cardBackRecordTexture = TryLoadTextureFromNamedStringMembers(cardBackRecord, "PrefabName", "Prefab", "Texture", "CardBack", "HighResTexture", "Thumbnail");
                    if (cardBackRecordTexture != null)
                        return cardBackRecordTexture;
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"TryResolveCardBackTexture record error: {ex.Message}");
            }

            if (targets.Count == 0)
            {
                errorMessage = "Card back record not found.";
                return null;
            }

            Texture texture = TryResolveTextureFromSearchTargets(targets, CardBackMemberHints, TAG_PREMIUM.NORMAL, out errorMessage);
            if (texture != null)
                return texture;

            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = "Card back texture not found.";
            return null;
        }

        private static Texture TryResolveBattlegroundsBoardTexture(int dbfId, out string errorMessage)
        {
            errorMessage = string.Empty;
            var targets = new List<object>();

            try
            {
                object boardRecord = GameDbf.BattlegroundsBoardSkin.GetRecord(dbfId);
                if (boardRecord != null)
                {
                    targets.Add(boardRecord);
                    Texture boardTexture = TryLoadTextureFromNamedStringMembers(boardRecord, "DetailsTexture", "FullTavernBoardPrefab", "DetailsMovie", "Preview", "Thumbnail", "Texture", "Prefab");
                    if (boardTexture != null)
                        return boardTexture;
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"TryResolveBattlegroundsBoardTexture error: {ex.Message}");
            }

            if (targets.Count == 0)
            {
                errorMessage = "Battlegrounds board record not found.";
                return null;
            }

            Texture texture = TryResolveTextureFromSearchTargets(targets, BattlegroundsBoardMemberHints, TAG_PREMIUM.NORMAL, out errorMessage);
            if (texture != null)
                return texture;

            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = "Battlegrounds board texture not found.";
            return null;
        }

        private static Texture TryResolveBattlegroundsFinisherTexture(int dbfId, out string errorMessage)
        {
            errorMessage = string.Empty;
            var targets = new List<object>();

            try
            {
                object finisherRecord = GameDbf.BattlegroundsFinisher.GetRecord(dbfId);
                if (finisherRecord != null)
                {
                    targets.Add(finisherRecord);
                    Texture finisherTexture = TryLoadTextureFromNamedStringMembers(finisherRecord, "DetailsTexture", "GameplaySettings", "DetailsMovie", "Preview", "Thumbnail", "Texture", "Prefab");
                    if (finisherTexture != null)
                        return finisherTexture;
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"TryResolveBattlegroundsFinisherTexture error: {ex.Message}");
            }

            if (targets.Count == 0)
            {
                errorMessage = "Battlegrounds finisher record not found.";
                return null;
            }

            Texture texture = TryResolveTextureFromSearchTargets(targets, BattlegroundsFinisherMemberHints, TAG_PREMIUM.NORMAL, out errorMessage);
            if (texture != null)
                return texture;

            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = "Battlegrounds finisher texture not found.";
            return null;
        }

        private static Texture TryResolveTextureFromSearchTargets(IEnumerable<object> targets, string[] memberHints, TAG_PREMIUM premium, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (targets == null)
            {
                errorMessage = "No search targets.";
                return null;
            }

            foreach (object target in targets)
            {
                if (target == null)
                    continue;

                Texture texture = TryExtractTexture(target);
                if (texture != null)
                    return texture;

                texture = TryResolveTextureFromNamedMembers(target, memberHints, premium);
                if (texture != null)
                    return texture;
            }

            errorMessage = "No texture-like asset found for the requested skin.";
            return null;
        }

        private static Texture TryResolveTextureFromNamedMembers(object target, string[] memberHints, TAG_PREMIUM premium)
        {
            if (target == null)
                return null;

            Type targetType = target.GetType();

            foreach (PropertyInfo property in targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;
                if (!ShouldInspectMember(property.Name, property.PropertyType, memberHints))
                    continue;

                object value;
                try
                {
                    value = property.GetValue(target, null);
                }
                catch
                {
                    continue;
                }

                Texture texture = TryExtractTexture(value);
                if (texture != null)
                    return texture;

                if (ShouldTryLoadAssetReference(property.Name, value))
                {
                    texture = TryLoadTextureFromAnyAssetValue(value);
                    if (texture != null)
                        return texture;
                }
            }

            foreach (FieldInfo field in targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!ShouldInspectMember(field.Name, field.FieldType, memberHints))
                    continue;

                object value;
                try
                {
                    value = field.GetValue(target);
                }
                catch
                {
                    continue;
                }

                Texture texture = TryExtractTexture(value);
                if (texture != null)
                    return texture;

                if (ShouldTryLoadAssetReference(field.Name, value))
                {
                    texture = TryLoadTextureFromAnyAssetValue(value);
                    if (texture != null)
                        return texture;
                }
            }

            foreach (MethodInfo method in targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.IsSpecialName || method.ReturnType == typeof(void))
                    continue;
                if (!ShouldInspectMember(method.Name, method.ReturnType, memberHints))
                    continue;

                object[] arguments = BuildInvocationArguments(method, premium);
                if (arguments == null || arguments.Length > 2)
                    continue;

                object returnValue;
                try
                {
                    returnValue = method.Invoke(target, arguments);
                }
                catch
                {
                    continue;
                }

                Texture texture = TryExtractTexture(returnValue);
                if (texture != null)
                    return texture;

                if (ShouldTryLoadAssetReference(method.Name, returnValue))
                {
                    texture = TryLoadTextureFromAnyAssetValue(returnValue);
                    if (texture != null)
                        return texture;
                }
            }

            return null;
        }

        private static bool ShouldInspectMember(string memberName, Type memberType, string[] memberHints)
        {
            if (ContainsHint(memberName, memberHints))
                return true;

            if (memberType == null)
                return false;

            if (typeof(Texture).IsAssignableFrom(memberType)
                || typeof(Sprite).IsAssignableFrom(memberType)
                || typeof(Material).IsAssignableFrom(memberType))
            {
                return true;
            }

            return LooksLikeAssetReferenceType(memberType);
        }

        private static Texture TryLoadTextureFromNamedStringMembers(object target, params string[] memberNames)
        {
            if (target == null || memberNames == null || memberNames.Length == 0)
                return null;

            foreach (string memberName in memberNames)
            {
                object value = TryGetMemberValue(target, memberName);
                Texture texture = TryLoadTextureFromAnyAssetValue(value);
                if (texture != null)
                    return texture;
            }

            return null;
        }

        private static Texture TryLoadTextureFromAnyAssetValue(object value)
        {
            if (value == null)
                return null;

            Texture texture = TryExtractTexture(value);
            if (texture != null)
                return texture;

            texture = TryLoadTextureFromAssetReference(value);
            if (texture != null)
                return texture;

            string assetString = value as string;
            if (string.IsNullOrWhiteSpace(assetString))
                assetString = value.ToString();

            if (string.IsNullOrWhiteSpace(assetString))
                return null;

            return TryLoadTextureFromAssetString(assetString.Trim());
        }

        private static bool ShouldTryLoadAssetReference(string memberName, object value)
        {
            if (value == null)
                return false;

            if (LooksLikeAssetReferenceType(value.GetType()))
                return true;

            if (ContainsHint(memberName, AssetReferenceMemberHints))
                return true;

            return value is string text && LooksLikeAssetReferenceString(text);
        }

        private static bool LooksLikeAssetReferenceType(Type type)
        {
            if (type == null)
                return false;

            string fullName = type.FullName ?? type.Name ?? string.Empty;
            return fullName.IndexOf("AssetReference", StringComparison.OrdinalIgnoreCase) >= 0
                || fullName.IndexOf("AssetRef", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeAssetReferenceString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string text = value.Trim();
            return text.IndexOf("/", StringComparison.Ordinal) >= 0
                || text.IndexOf("\\", StringComparison.Ordinal) >= 0
                || text.IndexOf(".", StringComparison.Ordinal) >= 0
                || text.IndexOf("_", StringComparison.Ordinal) >= 0
                || text.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("texture", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("prefab", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("thumbnail", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("preview", StringComparison.OrdinalIgnoreCase) >= 0
                || text.EndsWith("Texture", StringComparison.OrdinalIgnoreCase)
                || text.EndsWith("Prefab", StringComparison.OrdinalIgnoreCase)
                || text.EndsWith("Sprite", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsHint(string value, IEnumerable<string> hints)
        {
            if (string.IsNullOrEmpty(value) || hints == null)
                return false;

            foreach (string hint in hints)
            {
                if (!string.IsNullOrEmpty(hint)
                    && value.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static object TryGetMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            Type targetType = target.GetType();
            FieldInfo field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(target);

            PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                return property.GetValue(target, null);

            return null;
        }

        private static object TryGetIndexedValue(object container, int key)
        {
            if (container == null)
                return null;

            if (container is IDictionary dictionary && dictionary.Contains(key))
                return dictionary[key];

            Type containerType = container.GetType();
            MethodInfo tryGetValueMethod = containerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                {
                    if (!string.Equals(method.Name, "TryGetValue", StringComparison.Ordinal))
                        return false;
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 2 && parameters[1].ParameterType.IsByRef;
                });

            if (tryGetValueMethod != null)
            {
                ParameterInfo[] parameters = tryGetValueMethod.GetParameters();
                object[] arguments = new object[2];
                arguments[0] = Convert.ChangeType(key, parameters[0].ParameterType);
                arguments[1] = parameters[1].ParameterType.GetElementType() != null
                    ? GetDefaultValue(parameters[1].ParameterType.GetElementType())
                    : null;
                object result = tryGetValueMethod.Invoke(container, arguments);
                if (result is bool found && found)
                    return arguments[1];
            }

            PropertyInfo indexer = containerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(property =>
                {
                    ParameterInfo[] parameters = property.GetIndexParameters();
                    return property.CanRead && parameters.Length == 1 && parameters[0].ParameterType == typeof(int);
                });

            if (indexer != null)
            {
                try
                {
                    return indexer.GetValue(container, new object[] { key });
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static object GetDefaultValue(Type type)
        {
            if (type == null)
                return null;
            return type.IsValueType ? Activator.CreateInstance(type) : null;
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
            return TryExtractTexture(value, 0);
        }

        private static Texture TryExtractTexture(object value, int depth)
        {
            if (value == null || depth > 4)
                return null;

            if (value is Texture texture)
                return texture;

            if (value is Sprite sprite)
                return sprite.texture;

            if (value is Material material)
                return material.mainTexture;

            if (value is GameObject gameObject)
                return TryExtractTextureFromGameObject(gameObject, depth + 1);

            if (value is Component component)
            {
                Texture componentTexture = TryExtractTextureFromSpecialComponent(component);
                if (componentTexture != null)
                    return componentTexture;
            }

            Type valueType = value.GetType();
            PropertyInfo assetProperty = valueType.GetProperty("Asset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (assetProperty != null)
            {
                try
                {
                    Texture assetTexture = TryExtractTexture(assetProperty.GetValue(value, null), depth + 1);
                    if (assetTexture != null)
                        return assetTexture;
                }
                catch
                {
                }
            }

            foreach (PropertyInfo property in valueType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!property.CanRead)
                    continue;
                if (property.GetIndexParameters().Length > 0)
                    continue;
                if (property.Name != "Asset"
                    && !typeof(Texture).IsAssignableFrom(property.PropertyType)
                    && !typeof(Sprite).IsAssignableFrom(property.PropertyType)
                    && !typeof(Material).IsAssignableFrom(property.PropertyType))
                    continue;

                try
                {
                    Texture propertyTexture = TryExtractTexture(property.GetValue(value, null), depth + 1);
                    if (propertyTexture != null)
                        return propertyTexture;
                }
                catch
                {
                }
            }

            foreach (FieldInfo field in valueType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.Name != "Asset"
                    && !typeof(Texture).IsAssignableFrom(field.FieldType)
                    && !typeof(Sprite).IsAssignableFrom(field.FieldType)
                    && !typeof(Material).IsAssignableFrom(field.FieldType))
                    continue;

                try
                {
                    Texture fieldTexture = TryExtractTexture(field.GetValue(value), depth + 1);
                    if (fieldTexture != null)
                        return fieldTexture;
                }
                catch
                {
                }
            }

            if (!(value is string) && value is IEnumerable enumerable)
            {
                int count = 0;
                foreach (object item in enumerable)
                {
                    Texture itemTexture = TryExtractTexture(item, depth + 1);
                    if (itemTexture != null)
                        return itemTexture;

                    count++;
                    if (count >= 8)
                        break;
                }
            }

            return null;
        }

        private static Texture TryExtractTextureFromGameObject(GameObject gameObject, int depth)
        {
            if (gameObject == null || depth > 4)
                return null;

            try
            {
                foreach (Component component in gameObject.GetComponentsInChildren<Component>(true))
                {
                    Texture texture = TryExtractTexture(component, depth + 1);
                    if (texture != null)
                        return texture;
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"TryExtractTextureFromGameObject error: {ex.Message}");
            }

            return null;
        }

        private static Texture TryExtractTextureFromSpecialComponent(Component component)
        {
            if (component == null)
                return null;

            if (component is RawImage rawImage && rawImage.texture != null)
                return rawImage.texture;

            if (component is Image image && image.sprite != null)
                return image.sprite.texture;

            if (component is SpriteRenderer spriteRenderer && spriteRenderer.sprite != null)
                return spriteRenderer.sprite.texture;

            if (component is Renderer renderer)
            {
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null)
                    return renderer.sharedMaterial.mainTexture;

                if (renderer.sharedMaterials != null)
                {
                    foreach (Material sharedMaterial in renderer.sharedMaterials)
                    {
                        if (sharedMaterial != null && sharedMaterial.mainTexture != null)
                            return sharedMaterial.mainTexture;
                    }
                }
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
                foreach (Type assetType in AssetLoadTypeCandidates)
                {
                    try
                    {
                        object assetHandle = loadAssetMethod.MakeGenericMethod(assetType)
                            .Invoke(assetLoader, new[] { assetReference, loadingOptions });

                        Texture texture = TryExtractTexture(assetHandle);
                        if (texture != null)
                            return texture;
                    }
                    catch
                    {
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"TryLoadTextureFromAssetReference error: {ex.Message}");
                return null;
            }
        }

        private static Texture TryLoadTextureFromAssetString(string assetString)
        {
            if (string.IsNullOrWhiteSpace(assetString))
                return null;

            string normalized = assetString.Trim();
            var candidateReferences = new List<object>();

            try
            {
                AssetReference assetReference = AssetReference.CreateFromAssetString(normalized);
                if (assetReference != null)
                    candidateReferences.Add(assetReference);
            }
            catch
            {
            }

            foreach (string candidate in BuildAssetStringCandidates(normalized))
            {
                try
                {
                    AssetReference assetReference = AssetReference.CreateFromAssetString(candidate);
                    if (assetReference != null)
                        candidateReferences.Add(assetReference);
                }
                catch
                {
                }
            }

            foreach (object candidateReference in candidateReferences)
            {
                Texture texture = TryLoadTextureFromAssetReference(candidateReference);
                if (texture != null)
                    return texture;
            }

            try
            {
                Texture resourceTexture = Resources.Load<Texture>(normalized);
                if (resourceTexture != null)
                    return resourceTexture;
            }
            catch
            {
            }

            return null;
        }

        private static IEnumerable<string> BuildAssetStringCandidates(string assetString)
        {
            if (string.IsNullOrWhiteSpace(assetString))
                yield break;

            string normalized = assetString.Trim().Replace('\\', '/');
            yield return normalized;

            int dotIndex = normalized.LastIndexOf('.');
            if (dotIndex > 0)
                yield return normalized.Substring(0, dotIndex);

            string fileName = Path.GetFileName(normalized);
            if (!string.IsNullOrWhiteSpace(fileName) && !string.Equals(fileName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                yield return fileName;
                int fileDotIndex = fileName.LastIndexOf('.');
                if (fileDotIndex > 0)
                    yield return fileName.Substring(0, fileDotIndex);
            }

            if (!normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                && normalized.IndexOf("/", StringComparison.Ordinal) < 0)
            {
                yield return normalized + ".prefab";
            }

            if (!normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && normalized.IndexOf("/", StringComparison.Ordinal) < 0)
            {
                yield return normalized + ".png";
            }

            if (!normalized.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                && normalized.IndexOf("/", StringComparison.Ordinal) < 0)
            {
                yield return normalized + ".jpg";
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

        private static string FindLocalSkinImagePath(string dbfId, string cardStringId, TAG_PREMIUM premium, SkinImageKind kind)
        {
            string resolvedCardId = ResolveSkinImageCardId(kind, dbfId, cardStringId);
            foreach (string cacheKey in GetSkinImageCacheKeys(kind, dbfId, resolvedCardId, premium))
            {
                foreach (string extension in SkinImageExtensions)
                {
                    string filePath = Path.Combine(SkinImageCacheDirectory, cacheKey + extension);
                    if (File.Exists(filePath))
                        return filePath;
                }
            }

            if ((kind == SkinImageKind.Card || kind == SkinImageKind.Pet) && !string.IsNullOrEmpty(resolvedCardId))
            {
                string exportedPath = FindExportedCardTextureDownloadPath(resolvedCardId, premium);
                if (!string.IsNullOrEmpty(exportedPath))
                    return exportedPath;
            }

            return string.Empty;
        }

        private static string ResolveSkinImageCardId(SkinImageKind kind, string dbfId, string cardStringId)
        {
            switch (kind)
            {
                case SkinImageKind.Card:
                    return ResolveCardStringId(dbfId, cardStringId);
                case SkinImageKind.Pet:
                    return ResolvePetCardStringId(dbfId, cardStringId);
                default:
                    return !string.IsNullOrWhiteSpace(cardStringId) ? cardStringId.Trim() : string.Empty;
            }
        }

        private static string BuildSkinImageCacheKey(SkinImageKind kind, string dbfId, string cardStringId, TAG_PREMIUM premium)
        {
            if (kind == SkinImageKind.Card)
                return BuildCardArtCacheKey(dbfId, cardStringId, premium);

            string baseKey = !string.IsNullOrWhiteSpace(dbfId) ? dbfId.Trim() : cardStringId.Trim();
            if (kind == SkinImageKind.Pet && !string.IsNullOrWhiteSpace(cardStringId))
                baseKey = string.IsNullOrEmpty(baseKey) ? cardStringId.Trim() : baseKey + "-" + cardStringId.Trim();
            if (string.IsNullOrEmpty(baseKey))
                baseKey = "unknown";

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                baseKey = baseKey.Replace(invalidChar, '_');

            baseKey = kind.ToString().ToLowerInvariant() + "-" + baseKey;

            if (premium != TAG_PREMIUM.NORMAL)
                baseKey = baseKey + "-" + premium.ToString().ToUpperInvariant();

            return baseKey;
        }

        private static IEnumerable<string> GetSkinImageCacheKeys(SkinImageKind kind, string dbfId, string cardStringId, TAG_PREMIUM premium)
        {
            var keys = new List<string>();
            string primaryKey = BuildSkinImageCacheKey(kind, dbfId, cardStringId, premium);
            if (!string.IsNullOrEmpty(primaryKey))
                keys.Add(primaryKey);

            if (premium != TAG_PREMIUM.NORMAL)
            {
                string fallbackKey = BuildSkinImageCacheKey(kind, dbfId, cardStringId, TAG_PREMIUM.NORMAL);
                if (!string.IsNullOrEmpty(fallbackKey) && !keys.Contains(fallbackKey))
                    keys.Add(fallbackKey);
            }

            if (kind == SkinImageKind.Pet && !string.IsNullOrWhiteSpace(cardStringId))
            {
                string alternateKey = BuildSkinImageCacheKey(kind, string.Empty, cardStringId, premium);
                if (!string.IsNullOrEmpty(alternateKey) && !keys.Contains(alternateKey))
                    keys.Add(alternateKey);

                if (premium != TAG_PREMIUM.NORMAL)
                {
                    string alternateFallbackKey = BuildSkinImageCacheKey(kind, string.Empty, cardStringId, TAG_PREMIUM.NORMAL);
                    if (!string.IsNullOrEmpty(alternateFallbackKey) && !keys.Contains(alternateFallbackKey))
                        keys.Add(alternateFallbackKey);
                }
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
