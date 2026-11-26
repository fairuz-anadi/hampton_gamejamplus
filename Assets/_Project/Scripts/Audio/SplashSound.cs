using UnityEngine;

/// <summary>
/// A body hitting the water, made in code as there's no splash in the sound bank: a thump, then a burst of noise
/// that darkens and dies away, and a few late drips. Used when the Captain throws a player overboard (AudioManager,
/// on the ship) and in the Overboard ending film. Swap for a recorded splash in AudioBank when there is one.
/// </summary>
public static class SplashSound
{
    static AudioClip clip;

    public static AudioClip Clip => clip != null ? clip : (clip = Make());

    static AudioClip Make()
    {
        const int rate = 22050;
        int n = (int)(rate * 1.6f);
        var data = new float[n];
        var rng = new System.Random(17);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            // The noise is low-passed harder as it fades, so the hiss turns into a wash.
            float cut = Mathf.Lerp(0.55f, 0.06f, Mathf.Clamp01(t / 1.1f));
            lp += (noise - lp) * cut;
            lp2 += (lp - lp2) * cut;
            float body = lp2 * Mathf.Exp(-t * 3.2f) * Mathf.Clamp01(t / 0.012f);
            float thump = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 22f);
            float drips = 0f;
            foreach (float at in new[] { 0.62f, 0.81f, 1.04f, 1.22f })
            {
                float d = t - at;
                if (d > 0f && d < 0.06f) drips += Mathf.Sin(2f * Mathf.PI * (900f + at * 500f) * d * (1f + d * 8f)) * Mathf.Exp(-d * 70f) * 0.25f;
            }
            data[i] = Mathf.Clamp(body * 2.2f + thump * 0.7f + drips, -1f, 1f);
        }
        var made = AudioClip.Create("Splash", n, 1, rate, false);
        made.SetData(data, 0);
        return made;
    }
}
