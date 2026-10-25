using System;
using System.Collections.Generic;

namespace Icosa
{
    public static class Rng
    {
        public static Random R = new Random();
        public static int Seed;

        public static void Init(int seed)
        {
            Seed = seed;
            R = new Random(seed);
        }

        public static int Int(int maxExclusive) => maxExclusive <= 0 ? 0 : R.Next(maxExclusive);
        public static int Range(int lo, int hiInclusive) => hiInclusive < lo ? lo : R.Next(lo, hiInclusive + 1);
        public static int D(int sides) => sides <= 0 ? 0 : R.Next(1, sides + 1);
        public static bool Chance(int pct) => R.Next(100) < pct;
        public static bool OneIn(int n) => n <= 1 || R.Next(n) == 0;
        public static double Float() => R.NextDouble();

        public static int Roll(int n, int sides)
        {
            int t = 0;
            for (int i = 0; i < n; i++) t += D(sides);
            return t;
        }

        public static T Pick<T>(IList<T> list) => list[R.Next(list.Count)];

        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = R.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static T Weighted<T>(IList<T> items, Func<T, int> weight)
        {
            int total = 0;
            foreach (var it in items) total += Math.Max(0, weight(it));
            if (total <= 0) return items.Count > 0 ? items[R.Next(items.Count)] : default;
            int r = R.Next(total);
            foreach (var it in items)
            {
                int w = Math.Max(0, weight(it));
                if (r < w) return it;
                r -= w;
            }
            return items[items.Count - 1];
        }
    }

    public struct Dice
    {
        public int N, S, B;

        public Dice(int n, int s, int b = 0) { N = n; S = s; B = b; }

        public int Roll() => Rng.Roll(N, S) + B;
        public int Max => N * S + B;
        public int Min => N + B;
        public float Avg => N * (S + 1) / 2f + B;
        public bool IsZero => N == 0 && B == 0;

        public static Dice Parse(string s)
        {
            s = s.Trim();
            int d = s.IndexOf('d');
            if (d < 0) return new Dice(0, 0, int.Parse(s));
            int n = d == 0 ? 1 : int.Parse(s.Substring(0, d));
            string rest = s.Substring(d + 1);
            int b = 0;
            int pm = rest.IndexOfAny(new[] { '+', '-' });
            if (pm >= 0)
            {
                b = int.Parse(rest.Substring(pm));
                rest = rest.Substring(0, pm);
            }
            return new Dice(n, int.Parse(rest), b);
        }

        public override string ToString()
        {
            if (N == 0) return B.ToString();
            string s = N + "d" + S;
            if (B > 0) s += "+" + B;
            else if (B < 0) s += B;
            return s;
        }
    }
}
