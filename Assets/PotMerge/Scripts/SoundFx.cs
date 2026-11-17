using UnityEngine;

namespace PotMerge
{
    /// <summary>Procedurally generated sound effects (no audio files). Respects <see cref="PlayerData.SoundOn"/>.</summary>
    public static class SoundFx
    {
        const int Rate = 44100;
        static GameObject host;
        static AudioSource[] voices;
        static int nextVoice;
        static AudioClip pop, merge, swap, coin, error, tap, unlock;

        public static void Pop() => Play(pop, 1f + Random.Range(-0.05f, 0.05f), 0.55f);
        public static void Swap() => Play(swap, 1f, 0.5f);
        public static void Coin() => Play(coin, 1f, 0.45f);
        public static void Error() => Play(error, 1f, 0.5f);
        public static void Tap() => Play(tap, 1f, 0.5f);
        public static void Unlock() => Play(unlock, 1f, 0.6f);
        /// <summary>Merge chime, pitched up a little with each level.</summary>
        public static void Merge(int level) => Play(merge, Mathf.Pow(2f, (Mathf.Clamp(level, 1, 11) - 2) / 12f * 1.5f), 0.6f);

        static void Play(AudioClip clip, float pitch, float volume)
        {
            if (!PlayerData.SoundOn || !Application.isPlaying) return;
            Ensure();
            var src = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            src.pitch = pitch;
            src.PlayOneShot(clip == null ? tap : clip, volume);
        }

        static void Ensure()
        {
            if (host != null) return;
            host = new GameObject("SoundFx");
            Object.DontDestroyOnLoad(host);
            voices = new AudioSource[6];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = host.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }

            pop = Make("pop", 0.12f, (t, d) => Sine(Lerp(520f, 900f, t / d), t) * Env(t, d, 0.005f));
            swap = Make("swap", 0.09f, (t, d) => Tri(Lerp(700f, 420f, t / d), t) * Env(t, d, 0.004f) * 0.8f);
            tap = Make("tap", 0.05f, (t, d) => Sine(1200f, t) * Env(t, d, 0.002f) * 0.7f);
            coin = Make("coin", 0.28f, (t, d) =>
                (t < 0.07f ? Sine(988f, t) : Sine(1319f, t)) * Env(t, d, 0.003f) * 0.7f);
            error = Make("error", 0.22f, (t, d) => Square(150f, t) * Env(t, d, 0.01f) * 0.35f);
            merge = Make("merge", 0.42f, (t, d) =>
            {
                // three-note rising chime (C E G) with a soft shimmer
                float n = t < 0.07f ? 523.25f : t < 0.14f ? 659.25f : 783.99f;
                float local = t < 0.07f ? t : t < 0.14f ? t - 0.07f : t - 0.14f;
                float e = Mathf.Exp(-local * 9f) * Env(t, d, 0.003f);
                return (Sine(n, t) * 0.7f + Sine(n * 2f, t) * 0.2f + Sine(n * 3.01f, t) * 0.08f) * e;
            });
            unlock = Make("unlock", 0.7f, (t, d) =>
            {
                float[] notes = { 659.25f, 783.99f, 1046.5f, 1318.5f };
                int i = Mathf.Min((int)(t / 0.1f), notes.Length - 1);
                float local = t - i * 0.1f;
                return (Sine(notes[i], t) * 0.6f + Sine(notes[i] * 2f, t) * 0.2f) * Mathf.Exp(-local * 6f) * Env(t, d, 0.003f);
            });
        }

        delegate float Wave(float t, float duration);

        static AudioClip Make(string name, float duration, Wave wave)
        {
            int n = Mathf.CeilToInt(duration * Rate);
            var samples = new float[n];
            for (int i = 0; i < n; i++) samples[i] = Mathf.Clamp(wave(i / (float)Rate, duration), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Tri(float f, float t) => 2f * Mathf.Abs(2f * (t * f - Mathf.Floor(t * f + 0.5f))) - 1f;
        static float Square(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t) >= 0f ? 1f : -1f;
        static float Lerp(float a, float b, float k) => a + (b - a) * Mathf.Clamp01(k);
        /// <summary>Short attack, then fade to zero at the end.</summary>
        static float Env(float t, float d, float attack) => Mathf.Clamp01(t / attack) * Mathf.Clamp01((d - t) / (d * 0.6f));
    }
}
