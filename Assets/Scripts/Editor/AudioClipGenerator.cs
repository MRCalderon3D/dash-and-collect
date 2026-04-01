using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DashAndCollect
{
    /// <summary>
    /// Generates procedural audio clips for Dash &amp; Collect using pure C# math.
    /// No external tools required — all synthesis is SetData-equivalent PCM math
    /// written directly to .wav on disk.
    ///
    /// Menu: Tools → Generate Procedural Audio
    ///
    /// Outputs (overwrite existing placeholders so AudioManager inspector refs stay valid):
    ///   Assets/Audio/SFX/Dash.wav       — soft sine chirp whoosh (lane change)
    ///   Assets/Audio/SFX/Collect.wav    — double-sine ping 880+1100 Hz, 0.05s
    ///   Assets/Audio/SFX/GameOver.wav   — descending square 300→100 Hz + noise, 0.3s
    ///   Assets/Audio/Music/BGM.wav      — square-wave arpeggio C4-E4-G4-C5, 120 bpm, 4 bars
    ///
    /// All clips are mono 44100 Hz 16-bit PCM.
    /// Import settings are applied after AssetDatabase.Refresh():
    ///   SFX  → Compressed in Memory, Force To Mono
    ///   BGM  → Streaming, Force To Mono
    /// </summary>
    public static class AudioClipGenerator
    {
        const int SR = 44100;
        const float TwoPi = 2f * Mathf.PI;

        [MenuItem("Tools/Generate Procedural Audio")]
        public static void GenerateAll()
        {
            WriteDash();
            WriteCollect();
            WriteGameOver();
            WriteBGM();

            AssetDatabase.Refresh();

            ApplyImport("Assets/Audio/SFX/Dash.wav",      streaming: false);
            ApplyImport("Assets/Audio/SFX/Collect.wav",   streaming: false);
            ApplyImport("Assets/Audio/SFX/GameOver.wav",  streaming: false);
            ApplyImport("Assets/Audio/Music/BGM.wav",     streaming: true);

            Debug.Log("[AudioClipGenerator] 4 clips written and import settings applied.");
        }

        // ── sfx_dash — soft sine chirp 200→450 Hz, 0.09s ─────────────────
        // "subtle whoosh" — quiet, low-mid frequency sweep, gentle bell envelope
        static void WriteDash()
        {
            const float dur = 0.09f;
            int n = Samples(dur);
            var buf = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float norm = t / dur;
                // Bell envelope: peaks at 40% through, no click at boundaries
                float env = Mathf.Sin(Mathf.PI * norm) * (1f - norm * 0.4f);
                float freq = Mathf.Lerp(200f, 450f, norm);
                phase = WrapPhase(phase + TwoPi * freq / SR);
                buf[i] = env * 0.22f * Mathf.Sin(phase);
            }
            WriteWav("Assets/Audio/SFX/Dash.wav", buf);
        }

        // ── sfx_collect — double sine ping 880+1100 Hz, 0.05s ─────────────
        // Bright ding — two harmonically related partials, exponential decay
        static void WriteCollect()
        {
            const float dur = 0.05f;
            int n = Samples(dur);
            var buf = new float[n];
            float ph1 = 0f, ph2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Exp(-t * 40f);
                ph1 = WrapPhase(ph1 + TwoPi * 880f  / SR);
                ph2 = WrapPhase(ph2 + TwoPi * 1100f / SR);
                buf[i] = env * 0.32f * (Mathf.Sin(ph1) + Mathf.Sin(ph2));
            }
            WriteWav("Assets/Audio/SFX/Collect.wav", buf);
        }

        // ── sfx_gameover — descending square 300→100 Hz + noise, 0.3s ─────
        // Impact crunch — rough square descent, noise layer for texture
        static void WriteGameOver()
        {
            const float dur = 0.3f;
            int n = Samples(dur);
            var buf = new float[n];
            var rng = new System.Random(42);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Exp(-t * 4f);
                float freq = Mathf.Lerp(300f, 100f, t / dur);
                phase = WrapPhase(phase + TwoPi * freq / SR);
                float sq    = Mathf.Sin(phase) >= 0f ? 1f : -1f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                buf[i] = env * (0.38f * sq + 0.12f * noise);
            }
            WriteWav("Assets/Audio/SFX/GameOver.wav", buf);
        }

        // ── bgm_main — square arpeggio C4-E4-G4-C5, 120 bpm, 4 bars ──────
        // Lo-fi chiptune loop — 16 beats × 0.5s = 8s seamlessly loopable
        static void WriteBGM()
        {
            const float bpm    = 120f;
            const int   bars   = 4;
            const int   beats  = bars * 4;        // 16 quarter-note beats
            float       beat   = 60f / bpm;       // 0.5s per beat

            // C4, E4, G4, C5 — cycles every 4 beats (one bar)
            float[] freqs = { 261.63f, 329.63f, 392.00f, 523.25f };

            int n = Samples(beat * beats);
            var buf = new float[n];
            float phase = 0f;

            for (int i = 0; i < n; i++)
            {
                float t      = (float)i / SR;
                int   noteI  = (int)(t / beat) % freqs.Length;
                float tNote  = t - Mathf.Floor(t / beat) * beat;  // time within beat
                float env    = NoteEnv(tNote, beat);
                phase = WrapPhase(phase + TwoPi * freqs[noteI] / SR);
                float sq = Mathf.Sin(phase) >= 0f ? 1f : -1f;
                buf[i] = env * 0.26f * sq;
            }

            // Fade last 20ms to avoid a click at loop point
            int fadeLen = Samples(0.02f);
            for (int i = 0; i < fadeLen; i++)
                buf[n - fadeLen + i] *= 1f - (float)i / fadeLen;

            WriteWav("Assets/Audio/Music/BGM.wav", buf);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        static int   Samples(float sec) => Mathf.RoundToInt(sec * SR);
        static float WrapPhase(float p) => p > TwoPi ? p - TwoPi : p;

        // Per-note envelope: short attack, sustain, gentle release
        static float NoteEnv(float tNote, float beatDur)
        {
            const float att = 0.008f;
            float rel = beatDur * 0.25f;
            float sus = beatDur - att - rel;
            if (tNote < att)             return tNote / att;
            if (tNote < att + sus)       return 1f;
            float r = tNote - att - sus;
            return Mathf.Max(0f, 1f - r / rel);
        }

        // ── WAV writer (mono, 44100 Hz, 16-bit PCM) ────────────────────────
        static void WriteWav(string assetPath, float[] mono)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath    = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            int dataBytes = mono.Length * sizeof(short);

            using var fs = new FileStream(fullPath, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            // RIFF header
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataBytes);           // file size − 8
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            // fmt chunk
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);                        // chunk size
            bw.Write((short)1);                  // PCM
            bw.Write((short)1);                  // mono
            bw.Write(SR);                        // sample rate
            bw.Write(SR * sizeof(short));        // byte rate
            bw.Write((short)sizeof(short));      // block align
            bw.Write((short)16);                 // bits per sample
            // data chunk
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataBytes);
            foreach (float s in mono)
                bw.Write((short)Mathf.Clamp(s * 32767f, -32768f, 32767f));
        }

        // ── Import settings ────────────────────────────────────────────────
        static void ApplyImport(string assetPath, bool streaming)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null) return;

            importer.forceToMono  = true;
            importer.ambisonic    = false;
            importer.loadInBackground = false;

            var settings = importer.defaultSampleSettings;
            settings.loadType           = streaming
                ? AudioClipLoadType.Streaming
                : AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat  = AudioCompressionFormat.Vorbis;
            settings.quality            = streaming ? 0.4f : 0.7f;
            importer.defaultSampleSettings = settings;

            importer.SaveAndReimport();
        }
    }
}
