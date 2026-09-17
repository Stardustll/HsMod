using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Hearthstone;
using PegasusShared;
using UnityEngine;
using static HsMod.PluginConfig;

namespace HsMod
{
    public partial class Patcher
    {
	public class PatchCollectionUnlock
	{
		[ThreadStatic]
		private static bool skipOwnedOverride;

		private static object cardBackTable;

		private static object coinTable;

		private static HashSet<int> cardBackIds;

		private static HashSet<int> coinIds;

		private static bool IsEnabled()
		{
			return PluginConfig.CollectionVisualsEnabled;
		}

		private static bool ShouldOverrideOwned()
		{
			if (IsEnabled())
			{
				return !skipOwnedOverride;
			}
			return false;
		}

		private static bool IsConstructedHeroSkin(CollectibleCard card)
		{
			if (card != null)
			{
				return IsConstructedHeroSkin(card.CardId);
			}
			return false;
		}

		private static bool IsConstructedHeroSkin(string cardId)
		{
			if (!string.IsNullOrEmpty(cardId))
			{
				return LocalCollectionAppearance.IsConstructedHero(GameUtils.TranslateCardIdToDbId(cardId, false));
			}
			return false;
		}

		private static bool ReallyOwnsCardBack(int cardBackId)
		{
			NetCache obj = NetCache.Get();
			NetCache.NetCacheCardBacks val = ((obj != null) ? obj.GetNetObject<NetCache.NetCacheCardBacks>() : null);
			if (val != null && val.CardBacks != null)
			{
				return val.CardBacks.Contains(cardBackId);
			}
			return false;
		}

		private static bool ReallyOwnsCoin(int coinId)
		{
			NetCache obj = NetCache.Get();
			NetCache.NetCacheCoins val = ((obj != null) ? obj.GetNetObject<NetCache.NetCacheCoins>() : null);
			if (val != null && val.Coins != null)
			{
				return val.Coins.Contains(coinId);
			}
			return false;
		}

		internal static bool ReallyOwnsHeroCard(int heroCardDbid)
		{
			string text = GameUtils.TranslateDbIdToCardId(heroCardDbid, false);
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			bool flag = skipOwnedOverride;
			skipOwnedOverride = true;
			try
			{
				CollectionManager obj = CollectionManager.Get();
				return obj != null && obj.GetTotalOwnedCount(text) > 0;
			}
			finally
			{
				skipOwnedOverride = flag;
			}
		}

		private static HashSet<int> AllEnabledCardBackIds()
		{
			Dbf<CardBackDbfRecord> cardBack = GameDbf.CardBack;
			if (cardBackIds != null && cardBackTable == cardBack)
			{
				return cardBackIds;
			}
			HashSet<int> hashSet = new HashSet<int>();
			if (cardBack == null)
			{
				return hashSet;
			}
			foreach (CardBackDbfRecord record in cardBack.GetRecords())
			{
				if (record != null && record.Enabled)
				{
					hashSet.Add(((DbfRecord)record).ID);
				}
			}
			if (hashSet.Count > 0)
			{
				cardBackTable = cardBack;
				cardBackIds = hashSet;
			}
			return hashSet;
		}

		private static HashSet<int> AllEnabledCoinIds()
		{
			Dbf<CosmeticCoinDbfRecord> cosmeticCoin = GameDbf.CosmeticCoin;
			if (coinIds != null && coinTable == cosmeticCoin)
			{
				return coinIds;
			}
			HashSet<int> hashSet = new HashSet<int>();
			if (cosmeticCoin == null)
			{
				return hashSet;
			}
			foreach (CosmeticCoinDbfRecord record in cosmeticCoin.GetRecords())
			{
				if (record != null && record.Enabled)
				{
					hashSet.Add(((DbfRecord)record).ID);
				}
			}
			if (hashSet.Count > 0)
			{
				coinTable = cosmeticCoin;
				coinIds = hashSet;
			}
			return hashSet;
		}

		private static bool IsLocalFavoriteHero(string heroId)
		{
			if (string.IsNullOrEmpty(heroId))
			{
				return false;
			}
			int id = GameUtils.TranslateCardIdToDbId(heroId, false);
			if (LocalCollectionAppearance.IsConstructedHero(id))
			{
				return LocalCollectionAppearance.GetHeroFavorites(GameUtils.GetTagClassFromCardDbId(id)).Any((CollectionHeroAppearance item) => item.CardId == id);
			}
			return false;
		}

