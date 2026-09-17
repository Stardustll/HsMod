namespace HsMod
{


	internal sealed class HeroFormDefinition
	{
		internal readonly string BaseCardId;

		internal readonly string AlternateCardId;

		internal readonly HeroFormTrigger Trigger;

		internal readonly int SubSpellIndex;

		internal HeroFormDefinition(string baseCardId, string alternateCardId, HeroFormTrigger trigger, int subSpellIndex = 0)
		{
			BaseCardId = baseCardId;
			AlternateCardId = alternateCardId;
			Trigger = trigger;
			SubSpellIndex = subSpellIndex;
		}

		internal bool Contains(string cardId)
		{
			if (!(cardId == BaseCardId))
			{
				return cardId == AlternateCardId;
			}
			return true;
		}
	}
}
