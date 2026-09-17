namespace HsMod
{


	internal static class TransitionPopupStuckGuard
	{
		internal const float CancelAfterSeconds = 30f;

		internal const float ForceHideAfterCancelSeconds = 10f;

		internal static TransitionPopupGuardAction Decide(bool shown, bool finding, float shownSeconds, float secondsSinceCancel)
		{
			if (!shown | finding)
			{
				return TransitionPopupGuardAction.None;
			}
			if (shownSeconds < 30f)
			{
				return TransitionPopupGuardAction.None;
			}
			if (secondsSinceCancel < 0f)
			{
				return TransitionPopupGuardAction.Cancel;
			}
			if (secondsSinceCancel < 10f)
			{
				return TransitionPopupGuardAction.None;
			}
			return TransitionPopupGuardAction.ForceHide;
		}
	}
}
