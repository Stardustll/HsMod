using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using PegasusShared;

namespace HsMod
{


	internal static class LocalCollectionAppearance
	{
		private sealed class HeroQuality
		{
			internal int CardId;

			internal int Premium;
		}

		internal sealed class MatchAppearance
		{
			internal long DeckId;

			internal CollectionHeroAppearance Hero;

			internal int? CardBackId;

			internal int? CoinId;

			internal int? PetVariantId;
		}

		private static readonly Random Random = new Random();

		private static string loadedPath;

		private static CollectionAppearanceStore store;

		private static readonly ConditionalWeakTable<CollectionDeck, HeroQuality> DeckQualities = new ConditionalWeakTable<CollectionDeck, HeroQuality>();

		private static MatchAppearance match;

		private static object game;

		private static int gameHandle;

		private static bool pendingMatch;

		private static CollectionAppearanceStore Store
		{
			get
			{
				string text = Path.Combine(Paths.ConfigPath, "HsCollectionAppearance.json");
				if (loadedPath == text)
				{
					return store;
				}
				loadedPath = text;
				store = null;
				try
				{
					store = new CollectionAppearanceStore(text);
				}
				catch (Exception ex)
				{
					Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "读取收藏外观配置失败: " + ex.Message);
				}
				return store;
			}
		}

		internal static void Reload()
		{
			loadedPath = null;
			store = null;
		}

		internal static bool IsConstructedHero(int cardId)
		{
			CardHeroDbfRecord cardHeroRecordForCardId = GameUtils.GetCardHeroRecordForCardId(cardId);
			if (cardHeroRecordForCardId != null && (int)cardHeroRecordForCardId.HeroType != 3 && (int)cardHeroRecordForCardId.HeroType != 4)
			{
				return GameUtils.GetCardTagValue(cardId, (GAME_TAG)202) == 3;
			}
			return false;
		}

		private static bool IsHeroForClass(int cardId, TAG_CLASS heroClass)
		{
			if (IsConstructedHero(cardId))
			{
				return GameUtils.GetTagClassFromCardDbId(cardId) == heroClass;
			}
			return false;
		}

		private static int BaseHero(TAG_CLASS heroClass)
		{
			return GameUtils.TranslateCardIdToDbId(CollectionManager.GetVanillaHero(heroClass), false);
		}

		private static CollectionHeroAppearance Hero(int cardId, TAG_PREMIUM? premium = null)
		{
			CardHeroDbfRecord cardHeroRecordForCardId = GameUtils.GetCardHeroRecordForCardId(cardId);
			return new CollectionHeroAppearance
			{
				CardId = cardId,
				Premium = (((int?)premium) ?? ((cardHeroRecordForCardId != null && (int)cardHeroRecordForCardId.HeroType == 1) ? 1 : 0))
			};
		}

		internal static List<CollectionHeroAppearance> GetHeroFavorites(TAG_CLASS heroClass)
		{
			int num = BaseHero(heroClass);
			List<CollectionHeroAppearance> list = new List<CollectionHeroAppearance>();
			List<int> value2;
			int value3;
			if (Store != null && Store.Data.Heroes.TryGetValue(num, out var value) && value != null)
			{
				list.AddRange(from item in value
					where item != null
					select item.Copy());
			}
			else if (PluginConfig.HeroSkinPreferences.TryGetValue(num, out value2))
			{
				list.AddRange(value2.Select((int id) => Hero(id)));
			}
			else if (PluginConfig.HeroesMapping.TryGetValue(num, out value3))
			{
				list.Add(Hero(value3));
			}
			else
			{
				NetCache obj = NetCache.Get();
				NetCache.NetCacheFavoriteHeroes val = ((obj != null) ? obj.GetNetObject<NetCache.NetCacheFavoriteHeroes>() : null);
				if (val != null && val.FavoriteHeroes != null)
				{
					foreach (var favoriteHero in val.FavoriteHeroes)
					{
						if (favoriteHero.Item1 == heroClass && favoriteHero.Item2 != null)
						{
							int num2 = GameUtils.TranslateCardIdToDbId(favoriteHero.Item2.Name, false);
							list.Add(Hero(num2, (TAG_PREMIUM)((num2 == num) ? 1 : ((int)favoriteHero.Item2.Premium))));
						}
					}
				}
			}
			bool flag = list.Count > 0;
			list = (from item in list.Where(delegate(CollectionHeroAppearance item)
				{
					return IsHeroForClass(item.CardId, heroClass);
				})
				group item by item.CardId into @group
				select @group.First()).ToList();
			if (list.Count == 0 && !flag && num > 0)
			{
				list.Add(Hero(num));
			}
			return list;
		}

