using System;

namespace HsMod
{


	internal sealed class HeroFormState
	{
		internal const string DeathwingHeroCardId = "CATA_190h";

		internal static readonly HeroFormDefinition[] Definitions = new HeroFormDefinition[8]
		{
			new HeroFormDefinition("HERO_01az", "HERO_01ba", HeroFormTrigger.OwnerHealth),
			new HeroFormDefinition("HERO_03bg", "HERO_03bg_meta", HeroFormTrigger.OpponentHealth),
			new HeroFormDefinition("HERO_10am", "HERO_10as", HeroFormTrigger.OpponentHealth),
			new HeroFormDefinition("HERO_08cb", "HERO_08cc", HeroFormTrigger.OpponentHealth),
			new HeroFormDefinition("HERO_08cs", "HERO_08cs_necro", HeroFormTrigger.CosmeticAction),
			new HeroFormDefinition("HERO_11be", "HERO_11be_necro", HeroFormTrigger.CosmeticAction),
			new HeroFormDefinition("HERO_01bn", "HERO_01bn_meta", HeroFormTrigger.SignatureDeathwing, 1),
			new HeroFormDefinition("HERO_02bx", "HERO_02bx_meta", HeroFormTrigger.SignatureDeathwing, 1)
		};

		internal readonly HeroFormDefinition Definition;

		internal readonly string NativeCardId;

		internal readonly string SelectedCardId;

		internal readonly bool IsNative;

		private readonly HeroCosmeticTurnState cosmeticTurns;

		private int pendingCosmeticTurn = -1;

		internal string CurrentCardId { get; private set; }

		internal string PendingCardId { get; private set; }

		internal bool IsHealthTriggered
		{
			get
			{
				if (Definition.Trigger != HeroFormTrigger.OwnerHealth)
				{
					return Definition.Trigger == HeroFormTrigger.OpponentHealth;
				}
				return true;
			}
		}

		internal HeroFormState(string nativeCardId, string selectedCardId)
			: this(nativeCardId, selectedCardId, new HeroCosmeticTurnState())
		{
		}

		internal HeroFormState(string nativeCardId, string selectedCardId, HeroCosmeticTurnState cosmeticTurns)
		{
			this.cosmeticTurns = cosmeticTurns ?? throw new ArgumentNullException("cosmeticTurns");
			Definition = Find(selectedCardId);
			if (Definition == null)
			{
				throw new ArgumentException("Unknown hero form", "selectedCardId");
			}
			NativeCardId = nativeCardId;
			SelectedCardId = selectedCardId;
			IsNative = Definition.Contains(nativeCardId) && (selectedCardId == Definition.BaseCardId || nativeCardId == selectedCardId);
			CurrentCardId = (IsNative ? nativeCardId : selectedCardId);
		}

		internal static HeroFormDefinition Find(string cardId)
		{
			HeroFormDefinition[] definitions = Definitions;
			foreach (HeroFormDefinition heroFormDefinition in definitions)
			{
				if (heroFormDefinition.Contains(cardId))
				{
					return heroFormDefinition;
				}
			}
			return null;
		}

		internal void ObserveHealth(int ownerHealth, int opponentHealth)
		{
			if (IsNative || CurrentCardId != Definition.BaseCardId || PendingCardId != null)
			{
				return;
			}
			int num;
			if (Definition.Trigger == HeroFormTrigger.OwnerHealth)
			{
				num = ownerHealth;
			}
			else
			{
				if (Definition.Trigger != HeroFormTrigger.OpponentHealth)
				{
					return;
				}
				num = opponentHealth;
			}
			if (num > 0 && num <= 15)
			{
				PendingCardId = Definition.AlternateCardId;
			}
		}

		internal bool CanRequestCosmeticAction(int turn, bool isOwnTurn)
		{
			if (!IsNative && Definition.Trigger == HeroFormTrigger.CosmeticAction && PendingCardId == null)
			{
				return cosmeticTurns.CanUse(turn, isOwnTurn);
			}
			return false;
		}

		internal bool RequestCosmeticAction(int turn, bool isOwnTurn)
		{
			if (!CanRequestCosmeticAction(turn, isOwnTurn) || !cosmeticTurns.TryUse(turn, isOwnTurn))
			{
				return false;
			}
			pendingCosmeticTurn = turn;
			PendingCardId = ((CurrentCardId == Definition.BaseCardId) ? Definition.AlternateCardId : Definition.BaseCardId);
			return true;
		}

		internal bool CanStartTransition(int turn, bool isOwnTurn)
		{
			if (PendingCardId != null)
			{
				if (Definition.Trigger == HeroFormTrigger.CosmeticAction)
				{
					if (isOwnTurn)
					{
						return turn == pendingCosmeticTurn;
					}
					return false;
				}
				return true;
			}
			return false;
		}

		internal bool RequestDeathwing(bool isSignature)
		{
			if (IsNative || !isSignature || NativeCardId != "CATA_190h" || Definition.Trigger != HeroFormTrigger.SignatureDeathwing || PendingCardId != null)
			{
				return false;
			}
			PendingCardId = Definition.AlternateCardId;
			return true;
		}

		internal void Commit()
		{
			if (PendingCardId != null)
			{
				CurrentCardId = PendingCardId;
				PendingCardId = null;
				pendingCosmeticTurn = -1;
			}
		}

		internal void Cancel()
		{
			PendingCardId = null;
			pendingCosmeticTurn = -1;
		}

		internal bool CanReuse(string nativeCardId, string selectedCardId)
		{
			if (!IsNative && selectedCardId == SelectedCardId)
			{
				if (!(nativeCardId == NativeCardId))
				{
					return nativeCardId == CurrentCardId;
				}
				return true;
			}
			return false;
		}

		internal static bool IsEquivalentPower(string cardId, string nativePower, string selectedPower, string currentPower)
		{
			if (!string.IsNullOrEmpty(cardId))
			{
				if (!(cardId == nativePower) && !(cardId == selectedPower))
				{
					return cardId == currentPower;
				}
				return true;
			}
			return false;
		}
	}
}
