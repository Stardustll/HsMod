using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using Blizzard.T5.Game.Spells;
using Hearthstone.Timeline;
using UnityEngine;
using UnityEngine.Playables;

namespace HsMod
{


	internal static class LocalHeroForms
	{
		private sealed class SoulEffect
		{
			internal Spell Spell;

			internal float StartedAt;
		}

		private sealed class VisualState
		{
			internal Entity Hero;

			internal Player Owner;

			internal HeroFormState Form;

			internal string SelectionSource;

			internal int[] NativeTags;

			internal int NativeCorner;

			internal Entity Power;

			internal string NativePower;

			internal string AppliedPower;

			internal Spell Spell;

			internal Spell BoardSpell;

			internal bool BoardSpellStarted;

			internal bool BoardSpellFinished;

			internal DefLoader.DisposableCardDef TargetDef;

			internal float StartedAt;

			internal float NextSelectionCheck;

			internal float StartedScaledAt;

			internal bool Starting;

			internal bool Committed;

			internal bool TagsApplied;

			internal bool Failed;

			internal Exception Failure;

			internal int ObservedCorpses;

			internal List<SoulEffect> SoulEffects;

			internal bool SoulEffectsFailed;

			internal EmoteType? LastCosmeticEmote;

			internal bool PreserveHeroIdentity => Form.NativeCardId == "CATA_190h";
		}

		private static readonly GAME_TAG[] VisualTags;

		private static readonly VisualState[] States;

		private static readonly Dictionary<Entity, string> NativePowerIds;

		private static readonly Dictionary<Entity, string> LoadedHeroes;

		private static readonly ConditionalWeakTable<Player, HeroCosmeticTurnState> CosmeticTurns;

		private static readonly FieldInfo DynamicDefinition;

		private static readonly PropertyInfo OverlayBehaviour;

		private static GameState game;

		[ThreadStatic]
		private static Entity visualReload;

		internal static bool IsVisualReload(Entity entity)
		{
			if (entity == visualReload)
			{
				return entity != null;
			}
			return false;
		}

		internal static string GetCurrentSceneHero(Entity hero)
		{
			if (hero == null || !PluginConfig.CollectionVisualsEnabled || game != GameState.Get())
			{
				return null;
			}
			VisualState[] states = States;
			foreach (VisualState visualState in states)
			{
				if (visualState != null && visualState.Hero == hero)
				{
					if (visualState.Failed)
					{
						return visualState.Form.NativeCardId;
					}
					string currentCardId = visualState.Form.CurrentCardId;
					if (visualState.Form.Definition.Trigger == HeroFormTrigger.SignatureDeathwing && GameUtils.GetCardTagValue(GameUtils.TranslateCardIdToDbId(currentCardId, false), (GAME_TAG)3564) == 0)
					{
						return visualState.Form.Definition.BaseCardId;
					}
					return currentCardId;
				}
			}
			if (!LoadedHeroes.TryGetValue(hero, out var value))
			{
				return null;
			}
			return value;
		}

		private static int SideIndex(global::Player.Side side)
		{
			if ((int)side != 1)
			{
				if ((int)side != 2)
				{
					return -1;
				}
				return 1;
			}
			return 0;
		}

		private static Entity GetHero(Player player)
		{
			if (player != null)
			{
				GameState obj = game;
				if (obj == null)
				{
					return null;
				}
				return obj.GetEntity(((EntityBase)player).GetTag((GAME_TAG)27));
			}
			return null;
		}

		private static void EnsureGame(GameState current)
		{
			if (game != current)
			{
				Reset(restore: false);
				game = current;
			}
		}

		private static VisualState Create(Entity hero, Player owner, string nativeCardId, string selectedCardId)
		{
			VisualState visualState = new VisualState
			{
				Hero = hero,
				Owner = owner,
				Form = new HeroFormState(nativeCardId, selectedCardId, CosmeticTurns.GetValue(owner, (Player player) => new HeroCosmeticTurnState())),
				SelectionSource = nativeCardId,
				NativeTags = new int[VisualTags.Length],
				NativeCorner = ((EntityBase)owner).GetTag((GAME_TAG)3564),
				ObservedCorpses = ((EntityBase)owner).GetTag((GAME_TAG)2186)
			};
			for (int num = 0; num < VisualTags.Length; num++)
			{
				visualState.NativeTags[num] = ((EntityBase)hero).GetTag(VisualTags[num]);
			}
			return visualState;
		}

