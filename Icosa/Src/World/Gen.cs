using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class Gen
    {
        enum Style { Rooms, Caves, Mixed }

        class Design
        {
            public HashSet<Point> Room = new HashSet<Point>();
            public HashSet<Point> Hall = new HashSet<Point>();
            public Point Door;
            public bool Cave;
        }

        public static Map Generate(int depth)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var m = TryGenerate(depth);
                if (m != null) return m;
            }
            return TryGenerate(depth, true);
        }

        static Map TryGenerate(int depth, bool force = false)
        {
            var m = new Map(depth);
            SetTheme(m);
            for (int x = 0; x < Map.W; x++)
                for (int y = 0; y < Map.H; y++)
                    m.T[x, y] = (x == 0 || y == 0 || x == Map.W - 1 || y == Map.H - 1) ? Tile.PermWall : Tile.Wall;

            Style style = depth switch
            {
                4 => Style.Mixed,
                6 => Style.Caves,
                8 => Style.Mixed,
                10 or 11 or 12 => Rng.Chance(60) ? Style.Caves : Style.Mixed,
                13 or 14 => Style.Mixed,
                _ => Rng.Chance(15) ? Style.Mixed : Style.Rooms,
            };

            Room finalHall = null;
            if (depth == Rules.MaxDepth) finalHall = CarveFinalHall(m);
            else
            {
                var first = MakeDesign(style, true);
                var c = new Point(Rng.Range(30, 70), Rng.Range(12, 24));
                var bb = Bounds(first.Room);
                var off = new Point(c.X - bb.Center.X, c.Y - bb.Center.Y);
                if (!Fits(m, first, off, null)) off = new Point(Map.W / 2 - bb.Center.X, Map.H / 2 - bb.Center.Y);
                Carve(m, first, off, null, false);
            }

            int target = style == Style.Caves ? 22 : 30;
            int placed = 0;
            for (int i = 0; i < 900 && placed < target; i++)
            {
                if (!FindAttach(m, out var site, out var dir)) continue;
                var d = MakeDesign(style, false);
                if (!Orient(d, dir, Rng.Chance(style == Style.Caves ? 25 : 55) && !d.Cave)) continue;
                var off = new Point(site.X - d.Door.X, site.Y - d.Door.Y);
                if (!Fits(m, d, off, site)) continue;
                Carve(m, d, off, site, true);
                placed++;
            }
            if (placed < 8 && !force) return null;

            AddLoops(m, depth >= 10 ? 14 : 9);
            CleanDoors(m);
            AddLakes(m, depth);
            if (!PlaceStairs(m, finalHall)) { if (!force) return null; }
            Decorate(m, finalHall);
            AddVault(m);
            SecretAndLocked(m);
            LightRooms(m);
            if (depth == Rules.MaxDepth) SetupFinal(m, finalHall);
            Spawn.Populate(m);
            return m;
        }

        static void SetTheme(Map m)
        {
            int d = m.Depth;
            (Color wf, Color wb, Color ff) = d switch
            {
                <= 3 => (new Color(150, 140, 125), new Color(62, 56, 48), new Color(110, 104, 94)),
                4 => (new Color(165, 125, 85), new Color(72, 50, 32), new Color(120, 100, 80)),
                5 => (new Color(185, 175, 150), new Color(64, 60, 52), new Color(120, 115, 104)),
                6 => (new Color(130, 100, 150), new Color(42, 30, 52), new Color(100, 90, 115)),
                7 => (new Color(100, 145, 150), new Color(28, 50, 58), new Color(90, 115, 120)),
                8 => (new Color(110, 140, 95), new Color(36, 52, 32), new Color(95, 110, 85)),
                9 => (new Color(140, 140, 165), new Color(46, 46, 62), new Color(105, 105, 120)),
                <= 12 => (new Color(120, 90, 155), new Color(36, 22, 52), new Color(95, 85, 115)),
                <= 14 => (new Color(170, 80, 55), new Color(64, 22, 16), new Color(120, 90, 80)),
                _ => (new Color(220, 190, 120), new Color(50, 30, 30), new Color(130, 110, 90)),
            };
            m.WallFg = wf;
            m.WallBg = wb;
            m.FloorFg = ff;
            m.Ambient = 1.0f;
        }

        static Rectangle Bounds(IEnumerable<Point> pts)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var p in pts)
            {
                x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y);
                x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y);
            }
            return new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        static Design MakeDesign(Style style, bool first)
        {
            var d = new Design();
            int roll = Rng.Int(100);
            bool cave = style == Style.Caves ? roll < 70 : style == Style.Mixed ? roll < 35 : roll < 6;
            if (cave)
            {
                d.Cave = true;
                d.Room = CaveBlob(first ? 22 : Rng.Range(8, 18), first ? 12 : Rng.Range(5, 10));
                if (d.Room.Count < 10) d.Room = Rect(Rng.Range(4, 8), Rng.Range(3, 5));
                return d;
            }
            int r2 = Rng.Int(100);
            if (first) d.Room = Rect(Rng.Range(8, 14), Rng.Range(5, 8));
            else if (r2 < 52) d.Room = Rect(Rng.Range(3, 12), Rng.Range(3, 7));
            else if (r2 < 70) d.Room = Cross();
            else if (r2 < 82) d.Room = Ellipse(Rng.Range(3, 7), Rng.Range(2, 4));
            else if (r2 < 92) d.Room = Rect(Rng.Range(10, 18), Rng.Range(5, 8));
            else d.Room = Rect(Rng.Range(1, 2) == 1 ? 1 : 2, Rng.Range(4, 9));
            return d;
        }

        static HashSet<Point> Rect(int w, int h)
        {
            var s = new HashSet<Point>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    s.Add(new Point(x, y));
            return s;
        }

        static HashSet<Point> Cross()
        {
            var s = new HashSet<Point>();
            int w1 = Rng.Range(3, 12), h1 = Rng.Range(2, 4);
            int w2 = Rng.Range(2, Math.Max(3, w1 - 2)), h2 = Rng.Range(4, 8);
            int ox = Rng.Range(0, Math.Max(0, w1 - w2));
            int oy = -Rng.Range(1, Math.Max(1, h2 - h1));
            for (int x = 0; x < w1; x++) for (int y = 0; y < h1; y++) s.Add(new Point(x, y));
            for (int x = 0; x < w2; x++) for (int y = 0; y < h2; y++) s.Add(new Point(x + ox, y + oy));
            return s;
        }

        static HashSet<Point> Ellipse(int rx, int ry)
        {
            var s = new HashSet<Point>();
            for (int x = -rx; x <= rx; x++)
                for (int y = -ry; y <= ry; y++)
                    if ((x * x) / (float)(rx * rx + 0.5f) + (y * y) / (float)(ry * ry + 0.5f) <= 1f) s.Add(new Point(x + rx, y + ry));
            return s;
        }

        static HashSet<Point> CaveBlob(int w, int h)
        {
            var g = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    g[x, y] = x > 0 && y > 0 && x < w - 1 && y < h - 1 && Rng.Chance(55);
            for (int it = 0; it < 5; it++)
            {
                var n = new bool[w, h];
                for (int x = 1; x < w - 1; x++)
                    for (int y = 1; y < h - 1; y++)
                    {
                        int c = 0;
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                                if (g[x + dx, y + dy]) c++;
                        n[x, y] = g[x, y] ? c >= 4 : c >= 6;
                    }
                g = n;
            }
            var best = new HashSet<Point>();
            var seen = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!g[x, y] || seen[x, y]) continue;
                    var comp = new HashSet<Point>();
                    var q = new Queue<Point>();
                    q.Enqueue(new Point(x, y));
                    seen[x, y] = true;
                    while (q.Count > 0)
                    {
                        var p = q.Dequeue();
                        comp.Add(p);
                        foreach (var dd in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
                        {
                            int nx = p.X + dd.X, ny = p.Y + dd.Y;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[nx, ny] || !g[nx, ny]) continue;
                            seen[nx, ny] = true;
                            q.Enqueue(new Point(nx, ny));
                        }
                    }
                    if (comp.Count > best.Count) best = comp;
                }
            return best;
        }

        static bool Orient(Design d, Point dir, bool hallway)
        {
            var cands = d.Room.Where(c => !d.Room.Contains(new Point(c.X - dir.X, c.Y - dir.Y))).ToList();
            if (cands.Count == 0) return false;
            var c0 = Rng.Pick(cands);
            var door = new Point(c0.X - dir.X, c0.Y - dir.Y);
            if (hallway)
            {
                int len = Rng.Range(2, 9);
                for (int i = 0; i < len; i++) d.Hall.Add(new Point(door.X - dir.X * i, door.Y - dir.Y * i));
                door = new Point(door.X - dir.X * len, door.Y - dir.Y * len);
            }
            d.Door = door;
            return true;
        }

        static bool FindAttach(Map m, out Point site, out Point dir)
        {
            site = Point.Zero;
            dir = Point.Zero;
            var dirs = new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };
            for (int t = 0; t < 200; t++)
            {
                int x = Rng.Range(2, Map.W - 3), y = Rng.Range(2, Map.H - 3);
                if (m.T[x, y] != Tile.Wall) continue;
                foreach (var dd in dirs)
                {
                    var f = new Point(x - dd.X, y - dd.Y);
                    var b = new Point(x + dd.X, y + dd.Y);
                    if (!Map.InB(f) || !Map.InB(b)) continue;
                    var tf = m.T[f.X, f.Y];
                    if ((tf == Tile.Floor || tf == Tile.Corridor) && m.T[b.X, b.Y] == Tile.Wall)
                    {
                        var p1 = new Point(x + dd.Y, y + dd.X);
                        var p2 = new Point(x - dd.Y, y - dd.X);
                        if (m.T[p1.X, p1.Y] != Tile.Wall || m.T[p2.X, p2.Y] != Tile.Wall) continue;
                        site = new Point(x, y);
                        dir = dd;
                        return true;
                    }
                }
            }
            return false;
        }

        static bool Fits(Map m, Design d, Point off, Point? site)
        {
            foreach (var c in d.Room.Concat(d.Hall))
            {
                int x = c.X + off.X, y = c.Y + off.Y;
                if (x < 1 || y < 1 || x > Map.W - 2 || y > Map.H - 2) return false;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (site.HasValue && nx == site.Value.X && ny == site.Value.Y) continue;
                        if (!Map.InB(nx, ny)) return false;
                        if (m.T[nx, ny] != Tile.Wall && m.T[nx, ny] != Tile.PermWall) return false;
                    }
            }
            return true;
        }

        static void Carve(Map m, Design d, Point off, Point? site, bool door)
        {
            var room = new Room();
            foreach (var c in d.Room)
            {
                var p = new Point(c.X + off.X, c.Y + off.Y);
                m.T[p.X, p.Y] = Tile.Floor;
                room.Cells.Add(p);
            }
            foreach (var c in d.Hall)
            {
                var p = new Point(c.X + off.X, c.Y + off.Y);
                m.T[p.X, p.Y] = Tile.Corridor;
            }
            room.R = Bounds(room.Cells);
            room.Theme = d.Cave ? "cave" : null;
            m.Rooms.Add(room);
            if (site.HasValue && door)
            {
                var s = site.Value;
                m.T[s.X, s.Y] = (d.Cave || Rng.Chance(30)) ? Tile.Corridor : Tile.DoorClosed;
                if (m.T[s.X, s.Y] == Tile.DoorClosed && Rng.Chance(30)) m.T[s.X, s.Y] = Tile.DoorOpen;
            }
        }

        static void AddLoops(Map m, int count)
        {
            var cands = new List<(Point p, Point a, Point b)>();
            for (int x = 2; x < Map.W - 2; x++)
                for (int y = 2; y < Map.H - 2; y++)
                {
                    if (m.T[x, y] != Tile.Wall) continue;
                    bool lr = m.Walkable(x - 1, y) && m.Walkable(x + 1, y) && m.IsWallish(x, y - 1) && m.IsWallish(x, y + 1);
                    bool ud = m.Walkable(x, y - 1) && m.Walkable(x, y + 1) && m.IsWallish(x - 1, y) && m.IsWallish(x + 1, y);
                    if (lr) cands.Add((new Point(x, y), new Point(x - 1, y), new Point(x + 1, y)));
                    else if (ud) cands.Add((new Point(x, y), new Point(x, y - 1), new Point(x, y + 1)));
                }
            Rng.Shuffle(cands);
            int made = 0;
            foreach (var (p, a, b) in cands)
            {
                if (made >= count) break;
                var dist = Path.DistMap(m, new[] { a }, (x, y) => m.Walkable(x, y) || m.T[x, y] is Tile.DoorClosed or Tile.DoorLocked, 30);
                int d = dist[b.X, b.Y];
                if (d > 22)
                {
                    m.T[p.X, p.Y] = Rng.Chance(50) ? Tile.DoorClosed : Tile.Corridor;
                    made++;
                }
            }
        }

        static void CleanDoors(Map m)
        {
            for (int x = 1; x < Map.W - 1; x++)
                for (int y = 1; y < Map.H - 1; y++)
                {
                    if (m.T[x, y] is not (Tile.DoorClosed or Tile.DoorOpen)) continue;
                    bool lr = m.IsWallish(x - 1, y) && m.IsWallish(x + 1, y) && !m.IsWallish(x, y - 1) && !m.IsWallish(x, y + 1);
                    bool ud = m.IsWallish(x, y - 1) && m.IsWallish(x, y + 1) && !m.IsWallish(x - 1, y) && !m.IsWallish(x + 1, y);
                    if (!lr && !ud) m.T[x, y] = Tile.Floor;
                    else
                    {
                        foreach (var q in m.Neighbors(new Point(x, y)))
                            if (q.X != x && q.Y != y) continue;
                            else if (m.T[q.X, q.Y] is Tile.DoorClosed or Tile.DoorOpen) { m.T[x, y] = Tile.Corridor; break; }
                    }
                }
        }

        static bool Connected(Map m)
        {
            Point? start = null;
            int total = 0;
            for (int x = 0; x < Map.W; x++)
                for (int y = 0; y < Map.H; y++)
                    if (Pass(m, x, y))
                    {
                        total++;
                        start ??= new Point(x, y);
                    }
            if (start == null) return false;
            var dist = Path.DistMap(m, new[] { start.Value }, (x, y) => Pass(m, x, y));
            int reach = 0;
            for (int x = 0; x < Map.W; x++)
                for (int y = 0; y < Map.H; y++)
                    if (Pass(m, x, y) && dist[x, y] != int.MaxValue) reach++;
            return reach == total;
        }

        static bool Pass(Map m, int x, int y)
        {
            var t = m.T[x, y];
            if (t is Tile.Chasm or Tile.Lava or Tile.DeepWater) return false;
            return Map.Defs[t].Walk || t is Tile.DoorClosed or Tile.DoorLocked or Tile.DoorSecret;
        }

        static void AddLakes(Map m, int depth)
        {
            int lakes;
            Tile liquid;
            switch (depth)
            {
                case 7: lakes = 6; liquid = Tile.DeepWater; break;
                case 13: case 14: lakes = 4; liquid = Tile.Lava; break;
                case 9: case 11: lakes = 3; liquid = Tile.Chasm; break;
                case 15: lakes = 2; liquid = Tile.Lava; break;
                default:
                    lakes = Rng.Chance(45) ? Rng.Range(1, 2) : 0;
                    liquid = Rng.Chance(70) ? Tile.DeepWater : (depth >= 3 && Rng.Chance(50) ? Tile.Chasm : Tile.DeepWater);
                    break;
            }
            for (int i = 0; i < lakes; i++)
            {
                for (int tries = 0; tries < 15; tries++)
                {
                    var blob = CaveBlob(Rng.Range(6, 16), Rng.Range(4, 9));
                    if (blob.Count < 8) continue;
                    var bb = Bounds(blob);
                    var off = new Point(Rng.Range(1, Map.W - bb.Width - 2), Rng.Range(1, Map.H - bb.Height - 2));
                    var changed = new List<(Point, Tile)>();
                    foreach (var c in blob)
                    {
                        var p = new Point(c.X + off.X, c.Y + off.Y);
                        if (!Map.InB(p)) continue;
                        if (m.T[p.X, p.Y] is Tile.Floor or Tile.Corridor or Tile.Grass)
                        {
                            changed.Add((p, m.T[p.X, p.Y]));
                            m.T[p.X, p.Y] = liquid;
                        }
                    }
                    if (changed.Count < 6 || !Connected(m))
                    {
                        foreach (var (p, t) in changed) m.T[p.X, p.Y] = t;
                        continue;
                    }
                    if (liquid == Tile.DeepWater)
                        foreach (var (p, _) in changed)
                            foreach (var q in m.Neighbors(p))
                                if (m.T[q.X, q.Y] is Tile.Floor or Tile.Corridor && Rng.Chance(80)) m.T[q.X, q.Y] = Tile.Shallow;
                    if (liquid == Tile.Lava)
                        foreach (var (p, _) in changed)
                            if (Rng.OneIn(4)) m.Lights.Add(new StaticLight { P = p, R = 2, C = new Color(255, 100, 30), I = 0.6f, Flicker = true });
                    break;
                }
            }
        }

        static bool PlaceStairs(Map m, Room finalHall)
        {
            var floors = new List<Point>();
            for (int x = 1; x < Map.W - 1; x++)
                for (int y = 1; y < Map.H - 1; y++)
                    if (m.T[x, y] == Tile.Floor && (finalHall == null || !finalHall.R.Contains(new Point(x, y)))) floors.Add(new Point(x, y));
            if (floors.Count < 20) return false;
            Point up = Rng.Pick(floors);
            if (finalHall != null) up = floors.OrderBy(p => p.X).Take(30).OrderBy(_ => Rng.Int(100)).First();
            var dist = Path.DistMap(m, new[] { up }, (x, y) => Pass(m, x, y));
            m.Up = up;
            m.T[up.X, up.Y] = Tile.StairsUp;
            if (m.Depth < Rules.MaxDepth)
            {
                var far = floors.Where(p => dist[p.X, p.Y] != int.MaxValue && p != up).OrderByDescending(p => dist[p.X, p.Y]).Take(12).ToList();
                if (far.Count == 0) return false;
                var down = Rng.Pick(far);
                m.Down = down;
                m.T[down.X, down.Y] = Tile.StairsDown;
            }
            return true;
        }

        static Room CarveFinalHall(Map m)
        {
            var r = new Rectangle(66, 4, 31, 28);
            var room = new Room { R = r, Theme = "final", Lit = true };
            for (int x = r.X; x < r.Right; x++)
                for (int y = r.Y; y < r.Bottom; y++)
                {
                    m.T[x, y] = Tile.Floor;
                    room.Cells.Add(new Point(x, y));
                }
            m.Rooms.Add(room);
            return room;
        }

        static void SetupFinal(Map m, Room hall)
        {
            var r = hall.R;
            int sx = r.X + 12;
            for (int y = r.Y; y < r.Bottom; y++)
                if (y != r.Y + 4 && y != r.Bottom - 5) m.T[sx, y] = Tile.Screen;
            for (int x = r.X + 2; x < sx - 1; x += 3)
            {
                m.T[x, r.Y + 2] = Tile.Pillar;
                m.T[x, r.Bottom - 3] = Tile.Pillar;
            }
            int cy = r.Y + r.Height / 2;
            for (int x = sx + 4; x < r.Right - 2; x++)
                for (int y = cy - 3; y <= cy + 3; y++)
                    m.T[x, y] = Tile.Dais;
            for (int x = r.X; x < sx; x++) m.T[x, cy] = Tile.Rug;
            m.T[r.Right - 3, cy - 5] = Tile.Brazier;
            m.T[r.Right - 3, cy + 5] = Tile.Brazier;
            m.T[sx + 3, cy - 5] = Tile.Brazier;
            m.T[sx + 3, cy + 5] = Tile.Brazier;
            m.T[r.X + 1, r.Y + 1] = Tile.Brazier;
            m.T[r.X + 1, r.Bottom - 2] = Tile.Brazier;
            foreach (var (x, y) in new[] { (r.Right - 3, cy - 5), (r.Right - 3, cy + 5), (sx + 3, cy - 5), (sx + 3, cy + 5), (r.X + 1, r.Y + 1), (r.X + 1, r.Bottom - 2) })
                m.Lights.Add(new StaticLight { P = new Point(x, y), R = 6, C = new Color(255, 150, 60), I = 1f, Flicker = true });
            var heart = Item.Make("heart");
            m.Drop(heart, new Point(r.Right - 4, cy));
            var lich = new Monster(MonsterDb.Get("referee"));
            lich.Pos = new Point(r.Right - 7, cy);
            lich.Sleeping = true;
            lich.Home = lich.Pos;
            m.Monsters.Add(lich);
            m.Feeling = "Somewhere ahead, dice clatter on wood. A dry voice murmurs: \"Roll for initiative.\"";
            for (int x = r.X; x < r.Right; x++)
                for (int y = r.Y; y < r.Bottom; y++)
                    m.Lit[x, y] = x > sx;
            m.Engravings[new Point(r.X + 2, cy)] = "Here begins the end. Players beyond this point must have their character sheets ready.";
        }

        static void Decorate(Map m, Room finalHall)
        {
            var rooms = m.Rooms.Where(r => r != finalHall && r.Cells.Count >= 9 && !r.Cells.Contains(m.Up) && !r.Cells.Contains(m.Down)).ToList();
            Rng.Shuffle(rooms);
            int d = m.Depth;
            var themes = new List<string>();
            if (d >= 2 && d <= 13 && Rng.Chance(40)) themes.Add("shop");
            if (d >= 3 && Rng.Chance(25)) themes.Add("oracle");
            if (Rng.Chance(45)) themes.Add("fountain");
            if (Rng.Chance(30)) themes.Add("temple");
            if (d >= 3 && Rng.Chance(30)) themes.Add("crypt");
            if (Rng.Chance(30)) themes.Add("library");
            if (Rng.Chance(25)) themes.Add("armory");
            if (d >= 4 && Rng.Chance(25)) themes.Add("throne");
            if (Rng.Chance(d == 6 ? 100 : 25)) themes.Add("garden");
            if (Rng.Chance(30)) themes.Add("pillars");
            if (Rng.Chance(35)) themes.Add("brazier");
            if (d >= 4 && Rng.Chance(25)) themes.Add("web");
            if (d >= 5 && Rng.Chance(20)) themes.Add("statues");
            if (Rng.Chance(25)) themes.Add("lair");
            if (Rng.Chance(20)) themes.Add("flooded");
            Rng.Shuffle(themes);
            foreach (var t in themes)
            {
                var room = rooms.FirstOrDefault(r => r.Theme == null && Suits(m, r, t));
                if (room == null) continue;
                room.Theme = t;
                ApplyTheme(m, room, t);
            }
            foreach (var r in m.Rooms.Where(r => r.Theme == "cave"))
                foreach (var p in r.Cells)
                {
                    if (m.T[p.X, p.Y] != Tile.Floor) continue;
                    int n = 0;
                    foreach (var q in m.Neighbors(p)) if (m.IsWallish(q.X, q.Y)) n++;
                    if (d == 6 && Rng.Chance(14)) m.T[p.X, p.Y] = Rng.Chance(40) ? Tile.Moss : Tile.Grass;
                    else if (n >= 3 && Rng.Chance(10)) m.T[p.X, p.Y] = Tile.Rubble;
                    else if (Rng.Chance(3)) m.T[p.X, p.Y] = Tile.Grass;
                }
            int crystals = d >= 10 ? Rng.Range(3, 8) : 0;
            for (int i = 0; i < crystals; i++)
            {
                for (int t = 0; t < 50; t++)
                {
                    int x = Rng.Range(2, Map.W - 3), y = Rng.Range(2, Map.H - 3);
                    if (m.T[x, y] != Tile.Wall) continue;
                    bool adj = false;
                    foreach (var q in m.Neighbors(new Point(x, y))) if (m.Walkable(q)) adj = true;
                    if (!adj) continue;
                    m.T[x, y] = Tile.Crystal;
                    m.Lights.Add(new StaticLight { P = new Point(x, y), R = 3, C = new Color(120, 160, 255), I = 0.7f });
                    break;
                }
            }
            int engr = Rng.Range(1, 3);
            for (int i = 0; i < engr; i++)
            {
                var p = m.RandomFloor(q => m.T[q.X, q.Y] == Tile.Floor);
                m.Engravings[p] = Lore.Graffiti();
            }
        }

        static bool Suits(Map m, Room r, string t)
        {
            int w = r.R.Width, h = r.R.Height;
            bool rect = r.Cells.Count == w * h;
            switch (t)
            {
                case "shop":
                    return rect && w >= 4 && h >= 3 && w <= 10 && h <= 6 && Exits(m, r).Count == 1;
                case "oracle":
                case "fountain":
                case "temple":
                    return w >= 5 && h >= 3;
                case "throne":
                    return rect && w >= 7 && h >= 4;
                case "crypt":
                case "library":
                case "armory":
                case "pillars":
                    return rect && w >= 6 && h >= 4;
                default:
                    return r.Cells.Count >= 12;
            }
        }

        static List<Point> Exits(Map m, Room r)
        {
            var exits = new HashSet<Point>();
            var cells = new HashSet<Point>(r.Cells);
            foreach (var c in r.Cells)
                foreach (var d in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
                {
                    var q = new Point(c.X + d.X, c.Y + d.Y);
                    if (cells.Contains(q)) continue;
                    if (!m.IsWallish(q.X, q.Y)) exits.Add(q);
                }
            return exits.ToList();
        }

        static void ApplyTheme(Map m, Room r, string t)
        {
            var R = r.R;
            var c = r.Center;
            if (!r.Cells.Contains(c)) c = r.Cells[r.Cells.Count / 2];
            switch (t)
            {
                case "shop":
                    {
                        var door = Exits(m, r)[0];
                        Point inside = r.Cells.OrderBy(p => Path.Cheb(p, door)).First();
                        m.T[door.X, door.Y] = Tile.DoorOpen;
                        var shop = new Shop { Id = m.Shops.Count, Room = r, Door = door, Inside = inside, Kind = Rng.Pick(new[] { "general", "general", "armory", "apothecary", "scribe", "curio", "deli" }) };
                        r.ShopId = shop.Id;
                        r.Lit = true;
                        m.Shops.Add(shop);
                        break;
                    }
                case "oracle":
                    m.T[c.X, c.Y] = Tile.OracleDie;
                    m.Lights.Add(new StaticLight { P = c, R = 4, C = new Color(160, 160, 255), I = 0.8f });
                    m.Feeling ??= "You hear the distant clatter of an enormous die.";
                    break;
                case "fountain":
                    m.T[c.X, c.Y] = Tile.Fountain;
                    break;
                case "temple":
                    m.T[c.X, c.Y] = Tile.Altar;
                    m.FeatureUses[c] = Rng.Int(9);
                    foreach (var p in r.Cells) if (Math.Abs(p.X - c.X) <= 1 && p.Y != c.Y && m.T[p.X, p.Y] == Tile.Floor) m.T[p.X, p.Y] = Tile.Rug;
                    PlaceCorners(m, r, Tile.Brazier, true);
                    break;
                case "crypt":
                    for (int x = R.X + 1; x < R.Right - 1; x += 2)
                        for (int y = R.Y + 1; y < R.Bottom - 1; y += 2)
                            if (Rng.Chance(70) && m.T[x, y] == Tile.Floor) m.T[x, y] = Tile.Sarcophagus;
                    foreach (var p in r.Cells) if (m.T[p.X, p.Y] == Tile.Floor && Rng.Chance(12)) m.T[p.X, p.Y] = Tile.Bones;
                    m.Feeling ??= "The air here smells of dust and old linen.";
                    break;
                case "library":
                    for (int x = R.X; x < R.Right; x++)
                    {
                        if (Rng.Chance(80) && WallAbove(m, x, R.Y)) m.T[x, R.Y] = Tile.Bookshelf;
                        if (Rng.Chance(80) && WallBelow(m, x, R.Bottom - 1)) m.T[x, R.Bottom - 1] = Tile.Bookshelf;
                    }
                    break;
                case "armory":
                    break;
                case "throne":
                    {
                        var tp = new Point(R.Right - 1, c.Y);
                        if (!r.Cells.Contains(tp)) tp = c;
                        m.T[tp.X, tp.Y] = Tile.Throne;
                        for (int x = R.X; x < tp.X; x++) if (m.T[x, c.Y] == Tile.Floor) m.T[x, c.Y] = Tile.Rug;
                        PlaceCorners(m, r, Tile.Pillar, false);
                        m.Feeling ??= "You hear the murmur of a royal court.";
                        break;
                    }
                case "garden":
                    foreach (var p in r.Cells)
                    {
                        if (m.T[p.X, p.Y] != Tile.Floor) continue;
                        int roll = Rng.Int(100);
                        if (roll < 8 && !NearExit(m, p)) m.T[p.X, p.Y] = Tile.Mushroom;
                        else if (roll < 35) m.T[p.X, p.Y] = Tile.Moss;
                        else if (roll < 50) m.T[p.X, p.Y] = Tile.Grass;
                    }
                    foreach (var p in r.Cells)
                        if (m.T[p.X, p.Y] == Tile.Mushroom) m.Lights.Add(new StaticLight { P = p, R = 3, C = new Color(170, 90, 255), I = 0.7f });
                    break;
                case "pillars":
                    for (int x = R.X + 1; x < R.Right - 1; x += 2)
                        for (int y = R.Y + 1; y < R.Bottom - 1; y += 2)
                            if (m.T[x, y] == Tile.Floor && !NearExit(m, new Point(x, y))) m.T[x, y] = Tile.Pillar;
                    break;
                case "brazier":
                    PlaceCorners(m, r, Tile.Brazier, true);
                    break;
                case "web":
                    foreach (var p in r.Cells) if (m.T[p.X, p.Y] == Tile.Floor && Rng.Chance(30)) m.T[p.X, p.Y] = Tile.Web;
                    break;
                case "statues":
                    foreach (var p in r.Cells) if (m.T[p.X, p.Y] == Tile.Floor && Rng.Chance(10) && !NearExit(m, p)) m.T[p.X, p.Y] = Tile.Statue;
                    break;
                case "flooded":
                    foreach (var p in r.Cells) if (m.T[p.X, p.Y] == Tile.Floor && Rng.Chance(75)) m.T[p.X, p.Y] = Tile.Shallow;
                    break;
            }
        }

        static bool WallAbove(Map m, int x, int y) => m.IsWallish(x, y - 1) && !NearExit(m, new Point(x, y));
        static bool WallBelow(Map m, int x, int y) => m.IsWallish(x, y + 1) && !NearExit(m, new Point(x, y));

        static bool NearExit(Map m, Point p)
        {
            foreach (var q in m.Neighbors(p))
                if (m.T[q.X, q.Y] is Tile.DoorClosed or Tile.DoorOpen or Tile.Corridor or Tile.DoorLocked or Tile.StairsDown or Tile.StairsUp) return true;
            return false;
        }

        static void PlaceCorners(Map m, Room r, Tile t, bool light)
        {
            var R = r.R;
            foreach (var p in new[] { new Point(R.X, R.Y), new Point(R.Right - 1, R.Y), new Point(R.X, R.Bottom - 1), new Point(R.Right - 1, R.Bottom - 1) })
            {
                if (!r.Cells.Contains(p) || m.T[p.X, p.Y] != Tile.Floor || NearExit(m, p)) continue;
                m.T[p.X, p.Y] = t;
                if (light && t == Tile.Brazier) m.Lights.Add(new StaticLight { P = p, R = 5, C = new Color(255, 140, 50), I = 0.9f, Flicker = true });
            }
        }

        static void AddVault(Map m)
        {
            if (!Rng.Chance(35) || m.Depth >= Rules.MaxDepth) return;
            for (int t = 0; t < 200; t++)
            {
                int w = Rng.Range(2, 3), h = 2;
                int x0 = Rng.Range(3, Map.W - w - 4), y0 = Rng.Range(3, Map.H - h - 4);
                bool ok = true;
                for (int x = x0 - 2; x < x0 + w + 2 && ok; x++)
                    for (int y = y0 - 2; y < y0 + h + 2 && ok; y++)
                        if (!Map.InB(x, y) || m.T[x, y] != Tile.Wall) ok = false;
                if (!ok) continue;
                var room = new Room { R = new Rectangle(x0, y0, w, h), Theme = "vault", Lit = true };
                for (int x = x0; x < x0 + w; x++)
                    for (int y = y0; y < y0 + h; y++)
                    {
                        m.T[x, y] = Tile.Floor;
                        room.Cells.Add(new Point(x, y));
                    }
                m.Rooms.Add(room);
                foreach (var dir in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
                {
                    var start = dir.X > 0 ? new Point(x0 + w, y0) : dir.X < 0 ? new Point(x0 - 1, y0) : dir.Y > 0 ? new Point(x0, y0 + h) : new Point(x0, y0 - 1);
                    var path = new List<Point>();
                    var p = start;
                    bool found = false;
                    for (int i = 0; i < 8; i++)
                    {
                        if (!Map.InB(p) || m.T[p.X, p.Y] == Tile.PermWall) break;
                        if (m.Walkable(p)) { found = true; break; }
                        path.Add(p);
                        p = new Point(p.X + dir.X, p.Y + dir.Y);
                    }
                    if (!found || path.Count == 0) continue;
                    for (int i = 0; i < path.Count; i++) m.T[path[i].X, path[i].Y] = i == 0 ? Tile.DoorSecret : Tile.Corridor;
                    if (path.Count > 1) m.T[path[path.Count - 1].X, path[path.Count - 1].Y] = Tile.DoorSecret;
                    break;
                }
                m.Feeling ??= "You have a feeling that someone has hidden treasure nearby.";
                return;
            }
        }

        static void SecretAndLocked(Map m)
        {
            var doors = new List<Point>();
            for (int x = 1; x < Map.W - 1; x++)
                for (int y = 1; y < Map.H - 1; y++)
                    if (m.T[x, y] == Tile.DoorClosed) doors.Add(new Point(x, y));
            Rng.Shuffle(doors);
            int secrets = 0, locked = 0;
            foreach (var d in doors)
            {
                if (m.Shops.Any(s => s.Door == d)) continue;
                if (secrets < 1 + m.Depth / 4 && Rng.Chance(25))
                {
                    m.T[d.X, d.Y] = Tile.DoorSecret;
                    var dist = Path.DistMap(m, new[] { m.Up }, (x, y) => m.Walkable(x, y) || m.T[x, y] is Tile.DoorClosed or Tile.DoorLocked or Tile.DeepWater);
                    bool ok = m.Depth >= Rules.MaxDepth ? dist[m.Rooms[0].Center.X, m.Rooms[0].Center.Y] != int.MaxValue : dist[m.Down.X, m.Down.Y] != int.MaxValue;
                    if (!ok) m.T[d.X, d.Y] = Tile.DoorClosed;
                    else secrets++;
                    continue;
                }
                if (locked < 3 && Rng.Chance(20))
                {
                    m.T[d.X, d.Y] = Tile.DoorLocked;
                    locked++;
                }
            }
        }

        static void LightRooms(Map m)
        {
            int chance = Math.Max(15, 95 - m.Depth * 6);
            foreach (var r in m.Rooms)
            {
                if (r.Theme == "final") continue;
                if (r.Theme == "cave" && !r.Lit) r.Lit = Rng.Chance(chance / 3);
                else if (!r.Lit) r.Lit = Rng.Chance(chance);
                if (r.Theme is "shop" or "temple" or "throne" or "vault") r.Lit = true;
                if (r.Theme is "crypt") r.Lit = false;
                if (!r.Lit) continue;
                foreach (var p in r.Cells)
                {
                    m.Lit[p.X, p.Y] = true;
                    foreach (var q in m.Neighbors(p)) if (!m.Walkable(q) || m.IsDoor(q)) m.Lit[q.X, q.Y] = true;
                }
            }
        }
    }
}
