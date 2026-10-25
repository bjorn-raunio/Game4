using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Audio;

namespace Icosa
{
    public enum Sfx { Hit, Miss, Crit, Pickup, Gold, Door, Spell, Fire, Zap, LevelUp, Death, Dice, Hurt, Stairs, Drink, Kill, Holy, Trap, Fumble, Heal, Shriek, Read, Cold, Thunder, Heart }

    public static class Synth
    {
        const int Rate = 22050;
        static readonly Dictionary<Sfx, SoundEffect> sounds = new Dictionary<Sfx, SoundEffect>();
        static bool ok;
        public static float Volume = 0.45f;
        public static bool Muted;
        static readonly Random rnd = new Random(7);

        public static void Init()
        {
            try
            {
                Make(Sfx.Hit, 0.12, t => Noise() * Env(t, 0.12, 0.002) * 0.6 + Sine(90 - t * 300, t) * Env(t, 0.12, 0.001) * 0.8);
                Make(Sfx.Miss, 0.16, t => Noise() * Math.Sin(Math.PI * t / 0.16) * 0.25);
                Make(Sfx.Crit, 0.32, t => Noise() * Env(t, 0.1, 0.002) * 0.6 + Sine(70, t) * Env(t, 0.2, 0.001) + Sine(1320, t) * Env(t, 0.32, 0.05) * 0.35);
                Make(Sfx.Pickup, 0.14, t => Square(t < 0.06 ? 660 : 990, t) * Env(t, 0.14, 0.003) * 0.25);
                Make(Sfx.Gold, 0.35, t => (Sine(1760, t) * Env(t, 0.12, 0.001) + Sine(2349, Math.Max(0, t - 0.07)) * (t > 0.07 ? Env(t - 0.07, 0.25, 0.001) : 0)) * 0.35);
                Make(Sfx.Door, 0.35, t => Saw(80 + 30 * Math.Sin(t * 40), t) * Env(t, 0.35, 0.02) * 0.25 + Noise() * Env(t, 0.05, 0.001) * 0.3);
                Make(Sfx.Spell, 0.5, t => (Sine(440 * Math.Pow(2, Math.Floor(t * 16) / 12.0), t) * 0.6 + Sine(880 + 30 * Math.Sin(t * 60), t) * 0.3) * Env(t, 0.5, 0.02) * 0.4);
                Make(Sfx.Fire, 0.7, t => Lowpass(Noise(), 0.25) * Env(t, 0.7, 0.03) * 0.9 + Sine(55, t) * Env(t, 0.4, 0.01) * 0.5);
                Make(Sfx.Zap, 0.25, t => Square(1200 - t * 3800, t) * Env(t, 0.25, 0.002) * 0.22);
                Make(Sfx.LevelUp, 1.0, t =>
                {
                    double[] notes = { 523.25, 659.25, 783.99, 1046.5 };
                    int i = Math.Min(3, (int)(t / 0.13));
                    double f = notes[i];
                    double lt = t - i * 0.13;
                    return (Square(f, t) * 0.3 + Sine(f * 2, t) * 0.2) * Env(lt, i == 3 ? 0.6 : 0.13, 0.005) * 0.5;
                });
                Make(Sfx.Death, 1.6, t => (Saw(220 * Math.Pow(0.5, t * 1.2), t) * 0.4 + Sine(110 * Math.Pow(0.5, t), t) * 0.5) * Env(t, 1.6, 0.01) * 0.5);
                Make(Sfx.Dice, 0.4, t =>
                {
                    double v = 0;
                    double[] clicks = { 0, 0.07, 0.13, 0.2, 0.28, 0.33 };
                    foreach (var c in clicks)
                        if (t >= c && t < c + 0.02) v += Noise() * (1 - (t - c) / 0.02) * 0.7 + Sine(2400, t - c) * 0.3 * (1 - (t - c) / 0.02);
                    return v * 0.5;
                });
                Make(Sfx.Hurt, 0.2, t => (Sine(150 - t * 400, t) * 0.7 + Noise() * 0.3) * Env(t, 0.2, 0.002) * 0.6);
                Make(Sfx.Stairs, 0.6, t =>
                {
                    double ph = t % 0.15;
                    return (Sine(120 - (t / 0.15) * 15, ph) * 0.7 + Noise() * 0.2) * Env(ph, 0.15, 0.002) * 0.6;
                });
                Make(Sfx.Drink, 0.45, t => Sine(300 + 200 * Math.Abs(Math.Sin(t * 50)), t) * Env(t, 0.45, 0.02) * 0.3);
                Make(Sfx.Kill, 0.3, t => (Lowpass(Noise(), 0.4) * 0.8 + Square(60, t) * 0.3) * Env(t, 0.3, 0.002) * 0.6);
                Make(Sfx.Holy, 1.0, t => (Sine(523.25, t) + Sine(659.25, t) * 0.8 + Sine(783.99, t) * 0.7 + Sine(1046.5, t) * 0.4) * Env(t, 1.0, 0.15) * 0.16);
                Make(Sfx.Trap, 0.4, t => (Square(200 + 900 * Math.Exp(-t * 20), t) * 0.3 + Noise() * 0.3) * Env(t, 0.4, 0.002) * 0.5);
                Make(Sfx.Fumble, 0.5, t => (Saw(330 - t * 300, t) * 0.3 + Saw(320 - t * 300, t) * 0.3) * Env(t, 0.5, 0.01) * 0.4);
                Make(Sfx.Heal, 0.6, t => Sine(660 + 220 * t, t) * Env(t, 0.6, 0.1) * 0.25 + Sine(990 + 330 * t, t) * Env(t, 0.6, 0.15) * 0.15);
                Make(Sfx.Shriek, 0.6, t => (Saw(1400 + 300 * Math.Sin(t * 70), t) * 0.25 + Noise() * 0.1) * Env(t, 0.6, 0.03) * 0.5);
                Make(Sfx.Read, 0.3, t => Lowpass(Noise(), 0.1) * Math.Sin(Math.PI * t / 0.3) * 0.4);
                Make(Sfx.Cold, 0.6, t => (Sine(2200 + 400 * Math.Sin(t * 30), t) * 0.15 + Lowpass(Noise(), 0.6) * 0.3) * Env(t, 0.6, 0.02) * 0.5);
                Make(Sfx.Thunder, 1.2, t => (Lowpass(Noise(), 0.08) * 1.6 + Noise() * Env(t, 0.08, 0.001) * 0.6) * Env(t, 1.2, 0.005) * 0.8);
                Make(Sfx.Heart, 1.4, t =>
                {
                    double a = t < 0.7 ? Env(t, 0.25, 0.01) : Env(t - 0.35, 0.3, 0.01);
                    return (Sine(55, t) * 0.9 + Sine(110, t) * 0.3) * a * 0.9 + Sine(1567.98, t) * Env(t, 1.4, 0.3) * 0.08;
                });
                ok = true;
            }
            catch
            {
                ok = false;
            }
        }

