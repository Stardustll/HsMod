using PegasusUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static HsMod.PluginConfig;

namespace HsMod
{
    //商店商品浏览与零元购：遍历所有 ProductType 的商品并缓存，供设置页展示与购买
    public static class StorePanel
    {
        public class ProductEntry
        {
            public Hearthstone.Store.ProductInfo Bundle;
            public long PmtId;
            public string Title;
            public string Description;
            public bool HasGoldPrice;
            public double GoldPrice;
            public int ItemCount;
        }

        //展示筛选：全部 / 仅无价格（零元购目标）/ 仅有价
        public enum StoreFilter
        {
            All,
            Free,
            Priced
        }

        //展示排序：商店原顺序 / 价格升序 / 价格降序 / 名称
        public enum StoreSort
        {
            Default,
            PriceAsc,
            PriceDesc,
            Name
        }

        private static List<ProductEntry> s_products;
        private static bool s_loading;
        private static string s_error;

        //展示列表缓存：OnGUI 每帧会多次调用 GetDisplay，筛选/排序结果不必重复计算。
        //s_products 变化时（Refresh/Invalidate）置空失效
        private static List<ProductEntry> s_display;
        private static StoreFilter s_displayFilter;
        private static StoreSort s_displaySort;

        public static List<ProductEntry> Products => s_products;
        public static bool Loading => s_loading;
        public static string Error => s_error;

        //清除缓存，下次访问时重新遍历商店
        public static void Invalidate()
        {
            s_products = null;
            s_error = null;
            s_display = null;
        }

        public static void Refresh()
        {
            if (s_loading)
            {
                return;
            }
            s_loading = true;
            s_error = null;
            s_products = null;
            s_display = null;    //商品集变化，丢弃展示列表缓存（含提前返回的失败路径）
            try
            {
                if (StoreManager.Get() == null || !StoreManager.Get().IsOpen())
                {
                    s_error = "store closed";
                    return;
                }
                HashSet<long> seen = new HashSet<long>();
                List<ProductEntry> list = new List<ProductEntry>();
                foreach (ProductType t in Enum.GetValues(typeof(ProductType)))
                {
                    IEnumerable<Hearthstone.Store.ProductInfo> bundles = null;
                    try
                    {
                        bundles = StoreManager.Get().GetAvailableBundlesForProduct(t, false);
                    }
                    catch
                    {
                        continue;    //单个 ProductType 查询失败不影响其他类型
                    }
                    if (bundles == null)
                    {
                        continue;
                    }
                    foreach (Hearthstone.Store.ProductInfo bundle in bundles)
                    {
                        if (bundle == null)
                        {
                            continue;
                        }
                        long pmtId = bundle.Id.Value;    //同一商品可能出现在多个 ProductType 下，按 PMT 去重
                        if (pmtId <= 0 || !seen.Add(pmtId))
                        {
                            continue;
                        }
                        ProductEntry entry = new ProductEntry
                        {
                            Bundle = bundle,
                            PmtId = pmtId,
                            Title = bundle.Title,
                            Description = bundle.Description,
                            ItemCount = bundle.Items?.Count ?? 0
                        };
                        entry.HasGoldPrice = bundle.TryGetBundlePrice(CurrencyType.GOLD, out double gold);
                        if (entry.HasGoldPrice)
                        {
                            entry.GoldPrice = gold;
                        }
                        list.Add(entry);    //遍历到的商品全部保留，是否只买零元购由界面的筛选决定
                    }
                }
                s_products = list;
                s_display = null;    //商品集变化，丢弃展示列表缓存
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"StorePanel.Refresh: {ex.Message} \n{ex.StackTrace}");
                s_error = ex.ToString();
            }
            finally
            {
                s_loading = false;
            }
        }

        //是否免费（无任何有效价格）
        public static bool IsFree(ProductEntry entry)
        {
            return entry?.Bundle != null && entry.Bundle.IsFree();
        }

