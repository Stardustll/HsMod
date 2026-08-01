using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static HsMod.PluginConfig;

namespace HsMod
{
    //皮肤数据层：从游戏 DBF 加载各类皮肤列表并缓存，供设置页选择
    //英雄类皮肤为映射模式（HsSkins.cfg：原始皮肤:替换皮肤，支持多值随机），其他为直接设置
    public static class SkinPanel
    {
        public class SkinItem
        {
            public int Id;
            public string Name;
            public int ClassId = -1;    //英雄职业（TAG_CLASS），非英雄类为 -1
            public int PetId = -1;    //宠物类型（仅 Pet 有效）
        }

        public enum SkinType
        {
            CardBack,    //卡背 → skinCardBack
            Coin,        //幸运币 → skinCoin
            Board,       //对战面板 → skinBoard
            BgsBoard,    //酒馆面板 → skinBgsBoard
            Finisher,    //酒馆斩杀特效 → skinBgsFinisher
            Hero,        //对战英雄 → HsSkins.cfg 映射
            BgsHero,     //酒馆英雄 → HsSkins.cfg 映射
            Bob,         //鲍勃 → skinBob（直接设置）
            Pet          //宠物 → skinPet
        }

        private class SkinListCache
        {
            public List<SkinItem> Items;
            public DateTime Time;
        }

        private static readonly Dictionary<SkinType, SkinListCache> s_cache = new Dictionary<SkinType, SkinListCache>();
        private const double EmptyRetrySeconds = 10;    //空结果短缓存：DBF 未加载完时短暂返回空，随后自动重试

        //英雄映射表：原始皮肤ID → 目标皮肤ID列表（HsSkins.cfg）
        public static readonly Dictionary<int, List<int>> Mapping = new Dictionary<int, List<int>>();

        private static string MappingFile => Path.Combine(BepInEx.Paths.ConfigPath, "HsSkins.cfg");

        public static void Invalidate()
        {
            s_cache.Clear();
            LoadMapping();
        }

        public static bool IsHeroMappingType(SkinType type)
        {
            return type == SkinType.Hero || type == SkinType.BgsHero;
        }

