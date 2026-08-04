using BepInEx.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static HsMod.PluginConfig;

namespace HsMod
{
    //游戏内配置菜单（IMGUI 实现，不依赖游戏预制体），深色面板风格
    //注意：炉石的 UnityEngine.IMGUIModule 被裁剪，仅能使用其保留的 API 子集
    //（无 GUI.Window/hover/focused/GetNameOfFocusedControl 等），窗口与拖拽为手动实现
    //配置项通过反射 PluginConfig 自动收集，新配置无需修改此文件
    public class ModSettingsUI : MonoBehaviour
    {
        public static ModSettingsUI Instance;

        private class Entry
        {
            public string FieldName;
            public ConfigEntryBase Config;
            public string Name;
            public string Section;
            public string Description;
            public bool IsAdvanced;
        }

        private static readonly string[] InternalFields = { "pluginInitLanague", "isEulaRead" };

        private bool visible;
        private readonly List<Entry> allEntries = new List<Entry>();
        private readonly List<string> sections = new List<string>();
        private string selectedSection = "";
        private string searchText = "";
        private bool showAdvanced;
        private bool showStats;    //酒馆战绩页
        private bool showStore;    //商店零元购页
        private bool showSkins;    //皮肤选择页
        private int skinMapSource = -1;    //英雄映射选中的原始皮肤ID（-1 未选）
        private SkinPanel.SkinType skinMapType = SkinPanel.SkinType.Hero;    //映射操作所属类型（取 source 名称用）
        private readonly List<int> skinMapTargets = new List<int>();    //选中的目标皮肤ID列表
        private readonly HashSet<SkinPanel.SkinType> skinExpanded = new HashSet<SkinPanel.SkinType>();    //皮肤页展开的大分类（默认全部折叠）
        private readonly HashSet<string> skinClassExpanded = new HashSet<string>();    //英雄页展开的职业组（默认全部折叠）
        private bool miniMode;    //购买后最小化，避免 IMGUI 覆盖 NGUI 购买弹窗

        //战绩页筛选：时间段（0今天/1七天/2月/3全部）与模式（0单人/1双人），
        //选择会持久化到文件；-1表示未初始化，首次打开自动选对局更多的模式
        private int statsPeriod = 3;
        private int statsMode = -1;
        private bool statsFiltersLoaded;
        private Vector2 chartScroll;
        private bool chartScrollToEnd;
        private Vector2 sectionScroll;
        private Vector2 entryScroll;
        private Vector2 storeScroll;
        private Vector2 skinsScroll;
        private Vector2 windowPos = new Vector2(-1f, -1f);
        private bool dragging;
        private Vector2 dragOffset;
        private Entry capturingEntry;    //正在等待按键输入的快捷键项

        //唯一的未完成输入暂存：属于最后编辑的输入框，
        //切换到其它输入框/分区时清空——非法文本不会"卡"在框里
        private string bufferField;
        private string bufferText;

        private bool stylesReady;
        private Texture2D texOverlay, texWindow, texHeader, texRow, texButton, texOn, texOff, texSelected, texRed, texGrid;
        private GUIStyle headerStyle, titleStyle, sectionStyle, sectionSelectedStyle, nameStyle, descStyle,
            buttonStyle, onStyle, offStyle, fieldStyle, enumLabelStyle, hintStyle,
            chartUpStyle, chartDownStyle, chartPlaceStyle, filterStyle, filterActiveStyle;

        public static void Init()
        {
            if (Instance != null) return;
            Instance = new GameObject("HsModSettingsSceneObject", new Type[] { typeof(HSDontDestroyOnLoad) }).AddComponent<ModSettingsUI>();
        }

        public static bool IsVisible => Instance != null && Instance.visible;

        //主线程调度：WebServer 后台线程不能直接调 Unity API，通过此队列转发到 Update 执行
        private static readonly ConcurrentQueue<Action> s_mainThreadActions = new ConcurrentQueue<Action>();

        public static void RunOnMainThread(Action action)
        {
            if (action == null) return;
            s_mainThreadActions.Enqueue(action);
        }

