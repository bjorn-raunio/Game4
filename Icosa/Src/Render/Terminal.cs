using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public class Terminal
    {
        public const int W = 133, H = 45, CellW = 12, CellH = 20;
        public readonly char[,] Ch = new char[W, H];
        public readonly Color[,] Fg = new Color[W, H];
        public readonly Color[,] Bg = new Color[W, H];
        public readonly bool[,] Paper = new bool[W, H];

        public void Clear()
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    Ch[x, y] = ' ';
                    Fg[x, y] = Pal.Text;
                    Bg[x, y] = Pal.Black;
                    Paper[x, y] = false;
                }
        }

        public static bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

        public void Put(int x, int y, char c, Color fg)
        {
            if (!In(x, y)) return;
            Ch[x, y] = c;
            Fg[x, y] = fg;
        }

        public void Put(int x, int y, char c, Color fg, Color bg)
        {
            if (!In(x, y)) return;
            Ch[x, y] = c;
            Fg[x, y] = fg;
            Bg[x, y] = bg;
        }

        public void SetBg(int x, int y, Color bg)
        {
            if (In(x, y)) Bg[x, y] = bg;
        }

        public int Print(int x, int y, string s, Color fg)
        {
            for (int i = 0; i < s.Length; i++) Put(x + i, y, s[i], fg);
            return s.Length;
        }

        public int Print(int x, int y, string s, Color fg, Color bg)
        {
            for (int i = 0; i < s.Length; i++) Put(x + i, y, s[i], fg, bg);
            return s.Length;
        }

        public void Center(int y, string s, Color fg, int x0 = 0, int w = W) => PrintM(x0 + (w - Markup.Len(s)) / 2, y, s, fg);

        public int PrintM(int x, int y, string s, Color fg, int maxW = 9999)
        {
            var stack = new Stack<Color>();
            Color cur = fg;
            int cx = x;
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '{')
                {
                    int end = s.IndexOf('}', i);
                    if (end > i)
                    {
                        string tag = s.Substring(i + 1, end - i - 1);
                        if (tag == "/")
                        {
                            cur = stack.Count > 0 ? stack.Pop() : fg;
                            i = end + 1;
                            continue;
                        }
                        if (Markup.TryColor(tag, out var col))
                        {
                            stack.Push(cur);
                            cur = col;
                            i = end + 1;
                            continue;
                        }
                    }
                }
                if (cx - x < maxW) Put(cx, y, c, cur);
                cx++;
                i++;
            }
            return cx - x;
        }

        public int PrintWrap(int x, int y, int w, string s, Color fg, int maxLines = 999)
        {
            var lines = Markup.Wrap(s, w);
            int n = 0;
            foreach (var l in lines)
            {
                if (n >= maxLines) break;
                PrintM(x, y + n, l, fg);
                n++;
            }
            return n;
        }

        public void Fill(int x, int y, int w, int h, Color bg, char c = ' ')
        {
            for (int i = x; i < x + w; i++)
                for (int j = y; j < y + h; j++)
                    if (In(i, j))
                    {
                        Ch[i, j] = c;
                        Bg[i, j] = bg;
                        Paper[i, j] = false;
                    }
        }

        public void Frame(int x, int y, int w, int h, Color fg, Color bg, string title = null)
        {
            Fill(x, y, w, h, bg);
            for (int i = x + 1; i < x + w - 1; i++)
            {
                Put(i, y, '─', fg);
                Put(i, y + h - 1, '─', fg);
            }
            for (int j = y + 1; j < y + h - 1; j++)
            {
                Put(x, j, '│', fg);
                Put(x + w - 1, j, '│', fg);
            }
            Put(x, y, '┌', fg);
            Put(x + w - 1, y, '┐', fg);
            Put(x, y + h - 1, '└', fg);
            Put(x + w - 1, y + h - 1, '┘', fg);
            if (title != null)
            {
                string t = " " + title + " ";
                int tx = x + (w - Markup.Len(t)) / 2;
                PrintM(tx, y, t, Pal.Hi);
            }
        }

        public void HLine(int x, int y, int w, Color fg)
        {
            for (int i = x; i < x + w; i++) Put(i, y, '─', fg);
        }

        public void Bar(int x, int y, int w, float frac, Color full, Color empty, string label = null, Color? labelColor = null)
        {
            frac = Math.Clamp(frac, 0f, 1f);
            int filled = (int)Math.Round(frac * w);
            for (int i = 0; i < w; i++)
            {
                SetBg(x + i, y, i < filled ? full : empty);
                Put(x + i, y, ' ', Pal.White);
            }
            if (label != null)
            {
                int lx = x + (w - label.Length) / 2;
                for (int i = 0; i < label.Length; i++) Put(lx + i, y, label[i], labelColor ?? Pal.White);
            }
        }
    }

    public static class Markup
    {
        public static bool TryColor(string tag, out Color c)
        {
            if (tag.Length == 7 && tag[0] == '#')
            {
                try
                {
                    int r = Convert.ToInt32(tag.Substring(1, 2), 16);
                    int g = Convert.ToInt32(tag.Substring(3, 2), 16);
                    int b = Convert.ToInt32(tag.Substring(5, 2), 16);
                    c = new Color(r, g, b);
                    return true;
                }
                catch { }
            }
            return Pal.Named.TryGetValue(tag, out c);
        }

        public static string Hex(Color c) => "{#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2") + "}";

        public static string Strip(string s)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '{')
                {
                    int end = s.IndexOf('}', i);
                    if (end > i)
                    {
                        string tag = s.Substring(i + 1, end - i - 1);
                        if (tag == "/" || TryColor(tag, out _))
                        {
                            i = end + 1;
                            continue;
                        }
                    }
                }
                sb.Append(s[i]);
                i++;
            }
            return sb.ToString();
        }

        public static int Len(string s) => Strip(s).Length;

        public static List<string> Wrap(string s, int width)
        {
            var result = new List<string>();
            foreach (var rawPara in s.Split('\n'))
            {
                int indent = rawPara.Length - rawPara.TrimStart(' ').Length;
                string pad = new string(' ', indent);
                var para = rawPara.TrimStart(' ');
                var words = para.Split(' ');
                var line = new StringBuilder(pad);
                int lineLen = indent;
                var openTags = new List<string>();
                foreach (var word in words)
                {
                    int wl = Len(word);
                    if (lineLen > indent && lineLen + 1 + wl > width)
                    {
                        result.Add(line.ToString());
                        line.Clear();
                        line.Append(pad);
                        foreach (var t in openTags) line.Append(t);
                        lineLen = indent;
                    }
                    if (lineLen > indent)
                    {
                        line.Append(' ');
                        lineLen++;
                    }
                    line.Append(word);
                    lineLen += wl;
                    TrackTags(word, openTags);
                }
                result.Add(line.ToString());
            }
            return result;
        }

        static void TrackTags(string word, List<string> open)
        {
            int i = 0;
            while (i < word.Length)
            {
                if (word[i] == '{')
                {
                    int end = word.IndexOf('}', i);
                    if (end > i)
                    {
                        string tag = word.Substring(i + 1, end - i - 1);
                        if (tag == "/")
                        {
                            if (open.Count > 0) open.RemoveAt(open.Count - 1);
                            i = end + 1;
                            continue;
                        }
                        if (TryColor(tag, out _))
                        {
                            open.Add("{" + tag + "}");
                            i = end + 1;
                            continue;
                        }
                    }
                }
                i++;
            }
        }
    }
}