		internal static string ResolveLoadedCard(Entity entity, string nativeCardId, string selectedCardId)
		{
			if (PluginConfig.CollectionVisualsEnabled && !IsVisualReload(entity))
			{
				GameMgr obj = GameMgr.Get();
				if (obj == null || !obj.IsBattlegrounds())
				{
					GameState val = GameState.Get();
					if (val == null || entity == null || val.GetEntity(((EntityBase)entity).GetEntityId()) != entity)
					{
						return selectedCardId;
					}
					EnsureGame(val);
					Player controller = entity.GetController();
					int num = ((controller == null) ? (-1) : SideIndex(controller.GetSide()));
					if (num < 0)
					{
						return selectedCardId;
					}
					VisualState visualState = States[num];
					if (((EntityBase)entity).IsHeroPower())
					{
						NativePowerIds[entity] = nativeCardId;
						if (visualState == null || (int)((EntityBase)entity).GetZone() != 1 || ((EntityBase)entity).HasTag((GAME_TAG)3919))
						{
							return selectedCardId;
						}
						visualState.Power = entity;
						visualState.NativePower = nativeCardId;
						string text = ResolvePower(visualState, nativeCardId, visualState.Form.CurrentCardId);
						visualState.AppliedPower = ((text != nativeCardId) ? text : null);
						return text;
					}
					if (!((EntityBase)entity).IsHero() || (int)((EntityBase)entity).GetZone() != 1)
					{
						return selectedCardId;
					}
					LoadedHeroes[entity] = selectedCardId;
					if (visualState != null && visualState.PreserveHeroIdentity && visualState.Hero == entity && nativeCardId == "CATA_190h")
					{
						return nativeCardId;
					}
					if (TryAdoptDeathwing(visualState, entity, controller, nativeCardId, out var state))
					{
						Release(visualState);
						States[num] = state;
						return nativeCardId;
					}
					if (HeroFormState.Find(selectedCardId) == null)
					{
						if (visualState != null && visualState.Hero == entity)
						{
							Release(visualState);
							States[num] = null;
						}
						return selectedCardId;
					}
					if (visualState != null && visualState.Hero == entity && visualState.Form.CanReuse(nativeCardId, selectedCardId))
					{
						visualState.TagsApplied = false;
						return visualState.Form.CurrentCardId;
					}
					Release(visualState);
					VisualState visualState2 = Create(entity, controller, nativeCardId, selectedCardId);
					States[num] = visualState2;
					return visualState2.Form.CurrentCardId;
				}
			}
			return selectedCardId;
		}

		private static bool TryAdoptDeathwing(VisualState previous, Entity hero, Player owner, string nativeCardId, out VisualState state)
		{
			state = null;
			if (previous == null || previous.Form.IsNative || previous.PreserveHeroIdentity || previous.Form.Definition.Trigger != HeroFormTrigger.SignatureDeathwing || nativeCardId != "CATA_190h" || (int)((EntityBase)hero).GetTag<TAG_PREMIUM>((GAME_TAG)12) != 3)
			{
				return false;
			}
			state = Create(hero, owner, nativeCardId, previous.Form.SelectedCardId);
			state.SelectionSource = previous.SelectionSource;
			state.NativeCorner = previous.NativeCorner;
			state.Form.RequestDeathwing(isSignature: true);
			return true;
		}

		private static string HeroPower(string heroCardId)
		{
			int num = GameUtils.TranslateCardIdToDbId(heroCardId, false);
			if (num <= 0)
			{
				return null;
			}
			return GameUtils.GetHeroPowerCardIdFromHero(num);
		}

		private static string ResolvePower(VisualState state, string cardId, string targetHero)
		{
			if (state.Form.IsNative || state.PreserveHeroIdentity)
			{
				return cardId;
			}
			int num = GameUtils.TranslateCardIdToDbId(state.Form.NativeCardId, false);
			int num2 = GameUtils.TranslateCardIdToDbId(targetHero, false);
			if (!LocalCollectionAppearance.IsConstructedHero(num) || GameUtils.GetTagClassFromCardDbId(num) != GameUtils.GetTagClassFromCardDbId(num2) || !HeroFormState.IsEquivalentPower(cardId, HeroPower(state.Form.NativeCardId), HeroPower(state.Form.SelectedCardId), HeroPower(state.Form.CurrentCardId)))
			{
				return cardId;
			}
			string text = HeroPower(targetHero);
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
			return cardId;
		}

		internal static bool HasLocalCosmeticHero()
		{
			VisualState visualState = States[0];
			if (PluginConfig.CollectionVisualsEnabled && game != null && game == GameState.Get() && visualState != null && !visualState.Form.IsNative && visualState.Form.Definition.Trigger == HeroFormTrigger.CosmeticAction)
			{
				return GetHero(visualState.Owner) == visualState.Hero;
			}
			return false;
		}

		internal static bool HasLocalCosmeticAction()
		{
			if (HasLocalCosmeticHero() && !States[0].Failed && States[0].Spell == null && game.GetGameEntity() != null && !game.IsGameOver())
			{
				return States[0].Form.CanRequestCosmeticAction(game.GetTurn(), States[0].Owner.IsCurrentPlayer());
			}
			return false;
		}

		internal static EmoteType GetLocalCosmeticEmote()
		{
			if (HasLocalCosmeticAction())
			{
				return (EmoteType)75;
			}
			return (EmoteType)4;
		}

		internal static bool HandleCosmeticAction(Entity entity, out bool accepted)
		{
			accepted = false;
			VisualState visualState = States[0];
			if (visualState == null || visualState.Form.IsNative || visualState.Form.Definition.Trigger != HeroFormTrigger.CosmeticAction || visualState.Hero != entity || game != GameState.Get())
			{
				return false;
			}
			if (HasLocalCosmeticAction())
			{
				accepted = visualState.Form.RequestCosmeticAction(game.GetTurn(), visualState.Owner.IsCurrentPlayer());
			}
			return true;
		}