        //列表展示用的标题：无标题时退化为 PMT ID
        public static string DisplayTitle(ProductEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }
            return string.IsNullOrEmpty(entry.Title) ? entry.PmtId.ToString() : entry.Title;
        }

        //按筛选与排序返回展示列表；商品尚未加载时返回空列表
        public static List<ProductEntry> GetDisplay(StoreFilter filter, StoreSort sort)
        {
            if (s_display != null && s_displayFilter == filter && s_displaySort == sort)
            {
                return s_display;
            }

            List<ProductEntry> list = new List<ProductEntry>();
            if (s_products != null)
            {
                foreach (ProductEntry entry in s_products)
                {
                    if (filter == StoreFilter.Free && !IsFree(entry))
                    {
                        continue;
                    }
                    if (filter == StoreFilter.Priced && IsFree(entry))
                    {
                        continue;
                    }
                    list.Add(entry);
                }

                if (sort == StoreSort.Name)
                {
                    list.Sort((a, b) => string.Compare(DisplayTitle(a), DisplayTitle(b), StringComparison.Ordinal));
                }
                else if (sort == StoreSort.PriceAsc || sort == StoreSort.PriceDesc)
                {
                    //价格排序按 (可比值组, 价格, 名称) 字典序：
                    //升序 = 免费 → 金币由低到高 → 仅其他货币；降序 = 金币由高到低 → 免费 → 仅其他货币。
                    //仅其他货币的商品没有金币可比价，两个方向都排在最后，组内按名称
                    int dir = sort == StoreSort.PriceDesc ? -1 : 1;
                    list.Sort((a, b) =>
                    {
                        int groupA = PriceGroup(a);
                        int groupB = PriceGroup(b);
                        if (groupA != groupB)
                        {
                            return groupA.CompareTo(groupB);
                        }
                        if (groupA == 0)
                        {
                            int priceDiff = PriceKey(a).CompareTo(PriceKey(b));
                            if (priceDiff != 0)
                            {
                                return priceDiff * dir;
                            }
                        }
                        return string.Compare(DisplayTitle(a), DisplayTitle(b), StringComparison.Ordinal);
                    });
                }
                //StoreSort.Default：保持商店遍历顺序
            }

            s_display = list;
            s_displayFilter = filter;
            s_displaySort = sort;
            return list;
        }

        //0 = 可与金币比价（免费或金币价），1 = 仅其他货币（不可比价，恒定置末）
        private static int PriceGroup(ProductEntry entry)
        {
            return IsFree(entry) || entry.HasGoldPrice ? 0 : 1;
        }

        //比价用的数值：免费记 0，金币价记金额（PriceGroup 为 1 时不参与比较）
        private static double PriceKey(ProductEntry entry)
        {
            return entry.HasGoldPrice ? entry.GoldPrice : 0;
        }

        //商品价格文本：免费 / 金币价 / 其他币种价
        public static string GetPriceText(ProductEntry entry)
        {
            if (entry == null || entry.Bundle == null)
            {
                return string.Empty;
            }
            if (entry.Bundle.IsFree())
            {
                return LocalizationManager.GetLangValue("store.free");
            }
            if (entry.HasGoldPrice)
            {
                return $"{entry.GoldPrice:0} {LocalizationManager.GetLangValue("store.gold")}";
            }
            CurrencyType vc = entry.Bundle.GetFirstNonGoldVirtualCurrencyPriceType();
            if (vc != CurrencyType.NONE)
            {
                Hearthstone.DataModels.PriceDataModel pd = entry.Bundle.GetPriceDataModel(vc);
                if (pd != null && !string.IsNullOrEmpty(pd.DisplayText))
                {
                    return $"{pd.DisplayText} {vc}";
                }
                return $"{vc}";
            }
            return LocalizationManager.GetLangValue("store.other");
        }

        //发起购买：无金币价格的商品用金币渠道购买（零成本零元购），有金币价格的正常花金币
        public static bool TryBuy(ProductEntry entry, out string message)
        {
            message = string.Empty;
            try
            {
                if (entry?.Bundle == null)
                {
                    message = LocalizationManager.GetLangValue("store.cannotBuy");
                    return false;
                }
                if (SceneMgr.Get()?.GetMode() == SceneMgr.Mode.GAMEPLAY)
                {
                    message = LocalizationManager.GetLangValue("info.cantBuyInGame");
                    return false;
                }
                if (StoreManager.Get() == null || !StoreManager.Get().IsOpen())
                {
                    message = LocalizationManager.GetLangValue("info.shopInitFailed");
                    return false;
                }
                if (!StoreManager.Get().CanBuyBundle(entry.Bundle))
                {
                    message = LocalizationManager.GetLangValue("store.cannotBuy");
                    return false;
                }
                StoreManager.Get().StartStoreBuy(new BuyProductEventArgs(entry.Bundle, CurrencyType.GOLD, 1));
                message = LocalizationManager.GetLangValue("info.waitPurchase");
                return true;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"StorePanel.TryBuy: {ex.Message} \n{ex.StackTrace}");
                message = ex.Message;
                return false;
            }
        }
    }
}
