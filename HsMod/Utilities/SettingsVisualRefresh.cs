using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Hearthstone.Core;

namespace HsMod
{


	internal static class SettingsVisualRefresh
	{
		private static readonly HashSet<Func<bool>> Pending = new HashSet<Func<bool>>();

		private static bool pumping;

		private static bool scheduled;

		internal static void Request(Func<bool> refresh)
		{
			Pending.Add(refresh);
		}

		internal static void Pump()
		{
			if (pumping)
			{
				return;
			}
			if (PresentationShutdown.IsQuitting)
			{
				Pending.Clear();
			}
			else
			{
				if (Pending.Count == 0)
				{
					return;
				}
				List<Func<bool>> list = new List<Func<bool>>(Pending);
				Pending.Clear();
				pumping = true;
				try
				{
					foreach (Func<bool> item in list)
					{
						try
						{
							if (!item())
							{
								Pending.Add(item);
							}
						}
						catch (Exception ex)
						{
							Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "刷新设置显示失败: " + ex.Message);
						}
					}
				}
				finally
				{
					pumping = false;
				}
				if (Pending.Count == 0 || scheduled)
				{
					return;
				}
				scheduled = true;
				try
				{
					scheduled = Processor.ScheduleCallback(0.1f, true, delegate
					{
						scheduled = false;
						Pump();
					}, null);
				}
				catch (Exception ex2)
				{
					scheduled = false;
					Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "安排设置显示恢复失败: " + ex2.Message);
				}
			}
		}
	}
}
