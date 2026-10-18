using System.Collections;
using UnityEngine;

/// <summary>
/// Fades a looping disaster sound out instead of cutting it dead. Without this
/// an earthquake or flood that ends abruptly sounds like the audio broke, or
/// like the next disaster stomped on it.
/// </summary>
public static class DisasterAudioFade
{
    private const float DefaultSeconds = 0.5f;

    public static IEnumerator FadeOutAndStop(AudioSource source)
    {
        return FadeOutAndStop(source, DefaultSeconds);
    }

    public static IEnumerator FadeOutAndStop(AudioSource source, float seconds)
    {
        if (source == null)
            yield break;

        float startVolume = source.volume;
        float fade = Mathf.Max(0.05f, seconds);
        float elapsed = 0f;

        while (elapsed < fade && source != null && source.isPlaying)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / fade);
            yield return null;
        }

        if (source == null)
            yield break;

        source.Stop();
        source.loop = false;
        source.clip = null;
        source.volume = startVolume;
    }

    /// <summary>Stops it straight away when it is not playing anyway.</summary>
    public static void StopNow(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.loop = false;
        source.clip = null;
    }
}