        static double lp;
        static double Lowpass(double v, double a) { lp += (v - lp) * a; return lp * 2; }
        static double Noise() => rnd.NextDouble() * 2 - 1;
        static double Sine(double f, double t) => Math.Sin(2 * Math.PI * f * t);
        static double Square(double f, double t) => Math.Sin(2 * Math.PI * f * t) >= 0 ? 1 : -1;
        static double Saw(double f, double t) => 2 * ((f * t) % 1.0) - 1;

        static double Env(double t, double len, double attack)
        {
            if (t < 0 || t > len) return 0;
            if (t < attack) return t / attack;
            double r = (t - attack) / Math.Max(0.0001, len - attack);
            return Math.Pow(1 - r, 2);
        }

        static void Make(Sfx id, double dur, Func<double, double> fn)
        {
            int n = (int)(Rate * dur);
            var buf = new byte[n * 2];
            lp = 0;
            for (int i = 0; i < n; i++)
            {
                double v = fn(i / (double)Rate);
                v = Math.Clamp(v, -1, 1);
                short s = (short)(v * 30000);
                buf[i * 2] = (byte)(s & 0xff);
                buf[i * 2 + 1] = (byte)((s >> 8) & 0xff);
            }
            sounds[id] = new SoundEffect(buf, Rate, AudioChannels.Mono);
        }

