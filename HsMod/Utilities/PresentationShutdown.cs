using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace HsMod
{


	internal static class PresentationShutdown
	{
		internal static bool IsQuitting { get; private set; }

		internal static void Initialize()
		{
			IsQuitting = false;
		}

		internal static void BeginQuit()
		{
			IsQuitting = true;
			DetachRemainingTargets();
		}

		internal static void DetachRemainingTargets()
		{
			if (!IsQuitting)
			{
				return;
			}
			try
			{
				List<string> list = new List<string>();
				Camera[] array = Resources.FindObjectsOfTypeAll<Camera>();
				foreach (Camera val in array)
				{
					if (val == null)
					{
						continue;
					}
					try
					{
						RenderTexture targetTexture = val.targetTexture;
						if (!(targetTexture == null))
						{
							string name = (val).name;
							if (val != null && targetTexture != null && val.targetTexture == targetTexture)
							{
								val.targetTexture = null;
							}
							list.Add(name);
						}
					}
					catch (Exception ex)
					{
						Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "退出时解除相机渲染目标失败: " + ex.Message);
					}
				}
				if (list.Count > 0)
				{
					Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "退出前解除渲染目标: " + string.Join(", ", list));
				}
			}
			catch (Exception ex2)
			{
				try
				{
					Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "退出渲染清理失败: " + ex2.Message);
				}
				catch
				{
				}
			}
		}
	}
}