		internal static string GetLocalHeroForClass(TAG_CLASS heroClass)
		{
			CollectionHeroAppearance collectionHeroAppearance = (IsEnabled() ? LocalCollectionAppearance.GetHeroForClass(heroClass) : null);
			if (collectionHeroAppearance != null)
			{
				return GameUtils.TranslateDbIdToCardId(collectionHeroAppearance.CardId, false);
			}
			return null;
		}

		internal static string GetLocalHeroPowerForCard(string heroPowerCardId)
		{
			if (!IsEnabled() || string.IsNullOrEmpty(heroPowerCardId) || string.IsNullOrEmpty(Utils.CacheRawHeroCardId))
			{
				return null;
			}
			int num = GameUtils.TranslateCardIdToDbId(Utils.CacheRawHeroCardId, false);
			if (!LocalCollectionAppearance.IsConstructedHero(num))
			{
				return null;
			}
			if (!string.Equals(heroPowerCardId, GameUtils.GetHeroPowerCardIdFromHero(num), StringComparison.Ordinal))
			{
				return null;
			}
			TAG_CLASS tagClassFromCardDbId = GameUtils.GetTagClassFromCardDbId(num);
			string localHeroForClass = GetLocalHeroForClass(tagClassFromCardDbId);
			if (string.IsNullOrEmpty(localHeroForClass))
			{
				return null;
			}
			int num2 = GameUtils.TranslateCardIdToDbId(localHeroForClass, false);
			if (!LocalCollectionAppearance.IsConstructedHero(num2) || GameUtils.GetTagClassFromCardDbId(num2) != tagClassFromCardDbId)
			{
				return null;
			}
			return GameUtils.GetHeroPowerCardIdFromHero(num2);
		}

