using UnityEngine;

namespace Zombosland
{
    public enum Sfx { Shot, DryFire, MagOut, MagIn, Slide, HitFlesh, HitConcrete, ZombieGrowl, ZombieAttack, ZombieDeath, PlayerHurt, ReloadTactical, ReloadEmpty, Casing, AimIn, HitTick, PickupSoft, PickupHard, Deny, LidOpen, LidClose, BandageTie, Equip, Holster }

    // Procedural placeholder audio plus a pooled set of 3D AudioSources.
    // Real recordings assigned in `overrides` (builder reads Assets/Audio/<name>.wav) win over synthesis;
    // several overrides with the same id become variations. ReloadTactical/ReloadEmpty have no synthetic fallback (see Has).
    public class SfxBank : MonoBehaviour
    {
        [System.Serializable]
        public class Override { public Sfx id; public AudioClip clip; }

        public static SfxBank Instance { get; private set; }
        const int PoolSize = 24;

        public Override[] overrides;

        AudioClip[][] clips;
        AudioSource[] sources;
        float[] startedAt;

        void Awake()
        {
            Instance = this;
            BuildClips();
            sources = new AudioSource[PoolSize];
            startedAt = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Sfx " + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 2f;
                s.maxDistance = 60f;
                sources[i] = s;
            }
            GameSettings.ApplyAudio();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnApplicationQuit()
        {
            if (sources == null) return;
            foreach (AudioSource s in sources) if (s != null) s.Stop();
            AudioListener.pause = true;
        }

        void BuildClips()
        {
            int count = System.Enum.GetValues(typeof(Sfx)).Length;
            clips = new AudioClip[count][];
            var r = new System.Random(777);
            clips[(int)Sfx.Shot] = new[] { SfxSynth.Gunshot(r), SfxSynth.Gunshot(r), SfxSynth.Gunshot(r) };
            clips[(int)Sfx.DryFire] = new[] { SfxSynth.Click("dry", 0.06f, 2400f, 0.5f) };
            clips[(int)Sfx.MagOut] = new[] { SfxSynth.Click("magout", 0.12f, 900f, 0.8f) };
            clips[(int)Sfx.MagIn] = new[] { SfxSynth.Click("magin", 0.14f, 600f, 1f) };
            clips[(int)Sfx.Slide] = new[] { SfxSynth.Slide() };
            clips[(int)Sfx.HitFlesh] = new[] { SfxSynth.HitFlesh(r), SfxSynth.HitFlesh(r) };
            clips[(int)Sfx.HitConcrete] = new[] { SfxSynth.HitConcrete(r), SfxSynth.HitConcrete(r) };
            clips[(int)Sfx.ZombieGrowl] = new[] { SfxSynth.Growl(r, 1.2f, 0.07f, 6f), SfxSynth.Growl(r, 1.0f, 0.09f, 8f), SfxSynth.Growl(r, 1.4f, 0.06f, 5f) };
            clips[(int)Sfx.ZombieAttack] = new[] { SfxSynth.Growl(r, 0.6f, 0.18f, 14f) };
            clips[(int)Sfx.ZombieDeath] = new[] { SfxSynth.Death(r) };
            clips[(int)Sfx.PlayerHurt] = new[] { SfxSynth.PlayerHurt(r) };
            clips[(int)Sfx.Casing] = new[] { SfxSynth.Click("casing", 0.09f, 4300f, 0.6f), SfxSynth.Click("casing2", 0.08f, 3700f, 0.55f) };
            clips[(int)Sfx.HitTick] = new[] { SfxSynth.Click("hittick", 0.03f, 3200f, 0.6f) };
            clips[(int)Sfx.AimIn] = new[] { SfxSynth.Rustle() };
            clips[(int)Sfx.PickupSoft] = new[] { SfxSynth.Soft("pickup_soft", 0.14f, 0.3f), SfxSynth.Soft("pickup_soft2", 0.12f, 0.22f) };
            clips[(int)Sfx.PickupHard] = new[] { SfxSynth.Click("pickup_hard", 0.1f, 1500f, 0.7f), SfxSynth.Click("pickup_hard2", 0.09f, 1250f, 0.7f) };
            clips[(int)Sfx.Deny] = new[] { SfxSynth.Click("deny", 0.12f, 220f, 0.7f) };
            clips[(int)Sfx.LidOpen] = new[] { SfxSynth.Creak("lid_open", 0.25f, 180f, 320f) };
            clips[(int)Sfx.LidClose] = new[] { SfxSynth.Creak("lid_close", 0.2f, 300f, 120f) };
            clips[(int)Sfx.BandageTie] = new[] { SfxSynth.Soft("bandage_tie", 0.25f, 0.18f) };
            clips[(int)Sfx.Equip] = new[] { SfxSynth.Click("equip", 0.1f, 800f, 0.6f) };
            clips[(int)Sfx.Holster] = new[] { SfxSynth.Click("holster", 0.11f, 520f, 0.6f) };
            if (overrides != null)
            {
                var grouped = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<AudioClip>>();
                foreach (Override o in overrides)
                {
                    if (o == null || o.clip == null) continue;
                    System.Collections.Generic.List<AudioClip> list;
                    if (!grouped.TryGetValue((int)o.id, out list)) { list = new System.Collections.Generic.List<AudioClip>(); grouped[(int)o.id] = list; }
                    list.Add(o.clip);
                }
                foreach (var pair in grouped) clips[pair.Key] = pair.Value.ToArray();
            }
        }

