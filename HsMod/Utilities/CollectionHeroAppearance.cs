namespace HsMod
{


	internal sealed class CollectionHeroAppearance
	{
		public int CardId { get; set; }

		public int Premium { get; set; }

		internal CollectionHeroAppearance Copy()
		{
			return new CollectionHeroAppearance
			{
				CardId = CardId,
				Premium = Premium
			};
		}
	}
}
