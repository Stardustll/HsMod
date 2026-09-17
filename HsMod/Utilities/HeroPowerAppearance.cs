using System;
using BepInEx.Logging;
using UnityEngine;

namespace HsMod
{


	internal static class HeroPowerAppearance
	{
		private sealed class FrameState
		{
			internal Entity Power;

			internal string Frame;

			internal TAG_PREMIUM Premium;

			internal bool Applied;

			internal void Clear()
			{
				Power = null;
				Frame = null;
				Applied = false;
			}
		}

		private static readonly FrameState[] Frames = new FrameState[2]
		{
			new FrameState(),
			new FrameState()
		};

		private static bool stopped;

		internal static bool IsStopped => stopped;

		private static bool AppearanceEnabled
		{
			get
			{
				if (!stopped)
				{
					return PluginConfig.CollectionVisualsEnabled;
				}
				return false;
			}
		}

		private static bool Enabled
		{
			get
			{
				if (AppearanceEnabled)
				{
					if (PluginConfig.goldenCardState != null)
					{
						return PluginConfig.goldenCardState.Value != Utils.CardState.Disabled;
					}
					return true;
				}
				return false;
			}
		}

		private static bool IsLivePower(EntityBase entity, GameState current, bool battlegrounds, bool spectator)
		{
			if (current != null && entity is Entity && entity.IsHeroPower() && (int)entity.GetZone() == 1 && !battlegrounds && !spectator)
			{
				Player player = current.GetPlayer(entity.GetControllerId());
				if (player != null && (int)player.GetSide() == 1)
				{
					return (object)current.GetEntity(entity.GetEntityId()) == entity;
				}
			}
			return false;
		}

		internal static TAG_PREMIUM ResolvePremium(EntityBase entity, GameState current, bool battlegrounds, bool spectator, TAG_PREMIUM original)
		{
			if (!Enabled || (int)original != 0 || !IsLivePower(entity, current, battlegrounds, spectator))
			{
				return original;
			}
			return (TAG_PREMIUM)1;
		}

		internal static TAG_PREMIUM GetPremium(EntityBase entity, TAG_PREMIUM original)
		{
			GameMgr val = GameMgr.Get();
			if (val != null)
			{
				return ResolvePremium(entity, GameState.Get(), val.IsBattlegrounds(), val.IsSpectator(), original);
			}
			return original;
		}

		internal static bool TryGetPremium(EntityBase entity, TAG_PREMIUM original, out TAG_PREMIUM premium)
		{
			premium = (TAG_PREMIUM)(int)original;
			GameMgr val = GameMgr.Get();
			GameState current = GameState.Get();
			if (!Enabled || val == null || !IsLivePower(entity, current, val.IsBattlegrounds(), val.IsSpectator()))
			{
				return false;
			}
			premium = (TAG_PREMIUM)(int)ResolvePremium(entity, current, battlegrounds: false, spectator: false, original);
			return true;
		}

		internal static bool TryGetFrameAsset(EntityBase entity, TAG_PREMIUM premium, out string asset)
		{
			asset = null;
			GameState val = GameState.Get();
			GameMgr val2 = GameMgr.Get();
			if (!AppearanceEnabled || val2 == null || val == null || val2.IsBattlegrounds() || val2.IsSpectator() || !(entity is Entity) || !entity.IsHeroPower() || (int)entity.GetZone() != 1 || (object)val.GetEntity(entity.GetEntityId()) != entity)
			{
				return false;
			}
			Player player = val.GetPlayer(entity.GetControllerId());
			if (player == null || ((int)player.GetSide() != 1 && ((int)player.GetSide() != 2 || false)))
			{
				return false;
			}
			CornerReplacementContext val3 = new CornerReplacementContext
			{
				cornerReplacementSpellType = (CornerReplacementSpellType)((EntityBase)player).GetTag((GAME_TAG)3564)
			};
			if ((int)val3.cornerReplacementSpellType != 0)
			{
				CornerReplacementConfig obj = CornerReplacementConfig.Get();
				asset = ((obj != null) ? obj.GetActor(val3.cornerReplacementSpellType, (ActorNames.ACTOR_ASSET)10, premium) : null);
			}
			if (string.IsNullOrEmpty(asset))
			{
				asset = ActorNames.GetNameWithPremiumType((ActorNames.ACTOR_ASSET)10, premium, (string)null, (Entity)null);
			}
			return !string.IsNullOrEmpty(asset);
		}

