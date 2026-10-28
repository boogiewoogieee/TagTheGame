using UnityEngine;

// The timing behind the pass feedback, kept apart from the component so it can be tested:
// whether a confirmed pass was already shown at the press, and the shapes of the bomb pop
// and the light flash.
public static class PassFeedbackRules
{
    // A pass the host confirms for the local passer was already shown at the press when the
    // press is this recent; otherwise (a slow answer, or no press seen here) it is shown now.
    public static bool AlreadyShownAtPress(ulong passerId, ulong localId, double pressedAt, double now, double window)
    {
        return passerId == localId && now - pressedAt >= 0 && now - pressedAt < window;
    }

    // Bomb size during the pop: snaps up to the peak in the first fifth, then eases back to 1.
    public static float PopScale(float elapsed, float seconds, float peak)
    {
        if (seconds <= 0f || elapsed < 0f || elapsed >= seconds)
        {
            return 1f;
        }

        float t = elapsed / seconds;
        if (t < 0.2f)
        {
            return Mathf.Lerp(1f, peak, t / 0.2f);
        }

        float back = (t - 0.2f) / 0.8f;
        return Mathf.Lerp(peak, 1f, 1f - (1f - back) * (1f - back));
    }

    // Light intensity during the flash: full at once, fading out fast.
    public static float FlashIntensity(float elapsed, float seconds, float peak)
    {
        if (seconds <= 0f || elapsed < 0f || elapsed >= seconds)
        {
            return 0f;
        }

        float left = 1f - elapsed / seconds;
        return peak * left * left;
    }
}
