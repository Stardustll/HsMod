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

        private static List<ProductEntry> s_products;
        private static bool s_loading;
        private static string s_error;

        public static List<ProductEntry> Products => s_products;
        public static bool Loading => s_loading;
        public static string Error => s_error;

        //清除缓存，下次访问时重新遍历商店
        public static void Invalidate()
        {
            s_products = null;
            s_error = null;
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
                        if (!entry.Bundle.IsFree())
                        {
                            continue;    //只展示无价格的商品（零元购目标）
                        }
                        list.Add(entry);
                    }
                }
                s_products = list;
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
                StoreManager.Get().StartStoreBuy(new BuyPmtProductEventArgs(entry.Bundle, CurrencyType.GOLD, 1));
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