        public static void LoadMapping()
        {
            Mapping.Clear();
            try
            {
                if (File.Exists(MappingFile))
                {
                    foreach (string line in File.ReadAllLines(MappingFile))
                    {
                        string l = line.Trim();
                        if (string.IsNullOrEmpty(l) || l.StartsWith("#")) continue;
                        string[] parts = l.Split(':');
                        if (parts.Length != 2) continue;
                        if (!int.TryParse(parts[0].Trim(), out int src) || src <= 0) continue;
                        List<int> targets = new List<int>();
                        foreach (string t in parts[1].Split(','))
                            if (int.TryParse(t.Trim(), out int tid) && tid > 0) targets.Add(tid);
                        if (targets.Count > 0) Mapping[src] = targets;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"SkinPanel.LoadMapping: {ex.Message}");
            }
        }

        //保存映射到 HsSkins.cfg（保留注释行，替换/删除旧映射行），并刷新 HeroesMapping
        public static void SaveMapping()
        {
            try
            {
                List<string> lines = new List<string>();
                HashSet<int> handled = new HashSet<int>();
                if (File.Exists(MappingFile))
                {
                    foreach (string line in File.ReadAllLines(MappingFile))
                    {
                        string l = line.Trim();
                        if (string.IsNullOrEmpty(l) || l.StartsWith("#"))
                        {
                            lines.Add(line);
                            continue;
                        }
                        string[] parts = l.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int src) && src > 0)
                        {
                            if (Mapping.TryGetValue(src, out List<int> targets))
                            {
                                lines.Add($"{src}:{string.Join(",", targets)}");
                                handled.Add(src);
                            }
                            //已删除的映射行不再保留
                        }
                        else
                        {
                            lines.Add(line);
                        }
                    }
                }
                foreach (KeyValuePair<int, List<int>> kv in Mapping)
                    if (!handled.Contains(kv.Key)) lines.Add($"{kv.Key}:{string.Join(",", kv.Value)}");
                File.WriteAllLines(MappingFile, lines, new UTF8Encoding(false));
                LoadSkinsConfigFromFile();    //刷新 PatchFavorite 使用的 HeroesMapping
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"SkinPanel.SaveMapping: {ex.Message} \n{ex.StackTrace}");
            }
        }

        public static List<SkinItem> GetList(SkinType type)
        {
            lock (s_cache)
            {
                if (s_cache.TryGetValue(type, out SkinListCache cached))
                {
                    if (cached.Items.Count > 0 || (DateTime.UtcNow - cached.Time).TotalSeconds < EmptyRetrySeconds)
                        return cached.Items;
                    s_cache.Remove(type);    //空结果过期，重新加载
                }
            }

            List<SkinItem> list = new List<SkinItem>();
            try
            {
                switch (type)
                {
                    case SkinType.CardBack:
                        foreach (var r in GameDbf.CardBack.GetRecords().OrderBy(x => x.ID))
                            if (r != null) list.Add(new SkinItem { Id = r.ID, Name = r.Name.GetString() });
                        break;
                    case SkinType.Coin:
                        foreach (var r in GameDbf.CosmeticCoin.GetRecords().OrderBy(x => x.CardId))
                            if (r != null) list.Add(new SkinItem { Id = r.CardId, Name = r.Name.GetString() });
                        break;
                    case SkinType.Board:
                        foreach (var r in GameDbf.Board.GetRecords().OrderBy(x => x.ID))
                            if (r != null) list.Add(new SkinItem { Id = r.ID, Name = string.IsNullOrEmpty(r.NoteDesc) ? r.Prefab : r.NoteDesc });
                        break;
                    case SkinType.BgsBoard:
                        foreach (var r in GameDbf.BattlegroundsBoardSkin.GetRecords().OrderBy(x => x.ID))
                            if (r != null) list.Add(new SkinItem { Id = r.ID, Name = r.CollectionName.GetString() });
                        break;
                    case SkinType.Finisher:
                        foreach (var r in GameDbf.BattlegroundsFinisher.GetRecords().OrderBy(x => x.ID))
                            if (r != null) list.Add(new SkinItem { Id = r.ID, Name = r.CollectionName.GetString() });
                        break;
                    case SkinType.Hero:
                        AddHeroes(list, null);
                        break;
                    case SkinType.BgsHero:
                        AddHeroes(list, Assets.CardHero.HeroType.BATTLEGROUNDS_HERO);
                        break;
                    case SkinType.Bob:
                        AddHeroes(list, Assets.CardHero.HeroType.BATTLEGROUNDS_GUIDE);
                        break;
                    case SkinType.Pet:
                        foreach (var r in GameDbf.PetVariant.GetRecords().OrderBy(x => x.ID))
                            if (r != null) list.Add(new SkinItem { Id = r.ID, Name = r.Name.GetString(), PetId = r.PetId });
                        break;
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"SkinPanel.GetList({type}): {ex.Message} \n{ex.StackTrace}");
            }
            lock (s_cache)
            {
                s_cache[type] = new SkinListCache { Items = list, Time = DateTime.UtcNow };
            }
            return list;
        }

        private static void AddHeroes(List<SkinItem> list, Assets.CardHero.HeroType? filter)
        {
            foreach (var r in GameDbf.CardHero.GetRecords())
            {
                if (r == null) continue;
                if (filter.HasValue)
                {
                    if (r.HeroType != filter.Value) continue;
                }
                else if (r.HeroType == Assets.CardHero.HeroType.BATTLEGROUNDS_HERO
                    || r.HeroType == Assets.CardHero.HeroType.BATTLEGROUNDS_GUIDE)
                {
                    continue;    //对战英雄仅保留非酒馆类型
                }
                string name = GetHeroName(r.CardId);
                if (string.IsNullOrEmpty(name)) continue;
                list.Add(new SkinItem { Id = r.CardId, Name = name, ClassId = GetHeroClass(r.CardId) });
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        }

        private static string GetHeroName(int cardId)
        {
            try
            {
                return GameDbf.Card.GetRecord(cardId)?.Name.GetString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static int GetHeroClass(int cardId)
        {
            try
            {
                return DefLoader.Get()?.GetEntityDef(cardId)?.GetTag(GAME_TAG.CLASS) ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        //职业显示名：优先语言键，缺失时返回枚举名
        public static string GetClassName(int classId)
        {
            if (classId < 0) return LocalizationManager.GetLangValue("skin.class.INVALID");
            string tagName = ((TAG_CLASS)classId).ToString();
            string res = LocalizationManager.GetLangValue("skin.class." + tagName);
            return res == "VALUE_NOT_FOUND" ? tagName : res;
        }

        //英雄映射模式：source 已有映射 → 返回其目标列表；否则返回当前选中的 source（-1 无）
        public static bool HasMapping(int sourceId)
        {
            return Mapping.ContainsKey(sourceId);
        }

        public static List<int> GetMappingTargets(int sourceId)
        {
            return Mapping.TryGetValue(sourceId, out List<int> targets) ? new List<int>(targets) : new List<int>();
        }

        public static void SetMapping(int sourceId, List<int> targets)
        {
            if (targets == null || targets.Count == 0)
                Mapping.Remove(sourceId);
            else
                Mapping[sourceId] = new List<int>(targets);
        }

        //网页类型名 → SkinType（与 /api/skins 接口约定一致）
        public static SkinType? ParseType(string type)
        {
            switch (type)
            {
                case "cardBack": return SkinType.CardBack;
                case "coin": return SkinType.Coin;
                case "board": return SkinType.Board;
                case "bgsBoard": return SkinType.BgsBoard;
                case "bgsFinisher": return SkinType.Finisher;
                case "hero": return SkinType.Hero;
                case "bgsHero": return SkinType.BgsHero;
                case "bob": return SkinType.Bob;
                case "pet": return SkinType.Pet;
                default: return null;
            }
        }

        //非英雄类：直接设置的当前值
        public static int GetCurrentValue(SkinType type)
        {
            switch (type)
            {
                case SkinType.CardBack: return skinCardBack.Value;
                case SkinType.Coin: return skinCoin.Value;
                case SkinType.Board: return skinBoard.Value;
                case SkinType.BgsBoard: return skinBgsBoard.Value;
                case SkinType.Finisher: return skinBgsFinisher.Value;
                case SkinType.Bob: return skinBob.Value;
                case SkinType.Pet: return skinPet.Value;
                default: return -1;
            }
        }

        public static void SetValue(SkinType type, int id)
        {
            switch (type)
            {
                case SkinType.CardBack: skinCardBack.Value = id; break;
                case SkinType.Coin: skinCoin.Value = id; break;
                case SkinType.Board: skinBoard.Value = id; break;
                case SkinType.BgsBoard: skinBgsBoard.Value = id; break;
                case SkinType.Finisher: skinBgsFinisher.Value = id; break;
                case SkinType.Bob: skinBob.Value = id; break;
                case SkinType.Pet: skinPet.Value = id; break;
            }
        }
    }
}
