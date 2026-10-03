using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Sound for expeditions and battles. The project has no recorded audio yet, so every clip is synthesised the first
    // time it is needed: UI ticks, footsteps, hits, chimes, and looping ambience per biome and dungeon theme, with a
    // night layer (crickets, an owl) faded in after dusk. A recorded file at Resources/AdamsHaven/Audio/<id> always wins
    // over the synthesised clip, so real sounds can be dropped in one at a time.
    public sealed class TowerAudio : MonoBehaviour
    {
        private const int Rate = 22050;
        private const float LoopSeconds = 12f, FadeSeconds = 1.6f, SfxVolume = 0.8f, AmbienceVolume = 0.55f;
        private const string MuteKey = "AdamsHaven.Muted";

        private static TowerAudio instance;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly AudioSource[] voices = new AudioSource[6];
        private int nextVoice;
        private AudioSource ambNew, ambOld, nightLayer;
        private string ambKey = "";
        private float nightTarget;

        public static bool Muted
        {
            get { return PlayerPrefs.GetInt(MuteKey, 0) == 1; }
            set { PlayerPrefs.SetInt(MuteKey, value ? 1 : 0); PlayerPrefs.Save(); AudioListener.volume = value ? 0 : 1; }
        }

        private static TowerAudio I
        {
            get
            {
                if (instance == null)
                {
                    var host = new GameObject("Tower Audio");
                    DontDestroyOnLoad(host);
                    instance = host.AddComponent<TowerAudio>();
                    instance.Setup();
                }
                return instance;
            }
        }

        private void Setup()
        {
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            for (int i = 0; i < voices.Length; i++) voices[i] = Source(false);
            ambNew = Source(true); ambOld = Source(true); nightLayer = Source(true);
            AudioListener.volume = Muted ? 0 : 1;
        }

        private AudioSource Source(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0; s.volume = 0;
            return s;
        }

        // ---------------------------------------------------------------- API

        // A one-shot: click, confirm, error, step, step_road, card, swing, hit, crit, heal, status, down, ult, chime,
        // reward, door, shift, victory, defeat.
        // A sound the player imported in the media library (sfx.<id>) wins over everything else.
        public static void Play(string id, float volume = 1f, float pitch = 1f)
        {
            if (!Application.isPlaying) return;
            var me = I;
            var clip = MediaLibrary.AudioFor("sfx." + id) ?? me.Clip(id);
            if (clip == null) return;
            me.Voice(clip, volume, pitch);
        }

        // A one-shot from a clip the caller already has (imported move sounds, previews).
        public static void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (!Application.isPlaying || clip == null) return;
            I.Voice(clip, volume, pitch);
        }

        // True when a recorded (or imported) clip exists for the id, so callers can prefer it over a generic sound.
        public static bool Has(string id)
        {
            if (!Application.isPlaying) return false;
            return MediaLibrary.Has("sfx." + id) || I.Recorded(id) != null;
        }

        private void Voice(AudioClip clip, float volume, float pitch)
        {
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.Stop();
            voice.clip = clip; voice.pitch = pitch; voice.volume = SfxVolume * volume;
            voice.Play();
        }

        private readonly Dictionary<string, AudioClip> recorded = new Dictionary<string, AudioClip>();
        private AudioClip Recorded(string id)
        {
            AudioClip clip;
            if (!recorded.TryGetValue(id, out clip)) { clip = Resources.Load<AudioClip>("AdamsHaven/Audio/" + id); recorded[id] = clip; }
            return clip;
        }

        // Music the player imported for a place (music.battle / music.atlas / music.dungeon), looped over the ambience.
        // "" or a place without music fades it out.
        public static void Music(string place)
        {
            if (!Application.isPlaying) return;
            var me = I;
            if (me.music == null) { me.music = me.Source(true); }
            me.musicKey = string.IsNullOrEmpty(place) ? "" : "music." + place;
            if (me.musicKey.Length > 0) MediaLibrary.Preload(me.musicKey);
        }
        private AudioSource music;
        private string musicKey = "";

        // Play with a little pitch spread, so repeated hits and steps do not sound like a machine.
        public static void PlayVaried(string id, float volume = 1f, float spread = 0.08f)
        { Play(id, volume, 1f + UnityEngine.Random.Range(-spread, spread)); }

        // The looping bed: forest, marsh, highland, ruins, heart, cave, crystal, blight, battle, or "" for silence.
        // night (0..1) fades the cricket layer in on top.
        public static void Ambience(string key, float night = 0f)
        {
            if (!Application.isPlaying) return;
            var me = I;
            me.nightTarget = string.IsNullOrEmpty(key) || key == "battle" ? 0 : Mathf.Clamp01(night);
            if (me.nightTarget > 0 && me.nightLayer.clip == null) { me.nightLayer.clip = me.Clip("amb_night"); me.nightLayer.Play(); }
            if (key == me.ambKey) return;
            me.ambKey = key ?? "";
            // The playing bed becomes the old one and fades out; the new one fades in.
            var old = me.ambOld; me.ambOld = me.ambNew; me.ambNew = old;
            me.ambNew.Stop(); me.ambNew.volume = 0; me.ambNew.clip = null;
            if (me.ambKey.Length == 0) return;
            me.ambNew.clip = me.Clip("amb_" + me.ambKey);
            if (me.ambNew.clip == null) return;
            me.ambNew.time = UnityEngine.Random.Range(0f, me.ambNew.clip.length * 0.9f);
            me.ambNew.Play();
        }

        // Ambience for a dungeon theme and for an overworld biome.
        public static string ThemeBed(string theme)
        {
            switch (theme)
            {
                case "cave": case "mine": case "ruin": case "keep": return "cave";
                case "crystal": return "crystal";
                case "blight": return "blight";
                case "marsh": return "marsh";
                default: return "forest";
            }
        }

        public static string BiomeBed(string biome)
        {
            switch (biome)
            {
                case "lakes": return "marsh";
                case "mountains": return "highland";
                case "ruins": return "ruins";
                case "heart": return "heart";
                default: return "forest";
            }
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime / FadeSeconds;
            ambNew.volume = Mathf.MoveTowards(ambNew.volume, ambNew.clip != null ? AmbienceVolume : 0, step * AmbienceVolume);
            ambOld.volume = Mathf.MoveTowards(ambOld.volume, 0, step * AmbienceVolume);
            if (ambOld.volume <= 0 && ambOld.isPlaying) ambOld.Stop();
            nightLayer.volume = Mathf.MoveTowards(nightLayer.volume, nightTarget * AmbienceVolume * 0.8f, step * AmbienceVolume);
            if (music != null)
            {
                // The wanted track loads asynchronously; swap it in once it is ready, fading the old one out first.
                var want = musicKey.Length > 0 ? MediaLibrary.AudioFor(musicKey) : null;
                if (music.clip != want)
                {
                    music.volume = Mathf.MoveTowards(music.volume, 0, step);
                    if (music.volume <= 0) { music.Stop(); music.clip = want; if (want != null) music.Play(); }
                }
                else if (want != null) music.volume = Mathf.MoveTowards(music.volume, MusicVolume, step * MusicVolume);
            }
        }
        private const float MusicVolume = 0.6f;

        // ---------------------------------------------------------------- clips

        private AudioClip Clip(string id)
        {
            AudioClip clip;
            if (clips.TryGetValue(id, out clip)) return clip;
            clip = Resources.Load<AudioClip>("AdamsHaven/Audio/" + id);
            if (clip == null)
            {
                var samples = Synth(id);
                if (samples != null)
                {
                    clip = AudioClip.Create(id, samples.Length, 1, Rate, false);
                    clip.SetData(samples, 0);
                }
            }
            clips[id] = clip;
            return clip;
        }

        // Exposed for tests: the raw synthesised samples, or null for an unknown id.
        public static float[] Synth(string id)
        {
            var rng = new System.Random((int)(TowerForestLayouts.Hash(id, 7) & 0x7fffffff));
            float[] b;
            switch (id)
            {
                case "click":
                    b = Buffer(0.07f); Tone(b, 0, 0.05f, 1900, 1650, 0.35f, 90); Noise(b, rng, 0, 0.02f, 0.12f, 300, 6000); break;
                case "confirm":
                    b = Buffer(0.3f); Tone(b, 0, 0.12f, 660, 660, 0.3f, 28); Tone(b, 0.07f, 0.2f, 990, 990, 0.3f, 18); break;
                case "error":
                    b = Buffer(0.25f); Tone(b, 0, 0.2f, 220, 185, 0.35f, 14, 3); break;
                case "step":
                    b = Buffer(0.12f); Noise(b, rng, 0, 0.1f, 0.55f, 55, 700); Tone(b, 0, 0.08f, 95, 70, 0.25f, 50); break;
                case "step_road":
                    b = Buffer(0.1f); Noise(b, rng, 0, 0.08f, 0.5f, 70, 2600); Tone(b, 0, 0.06f, 120, 90, 0.18f, 60); break;
                case "card":
                    b = Buffer(0.1f); Noise(b, rng, 0, 0.08f, 0.3f, 45, 5200); break;
                case "swing":
                    b = Buffer(0.3f); Sweep(b, rng, 0, 0.26f, 0.35f, 500, 3500); break;
                case "hit":
                    b = Buffer(0.3f); Tone(b, 0, 0.25f, 150, 55, 0.8f, 20); Noise(b, rng, 0, 0.15f, 0.7f, 32, 3200); break;
                case "crit":
                    b = Buffer(0.7f); Tone(b, 0, 0.25f, 170, 50, 0.85f, 18); Noise(b, rng, 0, 0.18f, 0.75f, 26, 4200);
                    Tone(b, 0.01f, 0.6f, 1320, 1320, 0.2f, 7); Tone(b, 0.01f, 0.6f, 1985, 1985, 0.14f, 8); break;
                case "heal":
                    b = Buffer(0.8f);
                    float[] up = { 523, 659, 784, 1046 };
                    for (int i = 0; i < up.Length; i++) Bell(b, i * 0.08f, up[i], 0.18f, 6);
                    break;
                case "status":
                    b = Buffer(0.35f); Tone(b, 0, 0.3f, 880, 1180, 0.18f, 12); Tone(b, 0.05f, 0.25f, 1320, 1500, 0.08f, 14); break;
                case "down":
                    b = Buffer(0.9f); Tone(b, 0, 0.8f, 330, 105, 0.35f, 4, 2); Noise(b, rng, 0, 0.4f, 0.25f, 8, 500); break;
                case "ult":
                    b = Buffer(1.2f); Sweep(b, rng, 0, 1.1f, 0.55f, 250, 5000); Tone(b, 0, 1.0f, 180, 720, 0.18f, 2.5f, 2); break;
                case "chime":
                    b = Buffer(1.6f); Bell(b, 0, 880, 0.3f, 3.2f); Bell(b, 0.12f, 1318, 0.18f, 3.6f); break;
                case "reward":
                    b = Buffer(1.4f);
                    float[] notes = { 784, 988, 1175, 1568 };
                    for (int i = 0; i < notes.Length; i++) Bell(b, i * 0.07f, notes[i], 0.2f, 4);
                    break;
                case "door":
                    b = Buffer(0.9f); Tone(b, 0, 0.35f, 90, 50, 0.6f, 9); Noise(b, rng, 0.05f, 0.7f, 0.3f, 4, 380); break;
                case "shift":
                    b = Buffer(2.2f); Noise(b, rng, 0, 2.1f, 0.0f, 0, 160, swell: 0.9f); Sweep(b, rng, 0.4f, 1.6f, 0.35f, 300, 2200); break;
                case "victory":
                    b = Buffer(2.4f);
                    float[] win = { 523, 659, 784, 1046 };
                    for (int i = 0; i < win.Length; i++) Bell(b, i * 0.13f, win[i], 0.22f, 1.8f);
                    Bell(b, 0.6f, 1318, 0.16f, 1.4f);
                    break;
                case "defeat":
                    b = Buffer(2.4f);
                    float[] lose = { 392, 311, 262 };
                    for (int i = 0; i < lose.Length; i++) Tone(b, i * 0.4f, 0.9f, lose[i], lose[i] * 0.99f, 0.22f, 3, 2);
                    break;
                // ---- looping beds
                case "amb_forest": b = Loop(rng, (l, r) => Wind(l, r, 0.16f), (l, r) => Birds(l, r, 11, 0.11f)); break;
                case "amb_marsh": b = Loop(rng, (l, r) => { Wind(l, r, 0.1f); Buzz(l, 0.012f); }, (l, r) => Frogs(l, r, 9)); break;
                case "amb_highland": b = Loop(rng, (l, r) => Wind(l, r, 0.3f, gusty: true), (l, r) => Hawk(l, r, 2)); break;
                case "amb_ruins": b = Loop(rng, (l, r) => { Wind(l, r, 0.18f); Drone(l, 0.035f, 73.4f, 110f); }, (l, r) => Drips(l, r, 5, 0.06f)); break;
                case "amb_heart": b = Loop(rng, (l, r) => { Drone(l, 0.06f, 55f, 55.7f, 82.4f); Wind(l, r, 0.08f); }, (l, r) => Whispers(l, r, 3, 0.05f)); break;
                case "amb_cave": b = Loop(rng, (l, r) => Rumble(l, r, 0.3f), (l, r) => Drips(l, r, 8, 0.14f)); break;
                case "amb_crystal": b = Loop(rng, (l, r) => { Rumble(l, r, 0.18f); Drone(l, 0.025f, 130.8f, 196f); }, (l, r) => Chimes(l, r, 9, 0.06f)); break;
                case "amb_blight": b = Loop(rng, (l, r) => Drone(l, 0.07f, 49f, 49.6f, 73.4f), (l, r) => { Whispers(l, r, 4, 0.07f); Groans(l, r, 2); }); break;
                case "amb_battle": b = Loop(rng, (l, r) => Drone(l, 0.05f, 65.4f, 98f), (l, r) => Drums(l)); break;
                case "amb_night": b = Loop(rng, null, (l, r) => { Crickets(l, r); Owl(l, r); }); break;
                default: return null;
            }
            Normalize(b, id.StartsWith("amb_") ? 0.6f : 0.85f);
            return b;
        }

        // ---------------------------------------------------------------- synthesis

        private static float[] Buffer(float seconds) { return new float[Mathf.CeilToInt(seconds * Rate)]; }

        private static int At(float seconds) { return Mathf.RoundToInt(seconds * Rate); }

        private static void Add(float[] b, int i, float v, bool wrap) { if (wrap) b[((i % b.Length) + b.Length) % b.Length] += v; else if (i >= 0 && i < b.Length) b[i] += v; }

        // A pitch sweep with an exponential decay; overtones > 1 adds odd harmonics for a reedier note.
        private static void Tone(float[] b, float start, float length, float f0, float f1, float amp, float decay, int overtones = 1, bool wrap = false)
        {
            int n = At(length), s = At(start);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, k = i / (float)n;
                float f = Mathf.Lerp(f0, f1, k);
                phase += 2 * Math.PI * f / Rate;
                float v = (float)Math.Sin(phase);
                for (int h = 1; h < overtones; h++) v += (float)Math.Sin(phase * (2 * h + 1)) / (2 * h + 1);
                float env = Mathf.Min(1, t / 0.004f) * Mathf.Exp(-t * decay) * Mathf.Min(1, (n - i) / (0.01f * Rate));
                Add(b, s + i, v * amp * env, wrap);
            }
        }

        // A struck bell: three inharmonic partials.
        private static void Bell(float[] b, float start, float f, float amp, float decay, bool wrap = false)
        {
            float length = Mathf.Min(4f / decay + 0.1f, b.Length / (float)Rate);
            Tone(b, start, length, f, f, amp, decay, 1, wrap);
            Tone(b, start, length * 0.7f, f * 2.76f, f * 2.76f, amp * 0.45f, decay * 1.6f, 1, wrap);
            Tone(b, start, length * 0.5f, f * 5.4f, f * 5.4f, amp * 0.2f, decay * 2.4f, 1, wrap);
        }

        // Low-passed noise burst with a decay (decay 0 and swell > 0 rise and fall instead).
        private static void Noise(float[] b, System.Random rng, float start, float length, float amp, float decay, float cutoff, float swell = 0)
        {
            int n = At(length), s = At(start);
            float a = 1 - Mathf.Exp(-2 * Mathf.PI * cutoff / Rate), y = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, k = i / (float)n;
                y += a * ((float)rng.NextDouble() * 2 - 1 - y);
                float env = swell > 0 ? swell * Mathf.Sin(Mathf.PI * k) : Mathf.Min(1, t / 0.003f) * Mathf.Exp(-t * decay);
                Add(b, s + i, y * env * (swell > 0 ? 1 : amp) * Mathf.Min(1, (n - i) / (0.01f * Rate)) * (cutoff < 1000 ? 2.5f : 1f), false);
            }
        }

        // A whoosh: noise through a low-pass whose cutoff rises and falls.
        private static void Sweep(float[] b, System.Random rng, float start, float length, float amp, float lo, float hi)
        {
            int n = At(length), s = At(start);
            float y = 0;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f));
                float cutoff = Mathf.Lerp(lo, hi, env);
                float a = 1 - Mathf.Exp(-2 * Mathf.PI * cutoff / Rate);
                y += a * ((float)rng.NextDouble() * 2 - 1 - y);
                Add(b, s + i, y * env * amp * 1.6f, false);
            }
        }

        // A seamless loop: the continuous bed is generated a little long and its tail cross-faded into the head; the events
        // (calls, drips, drums) are added after, wrapping round the end, so a beat on the seam is not faded out.
        private static float[] Loop(System.Random rng, Action<float[], System.Random> bed, Action<float[], System.Random> events)
        {
            int n = At(LoopSeconds), tail = At(1.5f);
            var b = new float[n];
            if (bed != null)
            {
                var raw = new float[n + tail];
                bed(raw, rng);
                for (int i = 0; i < n; i++) b[i] = raw[i];
                for (int i = 0; i < tail; i++) { float k = i / (float)tail; b[i] = raw[i] * k + raw[n + i] * (1 - k); }
            }
            events(b, rng);
            return b;
        }

        // Brown-ish noise, low-passed, swelling in slow gusts.
        private static void Wind(float[] b, System.Random rng, float amp, bool gusty = false)
        {
            float y = 0, z = 0, a = 1 - Mathf.Exp(-2 * Mathf.PI * (gusty ? 520 : 380) / Rate);
            double p1 = rng.NextDouble() * 6, p2 = rng.NextDouble() * 6;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                y += a * ((float)rng.NextDouble() * 2 - 1 - y);
                z += a * (y - z);
                float gust = 0.55f + 0.3f * (float)Math.Sin(t * 0.52 + p1) + 0.15f * (float)Math.Sin(t * 1.31 + p2);
                if (gusty) gust = Mathf.Max(0.2f, gust * gust * 1.6f);
                b[i] += z * amp * 3.2f * gust;
            }
        }

        private static void Rumble(float[] b, System.Random rng, float amp)
        {
            float y = 0, z = 0, a = 1 - Mathf.Exp(-2 * Mathf.PI * 110 / Rate);
            for (int i = 0; i < b.Length; i++)
            {
                y += a * ((float)rng.NextDouble() * 2 - 1 - y);
                z += a * (y - z);
                b[i] += z * amp * 6f;
            }
        }

        private static void Drone(float[] b, float amp, params float[] freqs)
        {
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate, v = 0;
                foreach (var f in freqs) v += Mathf.Sin(2 * Mathf.PI * f * t);
                b[i] += v * amp * (0.8f + 0.2f * Mathf.Sin(t * 0.7f));
            }
        }

        private static float Span(float[] b) { return b.Length / (float)Rate; }

        private static void Birds(float[] b, System.Random rng, int calls, float amp)
        {
            for (int c = 0; c < calls; c++)
            {
                float at = (float)rng.NextDouble() * Span(b), f = 2600 + (float)rng.NextDouble() * 1800;
                int notes = 2 + rng.Next(4);
                bool down = rng.Next(2) == 0;
                for (int k = 0; k < notes; k++)
                {
                    float len = 0.05f + (float)rng.NextDouble() * 0.07f;
                    Tone(b, at, len, down ? f * 1.25f : f * 0.85f, down ? f * 0.85f : f * 1.25f, amp * (0.6f + 0.4f * (float)rng.NextDouble()), 18, 1, true);
                    at += len + 0.03f + (float)rng.NextDouble() * 0.06f;
                }
            }
        }

        private static void Frogs(float[] b, System.Random rng, int croaks)
        {
            for (int c = 0; c < croaks; c++)
            {
                float at = (float)rng.NextDouble() * Span(b), f = 130 + (float)rng.NextDouble() * 70;
                // A croak is a fast train of throaty pulses.
                for (int p = 0; p < 7; p++) Tone(b, at + p * 0.045f, 0.04f, f * 1.1f, f, 0.16f, 30, 3, true);
            }
        }

        private static void Buzz(float[] b, float amp)
        {
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] += Mathf.Sin(2 * Mathf.PI * 3100 * t) * (0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 38 * t)) * amp * (0.6f + 0.4f * Mathf.Sin(t * 0.9f));
            }
        }

        private static void Hawk(float[] b, System.Random rng, int cries)
        {
            for (int c = 0; c < cries; c++)
            {
                float at = (float)rng.NextDouble() * Span(b);
                Tone(b, at, 0.55f, 2300, 1550, 0.05f, 3, 2, true);
            }
        }

        private static void Drips(float[] b, System.Random rng, int drips, float amp)
        {
            for (int d = 0; d < drips; d++)
            {
                float at = (float)rng.NextDouble() * Span(b), f = 900 + (float)rng.NextDouble() * 700;
                // The drop and two echoes off the walls.
                Tone(b, at, 0.07f, f * 1.6f, f, amp, 55, 1, true);
                Tone(b, at + 0.19f, 0.07f, f * 1.6f, f, amp * 0.35f, 55, 1, true);
                Tone(b, at + 0.38f, 0.07f, f * 1.6f, f, amp * 0.12f, 55, 1, true);
            }
        }

        private static void Chimes(float[] b, System.Random rng, int count, float amp)
        {
            float[] scale = { 1046, 1175, 1397, 1568, 1760, 2093 };
            for (int c = 0; c < count; c++) Bell(b, (float)rng.NextDouble() * Span(b), scale[rng.Next(scale.Length)], amp, 2.2f, true);
        }

        // Band-limited noise that swells and fades, like voices just out of hearing.
        private static void Whispers(float[] b, System.Random rng, int count, float amp)
        {
            for (int c = 0; c < count; c++)
            {
                int s = At((float)rng.NextDouble() * Span(b)), n = At(1.2f + (float)rng.NextDouble());
                float lo = 0, hi = 0, aLo = 1 - Mathf.Exp(-2 * Mathf.PI * 900 / Rate), aHi = 1 - Mathf.Exp(-2 * Mathf.PI * 2600 / Rate);
                for (int i = 0; i < n; i++)
                {
                    float x = (float)rng.NextDouble() * 2 - 1;
                    lo += aLo * (x - lo); hi += aHi * (x - hi);
                    float k = i / (float)n, env = Mathf.Sin(Mathf.PI * k) * (0.6f + 0.4f * Mathf.Sin(k * 40f));
                    Add(b, s + i, (hi - lo) * env * amp * 6f, true);
                }
            }
        }

        private static void Groans(float[] b, System.Random rng, int count)
        {
            for (int c = 0; c < count; c++) Tone(b, (float)rng.NextDouble() * Span(b), 1.3f, 95, 68, 0.09f, 1.2f, 3, true);
        }

        // 90 bpm: a deep drum on the beat, a lighter one on the off-beat, a short roll closing the loop.
        private static void Drums(float[] b)
        {
            float beat = 60f / 90f;
            int beats = Mathf.RoundToInt(LoopSeconds / beat);
            for (int i = 0; i < beats; i++)
            {
                bool strong = i % 2 == 0;
                Tone(b, i * beat, 0.3f, strong ? 120 : 160, strong ? 45 : 70, strong ? 0.4f : 0.18f, 14, 1, true);
                if (i == beats - 1)
                    for (int r = 1; r <= 3; r++) Tone(b, i * beat + r * beat / 4, 0.18f, 180, 90, 0.14f, 18, 1, true);
            }
        }

        private static void Crickets(float[] b, System.Random rng)
        {
            float[] pitch = { 4600, 4950, 4300 };
            float[] period = { 0.62f, 0.81f, 0.93f };
            for (int c = 0; c < pitch.Length; c++)
                for (float at = (float)rng.NextDouble() * period[c]; at < LoopSeconds; at += period[c])
                    for (int p = 0; p < 3; p++) Tone(b, at + p * 0.028f, 0.02f, pitch[c], pitch[c], 0.035f, 40, 1, true);
        }

        private static void Owl(float[] b, System.Random rng)
        {
            float at = 2f + (float)rng.NextDouble() * 6f;
            Tone(b, at, 0.32f, 390, 370, 0.07f, 3, 2, true);
            Tone(b, at + 0.5f, 0.5f, 385, 360, 0.07f, 2.5f, 2, true);
        }

        private static void Normalize(float[] b, float peak)
        {
            float max = 0;
            foreach (var v in b) max = Mathf.Max(max, Mathf.Abs(v));
            if (max < 1e-5f) return;
            float k = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }
    }
}
