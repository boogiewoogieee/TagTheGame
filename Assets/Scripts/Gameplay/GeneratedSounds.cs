using System;
using UnityEngine;

// Short game sounds made in code, so beeps, ticks and thumps need no audio assets.
// Every component that uses them also takes a real clip that wins when one is set.
public static class GeneratedSounds
{
    private const int Rate = 44100;

    // A sine beep sliding from one pitch to another, with a fast attack and a falling tail.
    public static AudioClip Tone(string name, float fromHz, float toHz, float seconds)
    {
        int count = Mathf.CeilToInt(Rate * seconds);
        float[] data = new float[count];
        double phase = 0.0;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            phase += 2.0 * Math.PI * Mathf.Lerp(fromHz, toHz, t) / Rate;
            float attack = Mathf.Min(1f, i / (0.004f * Rate));
            float tail = Mathf.Pow(1f - t, 1.5f);
            data[i] = (float)Math.Sin(phase) * attack * tail;
        }

        return Make(name, data);
    }

    // A dry clock tick: a very short high click with a little noise, so it cuts through music.
    public static AudioClip Tick(string name, float hz)
    {
        int count = Mathf.CeilToInt(Rate * 0.035f);
        float[] data = new float[count];
        var noise = new System.Random(1);
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float decay = Mathf.Exp(-t * 7f);
            float click = (float)Math.Sin(2.0 * Math.PI * hz * i / Rate);
            float hiss = (float)(noise.NextDouble() * 2.0 - 1.0) * 0.35f;
            data[i] = (click + hiss) * decay * 0.8f;
        }

        return Make(name, data);
    }

    // A heartbeat "lub-dub": two low thumps, the second one softer.
    public static AudioClip Heartbeat(string name)
    {
        const float thumpSeconds = 0.12f;
        const float gapSeconds = 0.16f;
        int count = Mathf.CeilToInt(Rate * (gapSeconds + thumpSeconds));
        float[] data = new float[count];
        AddThump(data, 0, thumpSeconds, 62f, 1f);
        AddThump(data, Mathf.RoundToInt(Rate * gapSeconds), thumpSeconds, 54f, 0.7f);
        return Make(name, data);
    }

    // A rush of air: noise through a low-pass filter that opens and closes again as it swells and fades.
    public static AudioClip Whoosh(string name, float seconds)
    {
        int count = Mathf.CeilToInt(Rate * seconds);
        float[] data = new float[count];
        var noise = new System.Random(7);
        float low = 0f;
        float band = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float swell = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.6f));

            // Two one-pole filters: the difference keeps the middle of the noise, which sounds like wind.
            float cutoffHz = Mathf.Lerp(300f, 2600f, swell);
            float k = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / Rate);
            float white = (float)(noise.NextDouble() * 2.0 - 1.0);
            low += (white - low) * k;
            band += (low - band) * (k * 0.25f);
            data[i] = (low - band) * swell * 2.2f;
        }

        return Make(name, data);
    }

    private static void AddThump(float[] data, int start, float seconds, float hz, float gain)
    {
        int count = Mathf.CeilToInt(Rate * seconds);
        double phase = 0.0;
        for (int i = 0; i < count && start + i < data.Length; i++)
        {
            float t = (float)i / count;
            float attack = Mathf.Min(1f, i / (0.006f * Rate));
            float tail = Mathf.Pow(1f - t, 2f);

            // The pitch drops a little during the thump, which makes it feel like a beat rather than a tone.
            phase += 2.0 * Math.PI * hz * Mathf.Lerp(1.3f, 0.8f, t) / Rate;
            data[start + i] += (float)Math.Sin(phase) * attack * tail * gain;
        }
    }

    private static AudioClip Make(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
