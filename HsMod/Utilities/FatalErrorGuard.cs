using System;
using BepInEx.Logging;
using UnityEngine;

namespace HsMod
{
	//致命错误退出弹窗守卫：弹窗持续 10 秒仍无人处理时自动确认关闭，避免无人值守时卡在弹窗
	internal static class FatalErrorGuard
	{
		private static float s_errorSince = -1f;

		private static FatalErrorDialog s_exitDialog;

		private static void Reset()
		{
			s_errorSince = -1f;
			s_exitDialog = null;
		}

		internal static void Tick()
		{
			try
			{
				if (!PluginConfig.isFatalErrorAutoConfirmEnable.Value || !PluginConfig.isPluginEnable.Value)
				{
					Reset();
					return;
				}
				FatalErrorMgr fatalErrorMgr = FatalErrorMgr.Get();
				if (fatalErrorMgr == null || !fatalErrorMgr.HasError())
				{
					Reset();
					return;
				}
				FatalErrorDialog dialog = UnityEngine.Object.FindFirstObjectByType<FatalErrorDialog>();
				if (dialog == null || !dialog.isActiveAndEnabled)
				{
					Reset();
					return;
				}
				if (s_exitDialog != dialog || s_errorSince < 0f)
				{
					s_exitDialog = dialog;
					s_errorSince = Time.realtimeSinceStartup;
					return;
				}
				if (!FatalErrorExitGuard.Decide(exitDialogShown: true, Time.realtimeSinceStartup - s_errorSince))
				{
					return;
				}
				Utils.MyLogger(LogLevel.Warning, "致命错误退出弹窗持续超时，自动确认关闭");
				Reset();
				try
				{
					fatalErrorMgr.NotifyExitPressed();
				}
				catch (Exception ex)
				{
					Utils.MyLogger(LogLevel.Warning, "FatalErrorGuard: 致命错误退出异常: " + ex.Message);
				}
			}
			catch (Exception ex2)
			{
				Reset();
				try
				{
					Utils.MyLogger(LogLevel.Warning, "FatalErrorGuard: 致命错误检查异常: " + ex2.Message);
				}
				catch (Exception)
				{
				}
			}
		}
	}
}
