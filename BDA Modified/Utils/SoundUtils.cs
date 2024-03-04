using System.Collections.Generic;
using UnityEngine;

using BDArmory.Settings;

namespace BDArmory.Utils
{
    /// <summary>
    /// Tools to minimise GC and local copies of audioclips.
    /// </summary>
    public static class SoundUtils
    {
        static Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>(); // Cache audio clips so that they're not fetched from the GameDatabase every time. Really, the GameDatabase should be doing this!

        /// <summary>
        /// Get the requested audioclip from the cache.
        /// If it's not in the cache, then load the audioclip from the GameDatabase and cache it for future use.
        /// </summary>
        /// <param name="soundPath">The path to a valid audioclip.</param>
        /// <param name="allowMissing">Don't log an error if the sound file doesn't exist, just log it instead.</param>
        /// <returns>The AudioClip.</returns>
        public static AudioClip GetAudioClip(string soundPath, bool allowMissing = false)
        {
            if (!audioClips.TryGetValue(soundPath, out AudioClip audioClip) || audioClip is null)
            {
                audioClip = GameDatabase.Instance.GetAudioClip(soundPath);
                if (audioClip is null)
                {
                    if (allowMissing) Debug.Log($"[BDArmory.SoundUtils]: {soundPath} did not give a valid audioclip.");
                    else Debug.LogError($"[BDArmory.SoundUtils]: {soundPath} did not give a valid audioclip.");
                }
                else if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.SoundUtils]: Adding audioclip {soundPath} to the cache.");
                audioClips[soundPath] = audioClip;
            }
            return audioClip;
        }

        /// <summary>
        /// Reset the cache of audioclips.
        /// </summary>
        public static void ClearAudioCache() => audioClips.Clear(); // Maybe someone has a reason for doing this to reload sounds dynamically? They'd need a way to refresh the GameDatabase too though.

        /// <summary>
        /// Check whether the soundPath is in the audioclip cache or not and that the audioclip is not null if it is.
        /// </summary>
        /// <param name="soundPath"></param>
        /// <returns></returns>
        public static bool IsCached(string soundPath) => audioClips.ContainsKey(soundPath) && audioClips[soundPath] is not null;

        /// <summary>
        /// Helper extension to play a one-shot audio clip from the cache based on the sound path.
        /// Note: this is equivalent to making a local reference to the AudioClip with SoundUtils.GetAudioClip and playing it via PlayOneShot.
        /// </summary>
        /// <param name="audioSource">The AudioSource.</param>
        /// <param name="soundPath">A valid audioclip path.</param>
        public static void PlayClipOnce(this AudioSource audioSource, string soundPath) => audioSource.PlayOneShot(GetAudioClip(soundPath));
    }
}