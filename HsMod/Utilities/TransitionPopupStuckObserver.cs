namespace HsMod
{


	internal sealed class TransitionPopupStuckObserver
	{
		private float seenSince = -1f;

		private float cancelAt = -1f;

		internal void Reset()
		{
			seenSince = -1f;
			cancelAt = -1f;
		}

		internal TransitionPopupGuardAction Observe(float now, bool shown, bool finding)
		{
			if (!shown | finding)
			{
				Reset();
				return TransitionPopupGuardAction.None;
			}
			if (seenSince < 0f)
			{
				seenSince = now;
				cancelAt = -1f;
				return TransitionPopupGuardAction.None;
			}
			float secondsSinceCancel = ((cancelAt < 0f) ? (-1f) : (now - cancelAt));
			TransitionPopupGuardAction transitionPopupGuardAction = TransitionPopupStuckGuard.Decide(shown, finding, now - seenSince, secondsSinceCancel);
			switch (transitionPopupGuardAction)
			{
			case TransitionPopupGuardAction.Cancel:
				cancelAt = now;
				break;
			case TransitionPopupGuardAction.ForceHide:
				Reset();
				break;
			}
			return transitionPopupGuardAction;
		}
	}
}