        public bool Has(Sfx id)
        {
            if (clips == null) return false;
            AudioClip[] set = clips[(int)id];
            return set != null && set.Length > 0;
        }

        public void Play2D(Sfx id, float volume = 1f) { Play(id, Vector3.zero, volume, 1f, false); }

        public void Play(Sfx id, Vector3 position, float volume, float pitch, bool spatial)
        {
            if (clips == null) return;
            AudioClip[] set = clips[(int)id];
            if (set == null || set.Length == 0) return;
            AudioSource s = Acquire();
            s.clip = set[Random.Range(0, set.Length)];
            s.volume = volume;
            s.pitch = pitch;
            s.spatialBlend = spatial ? 1f : 0f;
            s.transform.position = position;
            s.Play();
        }

        // Free source first, otherwise steal the one that started earliest.
        AudioSource Acquire()
        {
            int oldest = 0;
            for (int i = 0; i < sources.Length; i++)
            {
                if (!sources[i].isPlaying) { startedAt[i] = Time.unscaledTime; return sources[i]; }
                if (startedAt[i] < startedAt[oldest]) oldest = i;
            }
            startedAt[oldest] = Time.unscaledTime;
            return sources[oldest];
        }
    }

    // Small synthesis helpers. Everything is deterministic given the System.Random.
    static class SfxSynth
    {
        const int Rate = 32000;
        const float TwoPi = 6.2831853f;

        static float Noise(System.Random r) { return (float)(r.NextDouble() * 2.0 - 1.0); }

        static AudioClip Make(string name, float[] data)
        {
            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.9f / Mathf.Max(peak, 0.9f); // only scales down if clipping
            for (int i = 0; i < data.Length; i++) data[i] = (float)System.Math.Tanh(data[i] * gain);
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip Gunshot(System.Random r)
        {
            int n = (int)(Rate * 0.4f);
            var d = new float[n];
            float low = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = Noise(r);
                low += 0.35f * (white - low);                           // low-passed body
                float high = white - lp; lp += 0.5f * (white - lp);     // high-passed crack
                float burst = (white * 0.5f + low * 0.9f) * Mathf.Exp(-t / 0.06f);
                float freq = Mathf.Lerp(90f, 60f, Mathf.Clamp01(t / 0.15f));
                float thump = Mathf.Sin(TwoPi * freq * t) * Mathf.Exp(-t / 0.05f) * 0.9f;
                float crack = high * Mathf.Exp(-t / 0.006f) * 1.2f;
                d[i] = burst + thump + crack;
            }
            return Make("gunshot", d);
        }