		internal static List<int> GetCardBackFavorites()
		{
			IEnumerable<int> enumerable = Store?.Data.CardBacks;
			if (enumerable == null && PluginConfig.skinCardBack != null && PluginConfig.skinCardBack.Value >= 0)
			{
				enumerable = new int[1] { PluginConfig.skinCardBack.Value };
			}
			if (enumerable == null)
			{
				NetCache obj = NetCache.Get();
				object obj2;
				if (obj == null)
				{
					obj2 = null;
				}
				else
				{
					NetCache.NetCacheCardBacks netObject = obj.GetNetObject<NetCache.NetCacheCardBacks>();
					obj2 = ((netObject != null) ? netObject.FavoriteCardBacks : null);
				}
				enumerable = (IEnumerable<int>)obj2;
			}
			List<int> list = CollectionAppearanceStore.DistinctIds(enumerable, 0);
			if (list.Count == 0)
			{
				list.Add(0);
			}
			return list;
		}

		internal static int CoinIdFromCard(int cardId)
		{
			if (GameDbf.CosmeticCoin == null)
			{
				return -1;
			}
			CosmeticCoinDbfRecord val = GameDbf.CosmeticCoin.GetRecords().FirstOrDefault((CosmeticCoinDbfRecord item) => item != null && item.CardId == cardId);
			if (val != null)
			{
				return ((DbfRecord)val).ID;
			}
			return -1;
		}

		internal static List<int> GetCoinFavorites()
		{
			IEnumerable<int> enumerable = Store?.Data.Coins;
			int num = ((PluginConfig.skinCoin == null) ? (-1) : CoinIdFromCard(PluginConfig.skinCoin.Value));
			if (enumerable == null && num > 0)
			{
				enumerable = new int[1] { num };
			}
			if (enumerable == null)
			{
				NetCache obj = NetCache.Get();
				object obj2;
				if (obj == null)
				{
					obj2 = null;
				}
				else
				{
					NetCache.NetCacheCoins netObject = obj.GetNetObject<NetCache.NetCacheCoins>();
					obj2 = ((netObject != null) ? netObject.FavoriteCoins : null);
				}
				enumerable = (IEnumerable<int>)obj2;
			}
			List<int> list = CollectionAppearanceStore.DistinctIds(enumerable, 1);
			if (list.Count == 0)
			{
				list.Add(1);
			}
			return list;
		}

