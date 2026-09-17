using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace HsMod
{


	internal static class DeferredPresentationCallbacks
	{
		private static readonly List<Action> Pending = new List<Action>();

		internal static bool Enabled { get; private set; }

		internal static void Start()
		{
			Enabled = true;
		}

		internal static bool TryEnqueue(Action callback)
		{
			if (!Enabled)
			{
				return false;
			}
			if (callback != null)
			{
				Pending.Add(callback);
			}
			return true;
		}

		internal static void Pump()
		{
			int count = Pending.Count;
			while (count-- > 0 && Pending.Count > 0)
			{
				Action action = Pending[0];
				Pending.RemoveAt(0);
				try
				{
					action();
				}
				catch (Exception ex)
				{
					Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "完成奖励展示回调失败: " + ex);
				}
			}
		}

		internal static void Stop(bool complete)
		{
			Enabled = false;
			if (complete)
			{
				Pump();
			}
			else
			{
				Pending.Clear();
			}
		}
	}
}
