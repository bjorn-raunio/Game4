using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class Pal
    {
        public static Color C(int r, int g, int b) => new Color(r, g, b);

        public static Color Mul(Color c, float f) =>
            new Color((int)Math.Clamp(c.R * f, 0, 255), (int)Math.Clamp(c.G * f, 0, 255), (int)Math.Clamp(c.B * f, 0, 255));

        public static Color Mul(Color c, float r, float g, float b) =>
            new Color((int)Math.Clamp(c.R * r, 0, 255), (int)Math.Clamp(c.G * g, 0, 255), (int)Math.Clamp(c.B * b, 0, 255));

        public static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, Math.Clamp(t, 0f, 1f));

        public static Color Add(Color a, Color b) =>
            new Color(Math.Min(255, a.R + b.R), Math.Min(255, a.G + b.G), Math.Min(255, a.B + b.B));

        public static Color Gray(Color c)
        {
            int l = (c.R * 3 + c.G * 5 + c.B * 2) / 10;
            return new Color(l, l, l);
        }

        public static Color Jitter(Color c, int amt) =>
            new Color(Math.Clamp(c.R + Rng.Range(-amt, amt), 0, 255), Math.Clamp(c.G + Rng.Range(-amt, amt), 0, 255), Math.Clamp(c.B + Rng.Range(-amt, amt), 0, 255));

        public static readonly Color Black = C(0, 0, 0);
        public static readonly Color White = C(235, 235, 225);
        public static readonly Color Text = C(200, 196, 182);
        public static readonly Color Dim = C(110, 106, 98);
        public static readonly Color Faint = C(60, 58, 54);
        public static readonly Color Hi = C(255, 240, 190);
        public static readonly Color Red = C(220, 60, 50);
        public static readonly Color DarkRed = C(130, 20, 20);
        public static readonly Color Green = C(90, 200, 90);
        public static readonly Color DarkGreen = C(40, 110, 40);
        public static readonly Color Blue = C(80, 120, 230);
        public static readonly Color Cyan = C(90, 210, 220);
        public static readonly Color Yellow = C(240, 220, 80);
        public static readonly Color Orange = C(240, 140, 40);
        public static readonly Color Magenta = C(210, 80, 210);
        public static readonly Color Purple = C(150, 90, 220);
        public static readonly Color Brown = C(150, 100, 50);
        public static readonly Color Gold = C(255, 205, 60);
        public static readonly Color Silver = C(200, 205, 215);
        public static readonly Color Pink = C(240, 150, 180);
        public static readonly Color Bone = C(225, 215, 190);
        public static readonly Color Holy = C(255, 245, 170);
        public static readonly Color Ink = C(110, 150, 225);
        public static readonly Color Paper = C(13, 18, 34);
        public static readonly Color PaperLine = C(26, 36, 64);
        public static readonly Color Frame = C(120, 96, 60);
        public static readonly Color FrameHi = C(200, 160, 90);
        public static readonly Color Panel = C(16, 13, 11);
        public static readonly Color Blood = C(150, 10, 10);

        public static readonly Dictionary<string, Color> Named = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            ["white"] = White, ["text"] = Text, ["dim"] = Dim, ["faint"] = Faint, ["hi"] = Hi,
            ["red"] = Red, ["darkred"] = DarkRed, ["green"] = Green, ["darkgreen"] = DarkGreen,
            ["blue"] = Blue, ["cyan"] = Cyan, ["yellow"] = Yellow, ["orange"] = Orange,
            ["magenta"] = Magenta, ["purple"] = Purple, ["brown"] = Brown, ["gold"] = Gold,
            ["silver"] = Silver, ["pink"] = Pink, ["bone"] = Bone, ["holy"] = Holy, ["ink"] = Ink,
            ["good"] = C(120, 220, 120), ["bad"] = C(235, 90, 70), ["warn"] = C(240, 180, 60),
            ["magic"] = C(170, 140, 255), ["dice"] = C(150, 170, 190), ["crit"] = C(255, 215, 60), ["fumble"] = C(255, 70, 70),
        };
    }
}
