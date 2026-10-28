using UnityEngine;

// How urgent the fuse feels at a given moment, as plain numbers. The bomb's ticking and
// sparks and the holder's vignette and heartbeat all read it, so they rise together, and
// every machine works it out from the synchronized fuse time without sending anything.
public static class FuseRules
{
    // 0 while the fuse is calm, rising evenly to 1 when the bomb explodes.
    public static float Danger(float remainingSeconds, float dangerSeconds)
    {
        if (dangerSeconds <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(1f - remainingSeconds / dangerSeconds);
    }

    // Something per second (ticks, heartbeats) that runs at the calm rate and speeds up to
    // the danger rate over the last seconds of the fuse.
    public static float Rate(float remainingSeconds, float dangerSeconds, float calmRate, float dangerRate)
    {
        return Mathf.Lerp(calmRate, dangerRate, Danger(remainingSeconds, dangerSeconds));
    }

    // Whether the shown whole-second timer is in the last, red seconds. It shows the
    // remaining time rounded up, so "5" is the first danger second when the danger time is 5 s.
    public static bool IsDangerSecond(int shownSeconds, float dangerSeconds)
    {
        return shownSeconds > 0 && shownSeconds <= dangerSeconds;
    }
}
