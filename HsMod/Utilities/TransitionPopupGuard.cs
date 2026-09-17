using System;
using BepInEx.Logging;
using UnityEngine;

namespace HsMod
{


	internal static class TransitionPopupGuard
	{
		private const float CheckIntervalSeconds = 2f;

		private static float s_nextCheck;

		internal static void Tick(TransitionPopupStuckObserver observer, string tag)
		{
			try
			{
				if (observer == null)
				{
					return;
				}
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				if (realtimeSinceStartup < s_nextCheck)
				{
					return;
				}
				s_nextCheck = realtimeSinceStartup + 2f;
				SceneMgr val = SceneMgr.Get();
				GameMgr val2 = GameMgr.Get();
				if (val == null || val.IsTransitioning() || val2 == null)
				{
					return;
				}
				if ((int)val.GetMode() == 4)
				{
					observer.Reset();
					return;
				}
				TransitionPopup val3 = null;
				try
				{
					TransitionPopup[] array = UnityEngine.Object.FindObjectsByType<TransitionPopup>((FindObjectsSortMode)0);
					if (array != null)
					{
						TransitionPopup[] array2 = array;
						foreach (TransitionPopup val4 in array2)
						{
							if (!(val4 == null))
							{
								bool flag = false;
								try
								{
									flag = val4.IsShown();
								}
								catch (Exception)
								{
									continue;
								}
								if (flag)
								{
									val3 = val4;
									break;
								}
							}
						}
					}
				}
				catch (Exception)
				{
					return;
				}
				bool flag2 = false;
				try
				{
					flag2 = val2.IsFindingGame();
				}
				catch (Exception)
				{
					return;
				}
				TransitionPopupGuardAction transitionPopupGuardAction = observer.Observe(realtimeSinceStartup, val3 != null, flag2);
				if (transitionPopupGuardAction == TransitionPopupGuardAction.None || val3 == null)
				{
					return;
				}
				if (transitionPopupGuardAction == TransitionPopupGuardAction.Cancel)
				{
					Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, tag + ": 找寻对手弹窗残留（显示但未在匹配），尝试正常取消");
					try
					{
						val3.Cancel();
						return;
					}
					catch (Exception ex4)
					{
						Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, tag + ": 弹窗取消异常: " + ex4.Message);
						return;
					}
				}
				Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, tag + ": 找寻对手弹窗取消后仍残留，强制隐藏");
				try
				{
					val3.Hide();
				}
				catch (Exception ex5)
				{
					Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, tag + ": 弹窗隐藏异常: " + ex5.Message);
				}
			}
			catch (Exception ex6)
			{
				try
				{
					Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, tag + ": 弹窗卡死检查异常: " + ex6.Message);
				}
				catch (Exception)
				{
				}
			}
		}
	}
}
