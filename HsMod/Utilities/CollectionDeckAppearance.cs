namespace HsMod
{


	internal sealed class CollectionDeckAppearance
	{
		public CollectionHeroAppearance Hero { get; set; }

		public int? CardBackId { get; set; }

		public int? CoinId { get; set; }

		public CollectionPetAppearance Pet { get; set; }

		public bool FavoriteHeroesOnly { get; set; } = true;

		public bool FavoriteCoinsOnly { get; set; } = true;
	}
}
