using UnityEngine;

/// <summary>
/// ゲーム状態などの条件を満たす時のみ OneShot 再生するゲート。
/// </summary>
public static class SfxOneShotGate
{
    public static void PlayIfAllowed(GameDirector gd, AudioSource src, AudioClip clip, float volume)
    {
        if (gd == null || gd.State != GameDirector.GameState.Playing) return;
        if (src != null && clip != null)
        {
            src.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}

