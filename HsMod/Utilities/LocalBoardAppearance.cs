using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HsMod
{
	//棋盘外观（精简版）：当前仅提供英雄形态所需的"死亡之翼废墟"场景标记。
	//完整的棋盘底图/角落替换见原 DLL 的 LocalBoardAppearance，本轮未移植。
	internal static class LocalBoardAppearance
	{
		private static readonly ConditionalWeakTable<GameState, HashSet<Player.Side>> DeathwingRuins =
			new ConditionalWeakTable<GameState, HashSet<Player.Side>>();

		private static bool stopped;

		private static Player.Side Opposite(Player.Side side)
		{
			return side == Player.Side.FRIENDLY ? Player.Side.OPPOSING : Player.Side.FRIENDLY;
		}

		//本局是否已标记过该玩家的死亡之翼废墟（对方侧）
		internal static bool HasDeathwingRuins(Player source)
		{
			GameState gameState = GameState.Get();
			if (gameState == null || source == null)
			{
				return false;
			}
			Player.Side opposite = Opposite(source.GetSide());
			Player playerBySide = gameState.GetPlayerBySide(opposite);
			if (playerBySide != null && playerBySide.GetTag((GAME_TAG)3564) == 10)
			{
				return true;
			}
			HashSet<Player.Side> marked;
			return DeathwingRuins.TryGetValue(gameState, out marked) && marked.Contains(opposite);
		}

		//标记该玩家已触发死亡之翼废墟，并刷新棋盘角落
		internal static void MarkDeathwingRuins(Player source)
		{
			GameState gameState = GameState.Get();
			GameMgr gameMgr = GameMgr.Get();
			if (stopped || !PluginConfig.CollectionVisualsEnabled
				|| gameState == null || gameMgr == null
				|| gameMgr.IsBattlegrounds() || gameMgr.IsSpectator()
				|| source == null || gameState.GetPlayerBySide(source.GetSide()) != source)
			{
				return;
			}
			HashSet<Player.Side> marked = DeathwingRuins.GetValue(gameState, delegate { return new HashSet<Player.Side>(); });
			if (marked.Add(Opposite(source.GetSide())))
			{
				gameState.UpdateCornerReplacements();
			}
		}

		internal static void Reset()
		{
			stopped = false;
		}
	}
}