		private static NetCache.CardDefinition HeroDefinition(CollectionHeroAppearance hero)
		{
			if (hero != null)
			{
				return new NetCache.CardDefinition
				{
					Name = GameUtils.TranslateDbIdToCardId(hero.CardId, false),
					Premium = (TAG_PREMIUM)hero.Premium
				};
			}
			return null;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CardBackManager), "IsCardBackOwned")]
		public static void PatchIsCardBackOwned(ref bool __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = true;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CardBackManager), "GetCardBacksOwned")]
		public static void PatchGetCardBacksOwned(ref HashSet<int> __result)
		{
			if (ShouldOverrideOwned() && __result != null)
			{
				HashSet<int> hashSet = new HashSet<int>(__result);
				hashSet.UnionWith(AllEnabledCardBackIds());
				__result = hashSet;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CardBackManager), "GetNumCardBacksOwned")]
		public static void PatchGetNumCardBacksOwned(ref int __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = Math.Max(__result, AllEnabledCardBackIds().Count);
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CardBackManager), "IsCardBackFavorited")]
		public static void PatchIsCardBackFavorited(int cardBackID, ref bool __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = LocalCollectionAppearance.GetCardBackFavorites().Contains(cardBackID);
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CardBackManager), "CanToggleFavoriteCardBack")]
		public static void PatchCanToggleFavoriteCardBack(int cardBackId, ref bool __result)
		{
			if (ShouldOverrideOwned())
			{
				CardBackDbfRecord val = GameDbf.CardBack?.GetRecord(cardBackId);
				List<int> cardBackFavorites = LocalCollectionAppearance.GetCardBackFavorites();
				__result = val != null && val.Enabled && !val.IsRandomCardBack && (!cardBackFavorites.Contains(cardBackId) || cardBackFavorites.Count > 1);
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CardBackManager), "TotalFavoriteCardBacks")]
		public static void PatchTotalFavoriteCardBacks(ref int __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = LocalCollectionAppearance.GetCardBackFavorites().Count;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CardBackManager), "MultipleFavoriteCardBacksEnabled")]
		public static void PatchMultipleFavoriteCardBacksEnabled(ref bool __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = true;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CollectionManagerDisplay), "CollectionPageContentsChangedToCardBacks")]
		public static void PatchCollectionPageContentsChangedToCardBacks(List<CardBackManager.OwnedCardBack> cardBacksToDisplay)
		{
			if (!ShouldOverrideOwned() || cardBacksToDisplay == null)
			{
				return;
			}
			foreach (CardBackManager.OwnedCardBack item in cardBacksToDisplay)
			{
				if (item != null)
				{
					item.m_owned = true;
				}
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionPageDisplay), "UpdateFavoriteCardBacks")]
		public static void PatchUpdateFavoriteCardBacks(CollectionPageDisplay __instance, CollectionUtils.ViewMode mode)
		{
			if (!IsEnabled() || (int)mode != 2)
			{
				return;
			}
			List<CollectionCardVisual> value = Traverse.Create((object)__instance).Field("m_collectionCardVisuals").GetValue<List<CollectionCardVisual>>();
			if (value == null)
			{
				return;
			}
			foreach (CollectionCardVisual item in value)
			{
				if (!(item == null) && item.IsShown())
				{
					Actor actor = item.GetActor();
					CollectionCardBack val = ((actor != null) ? ((Component)actor).GetComponent<CollectionCardBack>() : null);
					if (!(val == null))
					{
						val.ShowFavoriteBanner(LocalCollectionAppearance.GetCardBackFavorites().Contains(val.GetCardBackId()));
					}
				}
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Network), "SetFavoriteCardBack")]
		public static bool PatchSetFavoriteCardBack(int cardBack, bool isFavorite)
		{
			if (!IsEnabled())
			{
				return true;
			}
			try
			{
				bool flag = LocalCollectionAppearance.SetCardBackFavorite(cardBack, isFavorite);
				PatchFavorite.InvokeNetCacheEvent("FavoriteCardBackChanged", cardBack, flag);
				CollectionManager.Get()?.OnCollectionChanged();
			}
			catch (Exception message)
			{
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, message);
			}
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CosmeticCoinManager), "IsOwnedCoinCard")]
		public static void PatchIsOwnedCoinCard(ref bool __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = true;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CosmeticCoinManager), "GetCoinsOwned")]
		public static void PatchGetCoinsOwned(ref HashSet<int> __result)
		{
			if (ShouldOverrideOwned() && __result != null)
			{
				HashSet<int> hashSet = new HashSet<int>(__result);
				hashSet.UnionWith(AllEnabledCoinIds());
				__result = hashSet;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CosmeticCoinManager), "GetTotalCoinsOwned")]
		public static void PatchGetTotalCoinsOwned(ref int __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = Math.Max(__result, AllEnabledCoinIds().Count);
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CosmeticCoinManager), "IsFavoriteCoin")]
		public static void PatchIsFavoriteCoin(int coinId, ref bool __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = LocalCollectionAppearance.GetCoinFavorites().Contains(coinId);
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CosmeticCoinManager), "GetTotalFavoriteCoins")]
		public static void PatchGetTotalFavoriteCoins(ref int __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = LocalCollectionAppearance.GetCoinFavorites().Count;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Network), "SetFavoriteCosmeticCoin")]
		public static bool PatchSetFavoriteCosmeticCoin(int coin, bool isFavorite)
		{
			if (!IsEnabled())
			{
				return true;
			}
			try
			{
				bool flag = LocalCollectionAppearance.SetCoinFavorite(coin, isFavorite);
				PatchFavorite.InvokeNetCacheEvent("FavoriteCoinChanged", coin, flag);
				CollectionManager.Get()?.OnCollectionChanged();
			}
			catch (Exception message)
			{
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, message);
			}
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(HeroSkinUtils), "IsHeroSkinOwned")]
		public static void PatchIsHeroSkinOwned(string cardId, ref bool __result)
		{
			if (ShouldOverrideOwned() && IsConstructedHeroSkin(cardId))
			{
				__result = true;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(HeroSkinUtils), "CanToggleFavoriteHeroSkin")]
		public static void PatchCanToggleFavoriteHeroSkin(TAG_CLASS heroClass, string cardId, ref bool __result)
		{
			if (ShouldOverrideOwned() && IsConstructedHeroSkin(cardId))
			{
				List<CollectionHeroAppearance> heroFavorites = LocalCollectionAppearance.GetHeroFavorites(heroClass);
				int id = GameUtils.TranslateCardIdToDbId(cardId, false);
				__result = !heroFavorites.Any((CollectionHeroAppearance item) => item.CardId == id) || heroFavorites.Count > 1;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionManager), "IsCardOwned")]
		public static void PatchIsCardOwned(string cardId, ref bool __result)
		{
			if (ShouldOverrideOwned() && IsConstructedHeroSkin(cardId))
			{
				__result = true;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionManager), "IsCardInCollection")]
		public static void PatchIsCardInCollection(string cardID, ref bool __result)
		{
			if (ShouldOverrideOwned() && IsConstructedHeroSkin(cardID))
			{
				__result = true;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionManager), "GetTotalOwnedCount")]
		public static void PatchGetTotalOwnedCount(string cardId, ref int __result)
		{
			if (ShouldOverrideOwned() && IsConstructedHeroSkin(cardId))
			{
				__result = Math.Max(1, __result);
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectibleCard), "get_OwnedCount")]
		public static void PatchCollectibleCardOwnedCount(CollectibleCard __instance, ref int __result)
		{
			if (ShouldOverrideOwned() && __instance != null)
			{
				if (IsConstructedHeroSkin(__instance))
				{
					__result = Math.Max(1, __result);
				}
				else if (Utils.CheckInfo.IsCoin(__instance.CardId))
				{
					__result = Math.Max(1, __result);
				}
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionManager), "GetHeroPremium")]
		public static void PatchGetHeroPremium(TAG_CLASS classTag, ref TAG_PREMIUM __result)
		{
			if (ShouldOverrideOwned() && !string.IsNullOrEmpty(CollectionManager.GetVanillaHero(classTag)))
			{
				__result = (TAG_PREMIUM)1;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(ActorNames), "GetPlayActorByTags")]
		public static void PatchHeroPowerFrame(EntityBase entityBase, ref TAG_PREMIUM premiumType)
		{
			premiumType = (TAG_PREMIUM)(int)HeroPowerAppearance.GetPremium(entityBase, premiumType);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(ActorNames), "GetPlayActorByTags")]
		public static void PatchHeroPowerSkinFrame(EntityBase entityBase, TAG_PREMIUM premiumType, ref string __result)
		{
			if (HeroPowerAppearance.TryGetFrameAsset(entityBase, premiumType, out var asset))
			{
				__result = asset;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CollectionManager), "GetRandomHeroIdOwnedByPlayer")]
		[HarmonyPatch(typeof(CollectibleCard), "AddCounts")]
		[HarmonyPatch(typeof(CollectibleCard), "RemoveCounts")]
		public static void PatchGetRandomHeroIdOwnedByPlayerBegin(out bool __state)
		{
			__state = skipOwnedOverride;
			skipOwnedOverride = true;
		}

		[HarmonyFinalizer]
		[HarmonyPatch(typeof(CollectionManager), "GetRandomHeroIdOwnedByPlayer")]
		[HarmonyPatch(typeof(CollectibleCard), "AddCounts")]
		[HarmonyPatch(typeof(CollectibleCard), "RemoveCounts")]
		public static void PatchGetRandomHeroIdOwnedByPlayerFinally(bool __state)
		{
			skipOwnedOverride = __state;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionManager), "IsFavoriteHero")]
		public static void PatchIsFavoriteHero(string heroId, ref bool __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = IsLocalFavoriteHero(heroId);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CollectionManager), "GetFavoriteHero")]
		public static bool PatchGetFavoriteHero(string heroId, ref NetCache.CardDefinition __result)
		{
			if (!ShouldOverrideOwned() || !IsConstructedHeroSkin(heroId))
			{
				return true;
			}
			int id = GameUtils.TranslateCardIdToDbId(heroId, false);
			__result = HeroDefinition(LocalCollectionAppearance.GetHeroFavorites(GameUtils.GetTagClassFromCardDbId(id)).FirstOrDefault((CollectionHeroAppearance item) => item.CardId == id));
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionManager), "GetFavoriteHeroesForClass")]
		public static void PatchGetFavoriteHeroesForClass(TAG_CLASS heroClass, ref List<NetCache.CardDefinition> __result)
		{
			if (ShouldOverrideOwned())
			{
				__result = LocalCollectionAppearance.GetHeroFavorites(heroClass).Select(HeroDefinition).ToList();
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CollectionManager), "GetRandomFavoriteHero")]
		public static bool PatchGetRandomFavoriteHero(TAG_CLASS heroClass, ref NetCache.CardDefinition __result, int? heroIdToExclude = null)
		{
			if (!IsEnabled() || skipOwnedOverride)
			{
				return true;
			}
			NetCache.CardDefinition val = HeroDefinition(LocalCollectionAppearance.PickHero(heroClass, heroIdToExclude));
			if (val == null)
			{
				return true;
			}
			__result = val;
			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CollectionDeck), "GetDisplayHeroCardID")]
		public static bool PatchGetDisplayHeroCardID(CollectionDeck __instance, bool rerollFavoriteHero, ref string __result, ref string ___m_randomHeroCardId, ref string ___m_currentDisplayHeroCardId)
		{
			if (!IsEnabled() || __instance == null || __instance.HasUIHeroOverride() || __instance.IsDuelsDeck)
			{
				return true;
			}
			TAG_CLASS tagClassFromCardDbId = GameUtils.GetTagClassFromCardDbId(GameUtils.TranslateCardIdToDbId(__instance.HeroCardID, false));
			CollectionManager obj = CollectionManager.Get();
			CollectionDeckAppearance collectionDeckAppearance = ((((obj != null) ? obj.GetEditedDeck() : null) == __instance) ? LocalCollectionAppearance.ReadDeck(__instance) : (LocalCollectionAppearance.GetSavedDeck(__instance.ID) ?? LocalCollectionAppearance.ReadDeck(__instance)));
			if (collectionDeckAppearance.Hero != null)
			{
				__result = GameUtils.TranslateDbIdToCardId(collectionDeckAppearance.Hero.CardId, false);
			}
			else
			{
				if (!collectionDeckAppearance.FavoriteHeroesOnly)
				{
					return true;
				}
				List<CollectionHeroAppearance> heroFavorites = LocalCollectionAppearance.GetHeroFavorites(tagClassFromCardDbId);
				int previous = GameUtils.TranslateCardIdToDbId(___m_randomHeroCardId, false);
				if (rerollFavoriteHero || !heroFavorites.Any((CollectionHeroAppearance item) => item.CardId == previous))
				{
					CollectionHeroAppearance collectionHeroAppearance = LocalCollectionAppearance.PickHero(tagClassFromCardDbId, previous);
					if (collectionHeroAppearance == null)
					{
						return true;
					}
					___m_randomHeroCardId = GameUtils.TranslateDbIdToCardId(collectionHeroAppearance.CardId, false);
				}
				__result = ___m_randomHeroCardId;
			}
			___m_currentDisplayHeroCardId = __result;
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionDeck), "GetDisplayHeroPremiumOverride")]
		public static void PatchGetDisplayHeroPremiumOverride(CollectionDeck __instance, string ___m_currentDisplayHeroCardId, ref TAG_PREMIUM? __result)
		{
			if (!IsEnabled() || __instance == null || __instance.HasUIHeroOverride() || __instance.IsDuelsDeck)
			{
				return;
			}
			int id = GameUtils.TranslateCardIdToDbId(___m_currentDisplayHeroCardId, false);
			CollectionManager obj = CollectionManager.Get();
			CollectionHeroAppearance collectionHeroAppearance = ((((obj != null) ? obj.GetEditedDeck() : null) == __instance) ? LocalCollectionAppearance.ReadDeck(__instance) : LocalCollectionAppearance.GetSavedDeck(__instance.ID))?.Hero;
			if (collectionHeroAppearance == null || collectionHeroAppearance.CardId != id)
			{
				collectionHeroAppearance = LocalCollectionAppearance.GetHeroFavorites(GameUtils.GetTagClassFromCardDbId(GameUtils.TranslateCardIdToDbId(__instance.HeroCardID, false))).FirstOrDefault((CollectionHeroAppearance item) => item.CardId == id);
			}
			if (collectionHeroAppearance != null)
			{
				__result = (TAG_PREMIUM)collectionHeroAppearance.Premium;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionPageManager), "OnFavoriteHeroChanged")]
		public static void PatchCollectionPageManagerOnFavoriteHeroChanged(CollectionPageManager __instance)
		{
			if (!IsEnabled() || __instance == null)
			{
				return;
			}
			CollectionManager obj = CollectionManager.Get();
			CollectibleDisplay val = ((obj != null) ? obj.GetCollectibleDisplay() : null);
			if (val == null || (int)val.GetViewMode() != 8)
			{
				return;
			}
			object value = Traverse.Create((object)__instance).Method("GetCurrentPage", Array.Empty<object>()).GetValue();
			object obj2 = ((value is CollectionPageDisplay) ? value : null);
			if (obj2 == null)
			{
				return;
			}
			GameObject heroPicker = ((CollectionPageDisplay)obj2).m_heroPicker;
			if (heroPicker != null)
			{
				CollectionHeroPickerButtons componentInChildren = heroPicker.GetComponentInChildren<CollectionHeroPickerButtons>();
				if (componentInChildren != null)
				{
					componentInChildren.LoadHeroButtonsForFavoriteHeroes();
				}
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Network), "SetFavoriteHero")]
		public static bool PatchSetFavoriteHero(TAG_CLASS heroClass, NetCache.CardDefinition hero, bool isFavorite)
		{
			if (!IsEnabled() || hero == null || string.IsNullOrEmpty(hero.Name))
			{
				return true;
			}
			try
			{
				bool flag = LocalCollectionAppearance.SetHeroFavorite(heroClass, hero, isFavorite);
				NetCache.CardDefinition val = HeroDefinition(LocalCollectionAppearance.GetHeroFavorites(heroClass).FirstOrDefault((CollectionHeroAppearance item) => item.CardId == GameUtils.TranslateCardIdToDbId(hero.Name, false))) ?? hero;
				CollectionManager obj = CollectionManager.Get();
				if (obj != null)
				{
					obj.UpdateFavoriteHero(heroClass, hero.Name, val.Premium, flag);
				}
				CollectionManager.Get()?.OnCollectionChanged();
			}
			catch (Exception message)
			{
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, message);
			}
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionCardVisual), "IsInCollection")]
		public static void PatchCollectionCardVisualIsInCollection(CollectionCardVisual __instance, ref bool __result)
		{
			if (!ShouldOverrideOwned())
			{
				return;
			}
			CollectionManager obj = CollectionManager.Get();
			CollectibleDisplay val = ((obj != null) ? obj.GetCollectibleDisplay() : null);
			if (!(val == null))
			{
				CollectionUtils.ViewMode viewMode = val.GetViewMode();
				if ((int)viewMode == 1 || (int)viewMode == 2 || (int)viewMode == 5)
				{
					__result = true;
				}
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(DeckTrayHeroSkinContent), "UpdateHeroSkin")]
		public static void PatchDeckTrayUpdateHeroSkin(string cardId, TAG_PREMIUM premium, bool assigning, CollectionDeck ___m_currentDeck)
		{
			if (IsEnabled() && assigning && !string.IsNullOrEmpty(cardId))
			{
				LocalCollectionAppearance.SetDeckHeroPremium(___m_currentDeck, GameUtils.TranslateCardIdToDbId(cardId, false), premium);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(DeckTrayHeroSkinContent), "UpdateHeroSkin")]
		public static void PatchDeckTrayHeroPremium(string cardId, ref TAG_PREMIUM premium, bool assigning, CollectionDeck ___m_currentDeck)
		{
			if (!(!IsEnabled() | assigning) && ___m_currentDeck != null)
			{
				CollectionHeroAppearance hero = LocalCollectionAppearance.ReadDeck(___m_currentDeck).Hero;
				if (hero != null && hero.CardId == GameUtils.TranslateCardIdToDbId(cardId, false))
				{
					premium = (TAG_PREMIUM)hero.Premium;
				}
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CollectionDeck), "SendChanges")]
		public static void PatchSaveDeckAppearance(CollectionDeck __instance)
		{
			if (!IsEnabled() || __instance == null)
			{
				return;
			}
			CollectionManager obj = CollectionManager.Get();
			if (((obj != null) ? obj.GetEditedDeck() : null) != __instance)
			{
				return;
			}
			try
			{
				LocalCollectionAppearance.SaveDeck(__instance);
			}
			catch (Exception message)
			{
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, message);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(DeckTrayPetContent), "UpdatePet")]
		public static void PatchDeckPetPreviewActor(int petId, ref Actor baseActor)
		{
			if (IsEnabled() && !(baseActor == null) && LocalCollectionAppearance.GetPetPreviewVariant(0, petId) > 0)
			{
				baseActor = null;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(CollectionManager), "SetEditedDeck")]
		public static void PatchSetEditedDeck(CollectionDeck deck)
		{
			CollectionManager obj = CollectionManager.Get();
			if (((obj != null) ? obj.GetEditedDeck() : null) != deck)
			{
				LocalCollectionAppearance.ApplyEditedDeck(deck);
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CollectionManager), "UpdateDeckWithNewId")]
		public static void PatchUpdateDeckWithNewId(long oldId, long newId, bool __result)
		{
			if (!IsEnabled() || !__result)
			{
				return;
			}
			try
			{
				LocalCollectionAppearance.MoveDeck(oldId, newId);
				CollectionManager obj = CollectionManager.Get();
				CollectionDeck val = ((obj != null) ? obj.GetEditedDeck() : null);
				if (val != null && val.ID == newId)
				{
					LocalCollectionAppearance.ApplyEditedDeck(val);
					CollectionManager.Get()?.OnCollectionChanged();
				}
			}
			catch (Exception message)
			{
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, message);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Network), "SendDeckData")]
		public static void PatchSendDeckData(ref int newHeroAssetID, ref bool? newHeroOverridenStatus, ref int uiHeroOverrideAssetID, ref int? newCosmeticCoinID, ref int? newCardBackID, ref int? newPetID, ref int? newPetVariantID)
		{
			if (newHeroAssetID > 0 && !ReallyOwnsHeroCard(newHeroAssetID))
			{
				newHeroAssetID = -1;
				newHeroOverridenStatus = null;
			}
			if (uiHeroOverrideAssetID > 0 && !ReallyOwnsHeroCard(uiHeroOverrideAssetID))
			{
				uiHeroOverrideAssetID = -1;
			}
			if (newCardBackID.HasValue && newCardBackID.Value >= 0 && !ReallyOwnsCardBack(newCardBackID.Value))
			{
				newCardBackID = -1;
			}
			if (newCosmeticCoinID.HasValue && newCosmeticCoinID.Value >= 0 && !ReallyOwnsCoin(newCosmeticCoinID.Value))
			{
				newCosmeticCoinID = -1;
			}
			NetCache obj = NetCache.Get();
			NetCache.NetCachePets val = ((obj != null) ? obj.GetNetObject<NetCache.NetCachePets>() : null);
			if (newPetID.GetValueOrDefault() > 0 && (val == null || !val.Pets.ContainsKey(newPetID.Value)))
			{
				newPetID = -1;
			}
			if (newPetVariantID.GetValueOrDefault() > 0 && !LocalCollectionAppearance.ReallyOwnsPetVariant(newPetVariantID.Value))
			{
				newPetVariantID = -1;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Network), "FindGame")]
		public static void PatchFindGame(GameType gameType, long deckId, ref int heroCardDbId)
		{
			try
			{
				LocalCollectionAppearance.BeginMatch(gameType, deckId);
			}
			catch (Exception message)
			{
				LocalCollectionAppearance.StartMatch(null);
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, message);
			}
			if (deckId > 0 && heroCardDbId > 0 && !GameUtils.IsBattlegroundsGameType(gameType) && !ReallyOwnsHeroCard(heroCardDbId))
			{
				heroCardDbId = 0;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CosmeticCoinManager), "FindCoinToUse")]
		public static void PatchFindCoinToUse(ref int cosmeticCoinToUse)
		{
			if (cosmeticCoinToUse <= 0 || ReallyOwnsCoin(cosmeticCoinToUse))
			{
				return;
			}
			bool flag = skipOwnedOverride;
			skipOwnedOverride = true;
			try
			{
				cosmeticCoinToUse = CosmeticCoinManager.Get().GetRandomCoinIdOwnedByPlayer(false);
			}
			finally
			{
				skipOwnedOverride = flag;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(CardBackManager), "FindCardBackToUse")]
		public static void PatchFindCardBackToUse(ref int cardBackToUse)
		{
			if (cardBackToUse < 0 || ReallyOwnsCardBack(cardBackToUse))
			{
				return;
			}
			bool flag = skipOwnedOverride;
			skipOwnedOverride = true;
			try
			{
				cardBackToUse = CardBackManager.Get().GetRandomCardBackIdOwnedByPlayer(false);
			}
			finally
			{
				skipOwnedOverride = flag;
			}
		}
	}
    }
}
