using System.Collections.Generic;

namespace HsMod
{


	internal sealed class CollectionAppearanceData
	{
		public Dictionary<int, List<CollectionHeroAppearance>> Heroes { get; set; } = new Dictionary<int, List<CollectionHeroAppearance>>();

		public List<int> CardBacks { get; set; }

		public List<int> Coins { get; set; }

		public Dictionary<long, CollectionDeckAppearance> Decks { get; set; } = new Dictionary<long, CollectionDeckAppearance>();
	}
}
