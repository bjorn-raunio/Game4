using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class Fov
    {
        static readonly int[,] Mult =
        {
            { 1, 0, 0, -1, -1, 0, 0, 1 },
            { 0, 1, -1, 0, 0, -1, 1, 0 },
            { 0, 1, 1, 0, 0, -1, -1, 0 },
            { 1, 0, 0, 1, -1, 0, 0, -1 },
        };

        public static void Compute(Point o, int radius, Func<int, int, bool> blocks, Action<int, int> visit)
        {
            visit(o.X, o.Y);
            for (int oct = 0; oct < 8; oct++)
                Cast(o.X, o.Y, 1, 1.0, 0.0, radius, Mult[0, oct], Mult[1, oct], Mult[2, oct], Mult[3, oct], blocks, visit);
        }

        static void Cast(int cx, int cy, int row, double start, double end, int radius, int xx, int xy, int yx, int yy, Func<int, int, bool> blocks, Action<int, int> visit)
        {
            if (start < end) return;
            int r2 = radius * radius + radius;
            double newStart = 0;
            for (int j = row; j <= radius; j++)
            {
                int dx = -j - 1, dy = -j;
                bool blocked = false;
                while (dx <= 0)
                {
                    dx++;
                    int X = cx + dx * xx + dy * xy;
                    int Y = cy + dx * yx + dy * yy;
                    double lSlope = (dx - 0.5) / (dy + 0.5);
                    double rSlope = (dx + 0.5) / (dy - 0.5);
                    if (start < rSlope) continue;
                    if (end > lSlope) break;
                    if (dx * dx + dy * dy <= r2) visit(X, Y);
                    bool b = blocks(X, Y);
                    if (blocked)
                    {
                        if (b)
                        {
                            newStart = rSlope;
                            continue;
                        }
                        blocked = false;
                        start = newStart;
                    }
                    else if (b && j < radius)
                    {
                        blocked = true;
                        Cast(cx, cy, j + 1, start, lSlope, radius, xx, xy, yx, yy, blocks, visit);
                        newStart = rSlope;
                    }
                }
                if (blocked) break;
            }
        }

        public static bool Los(Map m, Point a, Point b)
        {
            foreach (var p in Path.Line(a, b))
            {
                if (p == a || p == b) continue;
                if (m.BlocksSight(p.X, p.Y)) return false;
            }
            return true;
        }
    }

    public static class Path
    {
        static readonly Point[] Dirs8 =
        {
            new Point(0, -1), new Point(1, 0), new Point(0, 1), new Point(-1, 0),
            new Point(1, -1), new Point(1, 1), new Point(-1, 1), new Point(-1, -1),
        };

        public static Point[] Dirs => Dirs8;

        public static int[,] DistMap(Map m, IEnumerable<Point> goals, Func<int, int, bool> passable, int maxDist = 9999)
        {
            var dist = new int[Map.W, Map.H];
            for (int x = 0; x < Map.W; x++)
                for (int y = 0; y < Map.H; y++)
                    dist[x, y] = int.MaxValue;
            var q = new Queue<Point>();
            foreach (var g in goals)
            {
                if (!Map.InB(g)) continue;
                dist[g.X, g.Y] = 0;
                q.Enqueue(g);
            }
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                int d = dist[p.X, p.Y];
                if (d >= maxDist) continue;
                foreach (var dir in Dirs8)
                {
                    int nx = p.X + dir.X, ny = p.Y + dir.Y;
                    if (!Map.InB(nx, ny) || dist[nx, ny] != int.MaxValue) continue;
                    if (!passable(nx, ny)) continue;
                    dist[nx, ny] = d + 1;
                    q.Enqueue(new Point(nx, ny));
                }
            }
            return dist;
        }

        public static List<Point> Line(Point a, Point b)
        {
            var pts = new List<Point>();
            int x0 = a.X, y0 = a.Y, x1 = b.X, y1 = b.Y;
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                pts.Add(new Point(x0, y0));
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
                if (pts.Count > 400) break;
            }
            return pts;
        }

        public static List<Point> Ray(Map m, Point from, Point to, int range, bool stopAtActor, bool passWalls = false)
        {
            var result = new List<Point>();
            if (from == to) return result;
            int ddx = to.X - from.X, ddy = to.Y - from.Y;
            int k = Math.Max(1, (range * 2) / Math.Max(1, Math.Max(Math.Abs(ddx), Math.Abs(ddy))) + 1);
            var far = new Point(from.X + ddx * k, from.Y + ddy * k);
            var line = Line(from, far);
            for (int i = 1; i < line.Count; i++)
            {
                var p = line[i];
                if (!Map.InB(p)) break;
                if (Math.Max(Math.Abs(p.X - from.X), Math.Abs(p.Y - from.Y)) > range) break;
                if (!passWalls && !m.Walkable(p) && m.BlocksSight(p.X, p.Y)) break;
                result.Add(p);
                if (stopAtActor && m.ActorAt(p) != null) break;
            }
            return result;
        }

        public static Point Step(Point from, Point to)
        {
            return new Point(Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
        }

        public static int Cheb(Point a, Point b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }
}
