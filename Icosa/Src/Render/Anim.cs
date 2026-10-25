using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public abstract class Fx
    {
        public float T, Dur;
        public bool Done => T >= Dur;
        public abstract void Draw(Terminal t, int ox, int oy);
    }

    public class BoltFx : Fx
    {
        readonly List<Point> path;
        readonly char glyph;
        readonly Color color;
        readonly bool trail;

        public BoltFx(List<Point> path, char glyph, Color color, bool trail, float speed)
        {
            this.path = path;
            this.glyph = glyph;
            this.color = color;
            this.trail = trail;
            Dur = Math.Max(0.05f, path.Count * speed);
        }

        public override void Draw(Terminal t, int ox, int oy)
        {
            if (path.Count == 0) return;
            int i = Math.Min(path.Count - 1, (int)(T / Dur * path.Count));
            if (trail)
                for (int j = 0; j < i; j++)
                {
                    float a = 1f - (i - j) / 6f;
                    if (a <= 0) continue;
                    var p = path[j];
                    t.SetBg(p.X + ox, p.Y + oy, Pal.Lerp(t.Bg[p.X + ox, p.Y + oy], color, a * 0.6f));
                }
            var q = path[i];
            t.Put(q.X + ox, q.Y + oy, glyph, color);
            if (trail) t.SetBg(q.X + ox, q.Y + oy, Pal.Mul(color, 0.5f));
        }
    }

    public class BurstFx : Fx
    {
        readonly List<Point> cells;
        readonly Point center;
        readonly Color color;
        readonly char glyph;

        public BurstFx(Point center, List<Point> cells, Color color, char glyph = '*', float dur = 0.35f)
        {
            this.center = center;
            this.cells = cells;
            this.color = color;
            this.glyph = glyph;
            Dur = dur;
        }

        public override void Draw(Terminal t, int ox, int oy)
        {
            float k = T / Dur;
            foreach (var p in cells)
            {
                float d = (float)Math.Sqrt((p.X - center.X) * (p.X - center.X) + (p.Y - center.Y) * (p.Y - center.Y));
                float a = Math.Clamp(1.2f - k * 1.3f - d * 0.08f, 0, 1);
                if (a <= 0) continue;
                int x = p.X + ox, y = p.Y + oy;
                if (!Terminal.In(x, y)) continue;
                t.SetBg(x, y, Pal.Lerp(t.Bg[x, y], color, a));
                if (glyph != ' ' && Rng.Chance(40)) t.Put(x, y, glyph, Pal.Lerp(color, Pal.White, 0.6f));
            }
        }
    }

    public class FlashFx : Fx
    {
        readonly Point p;
        readonly Color color;
        readonly char glyph;

        public FlashFx(Point p, Color color, float dur = 0.18f, char glyph = '\0')
        {
            this.p = p;
            this.color = color;
            this.glyph = glyph;
            Dur = dur;
        }

        public override void Draw(Terminal t, int ox, int oy)
        {
            int x = p.X + ox, y = p.Y + oy;
            if (!Terminal.In(x, y)) return;
            float a = 1f - T / Dur;
            t.SetBg(x, y, Pal.Lerp(t.Bg[x, y], color, a));
            if (glyph != '\0') t.Put(x, y, glyph, Pal.White);
        }
    }

    public class TextFx : Fx
    {
        readonly Point p;
        readonly string text;
        readonly Color color;

        public TextFx(Point p, string text, Color color, float dur = 0.7f)
        {
            this.p = p;
            this.text = text;
            this.color = color;
            Dur = dur;
        }

        public override void Draw(Terminal t, int ox, int oy)
        {
            int rise = (int)(T / Dur * 2);
            int x = p.X + ox - text.Length / 2 + 1, y = p.Y + oy - 1 - rise;
            if (y < oy) return;
            float a = 1f - Math.Max(0, T / Dur - 0.5f) * 2f;
            for (int i = 0; i < text.Length; i++)
                if (Terminal.In(x + i, y)) t.Put(x + i, y, text[i], Pal.Lerp(Pal.Black, color, a));
        }
    }

    public static class Anim
    {
        static readonly List<Fx> queue = new List<Fx>();
        static readonly List<Fx> parallel = new List<Fx>();
        public static float SpeedMul = 1f;

        public static bool Blocking => queue.Count > 0;

        public static void Clear()
        {
            queue.Clear();
            parallel.Clear();
        }

        public static void Seq(Fx f) { if (queue.Count < 40) queue.Add(f); }
        public static void Par(Fx f) { if (parallel.Count < 60) parallel.Add(f); }

        public static void Bolt(List<Point> path, char g, Color c, bool trail = true, float speed = 0.022f) => Seq(new BoltFx(path, g, c, trail, speed));
        public static void Burst(Point center, List<Point> cells, Color c, char g = '*', float dur = 0.35f) => Seq(new BurstFx(center, cells, c, g, dur));
        public static void Flash(Point p, Color c, float dur = 0.2f) => Par(new FlashFx(p, c, dur));
        public static void Float(Point p, string s, Color c) => Par(new TextFx(p, s, c));

        public static void SkipAll() => queue.Clear();

        public static void Update(float dt)
        {
            dt *= SpeedMul;
            if (queue.Count > 0)
            {
                queue[0].T += dt;
                if (queue[0].Done) queue.RemoveAt(0);
            }
            for (int i = parallel.Count - 1; i >= 0; i--)
            {
                parallel[i].T += dt;
                if (parallel[i].Done) parallel.RemoveAt(i);
            }
        }

        public static void Draw(Terminal t, int ox, int oy)
        {
            foreach (var f in parallel) f.Draw(t, ox, oy);
            if (queue.Count > 0) queue[0].Draw(t, ox, oy);
        }
    }
}
