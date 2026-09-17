using System;
using UnityEngine;

namespace HsMod
{


	internal static class SpectatorHandLayout
	{
		private static bool patched;

		private static GameState requestedGame;

		private static int requestVersion;

		private static readonly Func<bool> Refresh = TryRefresh;

		internal static bool Enabled
		{
			get
			{
				if (patched)
				{
					return PluginConfig.isMoveEnemyCardsEnable?.Value ?? false;
				}
				return false;
			}
		}

		internal static void SetPatched(bool value)
		{
			patched = value;
			requestVersion++;
			if (PresentationShutdown.IsQuitting)
			{
				requestedGame = null;
				return;
			}
			requestedGame = GameState.Get();
			SettingsVisualRefresh.Request(Refresh);
		}

		private static bool TryRefresh()
		{
			int version = requestVersion;
			GameState val = GameState.Get();
			if (val == null || val != requestedGame)
			{
				return Complete(version);
			}
			Player opposingSidePlayer = val.GetOpposingSidePlayer();
			ZoneHand val2 = ((opposingSidePlayer != null) ? opposingSidePlayer.GetHandZone() : null);
			if (val2 == null)
			{
				return Complete(version);
			}
			if (val.IsBusy() || val.HasPowersToProcess() || val2.IsDoNotUpdateLayout())
			{
				return false;
			}
			foreach (Card card in ((Zone)val2).GetCards())
			{
				if (card != null && (!card.IsActorReady() || card.IsTransitioningZones()))
				{
					return false;
				}
			}
			val2.UpdateLayout((Card)null, true, -1);
			return Complete(version);
		}

		private static bool Complete(int version)
		{
			if (version == requestVersion)
			{
				requestedGame = null;
			}
			return true;
		}
	}
}