        private void Update()
        {
            while (s_mainThreadActions.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"ModSettingsUI.RunOnMainThread: {ex.Message} \n{ex.StackTrace}");
                }
            }
        }

        public static void Toggle()
        {
            if (Instance == null) Init();
            Instance.SetVisible(!Instance.visible);
        }

        private void ClearBuffer(string fieldName = null)
        {
            if (fieldName != null && bufferField != fieldName) return;
            bufferField = null;
            bufferText = null;
        }

        public void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            if (visible) miniMode = false;    //重新打开设置时退出最小化
            capturingEntry = null;
            dragging = false;
            ClearBuffer();
            if (visible) ReloadEntries();
            try
            {
                //阻止游戏接收输入。注意：SetGameDialogActive内部是计数器（true=+1，false=-1），
                //必须严格成对调用，绝不能每帧重复调用true——否则计数永远大于0，游戏按钮全部失效
                UniversalInputManager.Get()?.SetGameDialogActive(visible);
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"ModSettingsUI SetGameDialogActive: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            if (visible) SetVisible(false);
        }

        //反射收集全部配置项；名称与分组改用语言文件本地化显示（配置文件内是语言无关的字段名）
        private void ReloadEntries()
        {
            allEntries.Clear();
            sections.Clear();
            foreach (FieldInfo field in typeof(PluginConfig).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition() != typeof(ConfigEntry<>))
                    continue;
                if (!(field.GetValue(null) is ConfigEntryBase config))
                    continue;
                object[] tags = config.Description?.Tags;
                bool advanced = (tags != null && tags.Any(t => "Advanced".Equals(t as string))) || InternalFields.Contains(field.Name);
                //两个内部保留项没有语言文件键（且不能新增：会把 HsMod.Init.Language/Eula 误判为
                //旧本地化 key 并迁移到不存在的键名），直接显示配置文件中的稳定 section/key
                string entryName;
                string entrySection;
                if (InternalFields.Contains(field.Name))
                {
                    entryName = config.Definition?.Key ?? field.Name;
                    entrySection = config.Definition?.Section ?? "";
                }
                else
                {
                    entryName = LocalizationManager.GetLangValue(field.Name + ".name");
                    entrySection = LocalizationManager.GetLangValue(field.Name + ".label");
                }
                Entry entry = new Entry
                {
                    FieldName = field.Name,
                    Config = config,
                    Name = entryName,
                    Section = entrySection,
                    Description = config.Description?.Description ?? "",
                    IsAdvanced = advanced
                };
                allEntries.Add(entry);
                if (!sections.Contains(entry.Section))
                    sections.Add(entry.Section);
            }
            if (string.IsNullOrEmpty(selectedSection) || !sections.Contains(selectedSection))
                selectedSection = sections.FirstOrDefault() ?? "";
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;    //防止被 UnloadUnusedAssets 回收
            return tex;
        }

        private GUIStyle skinStateStyle;    //悬浮面板状态文本（金色，超宽裁剪）
        private GUIStyle skinTipStyle;    //悬浮面板提示文本（灰色，超宽裁剪）

        private static GUIStyle MakeStyle(int fontSize, FontStyle fontStyle, TextAnchor anchor, Color textColor, Texture2D background, RectOffset padding)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
            style.alignment = anchor;
            style.normal.textColor = textColor;
            if (background != null) style.normal.background = background;
            if (padding != null) style.padding = padding;
            return style;
        }

        private void EnsureStyles()
        {
            if (stylesReady && texWindow != null) return;

            texOverlay = MakeTexture(new Color(0f, 0f, 0f, 0.55f));
            texWindow = MakeTexture(new Color(0.10f, 0.085f, 0.07f, 0.98f));
            texHeader = MakeTexture(new Color(0.17f, 0.14f, 0.10f, 1f));
            texRow = MakeTexture(new Color(0.15f, 0.125f, 0.10f, 1f));
            texButton = MakeTexture(new Color(0.26f, 0.22f, 0.165f, 1f));
            texOn = MakeTexture(new Color(0.16f, 0.42f, 0.16f, 1f));
            texOff = MakeTexture(new Color(0.30f, 0.26f, 0.22f, 1f));
            texSelected = MakeTexture(new Color(0.48f, 0.38f, 0.16f, 1f));
            texRed = MakeTexture(new Color(0.55f, 0.18f, 0.14f, 1f));
            texGrid = MakeTexture(new Color(1f, 1f, 1f, 0.05f));

            Color textColor = new Color(0.90f, 0.87f, 0.80f);
            Color goldColor = new Color(0.94f, 0.78f, 0.39f);
            Color grayColor = new Color(0.62f, 0.58f, 0.52f);

            headerStyle = MakeStyle(13, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, texHeader, new RectOffset(10, 10, 5, 5));
            titleStyle = MakeStyle(18, FontStyle.Bold, TextAnchor.MiddleLeft, goldColor, null, new RectOffset(2, 6, 2, 2));
            sectionStyle = MakeStyle(14, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, texButton, new RectOffset(10, 6, 7, 7));
            sectionSelectedStyle = MakeStyle(14, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, texSelected, new RectOffset(10, 6, 7, 7));
            nameStyle = MakeStyle(14, FontStyle.Bold, TextAnchor.MiddleLeft, textColor, null, null);
            nameStyle.wordWrap = true;
            descStyle = MakeStyle(12, FontStyle.Normal, TextAnchor.UpperLeft, grayColor, null, new RectOffset(0, 0, 3, 0));
            descStyle.wordWrap = true;
            buttonStyle = MakeStyle(13, FontStyle.Normal, TextAnchor.MiddleCenter, textColor, texButton, new RectOffset(6, 6, 5, 5));
            onStyle = MakeStyle(13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, texOn, new RectOffset(6, 6, 5, 5));
            offStyle = MakeStyle(13, FontStyle.Normal, TextAnchor.MiddleCenter, grayColor, texOff, new RectOffset(6, 6, 5, 5));
            fieldStyle = MakeStyle(13, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, texRow, new RectOffset(6, 6, 5, 5));
            fieldStyle.clipping = TextClipping.Clip;
            enumLabelStyle = MakeStyle(13, FontStyle.Normal, TextAnchor.MiddleCenter, textColor, null, new RectOffset(2, 2, 5, 5));
            hintStyle = MakeStyle(12, FontStyle.Normal, TextAnchor.MiddleLeft, grayColor, null, new RectOffset(2, 2, 5, 5));
            chartUpStyle = MakeStyle(11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.45f, 0.85f, 0.45f), null, null);
            chartDownStyle = MakeStyle(11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.95f, 0.45f, 0.40f), null, null);
            chartPlaceStyle = MakeStyle(11, FontStyle.Normal, TextAnchor.MiddleCenter, grayColor, null, null);
            filterStyle = MakeStyle(12, FontStyle.Normal, TextAnchor.MiddleCenter, grayColor, texButton, new RectOffset(10, 10, 5, 5));
            filterActiveStyle = MakeStyle(12, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, texSelected, new RectOffset(10, 10, 5, 5));

            skinStateStyle = MakeStyle(14, FontStyle.Bold, TextAnchor.MiddleLeft, goldColor, null, null);
            skinStateStyle.clipping = TextClipping.Clip;
            skinTipStyle = MakeStyle(12, FontStyle.Normal, TextAnchor.MiddleLeft, grayColor, null, null);
            skinTipStyle.clipping = TextClipping.Clip;

            stylesReady = true;
        }

        private static GUIContent C(string text)
        {
            return new GUIContent(text);
        }

        private void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();
            GUI.depth = -2333;

            //按 1080p 基准缩放，适配高分辨率
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);
            float screenWidth = Screen.width / scale;
            float screenHeight = Screen.height / scale;

            //购买后最小化：让出屏幕给商店购买弹窗/UIStatus 提示（NGUI 渲染，IMGUI 永远在其之上），点按钮恢复
            if (miniMode)
            {
                if (GUI.Button(new Rect(screenWidth - 120f, 10f, 110f, 40f), C(LocalizationManager.GetLangValue("modSettings.restore")), buttonStyle))
                {
                    miniMode = false;
                }
                return;
            }

            GUI.DrawTexture(new Rect(0f, 0f, screenWidth, screenHeight), texOverlay);

            float width = screenWidth * 0.75f;
            float height = screenHeight * 0.75f;
            if (windowPos.x < 0f)
                windowPos = new Vector2((screenWidth - width) / 2f, (screenHeight - height) / 2f);
            //窗口至少留240px在屏幕内，防止拖到几乎看不见
            windowPos.x = Mathf.Clamp(windowPos.x, 240f - width, screenWidth - 240f);
            windowPos.y = Mathf.Clamp(windowPos.y, 0f, screenHeight - 40f);
            Rect windowRect = new Rect(windowPos.x, windowPos.y, width, height);
            Rect floatingRect = showSkins ? GetSkinFloatingRect(windowRect, screenWidth) : new Rect(0f, 0f, 0f, 0f);

            HandleKeyEvents();

            //点击窗口外部关闭菜单（与游戏原生模态窗一致）；悬浮映射面板在窗口外，点击它不关闭
            if (Event.current.type == EventType.MouseDown
                && !windowRect.Contains(Event.current.mousePosition)
                && !floatingRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                SetVisible(false);
                return;
            }

            HandleWindowDrag(new Rect(windowRect.x, windowRect.y, windowRect.width - 180f, 36f));

            GUI.DrawTexture(windowRect, texWindow);
            GUILayout.BeginArea(windowRect);
            DrawWindow(windowRect);
            GUILayout.EndArea();

            DrawSkinMappingFloating(windowRect, screenWidth);    //悬浮映射操作区（窗口右侧外部）
        }

        //悬浮映射面板位置：窗口右侧外部（不覆盖窗口内容，避免点击穿透到背后选项）
        private Rect GetSkinFloatingRect(Rect windowRect, float screenWidth)
        {
            float x = windowRect.x + windowRect.width + 10f;
            float panelW = Mathf.Clamp(screenWidth - x - 10f, 230f, 320f);
            x = Mathf.Max(x, screenWidth - panelW - 10f);    //窗口贴右时面板收进屏幕
            float panelH = skinMapSource >= 0 ? 78f : 34f;
            return new Rect(x, windowRect.y + windowRect.height - panelH - 10f, panelW, panelH);
        }

        //悬浮映射操作区：选中原始皮肤后显示保存/删除/取消按钮，
        //避免操作按钮被折叠/滚动隐藏，方便用户确认
        private void DrawSkinMappingFloating(Rect windowRect, float screenWidth)
        {
            if (!showSkins) return;

            Rect panelRect = GetSkinFloatingRect(windowRect, screenWidth);
            GUI.DrawTexture(panelRect, texHeader);

            if (skinMapSource < 0)
            {
                GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 7f, panelRect.width - 20f, panelRect.height - 14f),
                    C(LocalizationManager.GetLangValue("skin.mapTip")), skinTipStyle);
                return;
            }

            string sourceName = TruncateSkinName(
                SkinPanel.GetList(skinMapType).FirstOrDefault(i => i.Id == skinMapSource)?.Name
                ?? skinMapSource.ToString());
            string stateText = string.Format(LocalizationManager.GetLangValue("skin.mapHint"), sourceName);
            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 6f, panelRect.width - 20f, 24f), C(stateText), skinStateStyle);

            //按钮从右往左排列：保存 / 删除 / 取消
            const float gap = 6f;
            float btnY = panelRect.y + 36f;
            float x = panelRect.x + panelRect.width - 10f;
            if (skinMapTargets.Count > 0)
            {
                x -= 76f;
                if (GUI.Button(new Rect(x, btnY, 76f, 30f), C(LocalizationManager.GetLangValue("skin.saveMapping")), onStyle))
                {
                    SkinPanel.SetMapping(skinMapSource, skinMapTargets);
                    SkinPanel.SaveMapping();
                    ResetSkinMapping();
                    UIStatus.Get()?.AddInfo(LocalizationManager.GetLangValue("skin.saved"));
                }
                x -= gap;
            }
            if (SkinPanel.HasMapping(skinMapSource))
            {
                x -= 76f;
                if (GUI.Button(new Rect(x, btnY, 76f, 30f), C(LocalizationManager.GetLangValue("skin.deleteMapping")), buttonStyle))
                {
                    SkinPanel.SetMapping(skinMapSource, null);
                    SkinPanel.SaveMapping();
                    ResetSkinMapping();
                    UIStatus.Get()?.AddInfo(LocalizationManager.GetLangValue("skin.deleted"));
                }
                x -= gap;
            }
            //取消选择（×）
            x -= 40f;
            if (GUI.Button(new Rect(x, btnY, 40f, 30f), C("×"), offStyle))
            {
                ResetSkinMapping();
            }
        }

        //手动拖拽标题栏（右侧按钮区不参与拖拽）
        private void HandleWindowDrag(Rect dragRect)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDown && dragRect.Contains(current.mousePosition))
            {
                dragging = true;
                dragOffset = current.mousePosition - windowPos;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && dragging)
            {
                windowPos = current.mousePosition - dragOffset;
                current.Use();
            }
            else if (current.type == EventType.MouseUp)
            {
                dragging = false;
            }
        }

        private void DrawWindow(Rect windowRect)
        {
            //标题栏
            GUILayout.BeginHorizontal(C(""), headerStyle, GUILayout.Height(36f));
            GUILayout.Label(C(LocalizationManager.GetLangValue("modSettings.title")), titleStyle, GUILayout.Height(26f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(C(LocalizationManager.GetLangValue("modSettings.openWeb")), buttonStyle, GUILayout.Height(24f)))
                Application.OpenURL($"http://127.0.0.1:{CommandConfig.webServerPort}/config");
            GUILayout.Space(4f);
            if (GUILayout.Button(C("X"), buttonStyle, GUILayout.Width(28f), GUILayout.Height(24f)))
                SetVisible(false);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(8f);

            //左栏：皮肤 / 搜索 + 分类 + 高级开关
            GUILayout.BeginVertical(GUILayout.Width(216f));
            if (GUILayout.Button(C(LocalizationManager.GetLangValue("skin.tab")), showSkins ? sectionSelectedStyle : sectionStyle, GUILayout.Height(30f)))
            {
                showSkins = !showSkins;
                showStats = false;
                showStore = false;
                ResetSkinMapping();
                searchText = "";
                ClearBuffer();
                entryScroll = Vector2.zero;
                skinsScroll = Vector2.zero;
                if (showSkins) SkinPanel.Invalidate();    //打开皮肤页时重新读取 DBF 与映射表
            }
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(C(LocalizationManager.GetLangValue("config.page.search")), hintStyle, GUILayout.Width(50f));
            string newSearch = GUILayout.TextField(searchText, fieldStyle, GUILayout.Height(26f));
            if (newSearch != searchText)
            {
                searchText = newSearch;
                showStats = false;
                showStore = false;
                showSkins = false;
                ResetSkinMapping();
                ClearBuffer();
                entryScroll = Vector2.zero;
            }
            if (!string.IsNullOrEmpty(searchText) && GUILayout.Button(C("X"), buttonStyle, GUILayout.Width(26f), GUILayout.Height(26f)))
                searchText = "";
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            sectionScroll = GUILayout.BeginScrollView(sectionScroll);
            foreach (string section in sections)
            {
                if (!allEntries.Any(e => e.Section == section && (showAdvanced || !e.IsAdvanced)))
                    continue;
                bool selected = section == selectedSection && string.IsNullOrEmpty(searchText) && !showStats;
                if (GUILayout.Button(C(section), selected ? sectionSelectedStyle : sectionStyle, GUILayout.Height(30f)))
                {
                    selectedSection = section;
                    searchText = "";
                    showStats = false;
                    showStore = false;
                    showSkins = false;
                    ResetSkinMapping();
                    ClearBuffer();
                    entryScroll = Vector2.zero;
                }
                GUILayout.Space(2f);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            if (GUILayout.Button(C(LocalizationManager.GetLangValue("bgStats.tab")), showStats ? sectionSelectedStyle : sectionStyle, GUILayout.Height(30f)))
            {
                showStats = !showStats;
                showStore = false;
                showSkins = false;
                ResetSkinMapping();
                searchText = "";
                ClearBuffer();
                entryScroll = Vector2.zero;
                if (showStats) chartScrollToEnd = true;    //打开战绩页时滚到最新一场
            }

            GUILayout.Space(4f);
            if (PluginConfig.isStoreEnable.Value)    //商店按钮默认隐藏，高级选项 isStoreEnable 开启后显示
            {
                if (GUILayout.Button(C(LocalizationManager.GetLangValue("store.tab")), showStore ? sectionSelectedStyle : sectionStyle, GUILayout.Height(30f)))
                {
                    showStore = !showStore;
                    showStats = false;
                    showSkins = false;
                    ResetSkinMapping();
                    searchText = "";
                    ClearBuffer();
                    entryScroll = Vector2.zero;
                    storeScroll = Vector2.zero;
                    if (showStore) StorePanel.Invalidate();    //每次打开商店页都重新遍历商品
                }
            }

            GUILayout.Space(4f);
            string advancedMark = showAdvanced ? "[x] " : "[   ] ";
            if (GUILayout.Button(C(advancedMark + LocalizationManager.GetLangValue("config.page.advanced")), showAdvanced ? sectionSelectedStyle : sectionStyle, GUILayout.Height(28f)))
            {
                showAdvanced = !showAdvanced;
                if (!showAdvanced && !string.IsNullOrEmpty(selectedSection) && !allEntries.Any(e => e.Section == selectedSection && !e.IsAdvanced))
                    selectedSection = sections.FirstOrDefault(s => allEntries.Any(e => e.Section == s && !e.IsAdvanced)) ?? selectedSection;
            }
            GUILayout.Space(8f);
            GUILayout.EndVertical();

            GUILayout.Space(10f);

            //右栏：配置项列表 / 酒馆战绩 / 商店 / 皮肤
            GUILayout.BeginVertical();
            if (showStore && !PluginConfig.isStoreEnable.Value)
                showStore = false;    //开关关闭时退出商店页
            if (showStore)
                storeScroll = GUILayout.BeginScrollView(storeScroll);
            else if (showSkins)
                skinsScroll = GUILayout.BeginScrollView(skinsScroll);
            else
                entryScroll = GUILayout.BeginScrollView(entryScroll);
            if (showStats)
            {
                DrawStats();
            }
            else if (showStore)
            {
                DrawStore();
            }
            else if (showSkins)
            {
                DrawSkins();
            }
            else
            {
                bool searching = !string.IsNullOrEmpty(searchText);
                List<Entry> shown = allEntries.Where(e =>
                    (showAdvanced || !e.IsAdvanced)
                    && (searching
                        ? (e.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                            || e.Description.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                            || e.FieldName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        : e.Section == selectedSection)).ToList();
                if (shown.Count == 0)
                    GUILayout.Label(C(LocalizationManager.GetLangValue("modSettings.noResults")), descStyle);
                foreach (Entry entry in shown)
                    DrawEntry(entry, searching);
            }
            GUILayout.EndScrollView();
            GUILayout.Space(8f);
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            //底部提示条：显示悬停控件的 tooltip（例如恢复默认按钮）
            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
                GUI.Label(new Rect(10f, windowRect.height - 24f, windowRect.width - 20f, 20f), C(GUI.tooltip), hintStyle);
        }

        private void HandleKeyEvents()
        {
            Event current = Event.current;

            //录制快捷键时也接受右键/中键/侧键（左键保留给UI点击）
            if (capturingEntry != null && current.type == EventType.MouseDown && current.button > 0)
            {
                KeyCode mouseKey = KeyCode.Mouse0 + Math.Min(current.button, 6);
                capturingEntry.Config.BoxedValue = new KeyboardShortcut(mouseKey, HeldModifiers().ToArray());
                capturingEntry = null;
                current.Use();
                return;
            }

            if (current.type != EventType.KeyDown) return;

            //快捷键录制模式：捕获下一个非修饰键
            if (capturingEntry != null)
            {
                if (current.keyCode == KeyCode.Escape)
                    capturingEntry = null;
                else if (current.keyCode != KeyCode.None && !IsModifierKey(current.keyCode))
                {
                    capturingEntry.Config.BoxedValue = new KeyboardShortcut(current.keyCode, HeldModifiers().ToArray());
                    capturingEntry = null;
                }
                current.Use();
                return;
            }
            if (current.keyCode == KeyCode.Escape)
            {
                SetVisible(false);
                current.Use();
            }
        }

        private static bool IsModifierKey(KeyCode key)
        {
            return key == KeyCode.LeftControl || key == KeyCode.RightControl
                || key == KeyCode.LeftShift || key == KeyCode.RightShift
                || key == KeyCode.LeftAlt || key == KeyCode.RightAlt;
        }

        private static List<KeyCode> HeldModifiers()
        {
            List<KeyCode> result = new List<KeyCode>();
            foreach (KeyCode key in new[] { KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftAlt, KeyCode.RightAlt })
                if (Input.GetKey(key)) result.Add(key);
            return result;
        }

        private string StatsFiltersPath => Path.Combine(PluginConfig.HsModWebSite, "bgstats_ui.txt");

        //读取上次选择的筛选条件；模式未保存时默认选对局更多的那个
        private void LoadStatsFilters()
        {
            if (statsFiltersLoaded) return;
            statsFiltersLoaded = true;
            try
            {
                if (File.Exists(StatsFiltersPath))
                {
                    foreach (string line in File.ReadAllLines(StatsFiltersPath))
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length != 2) continue;
                        if (parts[0] == "period" && int.TryParse(parts[1], out int p)) statsPeriod = Mathf.Clamp(p, 0, 3);
                        if (parts[0] == "mode" && int.TryParse(parts[1], out int m)) statsMode = Mathf.Clamp(m, 0, 1);
                    }
                }
            }
            catch { }
            if (statsMode < 0)
            {
                int solo = BgSessionStats.AllGames.Count(g => !g.Duo);
                int duo = BgSessionStats.AllGames.Count(g => g.Duo);
                statsMode = duo > solo ? 1 : 0;
            }
            chartScrollToEnd = true;
        }

        private void SaveStatsFilters()
        {
            try { File.WriteAllText(StatsFiltersPath, $"period={statsPeriod}\nmode={statsMode}"); }
            catch { }
        }

        private bool FilterButton(string label, bool active)
        {
            return GUILayout.Button(C(label), active ? filterActiveStyle : filterStyle, GUILayout.Height(26f));
        }

        //酒馆战绩页：筛选条 + 汇总 + 可横向滚动的柱状图 + 对局列表
        private void DrawStats()
        {
            LoadStatsFilters();

            //筛选条：时间段 + 单人/双人
            string[] periodKeys = { "bgStats.today", "bgStats.week", "bgStats.month", "bgStats.all" };
            GUILayout.BeginHorizontal();
            for (int i = 0; i < periodKeys.Length; i++)
            {
                if (FilterButton(LocalizationManager.GetLangValue(periodKeys[i]), statsPeriod == i) && statsPeriod != i)
                {
                    statsPeriod = i;
                    SaveStatsFilters();
                    chartScrollToEnd = true;
                }
                GUILayout.Space(2f);
            }
            GUILayout.FlexibleSpace();
            for (int i = 0; i < 2; i++)
            {
                if (FilterButton(LocalizationManager.GetLangValue(i == 0 ? "bgStats.solo" : "bgStats.duo"), statsMode == i) && statsMode != i)
                {
                    statsMode = i;
                    SaveStatsFilters();
                    chartScrollToEnd = true;
                }
                if (i == 0) GUILayout.Space(2f);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            //按筛选条件取数据
            DateTime cutoff = DateTime.MinValue;
            if (statsPeriod == 0) cutoff = DateTime.Today;
            else if (statsPeriod == 1) cutoff = DateTime.Now.AddDays(-7);
            else if (statsPeriod == 2) cutoff = DateTime.Now.AddDays(-30);
            bool duoMode = statsMode == 1;
            List<BgSessionStats.GameRecord> games = BgSessionStats.AllGames
                .Where(g => g.Duo == duoMode && g.Time >= cutoff).ToList();

            if (games.Count == 0)
            {
                GUILayout.Label(C(LocalizationManager.GetLangValue(BgSessionStats.AwaitingRating ? "bgStats.pending" : "bgStats.noGames")), descStyle);
                return;
            }

            //汇总（随筛选变化）。MMR合计含Synced；场次与平均名次只算真实对局（名次已知）
            int totalDelta = 0;
            int placedCount = 0;
            float placeSum = 0f;
            foreach (BgSessionStats.GameRecord g in games)
            {
                totalDelta += g.Delta;
                if (!g.Synced && g.Place > 0) { placeSum += g.Place; placedCount++; }
            }
            string avgPlaceStr = placedCount > 0 ? (placeSum / placedCount).ToString("F1") : "-";
            GUILayout.Label(C($"{placedCount} {LocalizationManager.GetLangValue("bgStats.games")}   ·   {LocalizationManager.GetLangValue("bgStats.avgPlace")}: {avgPlaceStr}   ·   MMR: {totalDelta:+#;-#;0}"), nameStyle);
            GUILayout.Label(C($"{games[0].RatingBefore} → {games[games.Count - 1].RatingAfter}"), descStyle);
            GUILayout.Space(8f);

            DrawDeltaChart(games);
            GUILayout.Space(8f);

            if (BgSessionStats.AwaitingRating)
            {
                GUILayout.Label(C(LocalizationManager.GetLangValue("bgStats.pending")), descStyle);
                GUILayout.Space(4f);
            }

            //对局列表，最近的在上
            GUIStyle rowStyle = new GUIStyle();
            rowStyle.normal.background = texRow;
            rowStyle.padding = new RectOffset(10, 10, 4, 4);
            for (int i = games.Count - 1; i >= 0; i--)
            {
                BgSessionStats.GameRecord g = games[i];
                GUILayout.BeginHorizontal(C(""), rowStyle, GUILayout.Height(24f));
                string when = g.Time.Date == DateTime.Today ? $"{g.Time:HH:mm}" : $"{g.Time:dd.MM HH:mm}";
                //Synced：无名次的对账补记，显示同步标记而非#名次
                string label = g.Synced
                    ? $"{when}   {LocalizationManager.GetLangValue("bgStats.synced")}"
                    : $"{when}   #{g.Place}{(string.IsNullOrEmpty(g.Hero) ? "" : $"   {g.Hero}")}";
                GUILayout.Label(C(label), new GUIStyle(nameStyle) { fontStyle = FontStyle.Normal });
                GUILayout.FlexibleSpace();
                //Uncertain：分数没等到更新，增减未知，显示问号
                GUILayout.Label(C(g.Uncertain ? "?" : $"{g.Delta:+#;-#;0}"), g.Uncertain ? chartPlaceStyle : (g.Delta >= 0 ? chartUpStyle : chartDownStyle), GUILayout.Width(48f));
                GUILayout.Label(C($"{g.RatingBefore} → {g.RatingAfter}"), descStyle, GUILayout.Width(110f));
                GUILayout.EndHorizontal();
                GUILayout.Space(2f);
            }
        }

        //商店零元购页：商品列表 + 购买按钮
        private void DrawStore()
        {
            //首次打开自动遍历商品；之后通过刷新按钮重新加载
            if (StorePanel.Products == null && !StorePanel.Loading)
                StorePanel.Refresh();

            GUILayout.BeginHorizontal();
            GUILayout.Label(C(LocalizationManager.GetLangValue("store.title")), titleStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(C(LocalizationManager.GetLangValue("store.refresh")), buttonStyle, GUILayout.Width(56f)))
            {
                StorePanel.Invalidate();
                StorePanel.Refresh();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            if (StorePanel.Loading)
            {
                GUILayout.Label(C(LocalizationManager.GetLangValue("store.loading")), descStyle);
                return;
            }
            if (StorePanel.Products == null)
            {
                GUILayout.Label(C(LocalizationManager.GetLangValue("store.loadFailed")), descStyle);
                if (!string.IsNullOrEmpty(StorePanel.Error))
                    GUILayout.Label(C(StorePanel.Error), descStyle);
                return;
            }
            if (StorePanel.Products.Count == 0)
            {
                GUILayout.Label(C(LocalizationManager.GetLangValue("store.empty")), descStyle);
                return;
            }

            GUIStyle rowStyle = new GUIStyle();
            rowStyle.normal.background = texRow;
            rowStyle.padding = new RectOffset(10, 10, 6, 8);
            string buyText = LocalizationManager.GetLangValue("store.buy");
            foreach (StorePanel.ProductEntry entry in StorePanel.Products)
            {
                GUILayout.BeginVertical(rowStyle);
                GUILayout.BeginHorizontal();
                string title = string.IsNullOrEmpty(entry.Title) ? entry.PmtId.ToString() : entry.Title;
                GUILayout.Label(C(title), nameStyle, GUILayout.ExpandWidth(true));
                if (entry.ItemCount > 1)
                    GUILayout.Label(C($"x{entry.ItemCount}"), hintStyle, GUILayout.Width(36f));
                GUILayout.Label(C(StorePanel.GetPriceText(entry)), enumLabelStyle, GUILayout.Width(96f));
                if (GUILayout.Button(C(buyText), buttonStyle, GUILayout.Width(56f)))
                {
                    string msg;
                    if (StorePanel.TryBuy(entry, out msg))
                    {
                        miniMode = true;
                    }
                    UIStatus.Get()?.AddInfo(msg);
                }
                GUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(entry.Description))
                    GUILayout.Label(C(entry.Description), descStyle);
                GUILayout.EndVertical();
                GUILayout.Space(3f);
            }
        }

        //皮肤选择页：非英雄类直接设置，英雄类（对战/酒馆）为映射模式（原始皮肤:目标皮肤）
        private void DrawSkins()
        {
            GUILayout.Label(C(LocalizationManager.GetLangValue("skin.title")), titleStyle);
            GUILayout.Space(6f);

            SkinPanel.SkinType[] types =
            {
                SkinPanel.SkinType.CardBack,
                SkinPanel.SkinType.Coin,
                SkinPanel.SkinType.Board,
                SkinPanel.SkinType.BgsBoard,
                SkinPanel.SkinType.Finisher,
                SkinPanel.SkinType.Hero,
                SkinPanel.SkinType.BgsHero,
                SkinPanel.SkinType.Bob,
                SkinPanel.SkinType.Pet
            };

            foreach (SkinPanel.SkinType type in types)
            {
                bool expanded = skinExpanded.Contains(type);    //大分类默认全部折叠
                int count = SkinPanel.GetList(type).Count;
                string mark = expanded ? "[v]" : "[>]";
                if (GUILayout.Button(C($"{mark} {GetSkinSectionTitle(type)} ({count})"), sectionStyle, GUILayout.Height(28f)))
                {
                    if (expanded) skinExpanded.Remove(type);
                    else skinExpanded.Add(type);
                }
                GUILayout.Space(2f);
                if (!expanded) continue;

                if (SkinPanel.IsHeroMappingType(type))
                {
                    DrawHeroMappingSection(type);
                }
                else
                {
                    DrawDirectSection(type);
                }
                GUILayout.Space(6f);
            }
        }

        //非英雄类：点击直接写入配置
        private void DrawDirectSection(SkinPanel.SkinType type)
        {
            int current = SkinPanel.GetCurrentValue(type);
            List<SkinPanel.SkinItem> items = SkinPanel.GetList(type);
            if (current > 0)
            {
                string curName = items.FirstOrDefault(i => i.Id == current)?.Name;
                GUILayout.Label(C(LocalizationManager.GetLangValue("skin.current") + (curName ?? current.ToString())), hintStyle);
                GUILayout.Space(2f);
            }

            if (items.Count == 0)
            {
                GUILayout.Label(C(LocalizationManager.GetLangValue("skin.empty")), descStyle);
                return;
            }

            GUILayout.BeginHorizontal();    //选项按钮流：等宽填满整行，按可用宽度决定每行数量
            int shown = 0;
            foreach (SkinPanel.SkinItem item in items)
            {
                if (shown > 0 && shown % 3 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                if (GUILayout.Button(C(TruncateSkinName(item.Name)), item.Id == current ? onStyle : buttonStyle, GUILayout.MinWidth(140f), GUILayout.ExpandWidth(true), GUILayout.Height(26f)))
                {
                    if (item.Id == current)
                    {
                        SkinPanel.SetValue(type, -1);    //再次点击取消选择
                    }
                    else
                    {
                        SkinPanel.SetValue(type, item.Id);
                    }
                    UIStatus.Get()?.AddInfo(item.Name);
                }
                shown++;
            }
            GUILayout.EndHorizontal();
        }

        //英雄类：按职业分组，先点原始皮肤，再点目标皮肤（可多选），保存写入 HsSkins.cfg
        private void DrawHeroMappingSection(SkinPanel.SkinType type)
        {
            List<SkinPanel.SkinItem> items = SkinPanel.GetList(type);
            if (items.Count == 0)
            {
                GUILayout.Label(C(LocalizationManager.GetLangValue("skin.empty")), descStyle);
                return;
            }

            //按职业分组：未分类（ClassId<0）排最后
            var groups = items.GroupBy(i => i.ClassId >= 0 ? i.ClassId : int.MaxValue)
                              .OrderBy(g => g.Key == int.MaxValue ? 1 : 0)
                              .ThenBy(g => g.Key)
                              .ToList();
            foreach (var group in groups)
            {
                int classId = group.Key == int.MaxValue ? -1 : group.Key;
                string groupKey = type + "_" + classId;
                bool expanded = skinClassExpanded.Contains(groupKey);
                string mark = expanded ? "[v]" : "[>]";
                GUILayout.BeginHorizontal();    //职业标题相对大分类缩进，便于区分层级
                GUILayout.Space(16f);
                if (GUILayout.Button(C($"{mark} {SkinPanel.GetClassName(classId)} ({group.Count()})"), sectionStyle, GUILayout.Height(26f)))
                {
                    if (expanded) skinClassExpanded.Remove(groupKey);
                    else skinClassExpanded.Add(groupKey);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(2f);
                if (!expanded) continue;    //默认全部折叠，展开才显示该职业皮肤

                GUILayout.BeginHorizontal();    //选项按钮流：等宽填满整行，按可用宽度决定每行数量
                int shown = 0;
                foreach (SkinPanel.SkinItem item in group)
                {
                    bool isSource = item.Id == skinMapSource;
                    bool isTarget = skinMapTargets.Contains(item.Id);
                    List<int> mapped = SkinPanel.HasMapping(item.Id) ? SkinPanel.GetMappingTargets(item.Id) : null;
                    GUIStyle style = isSource ? onStyle : (isTarget ? sectionSelectedStyle : (mapped != null ? offStyle : buttonStyle));
                    string label = TruncateSkinName(item.Name);
                    if (mapped != null && !isSource && !isTarget)
                        label += "→" + mapped.Count;    //已映射标记
                    if (shown > 0 && shown % 3 == 0)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                    if (GUILayout.Button(C(label), style, GUILayout.MinWidth(140f), GUILayout.ExpandWidth(true), GUILayout.Height(26f)))
                    {
                        if (skinMapSource < 0)
                        {
                            skinMapSource = item.Id;    //第一步：选择原始皮肤，预载已有映射目标
                            skinMapType = type;
                            skinMapTargets.Clear();
                            skinMapTargets.AddRange(SkinPanel.GetMappingTargets(item.Id));
                        }
                        else if (item.Id == skinMapSource)
                        {
                            skinMapSource = -1;    //点击原始皮肤自身 = 取消选择
                            skinMapTargets.Clear();
                        }
                        else
                        {
                            if (!skinMapTargets.Remove(item.Id))
                                skinMapTargets.Add(item.Id);    //切换目标皮肤（多选）
                        }
                    }
                    shown++;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }
        }

        private void ResetSkinMapping()
        {
            skinMapSource = -1;
            skinMapTargets.Clear();
        }

        private static string GetSkinSectionTitle(SkinPanel.SkinType type)
        {
            switch (type)
            {
                case SkinPanel.SkinType.CardBack: return LocalizationManager.GetLangValue("skinCardBack.name");
                case SkinPanel.SkinType.Coin: return LocalizationManager.GetLangValue("skinCoin.name");
                case SkinPanel.SkinType.Board: return LocalizationManager.GetLangValue("skinBoard.name");
                case SkinPanel.SkinType.BgsBoard: return LocalizationManager.GetLangValue("skinBgsBoard.name");
                case SkinPanel.SkinType.Finisher: return LocalizationManager.GetLangValue("skinBgsFinisher.name");
                case SkinPanel.SkinType.Hero: return LocalizationManager.GetLangValue("skinHero.name");
                case SkinPanel.SkinType.BgsHero: return LocalizationManager.GetLangValue("skin.bgsHero");
                case SkinPanel.SkinType.Bob: return LocalizationManager.GetLangValue("skinBob.name");
                case SkinPanel.SkinType.Pet: return LocalizationManager.GetLangValue("skinPet.name");
                default: return type.ToString();
            }
        }

        private static string TruncateSkinName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "-";
            return name.Length > 10 ? name.Substring(0, 10) + "…" : name;
        }

        //柱状图：左侧固定刻度列 + 可横向滚动的柱区（固定槽宽），默认滚到最新一场。
        //刻度在滚动区之外，数字不会与柱子重叠
        private void DrawDeltaChart(List<BgSessionStats.GameRecord> games)
        {
            const float chartHeight = 170f;
            const float slot = 46f;
            const float barWidth = 26f;
            const float topPad = 18f;
            const float bottomPad = 18f;
            const float axisWidth = 42f;

            int maxAbs = 1;
            foreach (BgSessionStats.GameRecord g in games)
                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(g.Delta));

            float plotHalf = (chartHeight - topPad - bottomPad) / 2f;
            float contentWidth = games.Count * slot;
            if (chartScrollToEnd && Event.current.type == EventType.Layout)
            {
                chartScroll.x = Mathf.Max(0f, contentWidth);
                chartScrollToEnd = false;
            }

            GUILayout.BeginHorizontal();

            //刻度列（不随图滚动）：+max / 0 / -max
            Rect axisRect = GUILayoutUtility.GetRect(axisWidth, chartHeight, GUIStyle.none, GUILayout.Width(axisWidth));
            if (axisRect.width >= 10f)
            {
                GUI.DrawTexture(axisRect, texRow);
                float axisBase = axisRect.y + topPad + plotHalf;
                GUIStyle axisStyle = new GUIStyle(chartPlaceStyle) { alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(axisRect.x, axisBase - plotHalf - 7f, axisWidth - 6f, 14f), C($"+{maxAbs}"), axisStyle);
                GUI.Label(new Rect(axisRect.x, axisBase - 7f, axisWidth - 6f, 14f), C("0"), axisStyle);
                GUI.Label(new Rect(axisRect.x, axisBase + plotHalf - 7f, axisWidth - 6f, 14f), C($"-{maxAbs}"), axisStyle);
            }

            //柱区：内容不足时铺满可视宽度，超出时出现横向滚动条
            chartScroll = GUILayout.BeginScrollView(chartScroll, GUILayout.Height(chartHeight + 20f));
            Rect rect = GUILayoutUtility.GetRect(contentWidth, chartHeight, GUIStyle.none, GUILayout.MinWidth(contentWidth), GUILayout.ExpandWidth(true));
            if (rect.width < 10f)    //layout阶段尚未计算出尺寸
            {
                GUILayout.EndScrollView();
                GUILayout.EndHorizontal();
                return;
            }

            GUI.DrawTexture(rect, texRow);
            float baseline = rect.y + topPad + plotHalf;

            //网格线：±50%与±100%幅度，基线更亮
            GUI.DrawTexture(new Rect(rect.x, baseline - plotHalf - 0.5f, rect.width, 1f), texGrid);
            GUI.DrawTexture(new Rect(rect.x, baseline - plotHalf / 2f - 0.5f, rect.width, 1f), texGrid);
            GUI.DrawTexture(new Rect(rect.x, baseline + plotHalf / 2f - 0.5f, rect.width, 1f), texGrid);
            GUI.DrawTexture(new Rect(rect.x, baseline + plotHalf - 0.5f, rect.width, 1f), texGrid);
            GUI.DrawTexture(new Rect(rect.x, baseline - 0.5f, rect.width, 1f), texButton);

            for (int i = 0; i < games.Count; i++)
            {
                BgSessionStats.GameRecord g = games[i];
                float x = rect.x + slot * i + (slot - barWidth) / 2f;
                float h = Mathf.Max(2f, plotHalf * Mathf.Abs(g.Delta) / maxAbs);
                bool up = g.Delta >= 0;
                Rect bar = up ? new Rect(x, baseline - h, barWidth, h) : new Rect(x, baseline, barWidth, h);
                GUI.DrawTexture(bar, g.Uncertain ? texOff : (up ? texOn : texRed));

                Rect deltaLabel = up
                    ? new Rect(x - 10f, bar.y - 15f, barWidth + 20f, 14f)
                    : new Rect(x - 10f, bar.yMax + 1f, barWidth + 20f, 14f);
                GUI.Label(deltaLabel, C(g.Uncertain ? "?" : $"{g.Delta:+#;-#;0}"), g.Uncertain ? chartPlaceStyle : (up ? chartUpStyle : chartDownStyle));

                //底部：真实对局显示名次，Synced显示同步标记
                string bottom = g.Synced ? LocalizationManager.GetLangValue("bgStats.synced") : (g.Place > 0 ? $"#{g.Place}" : "");
                GUI.Label(new Rect(x - 10f, rect.yMax - 15f, barWidth + 20f, 14f), C(bottom), chartPlaceStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
        }

        private void DrawEntry(Entry entry, bool searching)
        {
            GUIStyle rowStyle = new GUIStyle();
            rowStyle.normal.background = texRow;
            rowStyle.padding = new RectOffset(10, 10, 6, 8);
            GUILayout.BeginVertical(rowStyle);
            GUILayout.BeginHorizontal();
            //名称占满控件左侧的全部剩余宽度，避免过早换行
            GUILayout.Label(C(searching ? $"{entry.Name}  ({entry.Section})" : entry.Name), nameStyle, GUILayout.ExpandWidth(true));
            DrawWidget(entry);
            GUILayout.Space(4f);
            if (GUILayout.Button(new GUIContent("R", null, LocalizationManager.GetLangValue("modSettings.reset")), buttonStyle, GUILayout.Width(26f)))
            {
                entry.Config.BoxedValue = entry.Config.DefaultValue;
                ClearBuffer(entry.FieldName);
                if (capturingEntry == entry) capturingEntry = null;
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(entry.Description))
                GUILayout.Label(C(entry.Description), descStyle);
            GUILayout.EndVertical();
            GUILayout.Space(3f);
        }

        private void DrawWidget(Entry entry)
        {
            Type type = entry.Config.SettingType;
            try
            {
                if (type == typeof(bool))
                {
                    bool value = (bool)entry.Config.BoxedValue;
                    string text = LocalizationManager.GetLangValue(value ? "modSettings.on" : "modSettings.off");
                    if (GUILayout.Button(C(text), value ? onStyle : offStyle, GUILayout.Width(64f)))
                        entry.Config.BoxedValue = !value;
                }
                else if (type == typeof(KeyboardShortcut))
                {
                    KeyboardShortcut value = (KeyboardShortcut)entry.Config.BoxedValue;
                    string text = capturingEntry == entry
                        ? LocalizationManager.GetLangValue("modSettings.pressKey")
                        : (value.MainKey == KeyCode.None ? "-" : value.ToString());
                    if (GUILayout.Button(C(text), capturingEntry == entry ? onStyle : buttonStyle, GUILayout.Width(220f)))
                        capturingEntry = capturingEntry == entry ? null : entry;
                    if (GUILayout.Button(C("X"), buttonStyle, GUILayout.Width(26f)))
                    {
                        entry.Config.BoxedValue = KeyboardShortcut.Empty;
                        if (capturingEntry == entry) capturingEntry = null;
                    }
                }
                else if (type.IsEnum)
                {
                    Array values = Enum.GetValues(type);
                    int index = Array.IndexOf(values, entry.Config.BoxedValue);
                    if (GUILayout.Button(C("<"), buttonStyle, GUILayout.Width(26f)))
                    {
                        entry.Config.BoxedValue = values.GetValue((index - 1 + values.Length) % values.Length);
                        ClearBuffer(entry.FieldName);
                    }
                    if (values.Length > 24)    //超长枚举（如卡包ID）允许直接输入
                        DrawSerializedField(entry, 166f);
                    else
                        GUILayout.Label(C(entry.Config.BoxedValue.ToString()), enumLabelStyle, GUILayout.Width(166f));
                    if (GUILayout.Button(C(">"), buttonStyle, GUILayout.Width(26f)))
                    {
                        entry.Config.BoxedValue = values.GetValue((index + 1) % values.Length);
                        ClearBuffer(entry.FieldName);
                    }
                }
                else if (type == typeof(string))
                {
                    string value = (string)entry.Config.BoxedValue;
                    string newValue = GUILayout.TextField(value, fieldStyle, GUILayout.Width(248f));
                    if (newValue != value)
                        entry.Config.BoxedValue = newValue;
                }
                else if (type == typeof(int) || type == typeof(float) || type == typeof(long))
                {
                    if (TryGetRange(entry.Config, out float min, out float max))
                    {
                        float value = Convert.ToSingle(entry.Config.BoxedValue);
                        float slider = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(150f));
                        if (!Mathf.Approximately(slider, value))
                        {
                            SetNumeric(entry, slider, min, max);
                            ClearBuffer(entry.FieldName);
                        }
                        GUILayout.Space(4f);
                        DrawNumericField(entry, 62f, min, max);
                    }
                    else
                    {
                        DrawNumericField(entry, 110f, float.MinValue, float.MaxValue);
                    }
                }
                else
                {
                    DrawSerializedField(entry, 248f);
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"ModSettingsUI Draw {entry.FieldName}: {ex.Message}");
            }
        }

        //数值输入框：内容暂存，可完整解析时立即写入配置；写入值与文本一致后清除暂存
        private void DrawNumericField(Entry entry, float width, float min, float max)
        {
            string current = entry.Config.BoxedValue.ToString();
            string shown = bufferField == entry.FieldName ? bufferText : current;
            string newText = GUILayout.TextField(shown, fieldStyle, GUILayout.Width(width));
            if (newText == shown) return;
            bufferField = entry.FieldName;
            bufferText = newText;
            if (float.TryParse(newText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            {
                SetNumeric(entry, parsed, min, max);
                if (entry.Config.BoxedValue.ToString() == newText)
                    ClearBuffer(entry.FieldName);
            }
        }

        private static void SetNumeric(Entry entry, float value, float min, float max)
        {
            //先按 double 裁剪到目标类型范围，避免超大输入溢出变成垃圾值
            double clamped = Math.Min(Math.Max((double)Mathf.Clamp(value, min, max), int.MinValue), int.MaxValue);
            Type type = entry.Config.SettingType;
            if (type == typeof(int)) entry.Config.BoxedValue = (int)Math.Round(clamped);
            else if (type == typeof(long)) entry.Config.BoxedValue = (long)Math.Round(Math.Min(Math.Max((double)Mathf.Clamp(value, min, max), -9.2E18), 9.2E18));
            else entry.Config.BoxedValue = Mathf.Clamp(value, min, max);
        }

        //其它类型走 Toml 序列化字符串
        private void DrawSerializedField(Entry entry, float width)
        {
            string current = entry.Config.GetSerializedValue();
            string shown = bufferField == entry.FieldName ? bufferText : current;
            string newText = GUILayout.TextField(shown, fieldStyle, GUILayout.Width(width));
            if (newText == shown) return;
            bufferField = entry.FieldName;
            bufferText = newText;
            try
            {
                entry.Config.SetSerializedValue(newText);
                if (entry.Config.GetSerializedValue() == newText)
                    ClearBuffer(entry.FieldName);
            }
            catch
            {
                //输入未完成或非法，保留暂存等待继续输入
            }
        }

        private static bool TryGetRange(ConfigEntryBase config, out float min, out float max)
        {
            min = 0f;
            max = 0f;
            AcceptableValueBase acceptable = config.Description?.AcceptableValues;
            if (acceptable == null) return false;
            Type acceptableType = acceptable.GetType();
            if (!acceptableType.IsGenericType || acceptableType.GetGenericTypeDefinition() != typeof(AcceptableValueRange<>)) return false;
            min = Convert.ToSingle(acceptableType.GetProperty("MinValue").GetValue(acceptable, null));
            max = Convert.ToSingle(acceptableType.GetProperty("MaxValue").GetValue(acceptable, null));
            return true;
        }
    }
}