		private static bool CanStartPending(VisualState state)
		{
			bool isOwnTurn = state.Form.Definition.Trigger == HeroFormTrigger.CosmeticAction && game.GetGameEntity() != null && state.Owner.IsCurrentPlayer();
			return state.Form.CanStartTransition(game.GetTurn(), isOwnTurn);
		}

		private static void RefreshCosmeticMenu(VisualState state)
		{
			if (state != States[0] || !HasLocalCosmeticHero())
			{
				return;
			}
			EmoteType localCosmeticEmote = GetLocalCosmeticEmote();
			EmoteType? lastCosmeticEmote = state.LastCosmeticEmote;
			state.LastCosmeticEmote = localCosmeticEmote;
			if (lastCosmeticEmote.HasValue && lastCosmeticEmote.Value != localCosmeticEmote)
			{
				EmoteHandler obj = EmoteHandler.Get();
				if (obj != null)
				{
					obj.ChangeAvailableEmotes();
				}
			}
		}

		internal static void ObserveHealth()
		{
			if (!PluginConfig.CollectionVisualsEnabled || game == null || game != GameState.Get())
			{
				return;
			}
			VisualState[] states = States;
			foreach (VisualState visualState in states)
			{
				if (visualState != null && !visualState.Failed && visualState.Form.IsHealthTriggered && GetHero(visualState.Owner) == visualState.Hero)
				{
					Entity hero = GetHero(((int)visualState.Owner.GetSide() == 1) ? game.GetOpposingSidePlayer() : game.GetFriendlySidePlayer());
					visualState.Form.ObserveHealth(RemainingHealth(visualState.Hero), RemainingHealth(hero));
					TryStartTransition(visualState);
				}
			}
		}

		private static int RemainingHealth(Entity entity)
		{
			if (entity != null)
			{
				return ((EntityBase)entity).GetHealth() - ((EntityBase)entity).GetTag((GAME_TAG)44);
			}
			return 0;
		}

		private static bool IsKelthuzad(string cardId)
		{
			switch (cardId)
			{
			default:
				return cardId == "HERO_11be_necro";
			case "HERO_08cs":
			case "HERO_08cs_necro":
			case "HERO_11be":
				return true;
			}
		}

		private static bool CanPlaySoulEffect(VisualState state)
		{
			if (state == null || state.Failed || state.SoulEffectsFailed || !PluginConfig.CollectionVisualsEnabled || !IsKelthuzad(state.Form.CurrentCardId) || IsKelthuzad(state.Form.NativeCardId) || !IsCurrent(state) || game.IsGameOver() || (int)game.GetCreateGamePhase() != 2 || game.IsMulliganPhase() || game.IsMulliganPhasePending())
			{
				return false;
			}
			GameMgr val = GameMgr.Get();
			if (val == null || val.IsSpectator() || val.IsBattlegrounds() || val.IsMercenaries())
			{
				return false;
			}
			CornerSpellReplacementManager cornerReplacementManager = game.GetCornerReplacementManager();
			if (cornerReplacementManager == null)
			{
				return false;
			}
			CornerReplacementContext cornerReplacementContext = cornerReplacementManager.GetCornerReplacementContext(state.Owner.GetSide());
			if ((int)cornerReplacementContext.cornerReplacementPetType == 0)
			{
				if ((int)cornerReplacementContext.cornerReplacementSpellType != 16)
				{
					return (int)cornerReplacementContext.cornerReplacementSpellType == 18;
				}
				return true;
			}
			return false;
		}

		internal static void ObserveCorpses(Player owner, TagDelta change)
		{
			if (owner == null || change.tag != 2186)
			{
				return;
			}
			int num = SideIndex(owner.GetSide());
			VisualState visualState = ((num < 0) ? null : States[num]);
			if (visualState == null || visualState.Owner != owner || !IsCurrent(visualState))
			{
				return;
			}
			int observedCorpses = visualState.ObservedCorpses;
			visualState.ObservedCorpses = Math.Max(observedCorpses, change.newValue);
			if (change.newValue <= change.oldValue || change.newValue <= observedCorpses || !CanPlaySoulEffect(visualState))
			{
				return;
			}
			try
			{
				StartSoulEffect(visualState);
			}
			catch (Exception ex)
			{
				visualState.SoulEffectsFailed = true;
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "播放克尔苏加德吸魂特效失败: " + ex.Message);
			}
		}

		private static void StartSoulEffect(VisualState state)
		{
			Card card = state.Hero.GetCard();
			if (card == null || card.GetActor() == null)
			{
				return;
			}
			DefLoader.DisposableCardDef cardDef = DefLoader.Get().GetCardDef(state.Form.CurrentCardId);
			try
			{
				List<CardEffectDef> list = ((cardDef == null) ? null : cardDef.CardDef?.m_SubSpellEffectDefs);
				if (list == null || list.Count <= 1 || string.IsNullOrEmpty(list[1]?.m_SpellPath))
				{
					throw new InvalidOperationException("缺少克尔苏加德吸魂资源 " + state.Form.CurrentCardId);
				}
				Spell val = SpellUtils.LoadAndSetupSpell(list[1].m_SpellPath, (Component)(object)card);
				if (val == null)
				{
					throw new InvalidOperationException("克尔苏加德吸魂特效不可用");
				}
				if (state.SoulEffects == null)
				{
					state.SoulEffects = new List<SoulEffect>();
				}
				state.SoulEffects.Add(new SoulEffect
				{
					Spell = val,
					StartedAt = Time.realtimeSinceStartup
				});
				if (!((SpellBase)val).HasUsableState((SpellStateType)3))
				{
					throw new InvalidOperationException("克尔苏加德吸魂特效缺少动作状态");
				}
				((SpellBase)val).ActivateState((SpellStateType)3);
			}
			finally
			{
				((IDisposable)cardDef)?.Dispose();
			}
		}

