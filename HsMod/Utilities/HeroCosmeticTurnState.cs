namespace HsMod
{


	internal sealed class HeroCosmeticTurnState
	{
		private int usedTurn = -1;

		internal bool CanUse(int turn, bool isOwnTurn)
		{
			if (isOwnTurn && turn > 0)
			{
				return usedTurn != turn;
			}
			return false;
		}

		internal bool TryUse(int turn, bool isOwnTurn)
		{
			if (!CanUse(turn, isOwnTurn))
			{
				return false;
			}
			usedTurn = turn;
			return true;
		}
	}
}
