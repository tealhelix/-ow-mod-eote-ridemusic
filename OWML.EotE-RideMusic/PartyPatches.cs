// using HarmonyLib;

namespace RideMusic
{
	// [HarmonyPatch(typeof(PartyMusicController))]
	// [HarmonyPatchAll]
	public static class PartyPatches
	{
		private static bool Start(PartyMusicController __instance)
		{
			// just for safety
			__instance.enabled = false;

			// this tells Harmony to bypass the original implementation
			return false;
		}

		public static bool FadeIn(float duration)
		{
			// This gets called very early when the player is well out of range, and then multiple times after.
			// So, it ends up this has the same effect as a Play() on entering the area.
			s_FaderAudioSource.FadeTo(1.0f, 0.8f);
			return false;
		}

		public static bool FadeOut(float duration)
		{
			// Gets called too early, wait for a StaggerStop instead.
			// FadeTo(0f, duration);
			return false;
		}

		public static bool StaggerStop(PartyMusicController __instance)
		{
			// Oh no you've wrecked their party! *record needle yank*
			s_FaderAudioSource.FadeTo(0f, 0.7f);
			return false;
		}

		private static bool Update(PartyMusicController __instance)
		{
			// The Harmony docs say it has trouble with base class methods, but nothing about the fields and this access is through "this" not "base".
			// Haven't checked for sure that base.enabled is getting false, though.
			__instance.enabled = false;
			return false;
		}

		public static FaderAudioSource s_FaderAudioSource = new FaderAudioSource();
    }
}