		private static void ReleaseSoulEffects(VisualState state, bool all)
		{
			if (state.SoulEffects == null || state.SoulEffects.Count == 0)
			{
				return;
			}
			bool flag = all || !CanPlaySoulEffect(state);
			for (int num = state.SoulEffects.Count - 1; num >= 0; num--)
			{
				SoulEffect soulEffect = state.SoulEffects[num];
				if (flag || !(soulEffect.Spell != null) || !((SpellBase)soulEffect.Spell).IsActive() || !(Time.realtimeSinceStartup - soulEffect.StartedAt < 15f))
				{
					state.SoulEffects.RemoveAt(num);
					if (soulEffect.Spell != null)
					{
						ReleaseEffectSpell(soulEffect.Spell);
					}
				}
			}
		}

		internal static void Tick()
		{
			if (States[0] == null && States[1] == null && NativePowerIds.Count == 0 && LoadedHeroes.Count == 0)
			{
				return;
			}
			if (game == GameState.Get())
			{
				GameMgr obj = GameMgr.Get();
				if (obj == null || !obj.IsBattlegrounds())
				{
					if (!PluginConfig.CollectionVisualsEnabled)
					{
						Reset(restore: true);
					}
					else
					{
						if (States[0] == null && States[1] == null)
						{
							return;
						}
						ObserveHealth();
						for (int i = 0; i < States.Length; i++)
						{
							VisualState visualState = States[i];
							if (visualState != null)
							{
								try
								{
									Tick(visualState, i);
								}
								catch (Exception error)
								{
									FailTransition(visualState, error);
								}
							}
						}
					}
					return;
				}
			}
			Reset(restore: false);
		}

		private static void Tick(VisualState state, int index)
		{
			if (state.Failure != null)
			{
				FailTransition(state, state.Failure);
				return;
			}
			Entity hero = GetHero(state.Owner);
			if (hero != state.Hero)
			{
				if (hero != null || (int)game.GetCreateGamePhase() == 2)
				{
					TryAdoptDeathwing(state, hero, state.Owner, (hero != null) ? ((EntityBase)hero).GetCardId() : null, out var state2);
					Release(state);
					States[index] = state2;
				}
				return;
			}
			if (state.Failed)
			{
				Release(state);
				return;
			}
			if (game.IsGameOver())
			{
				ReleaseSpells(state);
				return;
			}
			ReleaseCompletedDeathwingSpells(state);
			if (state.SoulEffects != null && state.SoulEffects.Count > 0)
			{
				ReleaseSoulEffects(state, all: false);
			}
			if (Time.realtimeSinceStartup >= state.NextSelectionCheck)
			{
				state.NextSelectionCheck = Time.realtimeSinceStartup + 0.5f;
				if (Patcher.PatchFavorite.SelectConstructedHeroCard(state.SelectionSource, state.Owner.GetSide()) != state.Form.SelectedCardId)
				{
					Restore(state);
					States[index] = null;
					if (!state.PreserveHeroIdentity)
					{
						state.Hero.LoadCard(state.Form.NativeCardId, (Entity.LoadCardData)null, false);
					}
					return;
				}
			}
			if (state.Form.IsNative)
			{
				return;
			}
			Card card = state.Hero.GetCard();
			if (((card != null) ? card.GetActor() : null) == null)
			{
				return;
			}
			if (state.PreserveHeroIdentity && state.Committed)
			{
				SetHeroActorDefinition(state.Hero.GetCard().GetActor(), state.TargetDef);
			}
			if (state.Spell == null && state.Form.PendingCardId != null && !CanStartPending(state))
			{
				state.Form.Cancel();
			}
			RefreshCosmeticMenu(state);
			if (!state.TagsApplied)
			{
				ApplyTags(state, state.Form.CurrentCardId);
				state.Hero.GetCard().GetActor().UpdateAllComponents(true);
				state.TagsApplied = true;
			}
			if (state.Spell != null || state.BoardSpell != null)
			{
				if (!state.PreserveHeroIdentity && state.Committed && (state.Spell == null || !((SpellBase)state.Spell).IsActive()) && (state.BoardSpell == null || (state.BoardSpellFinished && !((SpellBase)state.BoardSpell).IsActive())))
				{
					Release(state);
				}
				else if (Time.realtimeSinceStartup - state.StartedAt > 30f && Time.time - state.StartedScaledAt > 30f && (state.Spell == null || !HasRunningTimelines(state.Spell)) && (state.BoardSpell == null || !HasRunningTimelines(state.BoardSpell)))
				{
					throw new InvalidOperationException("变身动画未结束");
				}
			}
			else
			{
				TryStartTransition(state);
			}
		}