		internal static void Tick()
		{
			GameMgr val = GameMgr.Get();
			GameState val2 = GameState.Get();
			if (stopped || val == null || val2 == null || val.IsBattlegrounds() || val.IsSpectator())
			{
				FrameState[] frames = Frames;
				for (int i = 0; i < frames.Length; i++)
				{
					frames[i].Clear();
				}
			}
			else
			{
				TickPower(val2, val2.GetFriendlySidePlayer(), Frames[0]);
				TickPower(val2, val2.GetOpposingSidePlayer(), Frames[1]);
			}
		}

		private static void TickPower(GameState current, Player owner, FrameState state)
		{
			Entity val = ((owner != null) ? ((Entity)owner).GetHeroPower() : null);
			if (val == null || current.GetEntity(((EntityBase)val).GetEntityId()) != val)
			{
				state.Clear();
				return;
			}
			TAG_PREMIUM __result = Utils.ReadNativePremium((EntityBase)(object)val);
			EntityBase ___m_entity = (EntityBase)(object)val;
			Utils.GetPremiumType<EntityBase>(ref ___m_entity, ref __result);
			bool flag = TryGetFrameAsset((EntityBase)(object)val, __result, out var asset);
			if (!flag && !state.Applied)
			{
				return;
			}
			if (!flag)
			{
				asset = ActorNames.GetNameWithPremiumType((ActorNames.ACTOR_ASSET)10, __result, (string)null, (Entity)(object)owner);
			}
			if (state.Power == val && flag == state.Applied && asset == state.Frame && __result == state.Premium)
			{
				return;
			}
			Card card = val.GetCard();
			if (card == null || !card.IsActorReady() || card.GetZone() == null || card.IsTransitioningZones() || current.IsBusy() || current.HasPowersToProcess())
			{
				return;
			}
			state.Power = val;
			state.Frame = asset;
			state.Premium = __result;
			state.Applied = flag;
			try
			{
				Refresh(val);
			}
			catch (Exception ex)
			{
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "刷新英雄技能品质失败: " + ex.Message);
			}
		}

		private static void Refresh(Entity power)
		{
			Card card = power.GetCard();
			TAG_PREMIUM premiumType = ((EntityBase)power).GetPremiumType();
			power.SetRealTimePremium(premiumType);
			card.UpdateActor(false, (string)null);
			Actor actor = card.GetActor();
			if (actor != null)
			{
				actor.SetPremium(premiumType);
				actor.UpdateAllComponents(true);
			}
		}

		internal static void Reset(bool restore)
		{
			bool flag = AppearanceEnabled || Frames[0].Applied || Frames[1].Applied;
			stopped = restore;
			try
			{
				GameState val = GameState.Get();
				GameMgr val2 = GameMgr.Get();
				if (!(restore & flag) || val2 == null || val == null || val2.IsBattlegrounds() || val2.IsSpectator())
				{
					return;
				}
				for (int i = 0; i < Frames.Length; i++)
				{
					if (i == 0 || Frames[i].Applied)
					{
						Player obj = ((i == 0) ? val.GetFriendlySidePlayer() : val.GetOpposingSidePlayer());
						Entity val3 = ((obj != null) ? ((Entity)obj).GetHeroPower() : null);
						Card val4 = ((val3 != null) ? val3.GetCard() : null);
						if (val3 != null && val.GetEntity(((EntityBase)val3).GetEntityId()) == val3 && val4 != null && val4.IsActorReady() && val4.GetZone() != null)
						{
							Refresh(val3);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "恢复英雄技能品质失败: " + ex.Message);
			}
			finally
			{
				FrameState[] frames = Frames;
				for (int j = 0; j < frames.Length; j++)
				{
					frames[j].Clear();
				}
			}
		}
	}
}
