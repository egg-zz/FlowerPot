using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static PotMerge.UIKit;

namespace PotMerge
{
    /// <summary>Puffy clouds that drift across the screen while it changes (home and town).</summary>
    public partial class MenuUI
    {
        const float CloudSeconds = 1.5f;

        VisualElement cloudLayer, cloudBank;
        readonly List<(VisualElement puff, float phase, float size)> puffs = new List<(VisualElement, float, float)>();
        float cloudT = -1f;
        int cloudDir;
        bool cloudSwapped;
        Action cloudSwap;
        Action<float> cloudPan;

        public bool CloudsMoving => cloudT >= 0f;

        VisualElement BuildClouds()
        {
            var layer = new VisualElement(); // blocks taps while the clouds pass
            Stretch(layer);
            layer.style.overflow = Overflow.Hidden;
            layer.style.display = DisplayStyle.None;

            // a solid middle as wide as the screen, with puffs all around and over it
            cloudBank = new VisualElement { pickingMode = PickingMode.Ignore };
            Stretch(cloudBank);
            var core = new VisualElement { pickingMode = PickingMode.Ignore };
            core.style.position = Position.Absolute;
            core.style.left = Length.Percent(-12); core.style.right = Length.Percent(-12); core.style.top = -60; core.style.bottom = -60;
            core.style.backgroundColor = new Color(0.97f, 0.985f, 1f);
            cloudBank.Add(core);

            var rng = new System.Random(11);
            Color[] tones = { new Color(0.89f, 0.94f, 1f), new Color(0.94f, 0.97f, 1f), Color.white };
            for (int tone = 0; tone < tones.Length; tone++)
                for (int i = 0; i < 34; i++)
                {
                    // two thirds of the puffs make the soft left and right edges
                    float x = i % 3 == 0 ? (float)rng.NextDouble() * 100f : (i % 3 == 1 ? -14f : 114f) + ((float)rng.NextDouble() - 0.5f) * 26f;
                    float y = -4f + 108f * (float)rng.NextDouble();
                    float size = 260f + 260f * (float)rng.NextDouble() - tone * 50f;
                    var puff = new VisualElement { pickingMode = PickingMode.Ignore };
                    puff.style.position = Position.Absolute;
                    puff.style.width = size; puff.style.height = size;
                    puff.style.left = Length.Percent(x); puff.style.top = Length.Percent(y);
                    puff.style.marginLeft = -size * 0.5f; puff.style.marginTop = -size * 0.5f;
                    puff.style.backgroundColor = tones[tone];
                    Round(puff, size * 0.5f);
                    cloudBank.Add(puff);
                    puffs.Add((puff, (float)rng.NextDouble() * 6.28f, size));
                }
            layer.Add(cloudBank);
            return layer;
        }

        /// <summary>
        /// Clouds cross the screen; <paramref name="swap"/> runs while they cover it. direction 1 = moving to the right
        /// (clouds drift left). <paramref name="pan"/> gets a sideways amount every frame: 0..1 before, -1..0 after.
        /// </summary>
        public void PlayClouds(int direction, Action swap, Action<float> pan)
        {
            if (CloudsMoving) return;
            cloudDir = direction;
            cloudSwap = swap;
            cloudPan = pan;
            cloudSwapped = false;
            cloudT = 0f;
            Show(cloudLayer, true);
            cloudLayer.BringToFront();
            SoundFx.Swap();
            AnimateClouds();
        }

        void AnimateClouds()
        {
            if (cloudT < 0f) return;
            cloudT += Mathf.Min(Time.unscaledDeltaTime, 0.05f) / CloudSeconds; // a slow frame must not skip the crossing
            float t = Mathf.Clamp01(cloudT);

            // glide in, hang in front for a moment, glide out
            float x = t < 0.5f ? 1f - Ease(t / 0.45f) : -Ease((t - 0.55f) / 0.45f);
            cloudBank.style.translate = new Translate(Length.Percent(x * 150f * cloudDir), 0);
            foreach (var p in puffs)
            {
                float s = 1f + 0.10f * Mathf.Sin(Time.unscaledTime * 2.6f + p.phase);
                p.puff.style.scale = new Scale(new Vector3(s, s, 1f));
            }

            if (!cloudSwapped && t >= 0.5f)
            {
                cloudSwapped = true;
                cloudSwap?.Invoke();
                cloudLayer.BringToFront();
            }
            float pan = t < 0.5f ? t * 2f * t * 2f : -(1f - Ease((t - 0.5f) * 2f));
            cloudPan?.Invoke(pan * cloudDir);

            if (cloudT >= 1f)
            {
                cloudT = -1f;
                cloudPan?.Invoke(0f);
                Show(cloudLayer, false);
            }
        }

        static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }
    }
}