		private static bool IsCurrent(VisualState state)
		{
			if (game == null || game != GameState.Get())
			{
				return false;
			}
			int num = SideIndex(state.Owner.GetSide());
			if (num >= 0 && States[num] == state)
			{
				return GetHero(state.Owner) == state.Hero;
			}
			return false;
		}

		private static void TryStartTransition(VisualState state)
		{
			if (state.Starting || state.Failed || state.Form.IsNative || state.Form.PendingCardId == null || state.Spell != null)
			{
				return;
			}
			state.Starting = true;
			try
			{
				if (PluginConfig.CollectionVisualsEnabled && IsCurrent(state) && !game.IsGameOver() && CanStartPending(state) && (int)game.GetCreateGamePhase() == 2 && !game.IsMulliganPhase() && !game.IsMulliganPhasePending() && (state.Form.IsHealthTriggered || (!game.HasPowersToProcess() && !game.IsBusy())))
				{
					StartTransition(state);
				}
			}
			catch (Exception failure)
			{
				state.Failed = true;
				state.Failure = failure;
			}
			finally
			{
				state.Starting = false;
			}
		}

		private static void StartTransition(VisualState state)
		{
			Card card = state.Hero.GetCard();
			if (((card != null) ? card.GetActor() : null) == null || Patcher.PatchFavorite.SelectConstructedHeroCard(state.SelectionSource, state.Owner.GetSide()) != state.Form.SelectedCardId)
			{
				return;
			}
			string pendingCardId = state.Form.PendingCardId;
			EntityDef entityDef = DefLoader.Get().GetEntityDef(pendingCardId);
			EntityDef entityDef2 = DefLoader.Get().GetEntityDef(state.Form.CurrentCardId);
			if (entityDef == null || entityDef2 == null || (int)((EntityBase)entityDef).GetCardType() != 3 || ((EntityBase)entityDef).GetClass() != ((EntityBase)entityDef2).GetClass())
			{
				throw new InvalidOperationException("缺少等价的英雄形态定义 " + pendingCardId);
			}
			state.TargetDef = DefLoader.Get().GetCardDef(pendingCardId);
			DefLoader.DisposableCardDef targetDef = state.TargetDef;
			if (((targetDef != null) ? targetDef.CardDef : null) == null)
			{
				throw new InvalidOperationException("缺少英雄形态资源 " + pendingCardId);
			}
			string text = (state.PreserveHeroIdentity ? state.Form.Definition.BaseCardId : state.Form.CurrentCardId);
			DefLoader.DisposableCardDef cardDef = DefLoader.Get().GetCardDef(text);
			try
			{
				int subSpellIndex = state.Form.Definition.SubSpellIndex;
				List<CardEffectDef> list = ((cardDef == null) ? null : cardDef.CardDef?.m_SubSpellEffectDefs);
				if (list == null || subSpellIndex >= list.Count || string.IsNullOrEmpty(list[subSpellIndex]?.m_SpellPath))
				{
					throw new InvalidOperationException("缺少英雄变身子法术 " + state.Form.CurrentCardId);
				}
				state.Spell = SpellUtils.LoadAndSetupSpell(list[subSpellIndex].m_SpellPath, (Component)(object)state.Hero.GetCard());
				if (state.PreserveHeroIdentity && !LocalBoardAppearance.HasDeathwingRuins(state.Owner))
				{
					if (list.Count == 0 || string.IsNullOrEmpty(list[0]?.m_SpellPath))
					{
						throw new InvalidOperationException("缺少死亡之翼棋盘轰炸资源");
					}
					state.BoardSpell = SpellUtils.LoadAndSetupSpell(list[0].m_SpellPath, (Component)(object)state.Hero.GetCard());
					if (state.BoardSpell == null || !((SpellBase)state.BoardSpell).HasUsableState((SpellStateType)3))
					{
						throw new InvalidOperationException("死亡之翼棋盘轰炸特效不可用");
					}
				}
			}
			finally
			{
				((IDisposable)cardDef)?.Dispose();
			}
			if (state.Spell == null || !((SpellBase)state.Spell).HasUsableState((SpellStateType)3))
			{
				throw new InvalidOperationException("英雄变身子法术不可用");
			}
			state.StartedAt = Time.realtimeSinceStartup;
			state.StartedScaledAt = Time.time;
			state.Committed = false;
			state.Spell.AddFinishedCallback((ISpellCallbackHandler<Spell>.FinishedCallback)OnTransitionFinished, (object)state);
			((SpellBase)state.Spell).ActivateState((SpellStateType)3);
			Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "英雄变身触发: " + state.Form.SelectedCardId + " -> " + pendingCardId);
		}

		private static void OnTransitionFinished(Spell spell, object userData)
		{
			VisualState visualState = userData as VisualState;
			if (visualState == null || visualState.Failed || visualState.Committed || visualState.Spell != spell || !PluginConfig.CollectionVisualsEnabled || !IsCurrent(visualState) || game.IsGameOver())
			{
				return;
			}
			try
			{
				ApplyPending(visualState);
				if (visualState.PreserveHeroIdentity && visualState.BoardSpell != null && !visualState.BoardSpellStarted)
				{
					StartBoardSpell(visualState);
				}
			}
			catch (Exception failure)
			{
				visualState.Failed = true;
				visualState.Failure = failure;
			}
		}

		private static void StartBoardSpell(VisualState state)
		{
			state.BoardSpellStarted = true;
			state.BoardSpellFinished = false;
			state.BoardSpell.AddFinishedCallback((ISpellCallbackHandler<Spell>.FinishedCallback)OnBoardSpellFinished, (object)state);
			((SpellBase)state.BoardSpell).ActivateState((SpellStateType)3);
			Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "死亡之翼棋盘轰炸触发: " + state.Form.SelectedCardId);
		}

		private static void OnBoardSpellFinished(Spell spell, object userData)
		{
			VisualState visualState = userData as VisualState;
			if (visualState == null || visualState.Failed || !visualState.Committed || !visualState.PreserveHeroIdentity || !visualState.BoardSpellStarted || visualState.BoardSpellFinished || visualState.BoardSpell != spell || !PluginConfig.CollectionVisualsEnabled || !IsCurrent(visualState) || game.IsGameOver())
			{
				return;
			}
			visualState.BoardSpellFinished = true;
			try
			{
				LocalBoardAppearance.MarkDeathwingRuins(visualState.Owner);
			}
			catch (Exception ex)
			{
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "应用死亡之翼废墟场景失败: " + ex.Message);
			}
		}

		private static void FailTransition(VisualState state, Exception error)
		{
			state.Failed = true;
			state.Failure = null;
			try
			{
				Restore(state);
			}
			catch
			{
				Release(state);
			}
			if ((int)state.Owner.GetSide() == 1)
			{
				EmoteHandler obj2 = EmoteHandler.Get();
				if (obj2 != null)
				{
					obj2.ChangeAvailableEmotes();
				}
			}
			Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "英雄形态切换失败 " + state.Form.SelectedCardId + ": " + error);
		}

		private static void ApplyPending(VisualState state)
		{
			string pendingCardId = state.Form.PendingCardId;
			if (pendingCardId == null || !PluginConfig.CollectionVisualsEnabled || GetHero(state.Owner) != state.Hero)
			{
				return;
			}
			Entity heroPower = ((Entity)state.Owner).GetHeroPower();
			string text = ((heroPower == null) ? null : ResolvePower(state, ((EntityBase)heroPower).GetCardId(), pendingCardId));
			if (state.PreserveHeroIdentity)
			{
				Card card = state.Hero.GetCard();
				SetHeroActorDefinition((card != null) ? card.GetActor() : null, state.TargetDef);
			}
			else
			{
				ReloadVisual(state.Hero, pendingCardId);
			}
			if (heroPower != null && text != ((EntityBase)heroPower).GetCardId())
			{
				if (state.Power != heroPower)
				{
					state.Power = heroPower;
					state.NativePower = (NativePowerIds.TryGetValue(heroPower, out var value) ? value : ((EntityBase)heroPower).GetCardId());
				}
				ReloadVisual(heroPower, text);
				state.AppliedPower = text;
			}
			state.Form.Commit();
			state.Committed = true;
			RefreshHeroVisuals(state);
			Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "英雄形态切换: " + state.Form.SelectedCardId + " -> " + pendingCardId);
		}

		private static void RefreshHeroVisuals(VisualState state)
		{
			ApplyTags(state, state.Form.CurrentCardId);
			Actor actor = state.Hero.GetCard().GetActor();
			if (actor != null)
			{
				actor.UpdateAllComponents(true);
			}
			Board obj = Board.Get();
			if (obj != null)
			{
				obj.UpdateCustomHeroTray(state.Owner.GetSide());
			}
			if ((int)state.Owner.GetSide() == 1)
			{
				EmoteHandler obj2 = EmoteHandler.Get();
				if (obj2 != null)
				{
					obj2.ChangeAvailableEmotes();
				}
			}
		}

		private static void SetHeroActorDefinition(Actor actor, DefLoader.DisposableCardDef definition)
		{
			if (!(actor == null) && !(((definition != null) ? definition.CardDef : null) == null) && !actor.HasSameCardDef(definition.CardDef))
			{
				actor.SetCardDef(definition);
				actor.UpdateAllComponents(true);
			}
		}

		internal static DefLoader.DisposableCardDef GetCardDisplayDefinition(DefLoader.DisposableCardDef native, Card card)
		{
			if (!PluginConfig.CollectionVisualsEnabled || card == null || game == null || game != GameState.Get())
			{
				return native;
			}
			GameMgr val = GameMgr.Get();
			if (val == null || val.IsBattlegrounds() || val.IsSpectator() || val.IsMercenaries())
			{
				return native;
			}
			VisualState[] states = States;
			foreach (VisualState visualState in states)
			{
				if (visualState != null && visualState.PreserveHeroIdentity && visualState.Committed && !visualState.Failed && IsCurrent(visualState) && visualState.Hero.GetCard() == card)
				{
					DefLoader.DisposableCardDef targetDef = visualState.TargetDef;
					if (((targetDef != null) ? targetDef.CardDef : null) != null)
					{
						return visualState.TargetDef;
					}
				}
			}
			return native;
		}

		internal static Card GetHeroSpellSource(Actor actor, SpellType spellType, Spell spell)
		{
			if (!PluginConfig.CollectionVisualsEnabled || actor == null || spell == null || spell.GetSource() != null || game == null || game != GameState.Get())
			{
				return null;
			}
			GameMgr val = GameMgr.Get();
			if (val == null || val.IsBattlegrounds() || val.IsSpectator() || val.IsMercenaries())
			{
				return null;
			}
			Card card = actor.GetCard();
			if (card == null)
			{
				return null;
			}
			VisualState[] states = States;
			foreach (VisualState visualState in states)
			{
				if (visualState == null || !visualState.PreserveHeroIdentity || visualState.Failed || !IsCurrent(visualState) || visualState.Hero.GetCard() != card)
				{
					continue;
				}
				DefLoader.DisposableCardDef targetDef = visualState.TargetDef;
				List<SpellTableOverride> list = ((targetDef == null) ? null : targetDef.CardDef?.m_SpellTableOverrides);
				if (list == null)
				{
					return null;
				}
				foreach (SpellTableOverride item in list)
				{
					if (item != null && item.m_Type == spellType && !string.IsNullOrEmpty(item.m_SpellPrefabName))
					{
						return card;
					}
				}
			}
			return null;
		}

		private static void ApplyTags(VisualState state, string cardId)
		{
			DefLoader obj = DefLoader.Get();
			EntityDef val = ((obj != null) ? obj.GetEntityDef(cardId) : null);
			if (val == null)
			{
				return;
			}
			GAME_TAG[] visualTags = VisualTags;
			foreach (GAME_TAG val2 in visualTags)
			{
				((EntityBase)state.Hero).SetTag(val2, ((EntityBase)val).GetTag(val2));
			}
			if ((int)state.Owner.GetSide() == 1)
			{
				game.UpdateCornerReplacements();
				return;
			}
			int tag = ((EntityBase)val).GetTag((GAME_TAG)3564);
			if (tag == 0 && state.Form.Definition.Trigger == HeroFormTrigger.SignatureDeathwing)
			{
				tag = ((EntityBase)DefLoader.Get().GetEntityDef(state.Form.Definition.BaseCardId)).GetTag((GAME_TAG)3564);
			}
			if (((EntityBase)state.Owner).GetTag((GAME_TAG)3564) != tag)
			{
				((EntityBase)state.Owner).SetTag((GAME_TAG)3564, tag);
				game.UpdateCornerReplacements();
			}
		}

		private static void ReloadVisual(Entity entity, string cardId)
		{
			Entity val = visualReload;
			object value = DynamicDefinition.GetValue(entity);
			visualReload = entity;
			try
			{
				entity.LoadCard(cardId, (Entity.LoadCardData)null, false);
			}
			finally
			{
				DynamicDefinition.SetValue(entity, value);
				visualReload = val;
			}
		}

		private static void ReleaseCompletedDeathwingSpells(VisualState state)
		{
			if (state.PreserveHeroIdentity && state.Committed)
			{
				if (state.Spell != null && !HasRunningTimelines(state.Spell))
				{
					ReleaseTransitionSpell(state);
				}
				if (state.BoardSpellFinished && state.BoardSpell != null && !HasRunningTimelines(state.BoardSpell))
				{
					ReleaseBoardSpell(state);
				}
			}
		}

		private static bool HasRunningTimelines(Spell spell)
		{
			PlayableDirector[] componentsInChildren = ((Component)spell).GetComponentsInChildren<PlayableDirector>(true);
			foreach (PlayableDirector val in componentsInChildren)
			{
				if (!(val == null) && ((Behaviour)val).enabled && ((Component)val).gameObject.activeInHierarchy)
				{
					if ((int)val.state == 1)
					{
						return true;
					}
					if (val.time > 0.0 && val.time < val.duration)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static void ReleaseTransitionSpell(VisualState state)
		{
			Spell spell = state.Spell;
			state.Spell = null;
			if (spell != null)
			{
				ReleaseVisualSpell(spell, isBoardSpell: false, state);
			}
		}

		private static void ReleaseBoardSpell(VisualState state)
		{
			Spell boardSpell = state.BoardSpell;
			state.BoardSpell = null;
			if (boardSpell != null)
			{
				ReleaseVisualSpell(boardSpell, isBoardSpell: true, state);
			}
		}

		private static void StopSpellTimelines(Spell spell)
		{
			PlayableDirector[] componentsInChildren = ((Component)spell).GetComponentsInChildren<PlayableDirector>(true);
			PlayableDirector[] array = componentsInChildren;
			foreach (PlayableDirector val in array)
			{
				if (val != null)
				{
					val.Stop();
				}
			}
			if (componentsInChildren.Length == 0 || OverlayBehaviour == null)
			{
				return;
			}
			CameraOverlayHelper[] array2 = UnityEngine.Object.FindObjectsByType<CameraOverlayHelper>((FindObjectsSortMode)0);
			foreach (CameraOverlayHelper val2 in array2)
			{
				object value = OverlayBehaviour.GetValue(val2, null);
				ITimelineEffectBehaviour val3 = (ITimelineEffectBehaviour)((value is ITimelineEffectBehaviour) ? value : null);
				PlayableDirector owner = ((val3 != null) ? val3.GetDirector() : null);
				if (owner != null && Array.Exists(componentsInChildren, (PlayableDirector director) => director == owner))
				{
					((TimelineEffectHelper)val2).Kill((TimelineEffectKillCause)2);
				}
			}
		}

		private static void ReleaseVisualSpell(Spell spell, bool isBoardSpell, VisualState state)
		{
			if (isBoardSpell)
			{
				spell.RemoveFinishedCallback((ISpellCallbackHandler<Spell>.FinishedCallback)OnBoardSpellFinished, (object)state);
			}
			else
			{
				spell.RemoveFinishedCallback((ISpellCallbackHandler<Spell>.FinishedCallback)OnTransitionFinished, (object)state);
			}
			ReleaseEffectSpell(spell);
		}

		private static void ReleaseEffectSpell(Spell spell)
		{
			try
			{
				StopSpellTimelines(spell);
			}
			catch (Exception ex)
			{
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "停止英雄外观动画失败: " + ex.Message);
			}
			SpellManager obj = SpellManager.Get();
			if (obj != null)
			{
				obj.ReleaseSpell(spell, true);
			}
		}

		private static void ReleaseSpells(VisualState state)
		{
			if (state != null)
			{
				ReleaseTransitionSpell(state);
				ReleaseBoardSpell(state);
				if (state.SoulEffects != null && state.SoulEffects.Count > 0)
				{
					ReleaseSoulEffects(state, all: true);
				}
			}
		}

		private static void Release(VisualState state)
		{
			if (state != null)
			{
				ReleaseSpells(state);
				DefLoader.DisposableCardDef targetDef = state.TargetDef;
				if (targetDef != null)
				{
					targetDef.Dispose();
				}
				state.TargetDef = null;
				state.Form.Cancel();
			}
		}

		private static void Restore(VisualState state)
		{
			Release(state);
			if (state.Form.IsNative || game != GameState.Get() || GetHero(state.Owner) != state.Hero)
			{
				return;
			}
			if (state.PreserveHeroIdentity)
			{
				Card card = state.Hero.GetCard();
				DefLoader.DisposableCardDef val = ((card != null) ? card.ShareDisposableCardDef() : null);
				try
				{
					SetHeroActorDefinition((card != null) ? card.GetActor() : null, val);
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			}
			else
			{
				ReloadVisual(state.Hero, state.Form.NativeCardId);
			}
			LoadedHeroes[state.Hero] = state.Form.NativeCardId;
			for (int i = 0; i < VisualTags.Length; i++)
			{
				((EntityBase)state.Hero).SetTag(VisualTags[i], state.NativeTags[i]);
			}
			if (state.Power != null && ((Entity)state.Owner).GetHeroPower() == state.Power && ((EntityBase)state.Power).GetCardId() == state.AppliedPower && !string.IsNullOrEmpty(state.NativePower))
			{
				ReloadVisual(state.Power, state.NativePower);
			}
			if ((int)state.Owner.GetSide() != 1)
			{
				((EntityBase)state.Owner).SetTag((GAME_TAG)3564, state.NativeCorner);
			}
			game.UpdateCornerReplacements();
			Card card2 = state.Hero.GetCard();
			if (card2 != null)
			{
				Actor actor = card2.GetActor();
				if (actor != null)
				{
					actor.UpdateAllComponents(true);
				}
			}
			Board obj = Board.Get();
			if (obj != null)
			{
				obj.UpdateCustomHeroTray(state.Owner.GetSide());
			}
		}

		internal static void Reset(bool restore)
		{
			bool flag = States[0] != null || States[1] != null;
			for (int i = 0; i < States.Length; i++)
			{
				VisualState visualState = States[i];
				States[i] = null;
				if (visualState == null)
				{
					continue;
				}
				try
				{
					if (restore)
					{
						Restore(visualState);
					}
					else
					{
						Release(visualState);
					}
				}
				catch (Exception ex)
				{
					Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "清理英雄形态失败: " + ex.Message);
				}
			}
			NativePowerIds.Clear();
			LoadedHeroes.Clear();
			game = null;
			if (restore & flag)
			{
				EmoteHandler obj = EmoteHandler.Get();
				if (obj != null)
				{
					obj.ChangeAvailableEmotes();
				}
			}
		}

		static LocalHeroForms()
		{
			//内联数组在反编译中丢失，值从原 DLL 的 FieldRVA 数据恢复
			VisualTags = new GAME_TAG[] { (GAME_TAG)2839, (GAME_TAG)2851, (GAME_TAG)3211, (GAME_TAG)3495 };
			States = new VisualState[2];
			NativePowerIds = new Dictionary<Entity, string>();
			LoadedHeroes = new Dictionary<Entity, string>();
			CosmeticTurns = new ConditionalWeakTable<Player, HeroCosmeticTurnState>();
			DynamicDefinition = typeof(Entity).GetField("m_dynamicEntityDef", BindingFlags.Instance | BindingFlags.NonPublic);
			OverlayBehaviour = typeof(TimelineEffectHelper).GetProperty("Behaviour", BindingFlags.Instance | BindingFlags.NonPublic);
		}
	}
}
