namespace HsMod
{


	internal sealed class CollectionPetAppearance
	{
		public int? PetId { get; set; }

		public int? VariantId { get; set; }

		public bool FavoritesOnly { get; set; } = true;
	}
}