        static readonly Dictionary<int, SoundEffect> ambiences = new Dictionary<int, SoundEffect>();
        static SoundEffectInstance ambInst;
        static int ambBand = -1;

        static SoundEffect MakeAmbience(int band)
        {
            const double L = 8.0;
            int n = (int)(Rate * L);
            var buf = new byte[n * 2];
            var r = new Random(band * 97 + 13);
            double[] drips = new double[band == 3 ? 4 : 7];
            for (int i = 0; i < drips.Length; i++) drips[i] = 0.3 + r.NextDouble() * (L - 1.2);
            double[] dripF = drips.Select(_ => 900 + r.NextDouble() * 900).ToArray();
            double lpState = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                double v = 0;
                double baseF = band switch { 0 => 55, 1 => 41.25, 2 => 36.75, _ => 32.5 };
                v += Math.Sin(2 * Math.PI * baseF * t) * 0.22;
                v += Math.Sin(2 * Math.PI * (baseF + 0.125) * t) * 0.18;
                v += Math.Sin(2 * Math.PI * baseF * 1.5 * t) * 0.07 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * t / L));
                double noise = r.NextDouble() * 2 - 1;
                lpState += (noise - lpState) * 0.02;
                lp2 += (lpState - lp2) * 0.05;
                double wind = lp2 * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 2 * t / L)) * (band >= 1 ? 3.0 : 1.6);
                v += wind;
                if (band == 2) v += Math.Sin(2 * Math.PI * 1320 * t + 3 * Math.Sin(2 * Math.PI * 0.25 * t)) * 0.012 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * 3 * t / L));
                if (band == 3)
                {
                    if (r.NextDouble() < 0.0009) v += (r.NextDouble() * 2 - 1) * 0.5;
                    v += Math.Sin(2 * Math.PI * 27.5 * t) * 0.15 * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 4 * t / L));
                }
                for (int d = 0; d < drips.Length; d++)
                {
                    double dt2 = t - drips[d];
                    if (dt2 >= 0 && dt2 < 0.35) v += Math.Sin(2 * Math.PI * dripF[d] * (1 + dt2 * 0.6) * dt2) * Math.Exp(-dt2 * 18) * 0.16;
                }
                double fade = Math.Min(1, Math.Min(t / 0.05, (L - t) / 0.05));
                v *= 0.55 * (0.85 + 0.15 * fade);
                short s = (short)(Math.Clamp(v, -1, 1) * 30000);
                buf[i * 2] = (byte)(s & 0xff);
                buf[i * 2 + 1] = (byte)((s >> 8) & 0xff);
            }
            return new SoundEffect(buf, Rate, AudioChannels.Mono);
        }

        public static void Ambience(int depth)
        {
            int band = depth <= 0 ? -1 : depth <= 4 ? 0 : depth <= 9 ? 1 : depth <= 14 ? 2 : 3;
            if (!ok) return;
            if (Muted) band = -1;
            if (band == ambBand) return;
            ambBand = band;
            try
            {
                ambInst?.Stop();
                ambInst?.Dispose();
                ambInst = null;
                if (band < 0) return;
                if (!ambiences.TryGetValue(band, out var se)) { se = MakeAmbience(band); ambiences[band] = se; }
                ambInst = se.CreateInstance();
                ambInst.IsLooped = true;
                ambInst.Volume = Math.Clamp(Volume * 0.55f, 0, 1);
                ambInst.Play();
            }
            catch { }
        }

        public static void Play(Sfx id, float vol = 1f, float pitch = 0f)
        {
            if (!ok || Muted) return;
            try
            {
                if (sounds.TryGetValue(id, out var s)) s.Play(Math.Clamp(Volume * vol, 0, 1), Math.Clamp(pitch + (float)(rnd.NextDouble() * 0.1 - 0.05), -1, 1), 0);
            }
            catch { }
        }
    }
}