        public static AudioClip Click(string name, float duration, float tone, float level)
        {
            var r = new System.Random(name.GetHashCode());
            int n = (int)(Rate * duration);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t / (duration * 0.12f));
                d[i] = (Noise(r) * 0.5f + Mathf.Sin(TwoPi * tone * t)) * env * level * 0.5f;
            }
            return Make(name, d);
        }

        public static AudioClip Slide()
        {
            var r = new System.Random(5);
            int n = (int)(Rate * 0.22f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += 0.25f * (Noise(r) - lp);
                float rasp = lp * Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t / 0.05f);
                float click = (t > 0.1f && t < 0.12f) ? Noise(r) * Mathf.Exp(-(t - 0.1f) / 0.004f) : 0f;
                d[i] = rasp * 0.9f + click * 0.8f + Mathf.Sin(TwoPi * 500f * t) * Mathf.Exp(-t / 0.03f) * 0.2f;
            }
            return Make("slide", d);
        }

        // Soft cloth / grip rustle for aiming in.
        public static AudioClip Rustle()
        {
            var r = new System.Random(11);
            int n = (int)(Rate * 0.18f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += 0.12f * (Noise(r) - lp);
                d[i] = lp * 2.2f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.18f)) * 0.5f;
            }
            return Make("aim_in", d);
        }

        // Short cloth / paper rustle with a soft attack (pickups, bandage).
        public static AudioClip Soft(string name, float duration, float cutoff)
        {
            var r = new System.Random(name.GetHashCode());
            int n = (int)(Rate * duration);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += cutoff * (Noise(r) - lp);
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t / (duration * 0.35f));
                d[i] = lp * 3f * env * (0.7f + 0.3f * Mathf.Sin(TwoPi * 38f * t));
            }
            return Make(name, d);
        }

        // Wooden hinge: a short tone gliding between two pitches over noise (lid, cabinet).
        public static AudioClip Creak(string name, float duration, float fromHz, float toHz)
        {
            var r = new System.Random(name.GetHashCode());
            int n = (int)(Rate * duration);
            var d = new float[n];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = t / duration;
                phase += Mathf.Lerp(fromHz, toHz, u) / Rate;
                lp += 0.15f * (Noise(r) - lp);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));
                float saw = (phase % 1f) * 2f - 1f;
                d[i] = (saw * 0.22f + lp * 1.2f) * env * 0.6f;
            }
            return Make(name, d);
        }

        public static AudioClip HitFlesh(System.Random r)
        {
            int n = (int)(Rate * 0.2f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += 0.08f * (Noise(r) - lp);
                d[i] = lp * 4f * Mathf.Exp(-t / 0.04f) + Mathf.Sin(TwoPi * 110f * t) * Mathf.Exp(-t / 0.05f) * 0.5f;
            }
            return Make("hit_flesh", d);
        }

        public static AudioClip HitConcrete(System.Random r)
        {
            int n = (int)(Rate * 0.18f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = Noise(r);
                float high = white - lp; lp += 0.3f * (white - lp);
                d[i] = high * Mathf.Exp(-t / 0.02f) + Mathf.Sin(TwoPi * 1400f * t) * Mathf.Exp(-t / 0.015f) * 0.3f + lp * Mathf.Exp(-t / 0.05f) * 0.3f;
            }
            return Make("hit_concrete", d);
        }

        // Low-passed noise with slow amplitude modulation and a low sawtooth for a growl.
        public static AudioClip Growl(System.Random r, float duration, float cutoff, float modHz)
        {
            int n = (int)(Rate * duration);
            var d = new float[n];
            float lp = 0f;
            float basePitch = 55f + (float)r.NextDouble() * 20f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float u = t / duration;
                lp += cutoff * (Noise(r) - lp);
                float mod = 0.55f + 0.45f * Mathf.Sin(TwoPi * modHz * t);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)) ;
                float saw = ((t * basePitch) % 1f) * 2f - 1f;
                d[i] = (lp * 5f + saw * 0.25f) * mod * env;
            }
            return Make("growl", d);
        }

        public static AudioClip Death(System.Random r)
        {
            float duration = 1.3f;
            int n = (int)(Rate * duration);
            var d = new float[n];
            float lp = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float u = t / duration;
                lp += 0.07f * (Noise(r) - lp);
                float pitch = Mathf.Lerp(90f, 38f, u);
                phase += pitch / Rate;
                float saw = (phase % 1f) * 2f - 1f;
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t / 0.5f);
                d[i] = (lp * 5f + saw * 0.35f) * env * (0.7f + 0.3f * Mathf.Sin(TwoPi * 9f * t));
            }
            return Make("zombie_death", d);
        }

        public static AudioClip PlayerHurt(System.Random r)
        {
            int n = (int)(Rate * 0.3f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += 0.1f * (Noise(r) - lp);
                d[i] = lp * 4f * Mathf.Exp(-t / 0.08f) + Mathf.Sin(TwoPi * 70f * t) * Mathf.Exp(-t / 0.1f) * 0.8f;
            }
            return Make("player_hurt", d);
        }
    }
}
