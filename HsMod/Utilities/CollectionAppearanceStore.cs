using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace HsMod
{


	internal sealed class CollectionAppearanceStore
	{
		internal readonly string Path;

		internal readonly CollectionAppearanceData Data;

		internal CollectionAppearanceStore(string path)
		{
			Path = path;
			Data = (File.Exists(path) ? JsonConvert.DeserializeObject<CollectionAppearanceData>(File.ReadAllText(path)) : new CollectionAppearanceData());
			if (Data == null)
			{
				throw new InvalidDataException("收藏外观配置为空");
			}
			if (Data.Heroes == null)
			{
				Data.Heroes = new Dictionary<int, List<CollectionHeroAppearance>>();
			}
			if (Data.Decks == null)
			{
				Data.Decks = new Dictionary<long, CollectionDeckAppearance>();
			}
		}

		internal void Save()
		{
			WriteUtf8Atomic(Path, JsonConvert.SerializeObject((object)Data, (Formatting)1) + "\r\n");
		}

		internal static List<int> DistinctIds(IEnumerable<int> ids, int minimum)
		{
			if (ids != null)
			{
				return ids.Where((int id) => id >= minimum).Distinct().ToList();
			}
			return new List<int>();
		}

		internal static bool SetFavorite(List<int> favorites, int id, bool favorite)
		{
			if (favorite)
			{
				if (favorites.Contains(id))
				{
					return false;
				}
				favorites.Add(id);
				return true;
			}
			if (favorites.Count > 1)
			{
				return favorites.Remove(id);
			}
			return false;
		}

		internal static bool SetFavorite(List<CollectionHeroAppearance> favorites, CollectionHeroAppearance hero, bool favorite)
		{
			int num = favorites.FindIndex((CollectionHeroAppearance item) => item.CardId == hero.CardId);
			if (favorite)
			{
				if (num < 0)
				{
					favorites.Add(hero.Copy());
				}
				else
				{
					if (favorites[num].Premium == hero.Premium)
					{
						return false;
					}
					favorites[num] = hero.Copy();
				}
				return true;
			}
			if (num < 0 || favorites.Count <= 1)
			{
				return false;
			}
			favorites.RemoveAt(num);
			return true;
		}

		internal static T Choose<T>(IList<T> candidates, Random random)
		{
			if (candidates == null || candidates.Count == 0)
			{
				return default(T);
			}
			if (candidates.Count == 1)
			{
				return candidates[0];
			}
			lock (random)
			{
				return candidates[random.Next(candidates.Count)];
			}
		}
		//原子写：先写临时文件再替换，避免写入中途失败损坏配置
		private static void WriteUtf8Atomic(string path, string content)
		{
			string temp = path + ".tmp";
			File.WriteAllText(temp, content, new System.Text.UTF8Encoding(false));
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			File.Move(temp, path);
		}

	}
}