		internal static bool SetHeroFavorite(TAG_CLASS heroClass, NetCache.CardDefinition hero, bool favorite)
		{
			int id = GameUtils.TranslateCardIdToDbId(hero.Name, false);
			if (!IsHeroForClass(id, heroClass))
			{
				return false;
			}
			List<CollectionHeroAppearance> heroFavorites = GetHeroFavorites(heroClass);
			bool num = CollectionAppearanceStore.SetFavorite(heroFavorites, Hero(id, hero.Premium), favorite);
			CollectionAppearanceStore collectionAppearanceStore = Store;
			if (collectionAppearanceStore == null)
			{
				throw new InvalidOperationException("收藏外观配置未能载入，无法保存偏好");
			}
			if (num || !collectionAppearanceStore.Data.Heroes.ContainsKey(BaseHero(heroClass)) || (PluginConfig.skinHero != null && PluginConfig.skinHero.Value != -1))
			{
				collectionAppearanceStore.Data.Heroes[BaseHero(heroClass)] = heroFavorites;
				collectionAppearanceStore.Save();
				if (PluginConfig.skinHero != null)
				{
					PluginConfig.skinHero.Value = -1;
					((ConfigEntryBase)PluginConfig.skinHero).ConfigFile.Save();
				}
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "英雄偏好已更新: " + heroClass.ToString() + "，共 " + heroFavorites.Count + " 个");
			}
			return heroFavorites.Any((CollectionHeroAppearance item) => item.CardId == id);
		}

		internal static bool SetCardBackFavorite(int id, bool favorite)
		{
			CardBackDbfRecord val = GameDbf.CardBack?.GetRecord(id);
			if (val == null || !val.Enabled || val.IsRandomCardBack)
			{
				return false;
			}
			List<int> cardBackFavorites = GetCardBackFavorites();
			bool num = CollectionAppearanceStore.SetFavorite(cardBackFavorites, id, favorite);
			CollectionAppearanceStore collectionAppearanceStore = Store;
			if (collectionAppearanceStore == null)
			{
				throw new InvalidOperationException("收藏外观配置未能载入，无法保存偏好");
			}
			if (num || collectionAppearanceStore.Data.CardBacks == null || (PluginConfig.skinCardBack != null && PluginConfig.skinCardBack.Value != -1))
			{
				collectionAppearanceStore.Data.CardBacks = cardBackFavorites;
				collectionAppearanceStore.Save();
				if (PluginConfig.skinCardBack != null)
				{
					PluginConfig.skinCardBack.Value = -1;
					((ConfigEntryBase)PluginConfig.skinCardBack).ConfigFile.Save();
				}
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "卡背偏好已更新，共 " + cardBackFavorites.Count + " 个");
			}
			return cardBackFavorites.Contains(id);
		}

		internal static bool SetCoinFavorite(int id, bool favorite)
		{
			CosmeticCoinDbfRecord val = GameDbf.CosmeticCoin?.GetRecord(id);
			if (val == null || !val.Enabled)
			{
				return false;
			}
			List<int> coinFavorites = GetCoinFavorites();
			bool num = CollectionAppearanceStore.SetFavorite(coinFavorites, id, favorite);
			CollectionAppearanceStore collectionAppearanceStore = Store;
			if (collectionAppearanceStore == null)
			{
				throw new InvalidOperationException("收藏外观配置未能载入，无法保存偏好");
			}
			if (num || collectionAppearanceStore.Data.Coins == null || (PluginConfig.skinCoin != null && PluginConfig.skinCoin.Value != -1))
			{
				collectionAppearanceStore.Data.Coins = coinFavorites;
				collectionAppearanceStore.Save();
				if (PluginConfig.skinCoin != null)
				{
					PluginConfig.skinCoin.Value = -1;
					((ConfigEntryBase)PluginConfig.skinCoin).ConfigFile.Save();
				}
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "幸运币偏好已更新，共 " + coinFavorites.Count + " 个");
			}
			return coinFavorites.Contains(id);
		}

		internal static CollectionHeroAppearance PickHero(TAG_CLASS heroClass, int? exclude = null)
		{
			List<CollectionHeroAppearance> heroFavorites = GetHeroFavorites(heroClass);
			if (exclude.HasValue && heroFavorites.Count > 1)
			{
				heroFavorites.RemoveAll((CollectionHeroAppearance item) => item.CardId == exclude.Value);
			}
			return CollectionAppearanceStore.Choose(heroFavorites, Random)?.Copy();
		}

		internal static CollectionDeckAppearance GetSavedDeck(long deckId)
		{
			if (Store == null || !Store.Data.Decks.TryGetValue(deckId, out var value))
			{
				return null;
			}
			return value;
		}

		internal static void SetDeckHeroPremium(CollectionDeck deck, int cardId, TAG_PREMIUM premium)
		{
			if (deck != null)
			{
				HeroQuality value = DeckQualities.GetValue(deck, (CollectionDeck key) => new HeroQuality());
				value.CardId = cardId;
				value.Premium = (int)premium;
			}
		}

		internal static CollectionDeckAppearance ReadDeck(CollectionDeck deck)
		{
			if (deck == null)
			{
				return new CollectionDeckAppearance();
			}
			int num = GameUtils.TranslateCardIdToDbId(deck.HeroCardID, false);
			CollectionHeroAppearance collectionHeroAppearance = ((deck.HeroOverridden && IsConstructedHero(num)) ? Hero(num) : null);
			if (collectionHeroAppearance != null && DeckQualities.TryGetValue(deck, out var value) && value.CardId == num)
			{
				collectionHeroAppearance.Premium = value.Premium;
			}
			return new CollectionDeckAppearance
			{
				Hero = collectionHeroAppearance,
				CardBackId = deck.CardBackID,
				CoinId = deck.CosmeticCoinID,
				Pet = new CollectionPetAppearance
				{
					PetId = deck.PetID,
					VariantId = deck.PetVariantID,
					FavoritesOnly = deck.RandomPetUseFavorite
				},
				FavoriteHeroesOnly = deck.RandomHeroUseFavorite,
				FavoriteCoinsOnly = deck.RandomCoinUseFavorite
			};
		}

		internal static void SaveDeck(CollectionDeck deck)
		{
			if (PluginConfig.CollectionVisualsEnabled && deck != null && Store != null)
			{
				Store.Data.Decks[deck.ID] = ReadDeck(deck);
				Store.Save();
			}
		}

		internal static void ApplyEditedDeck(CollectionDeck deck)
		{
			if (!PluginConfig.CollectionVisualsEnabled || deck == null)
			{
				return;
			}
			CollectionDeckAppearance savedDeck = GetSavedDeck(deck.ID);
			if (savedDeck != null)
			{
				TAG_CLASS tagClassFromCardDbId = GameUtils.GetTagClassFromCardDbId(GameUtils.TranslateCardIdToDbId(deck.HeroCardID, false));
				deck.HeroOverridden = savedDeck.Hero != null && IsHeroForClass(savedDeck.Hero.CardId, tagClassFromCardDbId);
				if (deck.HeroOverridden)
				{
					deck.HeroCardID = GameUtils.TranslateDbIdToCardId(savedDeck.Hero.CardId, false);
					SetDeckHeroPremium(deck, savedDeck.Hero.CardId, (TAG_PREMIUM)savedDeck.Hero.Premium);
				}
				deck.CardBackID = savedDeck.CardBackId;
				deck.CosmeticCoinID = savedDeck.CoinId;
				if (savedDeck.Pet != null)
				{
					deck.PetID = savedDeck.Pet.PetId;
					deck.PetVariantID = savedDeck.Pet.VariantId;
					deck.RandomPetUseFavorite = savedDeck.Pet.FavoritesOnly;
				}
				deck.RandomHeroUseFavorite = savedDeck.FavoriteHeroesOnly;
				deck.RandomCoinUseFavorite = savedDeck.FavoriteCoinsOnly;
			}
		}

		internal static void MoveDeck(long oldId, long newId)
		{
			CollectionDeckAppearance savedDeck = GetSavedDeck(oldId);
			if (savedDeck != null && oldId != newId && newId > 0)
			{
				Store.Data.Decks.Remove(oldId);
				Store.Data.Decks[newId] = savedDeck;
				Store.Save();
			}
		}

		private static bool ValidCardBack(int id)
		{
			CardBackDbfRecord val = GameDbf.CardBack?.GetRecord(id);
			if (val != null && val.Enabled)
			{
				return !val.IsRandomCardBack;
			}
			return false;
		}

		private static bool ValidCoin(int id)
		{
			CosmeticCoinDbfRecord val = GameDbf.CosmeticCoin?.GetRecord(id);
			if (val != null)
			{
				return val.Enabled;
			}
			return false;
		}

		internal static MatchAppearance SelectMatch(CollectionDeck deck, long deckId)
		{
			CollectionDeckAppearance collectionDeckAppearance = ((deckId > 0) ? GetSavedDeck(deckId) : null) ?? ReadDeck(deck);
			MatchAppearance matchAppearance = new MatchAppearance
			{
				DeckId = deckId
			};
			if (deck == null && collectionDeckAppearance.Hero != null && IsConstructedHero(collectionDeckAppearance.Hero.CardId))
			{
				matchAppearance.Hero = collectionDeckAppearance.Hero.Copy();
			}
			if (deck != null)
			{
				TAG_CLASS heroClass = GameUtils.GetTagClassFromCardDbId(GameUtils.TranslateCardIdToDbId(deck.HeroCardID, false));
				if (collectionDeckAppearance.Hero != null && IsHeroForClass(collectionDeckAppearance.Hero.CardId, heroClass))
				{
					matchAppearance.Hero = collectionDeckAppearance.Hero.Copy();
				}
				else if (PluginConfig.skinHero != null && IsHeroForClass(PluginConfig.skinHero.Value, heroClass))
				{
					matchAppearance.Hero = Hero(PluginConfig.skinHero.Value);
				}
				else if (collectionDeckAppearance.FavoriteHeroesOnly)
				{
					matchAppearance.Hero = PickHero(heroClass);
				}
				else
				{
					List<CollectionHeroAppearance> candidates = (from id in (from item in GameDbf.CardHero.GetRecords().Where(delegate(CardHeroDbfRecord item)
							{
								return item != null && IsHeroForClass(item.CardId, heroClass);
							})
							select item.CardId).Distinct()
						select Hero(id)).ToList();
					matchAppearance.Hero = CollectionAppearanceStore.Choose(candidates, Random)?.Copy();
				}
			}
			int num = collectionDeckAppearance.CardBackId ?? ((PluginConfig.skinCardBack == null) ? (-1) : PluginConfig.skinCardBack.Value);
			if (num >= 0 && ValidCardBack(num))
			{
				matchAppearance.CardBackId = num;
				goto IL_02ef;
			}
			List<int> list;
			if (collectionDeckAppearance.CardBackId.HasValue)
			{
				Dbf<CardBackDbfRecord> cardBack = GameDbf.CardBack;
				if (cardBack != null)
				{
					CardBackDbfRecord record = cardBack.GetRecord(collectionDeckAppearance.CardBackId.Value);
					if (((record != null) ? new bool?(record.IsRandomCardBack) : ((bool?)null)) == true)
					{
						list = (from item in GameDbf.CardBack.GetRecords()
							where item != null
							select ((DbfRecord)item).ID).ToList();
						goto IL_02ad;
					}
				}
			}
			list = GetCardBackFavorites();
			goto IL_02ad;
			IL_02ad:
			List<int> source = list;
			source = source.Where(ValidCardBack).Distinct().ToList();
			if (source.Count > 0)
			{
				matchAppearance.CardBackId = CollectionAppearanceStore.Choose(source, Random);
			}
			goto IL_02ef;
			IL_02ef:
			int num2 = collectionDeckAppearance.CoinId ?? ((PluginConfig.skinCoin == null) ? (-1) : CoinIdFromCard(PluginConfig.skinCoin.Value));
			if (num2 > 0 && ValidCoin(num2))
			{
				matchAppearance.CoinId = num2;
			}
			else
			{
				List<int> source2 = ((!collectionDeckAppearance.FavoriteCoinsOnly && GameDbf.CosmeticCoin != null) ? (from item in GameDbf.CosmeticCoin.GetRecords()
					where item != null
					select ((DbfRecord)item).ID).ToList() : GetCoinFavorites());
				source2 = source2.Where(ValidCoin).Distinct().ToList();
				if (source2.Count > 0)
				{
					matchAppearance.CoinId = CollectionAppearanceStore.Choose(source2, Random);
				}
			}
			matchAppearance.PetVariantId = SelectPetVariant(collectionDeckAppearance.Pet ?? ReadDeck(deck).Pet, (PluginConfig.skinPet == null) ? (-1) : PluginConfig.skinPet.Value);
			return matchAppearance;
		}

		private static bool ValidPetVariant(int variantId)
		{
			PetVariantDbfRecord val = GameDbf.PetVariant?.GetRecord(variantId);
			if (val != null && val.Enabled)
			{
				Dbf<PetDbfRecord> pet = GameDbf.Pet;
				if (pet == null)
				{
					return false;
				}
				PetDbfRecord record = pet.GetRecord(val.PetId);
				return ((record != null) ? new bool?(record.Enabled) : ((bool?)null)) == true;
			}
			return false;
		}

		private static int? PreferredPetVariant(int petId, int globalVariant)
		{
			if (PluginConfig.PetSkinMapping != null && PluginConfig.PetSkinMapping.TryGetValue(petId, out var value) && ValidPetVariant(value) && GameDbf.PetVariant.GetRecord(value).PetId == petId)
			{
				return value;
			}
			if (!ValidPetVariant(globalVariant) || GameDbf.PetVariant.GetRecord(globalVariant).PetId != petId)
			{
				return null;
			}
			return globalVariant;
		}

		internal static int GetPetPreviewVariant(int nativeVariant, int petId)
		{
			if (!PluginConfig.CollectionVisualsEnabled || petId <= 0)
			{
				return nativeVariant;
			}
			return PreferredPetVariant(petId, (PluginConfig.skinPet == null) ? (-1) : PluginConfig.skinPet.Value) ?? nativeVariant;
		}

		internal static int? SelectPetVariant(CollectionPetAppearance binding, int globalVariant)
		{
			if (binding != null && binding.VariantId.GetValueOrDefault() > 0)
			{
				int value = binding.VariantId.Value;
				if (!ValidPetVariant(value) || (binding.PetId.HasValue && binding.PetId.Value > 0 && GameDbf.PetVariant.GetRecord(value).PetId != binding.PetId.Value))
				{
					return null;
				}
				return value;
			}
			if (binding != null && binding.PetId.GetValueOrDefault() > 0)
			{
				int petId = binding.PetId.Value;
				List<int> list = ((GameDbf.PetVariant == null) ? new List<int>() : (from id in (from item in GameDbf.PetVariant.GetRecords()
						where item != null && item.PetId == petId && ValidPetVariant(((DbfRecord)item).ID)
						select ((DbfRecord)item).ID).Distinct()
					orderby id
					select id).ToList());
				if (list.Count == 0)
				{
					return null;
				}
				if (!binding.FavoritesOnly)
				{
					return CollectionAppearanceStore.Choose(list, Random);
				}
				return PreferredPetVariant(petId, globalVariant) ?? list[0];
			}
			if (!ValidPetVariant(globalVariant))
			{
				return null;
			}
			return globalVariant;
		}

		internal static int? GetRequestPetVariant(GameType gameType, long deckId)
		{
			bool flag = GameUtils.IsBattlegroundsGameType(gameType);
			if (!PluginConfig.LocalPetVisualEnabled(flag))
			{
				return null;
			}
			if (flag)
			{
				return SelectPetVariant(null, (PluginConfig.skinPet == null) ? (-1) : PluginConfig.skinPet.Value);
			}
			if (!pendingMatch || match == null || match.DeckId != deckId)
			{
				CollectionManager obj = CollectionManager.Get();
				StartMatch(SelectMatch((obj != null) ? obj.GetDeck(deckId) : null, deckId));
			}
			return match.PetVariantId;
		}

		internal static int? GetGamePetVariant()
		{
			GameMgr val = GameMgr.Get();
			bool flag = val != null && val.IsBattlegrounds();
			if (!PluginConfig.LocalPetVisualEnabled(flag) || (val != null && val.IsSpectator()))
			{
				return null;
			}
			if (flag)
			{
				return SelectPetVariant(null, (PluginConfig.skinPet == null) ? (-1) : PluginConfig.skinPet.Value);
			}
			if (pendingMatch && match != null)
			{
				return match.PetVariantId;
			}
			MatchAppearance matchAppearance = CurrentMatch();
			if (matchAppearance != null)
			{
				return matchAppearance.PetVariantId;
			}
			long valueOrDefault = ((val != null) ? val.LastDeckId : ((long?)null)).GetValueOrDefault();
			object obj;
			if (valueOrDefault <= 0)
			{
				obj = null;
			}
			else
			{
				CollectionManager obj2 = CollectionManager.Get();
				obj = ((obj2 != null) ? obj2.GetDeck(valueOrDefault) : null);
			}
			CollectionDeck deck = (CollectionDeck)obj;
			return SelectPetVariant(GetSavedDeck(valueOrDefault)?.Pet ?? ReadDeck(deck).Pet, (PluginConfig.skinPet == null) ? (-1) : PluginConfig.skinPet.Value);
		}

		internal static bool ReallyOwnsPetVariant(int variantId)
		{
			PetVariantDbfRecord val = GameDbf.PetVariant?.GetRecord(variantId);
			NetCache obj = NetCache.Get();
			NetCache.NetCachePets val2 = ((obj != null) ? obj.GetNetObject<NetCache.NetCachePets>() : null);
			if (val != null && val2 != null && val2.Pets.ContainsKey(val.PetId))
			{
				return val2.PetVariants.ContainsKey(variantId);
			}
			return false;
		}

		internal static string GetSelectedHeroForScene(Entity hero)
		{
			if (!PluginConfig.CollectionVisualsEnabled)
			{
				return null;
			}
			string currentSceneHero = (string)null;
			if (!string.IsNullOrEmpty(currentSceneHero))
			{
				return currentSceneHero;
			}
			MatchAppearance matchAppearance = CurrentMatch();
			if (matchAppearance?.Hero != null && (hero == null || string.IsNullOrEmpty(((EntityBase)hero).GetCardId()) || IsConstructedHero(GameUtils.TranslateCardIdToDbId(((EntityBase)hero).GetCardId(), false))))
			{
				return GameUtils.TranslateDbIdToCardId(matchAppearance.Hero.CardId, false);
			}
			if (hero == null || !IsConstructedHero(GameUtils.TranslateCardIdToDbId(((EntityBase)hero).GetCardId(), false)))
			{
				return null;
			}
			return ((EntityBase)hero).GetCardId();
		}

		internal static void BeginMatch(GameType gameType, long deckId)
		{
			Utils.CacheRawHeroCardId = null;
			if (!PluginConfig.CollectionVisualsEnabled || GameUtils.IsBattlegroundsGameType(gameType) || deckId <= 0)
			{
				StartMatch(null);
				return;
			}
			CollectionManager obj = CollectionManager.Get();
			StartMatch(SelectMatch((obj != null) ? obj.GetDeck(deckId) : null, deckId));
		}

		internal static void StartMatch(MatchAppearance selected)
		{
			match = selected;
			pendingMatch = selected != null;
			if (selected == null)
			{
				game = null;
				gameHandle = 0;
			}
		}

		internal static MatchAppearance UseMatch(object current, int handle, Func<MatchAppearance> select)
		{
			if (pendingMatch && game == current && handle == gameHandle)
			{
				return match;
			}
			if (match == null || (!pendingMatch && game != current && (handle == 0 || handle != gameHandle)))
			{
				match = select();
			}
			pendingMatch = false;
			game = current;
			gameHandle = handle;
			return match;
		}

		private static MatchAppearance CurrentMatch()
		{
			if (!PluginConfig.CollectionVisualsEnabled)
			{
				return null;
			}
			GameMgr manager = GameMgr.Get();
			GameState val = GameState.Get();
			if (manager == null || val == null || manager.IsBattlegrounds() || manager.IsSpectator())
			{
				return null;
			}
			return UseMatch(val, manager.GetGameHandle(), delegate
			{
				long valueOrDefault = manager.LastDeckId.GetValueOrDefault();
				object deck;
				if (valueOrDefault <= 0)
				{
					deck = null;
				}
				else
				{
					CollectionManager obj = CollectionManager.Get();
					deck = ((obj != null) ? obj.GetDeck(valueOrDefault) : null);
				}
				return SelectMatch((CollectionDeck)deck, valueOrDefault);
			});
		}

		internal static CollectionHeroAppearance GetHeroForClass(TAG_CLASS heroClass)
		{
			if (!PluginConfig.CollectionVisualsEnabled)
			{
				return null;
			}
			MatchAppearance matchAppearance = CurrentMatch();
			if (matchAppearance != null)
			{
				return ResolveMatchHero(matchAppearance, Utils.CacheRawHeroCardId, heroClass);
			}
			GameMgr obj = GameMgr.Get();
			if (obj != null && obj.IsSpectator())
			{
				return null;
			}
			if (PluginConfig.skinHero != null && IsHeroForClass(PluginConfig.skinHero.Value, heroClass))
			{
				return Hero(PluginConfig.skinHero.Value);
			}
			return PickHero(heroClass);
		}

		internal static CollectionHeroAppearance ResolveMatchHero(MatchAppearance current, string nativeCardId, TAG_CLASS heroClass)
		{
			if (current.Hero == null && !string.IsNullOrEmpty(nativeCardId) && IsHeroForClass(GameUtils.TranslateCardIdToDbId(nativeCardId, false), heroClass))
			{
				current.Hero = ((PluginConfig.skinHero != null && IsHeroForClass(PluginConfig.skinHero.Value, heroClass)) ? Hero(PluginConfig.skinHero.Value) : PickHero(heroClass));
			}
			if (current.Hero == null || !IsHeroForClass(current.Hero.CardId, heroClass))
			{
				return null;
			}
			return current.Hero.Copy();
		}

		internal static int? GetGameCardBack()
		{
			return CurrentMatch()?.CardBackId;
		}

		internal static string GetGameCoin()
		{
			int? num = CurrentMatch()?.CoinId;
			CosmeticCoinDbfRecord val = ((!num.HasValue) ? null : GameDbf.CosmeticCoin?.GetRecord(num.Value));
			if (val != null)
			{
				return GameUtils.TranslateDbIdToCardId(val.CardId, false);
			}
			return null;
		}

		internal static bool TryGetHeroPortraitPremium(string cardId, out TAG_PREMIUM premium)
		{
			premium = (TAG_PREMIUM)1;
			if (!PluginConfig.CollectionVisualsEnabled)
			{
				return false;
			}
			int num = GameUtils.TranslateCardIdToDbId(cardId, false);
			CardHeroDbfRecord cardHeroRecordForCardId = GameUtils.GetCardHeroRecordForCardId(num);
			if (cardHeroRecordForCardId == null || !IsConstructedHero(num))
			{
				return false;
			}
			CollectionHeroAppearance heroForClass = GetHeroForClass(GameUtils.GetTagClassFromCardDbId(num));
			if (heroForClass != null && heroForClass.CardId == num)
			{
				premium = (TAG_PREMIUM)heroForClass.Premium;
				return true;
			}
			return (int)cardHeroRecordForCardId.HeroType == 1;
		}
	}
}
