using System;

namespace HsMod
{


	internal static class LocalPetSnapshot
	{
		internal static NetCache.PetInfo CreateInfo(NetCache.PetInfo original, int petId, int maxLevel, bool favorite)
		{
			NetCache.PetInfo val = new NetCache.PetInfo
			{
				PetId = petId,
				CurrentLevel = Math.Max((original != null) ? original.CurrentLevel : 0, maxLevel),
				CurrentXp = ((original != null) ? original.CurrentXp : 0),
				IsHsFavorite = favorite,
				IsBgFavorite = favorite
			};
			if (original != null && original.VariantIDs != null)
			{
				val.VariantIDs.UnionWith(original.VariantIDs);
			}
			return val;
		}

		internal static NetCache.PetVariantInfo CreateVariant(int variantId, bool favorite)
		{
			return new NetCache.PetVariantInfo
			{
				PetVariantId = variantId,
				IsHsFavorite = favorite,
				IsBgFavorite = favorite
			};
		}
	}
}
