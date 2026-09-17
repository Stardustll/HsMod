namespace HsMod
{


	internal static class FatalErrorExitGuard
	{
		internal const float ExitAfterSeconds = 10f;

		internal static bool Decide(bool exitDialogShown, float shownSeconds)
		{
			if (exitDialogShown)
			{
				return shownSeconds >= 10f;
			}
			return false;
		}
	}
}
