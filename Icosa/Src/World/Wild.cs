using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public class EncounterSpec
    {
        public Tile Terrain;
        public MonsterDef Def;
        public int Count;
        public bool PlayerSurprised, MonstersSurprised;
    }

    public static class Wild
    {
        const int W = Map.W, H = Map.H;
        static string fail;
        public static EncounterSpec Pending;

        static Map Fail(string why)
        {
            fail = why;
            return null;
        }

        public static int TravelTurns(Tile t) => t switch
        {
            Tile.Road or Tile.Bridge => 10,
            Tile.Forest or Tile.Hills => 25,
            Tile.Marsh => 35,
            Tile.Wildwood or Tile.Shallow => 40,
            _ => 15,
        };

        static Map NewMap(int depth, int branch) => new Map(depth, branch)
        {
            WallFg = new Color(170, 125, 80),
            WallBg = new Color(78, 50, 28),
            FloorFg = new Color(150, 120, 85),
            FloorBg = new Color(30, 22, 14),
            Ambient = 1f,
        };

        static void Daylight(Map m)
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    m.Lit[x, y] = true;
        }

        static int Far(Point a, Point b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y) * 2);

        public static Map Generate()
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                Attempts = attempt + 1;
                var m = TryOverland(attempt > 50);
                if (m != null) return m;
                FailWhy[fail] = FailWhy.GetValueOrDefault(fail) + 1;
            }
            return TryOverland(true) ?? throw new InvalidOperationException("The Weald failed to generate: " + fail);
        }

        public static int Fords, Filled, LakeCells, RiverCells, Attempts, Valleys;
        public static readonly Dictionary<string, int> FailWhy = new Dictionary<string, int>();
        static readonly Point[] Dirs4 = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };

        static Map TryOverland(bool loose)
        {
            var m = NewMap(0, 0);
            var h1 = Noise(16);
            var h2 = Noise(7);
            var h3 = Noise(3);
            var wx = Noise(12);
            var wy = Noise(12);
            var wet = Noise(10);
            var wood = Noise(8);
            var raw = new float[W, H];
            var elev = new float[W, H];
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    float sx = x + (wx[x, y] - 0.5f) * 18, sy = y + (wy[x, y] - 0.5f) * 9;
                    float e = Math.Min(Math.Min(x, W - 1 - x), Math.Min(y, H - 1 - y) * 2.2f);
                    raw[x, y] = Sample(h1, sx, sy) * 0.6f + Sample(h2, sx, sy) * 0.3f + h3[x, y] * 0.1f;
                    elev[x, y] = raw[x, y] + Math.Max(0, (5 - e) / 5f) * 0.5f;
                }
            var lake = new bool[W, H];
            var river = new bool[W, H];
            var pool = new bool[W, H];
            Hydro(raw, lake, river, pool);
            var water = new List<Point>();
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (lake[x, y] || river[x, y]) water.Add(new Point(x, y));
            var near = Path.DistMap(m, water, (x, y) => true);
            var rain = Shadow(raw);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    float e = elev[x, y];
                    float prox = near[x, y] == int.MaxValue ? 0 : Math.Max(0, 1 - near[x, y] / 9f);
                    float w = prox * 0.45f + rain[x, y] * 0.25f + wet[x, y] * 0.3f + (wood[x, y] - 0.5f) * 0.4f;
                    Tile t;
                    if (lake[x, y] || river[x, y]) t = Tile.DeepWater;
                    else if (e > 0.8f) t = Tile.Mountain;
                    else if (e > 0.66f) t = Tile.Hills;
                    else if (pool[x, y] || (prox > 0.75f && w > 0.76f && e < 0.55f)) t = Tile.Marsh;
                    else if (w > 0.7f) t = Tile.Wildwood;
                    else if (w > 0.55f) t = Tile.Forest;
                    else t = Rng.Chance(4) ? Tile.Forest : Tile.Plains;
                    m.T[x, y] = t;
                }
            foreach (var p in water)
                foreach (var q in m.Neighbors(p))
                    if (m.T[q.X, q.Y] is Tile.Plains or Tile.Forest && elev[q.X, q.Y] < 0.6f && Rng.Chance(lake[p.X, p.Y] ? 25 : 6)) m.T[q.X, q.Y] = Tile.Marsh;
            var taken = new List<Point>();
            var kinds = new Dictionary<Place, string>();
            int gap = loose ? 8 : 12;
            var home = new Point(-1, -1);
            var mid = new Point(W / 2, H / 2);
            int Count(Point c, int r, Func<Tile, bool> f)
            {
                int n = 0;
                for (int x = c.X - r; x <= c.X + r; x++)
                    for (int y = c.Y - r; y <= c.Y + r; y++)
                        if (Map.InB(x, y) && f(m.T[x, y])) n++;
                return n;
            }
            bool Near(Point c, int r, bool[,] mask)
            {
                for (int x = c.X - r; x <= c.X + r; x++)
                    for (int y = c.Y - r; y <= c.Y + r; y++)
                        if (Map.InB(x, y) && mask[x, y] && m.T[x, y] == Tile.DeepWater) return true;
                return false;
            }
            float Fit(Point c, string kind) => kind switch
            {
                "plains" => Count(c, 2, t => t == Tile.Plains) - Count(c, 1, t => t == Tile.DeepWater) * 3,
                "lonehill" => Count(c, 3, t => t is Tile.Plains or Tile.Forest) - Count(c, 3, t => t is Tile.Hills or Tile.Mountain or Tile.DeepWater) * 2,
                "lake" => (Near(c, 1, lake) ? 20 : Near(c, 2, lake) ? 10 : 0) + Count(c, 2, t => t == Tile.Marsh),
                "forest" => (m.T[c.X, c.Y] is Tile.Forest or Tile.Wildwood ? 6 : 0) + Count(c, 3, t => t is Tile.Forest or Tile.Wildwood) * 0.6f,
                "hills" => (m.T[c.X, c.Y] == Tile.Hills ? 6 : 0) + Count(c, 2, t => t == Tile.Hills) * 1.2f + Count(c, 3, t => t == Tile.Mountain) * 0.4f,
                "river" => (Near(c, 1, river) ? 18 : Near(c, 2, river) ? 8 : 0) - Far(c, mid) / 6f,
                "foothills" => Math.Min(Count(c, 3, t => t == Tile.Mountain), 12) * 1.5f - Count(c, 2, t => t == Tile.DeepWater) * 2,
                _ => 0,
            };
            Point Site(string kind, int want)
            {
                var best = new Point(-1, -1);
                float bs = float.MinValue;
                for (int i = 0; i < 700; i++)
                {
                    var c = new Point(Rng.Range(4, W - 5), Rng.Range(3, H - 4));
                    if (m.T[c.X, c.Y] is Tile.Mountain or Tile.DeepWater) continue;
                    if (taken.Any(o => Far(o, c) < gap)) continue;
                    float s = Fit(c, kind) + (float)Rng.Float() * 4;
                    if (want > 0)
                    {
                        int f = Far(c, home);
                        if (!loose && f < 16) continue;
                        s -= Math.Abs(f - want) * 0.25f;
                    }
                    if (s > bs) { bs = s; best = c; }
                }
                if (best.X >= 0) taken.Add(best);
                return best;
            }
            void Add(Point c, string name, string kind, int branch, string blurb, string site, Tile t)
            {
                Shape(m, c, site, lake);
                m.T[c.X, c.Y] = t;
                var pl = new Place { Name = name, Kind = kind, Branch = branch, At = c, R = new Rectangle(c.X, c.Y, 1, 1), Blurb = blurb };
                m.Places.Add(pl);
                kinds[pl] = site;
            }
            var start = WorldDb.StartTown;
            home = Site(start.Site, 0);
            if (home.X < 0) return Fail("home site");
            Add(home, start.Name, "town", start.Branch, start.Blurb, start.Site, Tile.TownSite);
            foreach (var d in WorldDb.Dungeons.OrderBy(d => d.Branch == 0 ? -1 : d.Top))
            {
                var c = Site(d.Site, 12 + d.Top * 6);
                if (c.X < 0) return Fail("dungeon site " + d.Short);
                Add(c, d.Name, "dungeon", d.Branch, d.Blurb, d.Site, Tile.StairsDown);
            }
            foreach (var t in WorldDb.Towns.Where(t => !t.Start))
            {
                var c = Site(t.Site, 0);
                if (c.X < 0) return Fail("town site " + t.Name);
                Add(c, t.Name, "town", t.Branch, t.Blurb, t.Site, Tile.TownSite);
            }
            foreach (var pl in m.Places)
            {
                bool wetSite = kinds[pl] is "lake" or "river";
                foreach (var q in m.Neighbors(pl.At))
                {
                    if (m.T[q.X, q.Y] == Tile.Mountain) m.T[q.X, q.Y] = Tile.Hills;
                    else if (m.T[q.X, q.Y] == Tile.DeepWater && !wetSite) m.T[q.X, q.Y] = Tile.Marsh;
                }
                if (!Dirs4.Any(d => m.Walkable(pl.At.X + d.X, pl.At.Y + d.Y))) m.T[pl.At.X - 1, pl.At.Y] = Tile.Plains;
            }
            var known = new List<Point>();
            if (!Roads(m, m.Places[0], known)) return Fail("roads");
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (x == 0 || y == 0 || x == W - 1 || y == H - 1) m.T[x, y] = Tile.Mountain;
            Bells(m);
            Daylight(m);
            Hollows(m);
            Connect(m, home);
            var dist = Path.DistMap(m, new[] { home }, (x, y) => m.Walkable(x, y));
            foreach (var pl in m.Places) if (dist[pl.At.X, pl.At.Y] == int.MaxValue) return Fail("unreachable " + pl.Name);
            LakeCells = RiverCells = 0;
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (m.T[x, y] is Tile.DeepWater or Tile.Bridge or Tile.Shallow) { if (lake[x, y]) LakeCells++; else if (river[x, y]) RiverCells++; }
            m.Up = home;
            m.Down = m.Places.First(p => p.Dungeon && p.Branch == 0).At;
            Reveal(m, new Rectangle(home.X, home.Y, 1, 1), 3);
            Reveal(m, new Rectangle(m.Down.X, m.Down.Y, 1, 1), 1);
            foreach (var p in known) Reveal(m, new Rectangle(p.X, p.Y, 1, 1), 1);
            m.Feeling = "Somewhere in the Weald, a bell with no clapper rings once.";
            return m;
        }

        static float Sample(float[,] n, float x, float y)
        {
            x = Math.Clamp(x, 0, W - 1.001f);
            y = Math.Clamp(y, 0, H - 1.001f);
            int ix = (int)x, iy = (int)y;
            float tx = x - ix, ty = y - iy;
            float a = n[ix, iy] + (n[ix + 1, iy] - n[ix, iy]) * tx;
            float b = n[ix, iy + 1] + (n[ix + 1, iy + 1] - n[ix, iy + 1]) * tx;
            return a + (b - a) * ty;
        }

        static void Hydro(float[,] h, bool[,] lake, bool[,] river, bool[,] pool)
        {
            var fill = new float[W, H];
            var done = new bool[W, H];
            var to = new Point[W, H];
            var order = new List<Point>(W * H);
            var q = new PriorityQueue<Point, float>();
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (x == 0 || y == 0 || x == W - 1 || y == H - 1)
                    {
                        fill[x, y] = h[x, y];
                        done[x, y] = true;
                        to[x, y] = new Point(x, y);
                        q.Enqueue(new Point(x, y), h[x, y]);
                    }
            while (q.TryDequeue(out var c, out _))
            {
                order.Add(c);
                foreach (var d in Dirs4)
                {
                    int nx = c.X + d.X, ny = c.Y + d.Y;
                    if (!Map.InB(nx, ny) || done[nx, ny]) continue;
                    done[nx, ny] = true;
                    fill[nx, ny] = Math.Max(h[nx, ny], fill[c.X, c.Y] + 0.0004f);
                    to[nx, ny] = c;
                    q.Enqueue(new Point(nx, ny), fill[nx, ny]);
                }
            }
            var acc = new float[W, H];
            for (int i = order.Count - 1; i >= 0; i--)
            {
                var c = order[i];
                acc[c.X, c.Y] += 0.5f + h[c.X, c.Y];
                var t = to[c.X, c.Y];
                if (t != c) acc[t.X, t.Y] += acc[c.X, c.Y];
            }
            var seen = new bool[W, H];
            for (int x = 1; x < W - 1; x++)
                for (int y = 1; y < H - 1; y++)
                {
                    if (seen[x, y] || fill[x, y] - h[x, y] < 0.07f) continue;
                    var blob = new List<Point> { new Point(x, y) };
                    seen[x, y] = true;
                    for (int i = 0; i < blob.Count; i++)
                        foreach (var d in Dirs4)
                        {
                            int nx = blob[i].X + d.X, ny = blob[i].Y + d.Y;
                            if (nx <= 0 || ny <= 0 || nx >= W - 1 || ny >= H - 1 || seen[nx, ny] || fill[nx, ny] - h[nx, ny] < 0.07f) continue;
                            seen[nx, ny] = true;
                            blob.Add(new Point(nx, ny));
                        }
                    foreach (var p in blob)
                        if (blob.Count >= 6) lake[p.X, p.Y] = true;
                        else pool[p.X, p.Y] = true;
                }
            float flow = Rng.Range(90, 160);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (acc[x, y] >= flow && !lake[x, y]) river[x, y] = true;
        }

        static float[,] Shadow(float[,] h)
        {
            var r = new float[W, H];
            bool east = Rng.Chance(50);
            for (int y = 0; y < H; y++)
            {
                float rain = 1;
                for (int i = 0; i < W; i++)
                {
                    int x = east ? i : W - 1 - i;
                    rain = h[x, y] > 0.68f ? Math.Max(0.1f, rain - 0.1f) : Math.Min(1, rain + 0.012f);
                    r[x, y] = rain;
                }
            }
            return r;
        }

        static void Shape(Map m, Point c, string kind, bool[,] lake)
        {
            if (kind is "lonehill" or "forest")
                foreach (var q in m.Neighbors(c))
                    if (m.T[q.X, q.Y] != Tile.DeepWater) m.T[q.X, q.Y] = kind == "forest" ? Tile.Forest : Tile.Hills;
            if (kind != "lake" || m.Neighbors(c).Any(q => lake[q.X, q.Y] && m.T[q.X, q.Y] == Tile.DeepWater)) return;
            var d = Rng.Pick(Dirs4);
            var o = new Point(c.X + d.X * 2, c.Y + d.Y * 2);
            for (int x = o.X - 1; x <= o.X + 1; x++)
                for (int y = o.Y - 1; y <= o.Y + 1; y++)
                {
                    if (x <= 1 || y <= 1 || x >= W - 2 || y >= H - 2) continue;
                    if (x != o.X && y != o.Y && Rng.Chance(50)) continue;
                    m.T[x, y] = Tile.DeepWater;
                    lake[x, y] = true;
                }
        }

        static void Hollows(Map m)
        {
            bool Rock(int x, int y)
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (!Map.InB(x + dx, y + dy) || m.T[x + dx, y + dy] != Tile.Mountain) return false;
                return x > 1 && y > 1 && x < W - 2 && y < H - 2;
            }
            int made = 0;
            for (int t = 0; t < 300 && made < 2; t++)
            {
                var c = new Point(Rng.Range(2, W - 3), Rng.Range(2, H - 3));
                if (!Rock(c.X, c.Y)) continue;
                var blob = new List<Point> { c };
                int want = Rng.Range(6, 12);
                for (int i = 0; i < 60 && blob.Count < want; i++)
                {
                    var b = Rng.Pick(blob);
                    var d = Rng.Pick(Dirs4);
                    var n = new Point(b.X + d.X, b.Y + d.Y);
                    if (blob.Contains(n) || !Rock(n.X, n.Y)) continue;
                    blob.Add(n);
                }
                if (blob.Count < 5) continue;
                foreach (var b in blob) m.T[b.X, b.Y] = Rng.Chance(30) ? Tile.Forest : Tile.Plains;
                made++;
            }
        }

        static void Connect(Map m, Point home)
        {
            Fords = Filled = Valleys = 0;
            Func<int, int, bool> walk = (x, y) => m.Walkable(x, y);
            for (int it = 0; ; it++)
            {
                var dist = Path.DistMap(m, new[] { home }, walk);
                var lost = new Point(-1, -1);
                for (int x = 0; x < W && lost.X < 0; x++)
                    for (int y = 0; y < H; y++)
                        if (dist[x, y] == int.MaxValue && m.Walkable(x, y)) { lost = new Point(x, y); break; }
                if (lost.X < 0) return;
                var pocket = Path.DistMap(m, new[] { lost }, walk);
                var cells = new List<Point>();
                var from = new HashSet<Point>();
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                    {
                        if (pocket[x, y] != int.MaxValue) cells.Add(new Point(x, y));
                        if (dist[x, y] != int.MaxValue) from.Add(new Point(x, y));
                    }
                var path = cells.Count < 3 || it > 60 ? null : Route(m, from, lost, (x, y) => x <= 0 || y <= 0 || x >= W - 1 || y >= H - 1 ? -1 : m.Walkable(x, y) ? 1 : m.T[x, y] == Tile.DeepWater ? 6 : m.T[x, y] == Tile.Mountain ? 40 : -1);
                if (path == null)
                {
                    foreach (var p in cells) m.T[p.X, p.Y] = Tile.Mountain;
                    Filled += cells.Count;
                    continue;
                }
                var pass = new Point(-1, -1);
                foreach (var p in path)
                    if (m.T[p.X, p.Y] == Tile.DeepWater) { m.T[p.X, p.Y] = Tile.Shallow; Fords++; }
                    else if (m.T[p.X, p.Y] == Tile.Mountain) { m.T[p.X, p.Y] = Tile.Hills; if (pass.X < 0) pass = p; }
                if (pass.X < 0 || cells.Count < 6) continue;
                Valleys++;
                m.Engravings[pass] = Rng.Pick(new[] { "A cairn of flat stones marks a goat-path over the crags. Someone has scratched a lantern on the top stone.", "Rungs of old iron are hammered into the rock here, leading up and over. They are warm.", "A notch in the mountains, worn by feet. A dead bell hangs from a spike in the stone, clapperless, cracked." });
                Spawn.DropAt(m, Rng.Chance(30) ? Spawn.Gold(4) : Spawn.RandomItem(3), Rng.Pick(cells));
            }
        }

        static float[,] Noise(int cell)
        {
            int gw = W / cell + 3, gh = H / cell + 3;
            var g = new float[gw, gh];
            for (int i = 0; i < gw; i++)
                for (int j = 0; j < gh; j++)
                    g[i, j] = (float)Rng.Float();
            var n = new float[W, H];
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    float fx = x / (float)cell, fy = y / (float)cell;
                    int ix = (int)fx, iy = (int)fy;
                    float tx = fx - ix, ty = fy - iy;
                    tx = tx * tx * (3 - 2 * tx);
                    ty = ty * ty * (3 - 2 * ty);
                    float a = g[ix, iy] + (g[ix + 1, iy] - g[ix, iy]) * tx;
                    float b = g[ix, iy + 1] + (g[ix + 1, iy + 1] - g[ix, iy + 1]) * tx;
                    n[x, y] = a + (b - a) * ty;
                }
            return n;
        }

        static int OverCost(Map m, int x, int y)
        {
            if (x <= 0 || y <= 0 || x >= W - 1 || y >= H - 1) return -1;
            return m.T[x, y] switch
            {
                Tile.Road or Tile.Bridge => 1,
                Tile.Plains => 2,
                Tile.Shallow => 4,
                Tile.Forest => 4,
                Tile.Hills => 5,
                Tile.Marsh => 6,
                Tile.Wildwood => 7,
                Tile.DeepWater => 9,
                Tile.Mountain => 25,
                _ => -1,
            };
        }

        static bool Roads(Map m, Place start, List<Point> known)
        {
            var net = new HashSet<Point> { start.At };
            var targets = m.Places.Where(pl => pl != start).OrderBy(pl => pl.Dungeon && pl.Branch == 0 ? 0 : 1).ThenBy(pl => Far(pl.At, start.At)).ToList();
            bool first = true;
            foreach (var pl in targets)
            {
                var path = Route(m, net, pl.At, (x, y) => OverCost(m, x, y));
                if (path == null) return false;
                foreach (var p in path)
                {
                    net.Add(p);
                    if (p == pl.At) continue;
                    var t = m.T[p.X, p.Y];
                    m.T[p.X, p.Y] = t == Tile.DeepWater ? Tile.Bridge : t == Tile.Bridge ? t : Tile.Road;
                }
                if (first) known.AddRange(path);
                first = false;
                if (pl.Dungeon && path.Count >= 2)
                {
                    var d = WorldDb.Get(pl.Branch);
                    m.Engravings[path[path.Count - 2]] = "A waymark, carved with a " + (d.Branch == 0 ? "twenty-sided die" : d.Branch == 1 ? "bell" : d.Branch == 2 ? "tooth" : "compass rose") + ": " + WorldDb.Cap(d.Name) + ". " + (d.Branch == 0 ? "Fifteen floors. One Heart." : (d.Bottom - d.Top + 1) + " floors, for those who would face what waits at levels " + d.Top + " to " + d.Bottom + ".");
                }
            }
            return true;
        }

        static List<Point> Route(Map m, HashSet<Point> from, Point to, Func<int, int, int> costOf)
        {
            var dist = new int[W, H];
            var prev = new Point[W, H];
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) dist[x, y] = int.MaxValue;
            var q = new PriorityQueue<Point, int>();
            foreach (var s in from) { dist[s.X, s.Y] = 0; prev[s.X, s.Y] = s; q.Enqueue(s, 0); }
            var dirs = new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };
            while (q.TryDequeue(out var c, out int d))
            {
                if (d > dist[c.X, c.Y]) continue;
                if (c == to) break;
                foreach (var dd in dirs)
                {
                    int nx = c.X + dd.X, ny = c.Y + dd.Y;
                    if (!Map.InB(nx, ny)) continue;
                    int cost = nx == to.X && ny == to.Y ? 1 : costOf(nx, ny);
                    if (cost < 0) continue;
                    int nd = d + cost;
                    if (nd >= dist[nx, ny]) continue;
                    dist[nx, ny] = nd;
                    prev[nx, ny] = c;
                    q.Enqueue(new Point(nx, ny), nd);
                }
            }
            if (dist[to.X, to.Y] == int.MaxValue) return null;
            var path = new List<Point>();
            var cur = to;
            while (!from.Contains(cur)) { path.Add(cur); cur = prev[cur.X, cur.Y]; }
            path.Reverse();
            return path;
        }

        static void Bells(Map m)
        {
            var bells = new List<Point>();
            for (int t = 0; t < 800 && bells.Count < 5; t++)
            {
                var p = new Point(Rng.Range(2, W - 3), Rng.Range(2, H - 3));
                if (m.T[p.X, p.Y] is not (Tile.Plains or Tile.Forest or Tile.Hills)) continue;
                if (!m.Neighbors(p).Any(q => m.T[q.X, q.Y] == Tile.Road)) continue;
                if (m.Places.Any(pl => Far(pl.At, p) < 5)) continue;
                if (bells.Any(b => Far(b, p) < 14)) continue;
                bells.Add(p);
                m.T[p.X, p.Y] = Tile.Bell;
            }
        }

        public static void Reveal(Map m, Rectangle r, int pad)
        {
            for (int x = r.X - pad; x < r.Right + pad; x++)
                for (int y = r.Y - pad; y < r.Bottom + pad; y++)
                {
                    if (!Map.InB(x, y)) continue;
                    m.Seen[x, y] = true;
                    var d = m.Def(x, y);
                    m.MemCh[x, y] = d.G;
                    m.MemFg[x, y] = d.Fg;
                }
        }

        public static Map Town(int branch)
        {
            var t = WorldDb.TownOf(branch);
            for (int i = 0; i < 60; i++)
            {
                var m = TryTown(t);
                if (m != null) return m;
            }
            throw new InvalidOperationException(t.Name + " failed to generate: " + fail);
        }

        static int TownCost(Map m, int x, int y)
        {
            if (x <= 0 || y <= 0 || x >= W - 1 || y >= H - 1) return -1;
            return m.T[x, y] switch
            {
                Tile.Road => 1,
                Tile.Meadow or Tile.TallGrass or Tile.Flowers or Tile.Field => 3,
                Tile.Tree => 5,
                _ => -1,
            };
        }

        static bool Open(Tile t) => t is Tile.Meadow or Tile.TallGrass or Tile.Flowers or Tile.Tree;

        static Map TryTown(TownDef t)
        {
            var m = NewMap(0, t.Branch);
            var wood = Noise(6);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    m.T[x, y] = wood[x, y] > 0.68f ? Tile.Tree : Rng.Chance(12) ? Tile.TallGrass : Rng.Chance(3) ? Tile.Flowers : Tile.Meadow;
            var R = new Rectangle((W - t.W) / 2, (H - t.H) / 2, t.W, t.H);
            int cx = R.X + R.Width / 2, cy = R.Y + R.Height / 2;
            for (int i = 0; i < 8; i++)
            {
                int fw = Rng.Range(6, 12), fh = Rng.Range(2, 4);
                int fx = Rng.Range(1, W - fw - 1), fy = Rng.Range(1, H - fh - 1);
                if (new Rectangle(fx - 2, fy - 2, fw + 4, fh + 4).Intersects(R)) continue;
                for (int x = fx; x < fx + fw; x++)
                    for (int y = fy; y < fy + fh; y++)
                        if (m.T[x, y] is Tile.Meadow or Tile.TallGrass) m.T[x, y] = Tile.Field;
            }
            for (int x = R.X; x < R.Right; x++)
                for (int y = R.Y; y < R.Bottom; y++)
                {
                    bool edge = x == R.X || y == R.Y || x == R.Right - 1 || y == R.Bottom - 1;
                    m.T[x, y] = edge && t.Walled ? Tile.Wall : Rng.Chance(8) ? Tile.TallGrass : Tile.Meadow;
                }
            for (int x = 0; x < W; x++) m.T[x, cy] = Tile.Road;
            for (int y = 0; y < H; y++) m.T[cx, y] = Tile.Road;
            for (int x = cx - 4; x <= cx + 4; x++)
                for (int y = cy - 2; y <= cy + 2; y++)
                    m.T[x, y] = Tile.Road;
            m.T[cx, cy] = t.Centre switch { "well" => Tile.Fountain, "vane" => Tile.Pillar, _ => Tile.Bell };
            m.Engravings[new Point(cx - 3, cy)] = t.Start ? "Lantern Hollow. Light one for the dead, and keep one for yourself." : t.Oracle ? "Nine clocks. One hermit. No refunds on prophecy." : "Gallowmere Market. The gallows-vane points whichever way your luck is blowing.";
            var net = new HashSet<Point>();
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (m.T[x, y] == Tile.Road) net.Add(new Point(x, y));
            int pad = t.Walled ? 2 : 1;
            var inner = new Rectangle(R.X + pad, R.Y + pad, R.Width - pad * 2, R.Height - pad * 2);
            var jobs = new List<string>(t.Shops);
            if (t.Temple) jobs.Add("temple");
            if (t.Inn != null) jobs.Add("inn");
            if (t.Oracle) jobs.Add("oracle");
            foreach (var job in jobs)
                if (!Building(m, inner, cx, cy, net, job, t)) return Fail(job);
            for (int i = 0, made = 0; i < t.Houses * 8 && made < t.Houses; i++)
                if (Building(m, inner, cx, cy, net, "house", t)) made++;
            if (t.Start)
            {
                Graveyard(m, inner);
                foreach (var p in net.ToList())
                {
                    if (!R.Contains(p) || (p.X + p.Y) % 7 != 0) continue;
                    var spot = m.Neighbors(p).FirstOrDefault(q => R.Contains(q) && m.T[q.X, q.Y] is Tile.Meadow or Tile.TallGrass && !m.Neighbors(q).Any(z => m.IsDoor(z)));
                    if (spot == Point.Zero) continue;
                    m.T[spot.X, spot.Y] = Tile.Brazier;
                    m.Lights.Add(new StaticLight { P = spot, R = 4, C = new Color(255, 170, 80), I = 0.6f, Flicker = true });
                }
            }
            for (int x = inner.X; x < inner.Right; x++)
                for (int y = inner.Y; y < inner.Bottom; y++)
                    if (m.T[x, y] == Tile.Meadow && Rng.Chance(2) && !m.Neighbors(new Point(x, y)).Any(q => m.IsDoor(q))) m.T[x, y] = Tile.Tree;
            Daylight(m);
            m.Up = new Point(cx + 3, cy);
            m.Down = new Point(cx, H - 1);
            var dist = Path.DistMap(m, new[] { m.Up }, (x, y) => m.Walkable(x, y) || m.T[x, y] is Tile.DoorClosed);
            if (dist[m.Down.X, m.Down.Y] == int.MaxValue) return Fail("gate");
            foreach (var r in m.Rooms) if (r.Cells.All(c => dist[c.X, c.Y] == int.MaxValue)) return Fail("sealed " + r.Theme);
            m.Places.Add(new Place { Name = t.Name, Kind = "town", Branch = t.Branch, R = R, At = m.Up, Blurb = t.Blurb });
            m.Feeling = t.Blurb;
            Reveal(m, new Rectangle(0, 0, W, H), 0);
            Spawn.PopulateTown(m, t);
            return m;
        }

        static bool Building(Map m, Rectangle inner, int cx, int cy, HashSet<Point> net, string job, TownDef t)
        {
            for (int tries = 0; tries < 250; tries++)
            {
                int w = job switch { "temple" => Rng.Range(9, 12), "inn" => Rng.Range(9, 11), "house" => Rng.Range(5, 8), _ => Rng.Range(6, 9) };
                int h = job is "temple" or "inn" ? Rng.Range(6, 7) : Rng.Range(5, 6);
                if (w > inner.Width || h > inner.Height) return false;
                int x0 = Rng.Range(inner.X, inner.Right - w), y0 = Rng.Range(inner.Y, inner.Bottom - h);
                bool clear = true;
                for (int x = x0 - 1; x <= x0 + w && clear; x++)
                    for (int y = y0 - 1; y <= y0 + h && clear; y++)
                        if (!Map.InB(x, y) || !Open(m.T[x, y])) clear = false;
                if (!clear) continue;
                int dx = cx - (x0 + w / 2), dy = cy - (y0 + h / 2);
                Point door, normal;
                if (Math.Abs(dx) > Math.Abs(dy) * 3)
                {
                    normal = new Point(dx >= 0 ? 1 : -1, 0);
                    door = new Point(normal.X > 0 ? x0 + w - 1 : x0, Rng.Range(y0 + 1, y0 + h - 2));
                }
                else
                {
                    normal = new Point(0, dy >= 0 ? 1 : -1);
                    door = new Point(Rng.Range(x0 + 1, x0 + w - 2), normal.Y > 0 ? y0 + h - 1 : y0);
                }
                var outside = new Point(door.X + normal.X, door.Y + normal.Y);
                var inside = new Point(door.X - normal.X, door.Y - normal.Y);
                var saved = new List<(Point, Tile)>();
                var room = new Room { R = new Rectangle(x0 + 1, y0 + 1, w - 2, h - 2), Lit = true, Theme = job == "house" ? "house" : job is "temple" or "oracle" or "inn" ? job : "shop" };
                for (int x = x0; x < x0 + w; x++)
                    for (int y = y0; y < y0 + h; y++)
                    {
                        saved.Add((new Point(x, y), m.T[x, y]));
                        bool edge = x == x0 || y == y0 || x == x0 + w - 1 || y == y0 + h - 1;
                        m.T[x, y] = edge ? Tile.Wall : Tile.Floor;
                        if (!edge) room.Cells.Add(new Point(x, y));
                    }
                m.T[door.X, door.Y] = job == "house" ? Tile.DoorClosed : Tile.DoorOpen;
                var path = Route(m, net, outside, (x, y) => TownCost(m, x, y));
                if (path == null)
                {
                    foreach (var (p, old) in saved) m.T[p.X, p.Y] = old;
                    continue;
                }
                foreach (var p in path) { m.T[p.X, p.Y] = Tile.Road; net.Add(p); }
                m.Rooms.Add(room);
                var c = room.Center;
                if (c == inside) c = room.Cells.OrderByDescending(p => Path.Cheb(p, inside)).First();
                switch (job)
                {
                    case "temple":
                        foreach (var p in room.Cells) if (p.X == c.X || p == inside) m.T[p.X, p.Y] = Tile.Rug;
                        m.T[c.X, c.Y] = Tile.Altar;
                        m.FeatureUses[c] = 0;
                        m.Lights.Add(new StaticLight { P = c, R = 4, C = new Color(230, 230, 255), I = 0.5f });
                        break;
                    case "inn":
                        foreach (var p in room.Cells) if (p.Y == c.Y) m.T[p.X, p.Y] = Tile.Rug;
                        var hearth = room.Cells.Where(p => p != inside && Path.Cheb(p, inside) > 1).OrderByDescending(p => Path.Cheb(p, inside)).First();
                        m.T[hearth.X, hearth.Y] = Tile.Brazier;
                        m.Lights.Add(new StaticLight { P = hearth, R = 5, C = new Color(255, 150, 60), I = 0.8f, Flicker = true });
                        m.Engravings[inside] = "Carved over the door: " + WorldDb.Cap(t.Inn) + ". Beds, bread, and no questions about what you brought back up.";
                        break;
                    case "oracle":
                        m.T[c.X, c.Y] = Tile.OracleDie;
                        m.Lights.Add(new StaticLight { P = c, R = 4, C = new Color(160, 160, 255), I = 0.8f });
                        break;
                    case "house":
                        break;
                    default:
                        {
                            var shop = new Shop { Id = m.Shops.Count, Room = room, Door = door, Inside = inside, Kind = job, Level = t.Stock };
                            room.ShopId = shop.Id;
                            m.Shops.Add(shop);
                            break;
                        }
                }
                return true;
            }
            return false;
        }

        static void Graveyard(Map m, Rectangle inner)
        {
            for (int tries = 0; tries < 200; tries++)
            {
                int gw = Rng.Range(9, 13), gh = Rng.Range(4, 5);
                int x0 = Rng.Range(inner.X, inner.Right - gw), y0 = Rng.Range(inner.Y, inner.Bottom - gh);
                bool clear = true;
                for (int x = x0 - 1; x <= x0 + gw && clear; x++)
                    for (int y = y0 - 1; y <= y0 + gh && clear; y++)
                        if (!Map.InB(x, y) || !Open(m.T[x, y])) clear = false;
                if (!clear) continue;
                for (int x = x0; x < x0 + gw; x++)
                    for (int y = y0; y < y0 + gh; y++)
                    {
                        m.T[x, y] = x % 2 == 0 && y % 2 == 0 ? Tile.Grave : Rng.Chance(20) ? Tile.Flowers : Tile.Meadow;
                        if (m.T[x, y] == Tile.Grave && Rng.Chance(35)) m.Lights.Add(new StaticLight { P = new Point(x, y), R = 2, C = new Color(255, 190, 110), I = 0.5f, Flicker = true });
                    }
                var mark = new Point(x0 + 1, y0 + gh - 1);
                if (m.T[mark.X, mark.Y] == Tile.Grave) mark = new Point(x0, y0 + gh - 1);
                if (m.T[mark.X, mark.Y] != Tile.Grave) m.Engravings[mark] = "The lamplighters' yard. " + Lore.Epitaph();
                return;
            }
        }

        public static void CheckEncounter(bool resting)
        {
            var m = Dm.Map;
            var p = Dm.Player;
            if (!m.Wild || Dm.GameOver) return;
            if (m.Places.Any(pl => !pl.Dungeon && Far(pl.At, p.Pos) <= 3)) return;
            var t = m.T[p.Pos.X, p.Pos.Y];
            int odds = t switch { Tile.Road or Tile.Bridge => 20, Tile.Forest => 9, Tile.Hills => 10, Tile.Marsh => 8, Tile.Wildwood => 7, _ => 13 };
            odds = Math.Max(5, (int)Math.Round(odds * 1.5f / (0.75f + Wildness(m, p.Pos) * 0.375f)));
            if (resting) odds *= 2;
            if (Rng.D(odds) != 1) return;
            Dm.AddDie(odds, 1, 0, 1, "encounter check", false);
            Begin(t);
        }

        public static float Wildness(Map m, Point p)
        {
            var home = m.Places.FirstOrDefault(pl => pl.Branch == WorldDb.StartTown.Branch);
            float w = Far(p, home?.At ?? m.Up) / 18f;
            int road = 4;
            for (int x = p.X - 3; x <= p.X + 3; x++)
                for (int y = p.Y - 3; y <= p.Y + 3; y++)
                    if (Map.InB(x, y) && m.T[x, y] is Tile.Road or Tile.Bridge) road = Math.Min(road, Path.Cheb(p, new Point(x, y)));
            w -= (4 - road) * 0.35f;
            foreach (var pl in m.Places)
            {
                int f = Far(pl.At, p);
                if (pl.Dungeon) w += Math.Max(0, 6 - f) * 0.08f * WorldDb.Get(pl.Branch).Top;
                else w -= Math.Max(0, 10 - f) * (pl == home ? 0.2f : 0.12f);
            }
            w += m.T[p.X, p.Y] switch { Tile.Wildwood => 1f, Tile.Marsh or Tile.Shallow => 0.6f, Tile.Hills => 0.4f, Tile.Forest => 0.3f, _ => 0f };
            return Math.Max(0, w);
        }

        public static string WildWord(float w) => w < 1 ? "{good}tame{/}" : w < 2 ? "wild" : w < 3.5f ? "{warn}dangerous{/}" : "{bad}haunted{/}";

        static readonly Dictionary<string, string[]> Pools = new Dictionary<string, string[]>
        {
            ["plains"] = new[] { "jackal", "bandit", "orc", "hobgoblin", "wolf", "gnoll", "worg", "ogre", "hill_giant", "manticore" },
            ["forest"] = new[] { "goblin", "wolf", "spider", "bugbear", "wererat", "owlbear", "giant_spider", "displacer", "young_green" },
            ["wildwood"] = new[] { "goblin", "wolf", "spider", "bugbear", "owlbear", "giant_spider", "troll", "displacer", "young_green" },
            ["hills"] = new[] { "goblin", "orc", "hobgoblin", "orc_archer", "gnoll", "ogre", "harpy", "minotaur", "hill_giant", "manticore" },
            ["marsh"] = new[] { "rat", "centipede", "stirge", "zombie", "lizardman", "ghoul", "troll", "young_black" },
        };

        public static void Begin(Tile t)
        {
            var m = Dm.Map;
            var p = Dm.Player;
            var home = m.Places.FirstOrDefault(pl => pl.Branch == WorldDb.StartTown.Branch)?.At ?? m.Up;
            int danger = Math.Clamp(1 + (int)Wildness(m, p.Pos), 1, 6);
            string terrain = t switch { Tile.Forest => "forest", Tile.Wildwood => "wildwood", Tile.Hills => "hills", Tile.Marsh or Tile.Shallow => "marsh", _ => "plains" };
            var cands = Pools[terrain].Select(MonsterDb.Get).Where(d => d != null && d.Depth <= danger + 1).ToList();
            var def = cands.Count == 0 ? MonsterDb.Get("jackal") : Rng.Weighted(cands, d => d.Freq * (d.Depth >= danger - 1 ? 3 : 1));
            int n = Math.Clamp(Rng.Range(def.GroupMin, def.GroupMax), 1, 2 + danger / 2);
            int ps = Rng.D(6), ms = Rng.D(6);
            var spec = new EncounterSpec { Terrain = t, Def = def, Count = n, PlayerSurprised = ps <= 2 && ms > 2, MonstersSurprised = ms <= 2 && ps > 2 };
            Dm.AddDie(6, ps, 0, 3, "your surprise", ps > 2);
            Dm.AddDie(6, ms, 0, 3, "their surprise", ms <= 2);
            string where = t switch { Tile.Road or Tile.Bridge => "on the road", Tile.Forest => "in the forest", Tile.Wildwood => "in the wildwood", Tile.Hills => "in the hills", Tile.Marsh => "in the marsh", Tile.Shallow => "at the ford", _ => "on the plains" };
            string what = n == 1 ? Item.Article(def.Name) : n + " " + MonsterDb.PluralName(def);
            Synth.Play(Sfx.Shriek, 0.4f);
            Dm.Msg("{bad}Encounter " + where + "! You come upon " + what + "." + (spec.PlayerSurprised ? " They have the drop on you!" : spec.MonstersSurprised ? " They haven't noticed you yet." : "") + "{/}", true);
            Dm.Msg("{dim}Walk off the edge of the map to flee back to the overland map.{/}");
            Pending = spec;
            Dm.Levels.Remove(99 * 100 + danger);
            Dm.EnterLevel(danger, Dm.Arrival.Down, 99);
        }

        public static Map Encounter(int depth)
        {
            var s = Pending ?? new EncounterSpec { Terrain = Tile.Plains, Def = MonsterDb.Get("jackal"), Count = 2 };
            Pending = null;
            var m = NewMap(depth, 99);
            var n = Noise(5);
            var n2 = Noise(9);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    float a = n[x, y], b = n2[x, y];
                    m.T[x, y] = s.Terrain switch
                    {
                        Tile.Forest => a > 0.74f ? Tile.Pine : a > 0.45f ? Tile.Tree : Rng.Chance(20) ? Tile.TallGrass : Tile.Meadow,
                        Tile.Wildwood => a > 0.62f ? Tile.Pine : a > 0.3f ? Tile.Tree : Tile.TallGrass,
                        Tile.Hills => a > 0.78f ? Tile.Mountain : a > 0.64f ? Tile.Rubble : Rng.Chance(15) ? Tile.TallGrass : Tile.Meadow,
                        Tile.Marsh or Tile.Shallow => b > 0.74f ? Tile.DeepWater : b > 0.5f ? Tile.Shallow : a > 0.7f ? Tile.Tree : Tile.TallGrass,
                        _ => a > 0.76f ? Tile.Tree : Rng.Chance(18) ? Tile.TallGrass : Rng.Chance(3) ? Tile.Flowers : Tile.Meadow,
                    };
                }
            if (s.Terrain is Tile.Road or Tile.Bridge)
                for (int x = 0; x < W; x++)
                {
                    int y = H / 2 + (int)Math.Round(Math.Sin(x * 0.07) * 3);
                    m.T[x, y] = Tile.Road;
                    m.T[x, y + 1] = Tile.Road;
                }
            var c = new Point(W / 2, H / 2);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                    if ((edge && !m.Walkable(x, y)) || (Path.Cheb(new Point(x, y), c) <= 2 && m.T[x, y] != Tile.Road)) m.T[x, y] = Tile.Meadow;
                }
            Func<int, int, bool> walk = (x, y) => m.Walkable(x, y);
            var dist = Path.DistMap(m, new[] { c }, walk);
            Point exit = new Point(-1, -1);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (dist[x, y] == int.MaxValue) { if (m.Walkable(x, y)) m.T[x, y] = m.T[x, y] == Tile.Shallow ? Tile.DeepWater : Tile.Pine; continue; }
                    bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                    if (edge && (exit.X < 0 || dist[x, y] < dist[exit.X, exit.Y])) exit = new Point(x, y);
                }
            if (exit.X < 0)
            {
                for (int x = 0; x <= c.X; x++) m.T[x, c.Y] = Tile.Meadow;
                exit = new Point(0, c.Y);
                dist = Path.DistMap(m, new[] { c }, walk);
            }
            m.Up = c;
            m.Down = exit;
            Daylight(m);
            int near = s.PlayerSurprised ? 4 : 9, far = s.PlayerSurprised ? 7 : 15;
            var spot = m.RandomFloor(q => Path.Cheb(q, c) >= near && Path.Cheb(q, c) <= far && dist[q.X, q.Y] != int.MaxValue);
            for (int i = 0; i < s.Count; i++)
            {
                var mon = Spawn.PlaceMonster(m, s.Def, spot, s.MonstersSurprised);
                if (mon == null) continue;
                if (!s.MonstersSurprised) { mon.Aware = 40; mon.LastSeen = c; }
                if (s.PlayerSurprised) mon.Energy = Dm.ActCost * 2;
            }
            if (s.Def.Has(MF.Hands) && Rng.Chance(50)) Spawn.DropAt(m, Spawn.Gold(depth * 3), spot);
            if (Rng.Chance(25)) Spawn.DropAt(m, Spawn.RandomItem(depth + 1), spot);
            return m;
        }
    }
}
