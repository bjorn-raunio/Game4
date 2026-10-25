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
            public HashSet<Point> Shell = new HashSet<Point>();
            public Dictionary<Point, char> Glyphs;
            public VaultDef Set;
            public Point Door;
            public bool Cave;
            public bool Inside(Point p) => Glyphs != null ? Glyphs.ContainsKey(p) : Room.Contains(p);
        }

        static readonly HashSet<Point> guard = new HashSet<Point>();
        public static readonly List<string> LastSets = new List<string>();
        public static OwnerDef Owner;
        public static Point LeverGuard;
        public static bool River;
        public static readonly List<string> LastDecay = new List<string>();
        static Map above;

        public static Map Above(int depth, int branch) => depth > 1 && Dm.Levels.TryGetValue(branch * 100 + depth - 1, out var a) && !a.Outdoors && a.Branch == branch ? a : null;

        public static List<Point> Holes(Map a)
        {
            var holes = new List<Point>();
            if (a == null) return holes;
            for (int x = 1; x < Map.W - 1; x++)
                for (int y = 1; y < Map.H - 1; y++)
                    if (a.T[x, y] == Tile.Chasm) holes.Add(new Point(x, y));
            holes.AddRange(a.Traps.Values.Where(t => t.T == TrapT.Trapdoor).Select(t => t.Pos));
            return holes;
        }

        static OwnerDef PickOwner(Map m)
        {
            var pool = OwnerDb.All.Where(o => o.Branch == m.Branch && m.Depth >= o.MinDepth && m.Depth <= o.MaxDepth).ToList();
            return pool.Count > 0 ? Rng.Weighted(pool, o => o.Freq) : OwnerDb.All[0];
        }

        public static Map Generate(int depth, int branch = 0)
        {
            if (branch == 0 && depth == 0) return Wild.Generate();
            if (WorldDb.TownOf(branch) != null) return Wild.Town(branch);
            if (branch == 99) return Wild.Encounter(depth);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var m = TryGenerate(depth, branch);
                if (m != null) return m;
            }
            return TryGenerate(depth, branch, true);
        }

        static Map TryGenerate(int depth, int branch, bool force = false)
        {
            var m = new Map(depth, branch);
            guard.Clear();
            LastSets.Clear();
            LastDecay.Clear();
            LeverGuard = Point.Zero;
            above = Above(depth, branch);
            Owner = PickOwner(m);
            if (Owner.Feeling != null && Rng.Chance(50)) m.Feeling = Owner.Feeling;
            SetTheme(m);
            for (int x = 0; x < Map.W; x++)
                for (int y = 0; y < Map.H; y++)
                    m.T[x, y] = (x == 0 || y == 0 || x == Map.W - 1 || y == Map.H - 1) ? Tile.PermWall : Tile.Wall;

            int caves = branch == 0 ? -1 : WorldDb.Get(branch).CaveChance;
            Style style = caves >= 0 ? (caves >= 60 ? Style.Caves : Rng.Chance(caves * 2) ? Style.Mixed : Style.Rooms) : depth switch
            {
                4 => Style.Mixed,
                6 => Style.Caves,
                8 => Style.Mixed,
                10 or 11 or 12 => Rng.Chance(60) ? Style.Caves : Style.Mixed,
                13 or 14 => Style.Mixed,
                _ => Rng.Chance(15) ? Style.Mixed : Style.Rooms,
            };

            Room finalHall = null;
            if (branch == 0 && depth == Rules.MaxDepth) finalHall = CarveFinalHall(m);
            if (finalHall == null || above != null && above.Down.X < 50)
            {
                var first = MakeDesign(style, true);
                var c = above != null ? new Point(Math.Clamp(above.Down.X, 10, Map.W - 11), Math.Clamp(above.Down.Y, 6, Map.H - 7)) : new Point(Rng.Range(30, 70), Rng.Range(12, 24));
                var bb = Bounds(first.Room);
                var off = new Point(c.X - bb.Center.X, c.Y - bb.Center.Y);
                if (!Fits(m, first, off, null)) off = new Point(Map.W / 2 - bb.Center.X, Map.H / 2 - bb.Center.Y);
                Carve(m, first, off, null, false);
            }
            CarveLanding(m, Holes(above));

            int target = style == Style.Caves ? 22 : 30;
            int placed = 0;
            int sets = finalHall != null ? 2 : style == Style.Caves ? 1 : 0;
            for (int i = 0; i < 900 && placed < target; i++)
            {
                if (!FindAttach(m, out var site, out var dir)) continue;
                var d = (sets < 2 && Rng.Chance(12) ? MakeSet(m) : null) ?? MakeDesign(style, false);
                if (!Orient(d, dir, Rng.Chance(style == Style.Caves ? 25 : 55) && !d.Cave)) continue;
                var off = new Point(site.X - d.Door.X, site.Y - d.Door.Y);
                if (!Fits(m, d, off, site)) continue;
                Carve(m, d, off, site, true);
                if (d.Set != null) sets++;
                placed++;
            }
            if (placed < 8 && !force) return null;

            AddLoops(m, depth >= 10 ? 14 : 9);
            Stitch(m);
            CleanDoors(m);
            AddLakes(m, depth);
            if (!PlaceStairs(m, finalHall)) { if (!force) return null; }
            if (branch != 0 && m.IsBottom) SetupSanctum(m);
            Decorate(m, finalHall);
            if (finalHall == null)
            {
                Masonry(m);
                Decay(m);
            }
            Seep(m);
            AddVault(m);
            SecretAndLocked(m);
            PlaceGate(m);
            LightRooms(m);
            if (finalHall != null) SetupFinal(m, finalHall);
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
            if (m.Branch != 0)
            {
                var dd = WorldDb.Get(m.Branch);
                (wf, wb, ff) = (dd.WallFg, dd.WallBg, dd.FloorFg);
            }
            m.WallFg = wf;
            m.WallBg = wb;
            m.FloorFg = ff;
            m.Ambient = 1.0f;
        }

        static void SetupSanctum(Map m)
        {
            var dd = WorldDb.Get(m.Branch);
            var dist = Path.DistMap(m, new[] { m.Up }, (x, y) => Pass(m, x, y));
            int Far(Room r) => r.Cells.Where(c => dist[c.X, c.Y] != int.MaxValue).Select(c => dist[c.X, c.Y]).DefaultIfEmpty(-1).Max();
            var room = m.Rooms.Where(r => r.Cells.Count >= 12 && !r.Cells.Contains(m.Up) && Far(r) > 0).OrderByDescending(Far).FirstOrDefault()
                ?? m.Rooms.Where(r => !r.Cells.Contains(m.Up)).OrderByDescending(r => r.Cells.Count).FirstOrDefault() ?? m.Rooms[0];
            room.Theme = "sanctum";
            room.Lit = true;
            var c = room.Cells.Contains(room.Center) ? room.Center : room.Cells[room.Cells.Count / 2];
            PlaceCorners(m, room, Tile.Brazier, true);
            var boss = new Monster(MonsterDb.Get(dd.Boss)) { Pos = c, Home = c, Sleeping = true };
            boss.Inv.Add(Spawn.RandomItem(m.Depth + 4, dd.Prize));
            boss.Inv.Add(Spawn.Gold(m.Depth * 8));
            m.Monsters.Add(boss);
            var spot = room.Cells.Where(p => p != c && m.T[p.X, p.Y] == Tile.Floor).OrderByDescending(p => Path.Cheb(p, c)).FirstOrDefault();
            if (spot != Point.Zero) m.Drop(Spawn.MakeChest(m.Depth + 3), spot);
            var e = room.Cells.FirstOrDefault(p => p != c && p != spot && m.T[p.X, p.Y] == Tile.Floor);
            if (e != Point.Zero) m.Engravings[e] = dd.Branch switch
            {
                1 => "TOLL FOR NO ONE. â€” the Bellwright's mark, scratched under a thousand others",
                2 => "Every mouth is a seed. Every seed is a mouth.",
                _ => "You are here. You are always here. That is the problem with maps.",
            };
            m.Feeling = dd.Feeling;
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

        static Design MakeSet(Map m)
        {
            var pool = VaultDb.All.Where(v => m.Depth >= v.MinDepth && m.Depth <= v.MaxDepth && (v.Branch < 0 || v.Branch == m.Branch) && !LastSets.Contains(v.Id)).ToList();
            if (pool.Count == 0) return null;
            var v = Rng.Weighted(pool, x => x.Freq * Owner.Set(x.Id));
            var d = new Design { Set = v, Glyphs = new Dictionary<Point, char>() };
            int h = v.Rows.Length, w = v.Rows.Max(r => r.Length), rot = Rng.Int(4);
            bool flip = Rng.Chance(50);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < v.Rows[y].Length; x++)
                {
                    char ch = v.Rows[y][x];
                    if (ch == ' ') continue;
                    int fx = flip ? w - 1 - x : x;
                    var p = rot switch { 1 => new Point(h - 1 - y, fx), 2 => new Point(w - 1 - fx, h - 1 - y), 3 => new Point(y, w - 1 - fx), _ => new Point(fx, y) };
                    d.Glyphs[p] = ch;
                    if (ch is '#' or '+' or 'L' or 'x') d.Shell.Add(p);
                    else d.Room.Add(p);
                }
            return d;
        }

        static Tile SetTile(Map m, char ch) => ch switch
        {
            '#' => Tile.Wall,
            '+' => Tile.DoorClosed,
            'L' => Tile.DoorLocked,
            'x' => Tile.DoorSecret,
            '~' => Tile.DeepWater,
            '-' => Tile.Shallow,
            '=' => Tile.Bridge,
            'O' => Tile.Pillar,
            'T' => Tile.Statue,
            'C' => Tile.Sarcophagus,
            'B' => Tile.Brazier,
            'b' => Tile.Bones,
            'K' => Tile.Bookshelf,
            'A' => Tile.Altar,
            'F' => Tile.Fountain,
            'R' => Tile.Rug,
            ':' => Tile.Rubble,
            'w' => Tile.Web,
            'G' => Tile.Grass,
            'v' => m.NoFall ? Tile.DeepWater : Tile.Chasm,
            _ => Tile.Floor,
        };

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
            var cands = d.Room.Where(c => !d.Inside(new Point(c.X - dir.X, c.Y - dir.Y)) && (d.Glyphs == null || d.Glyphs[c] == '.')).ToList();
            if (cands.Count == 0) return false;
            var c0 = Rng.Pick(cands);
            var door = new Point(c0.X - dir.X, c0.Y - dir.Y);
            if (hallway)
            {
                int len = Rng.Range(2, 9);
                for (int i = 0; i < len; i++)
                {
                    var hp = new Point(door.X - dir.X * i, door.Y - dir.Y * i);
                    if (d.Inside(hp)) return false;
                    d.Hall.Add(hp);
                }
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
            foreach (var c in d.Room.Concat(d.Hall).Concat(d.Shell))
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
            if (d.Set != null)
            {
                var v = d.Set;
                room.Theme = v.Theme;
                room.Lit = v.Lit;
                room.Marks = new List<(Point, char)>();
                LastSets.Add(v.Id);
                foreach (var (c, ch) in d.Glyphs)
                {
                    var p = new Point(c.X + off.X, c.Y + off.Y);
                    m.T[p.X, p.Y] = SetTile(m, ch);
                    foreach (var q in m.Neighbors(p)) guard.Add(q);
                    guard.Add(p);
                    if ("mMug$*!c^".IndexOf(ch) >= 0) room.Marks.Add((p, ch));
                    if (ch == 'E') m.Engravings[p] = v.Text ?? Lore.Graffiti();
                    if (ch == 'A') m.FeatureUses[p] = Rng.Int(9);
                    if (ch == 'B') m.Lights.Add(new StaticLight { P = p, R = 5, C = new Color(255, 140, 50), I = 0.9f, Flicker = true });
                }
                if (v.Feeling != null && Rng.Chance(60)) m.Feeling ??= v.Feeling;
            }
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
                    if (m.T[x, y] != Tile.Wall || guard.Contains(new Point(x, y))) continue;
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
            return Map.Defs[t].Walk || t is Tile.DoorClosed or Tile.DoorLocked or Tile.DoorSecret or Tile.Gate;
        }

        static readonly Point[] Dirs4 = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };

        static void CarveLanding(Map m, List<Point> holes)
        {
            if (holes.Count == 0) return;
            var hs = new HashSet<Point>(holes);
            var room = new Room { Theme = "cave" };
            foreach (var h in holes)
                foreach (var q in m.Neighbors(h).Append(h))
                {
                    if (q.X < 2 || q.Y < 2 || q.X > Map.W - 3 || q.Y > Map.H - 3 || m.T[q.X, q.Y] != Tile.Wall) continue;
                    if (q != h && !Rng.Chance(55)) continue;
                    m.T[q.X, q.Y] = Rng.Chance(hs.Contains(q) ? 30 : 8) ? Tile.Rubble : Tile.Floor;
                    room.Cells.Add(q);
                }
            if (room.Cells.Count == 0) return;
            room.R = Bounds(room.Cells);
            m.Rooms.Add(room);
        }

        static void Stitch(Map m)
        {
            for (int pass = 0; pass < 30; pass++)
            {
                var comp = new int[Map.W, Map.H];
                var sizes = new List<int> { 0 };
                for (int x = 1; x < Map.W - 1; x++)
                    for (int y = 1; y < Map.H - 1; y++)
                    {
                        if (comp[x, y] != 0 || !Pass(m, x, y)) continue;
                        int id = sizes.Count, n = 0;
                        var q = new Queue<Point>();
                        q.Enqueue(new Point(x, y));
                        comp[x, y] = id;
                        while (q.Count > 0)
                        {
                            var p = q.Dequeue();
                            n++;
                            foreach (var nb in m.Neighbors(p))
                                if (comp[nb.X, nb.Y] == 0 && Pass(m, nb.X, nb.Y)) { comp[nb.X, nb.Y] = id; q.Enqueue(nb); }
                        }
                        sizes.Add(n);
                    }
                if (sizes.Count <= 2) return;
                int small = Enumerable.Range(1, sizes.Count - 1).OrderBy(i => sizes[i]).First();
                var from = new Dictionary<Point, Point>();
                var bfs = new Queue<Point>();
                for (int x = 1; x < Map.W - 1; x++)
                    for (int y = 1; y < Map.H - 1; y++)
                        if (comp[x, y] == small) { var p = new Point(x, y); from[p] = p; bfs.Enqueue(p); }
                Point hit = Point.Zero;
                while (bfs.Count > 0 && hit == Point.Zero)
                {
                    var p = bfs.Dequeue();
                    foreach (var d in Dirs4)
                    {
                        var nb = new Point(p.X + d.X, p.Y + d.Y);
                        if (nb.X < 1 || nb.Y < 1 || nb.X > Map.W - 2 || nb.Y > Map.H - 2 || from.ContainsKey(nb)) continue;
                        if (Pass(m, nb.X, nb.Y) && comp[nb.X, nb.Y] != small) { from[nb] = p; hit = nb; break; }
                        if (m.T[nb.X, nb.Y] != Tile.Wall || guard.Contains(nb)) continue;
                        from[nb] = p;
                        bfs.Enqueue(nb);
                    }
                }
                if (hit == Point.Zero) return;
                for (var p = from[hit]; comp[p.X, p.Y] != small; p = from[p]) m.T[p.X, p.Y] = Tile.Corridor;
            }
        }

        static bool Inner(Point p) => p.X >= 2 && p.Y >= 2 && p.X <= Map.W - 3 && p.Y <= Map.H - 3;

        static void Masonry(Map m)
        {
            foreach (var r in m.Rooms.Where(r => r.Theme == null && r.Cells.Count == r.R.Width * r.R.Height && r.R.Width >= 7 && r.R.Height >= 5).ToList())
            {
                if (!Rng.Chance(30 * Owner.Ruins("masonry") / 100)) continue;
                var R = r.R;
                bool wide = R.Width >= R.Height;
                if (Rng.Chance(50))
                {
                    int span = wide ? R.Width : R.Height;
                    for (int i = 2; i < span / 2; i += 3)
                        foreach (int k in new[] { i, span - 1 - i })
                            foreach (var side in new[] { -1, 1 })
                            {
                                var p = wide ? new Point(R.X + k, side < 0 ? R.Y - 1 : R.Bottom) : new Point(side < 0 ? R.X - 1 : R.Right, R.Y + k);
                                var back = wide ? new Point(p.X, p.Y + side) : new Point(p.X + side, p.Y);
                                var front = wide ? new Point(p.X, p.Y - side) : new Point(p.X - side, p.Y);
                                if (m.T[front.X, front.Y] != Tile.Floor) continue;
                                var s1 = wide ? new Point(p.X - 1, p.Y) : new Point(p.X, p.Y - 1);
                                var s2 = wide ? new Point(p.X + 1, p.Y) : new Point(p.X, p.Y + 1);
                                if (!Inner(p) || new[] { p, back, s1, s2 }.Any(q => m.T[q.X, q.Y] != Tile.Wall || guard.Contains(q))) continue;
                                if (m.Neighbors(back).Any(q => q != p && m.T[q.X, q.Y] != Tile.Wall && m.T[q.X, q.Y] != Tile.PermWall)) continue;
                                m.T[p.X, p.Y] = Tile.Floor;
                            }
                    LastDecay.Add("alcoves");
                }
                else
                {
                    var inset = new[] { new Point(R.X + 1, R.Y + 1), new Point(R.Right - 2, R.Y + 1), new Point(R.X + 1, R.Bottom - 2), new Point(R.Right - 2, R.Bottom - 2) };
                    if (inset.Any(p => m.T[p.X, p.Y] != Tile.Floor || NearExit(m, p))) continue;
                    foreach (var p in inset) m.T[p.X, p.Y] = Tile.Pillar;
                    if (R.Width >= 11 && R.Height >= 5)
                        foreach (var p in new[] { new Point(R.X + R.Width / 2, R.Y + 1), new Point(R.X + R.Width / 2, R.Bottom - 2) })
                            if (m.T[p.X, p.Y] == Tile.Floor && !NearExit(m, p)) m.T[p.X, p.Y] = Tile.Pillar;
                    if (!Connected(m)) { foreach (var p in r.Cells) if (m.T[p.X, p.Y] == Tile.Pillar) m.T[p.X, p.Y] = Tile.Floor; continue; }
                    LastDecay.Add("colonnade");
                }
            }
        }

        static void Decay(Map m)
        {
            int age = Rng.Range(0, 2) + m.Depth / 5;
            int W(string k, int pct) => pct * Owner.Ruins(k) / 100;
            for (int i = 0; i <= age; i++) if (Rng.Chance(W("cavein", 45))) CaveIn(m);
            if (Rng.Chance(W("moss", 25 + age * 10))) Moss(m, 1 + Rng.Int(age + 1));
            if (Rng.Chance(W("flood", 12 + age * 5))) Flood(m);
            if (Rng.Chance(W("lair", 18 + age * 7))) BreakIn(m);
            if (LastDecay.Count(d => d is not ("alcoves" or "colonnade")) >= 3) m.Feeling ??= "Whoever built these halls is long gone. What lives here now has let them go to ruin.";
        }

        static bool Touchable(Map m, Point p) => Inner(p) && !guard.Contains(p) && m.ShopAt(p) == null;

        static void CaveIn(Map m)
        {
            for (int t = 0; t < 40; t++)
            {
                var w = new Point(Rng.Range(3, Map.W - 4), Rng.Range(3, Map.H - 4));
                if (m.T[w.X, w.Y] != Tile.Wall || !Touchable(m, w) || !Dirs4.Any(d => m.T[w.X + d.X, w.Y + d.Y] is Tile.Floor or Tile.Corridor)) continue;
                float rad = Rng.Range(10, 25) / 10f;
                int n = 0;
                for (int dx = -3; dx <= 3; dx++)
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        var q = new Point(w.X + dx, w.Y + dy);
                        if (dx * dx + dy * dy > rad * rad + Rng.Int(2) || !Touchable(m, q)) continue;
                        var tq = m.T[q.X, q.Y];
                        if (tq == Tile.Wall || tq is Tile.Floor or Tile.Corridor or Tile.Grass && Rng.Chance(45)) { m.T[q.X, q.Y] = Tile.Rubble; n++; }
                    }
                if (n > 0) { LastDecay.Add("cave-in"); return; }
            }
        }

        static void Moss(Map m, int seeds)
        {
            var front = new List<Point>();
            for (int i = 0; i < seeds; i++)
            {
                var wet = Rng.Chance(70) ? m.RandomFloor(q => m.Neighbors(q).Any(n => m.T[n.X, n.Y] is Tile.Shallow or Tile.DeepWater or Tile.Fountain)) : Point.Zero;
                front.Add(wet != Point.Zero && wet != new Point(Map.W / 2, Map.H / 2) ? wet : m.RandomFloor(q => m.T[q.X, q.Y] == Tile.Floor));
            }
            int budget = Rng.Range(25, 70), grown = 0;
            while (front.Count > 0 && grown < budget)
            {
                var p = front[Rng.Int(front.Count)];
                front.Remove(p);
                if (!Inner(p) || m.ShopAt(p) != null || m.T[p.X, p.Y] is not (Tile.Floor or Tile.Corridor or Tile.Rubble or Tile.Grass)) continue;
                m.T[p.X, p.Y] = Rng.Chance(75) ? Tile.Moss : Tile.Grass;
                grown++;
                foreach (var d in Dirs4) if (Rng.Chance(70)) front.Add(new Point(p.X + d.X, p.Y + d.Y));
            }
            if (grown > 8) LastDecay.Add("moss");
        }

        static void Flood(Map m)
        {
            var corner = new Point(Rng.Chance(50) ? 1 : Map.W - 2, Rng.Chance(50) ? 1 : Map.H - 2);
            float reach = Rng.Range(22, 38);
            var deep = new List<Point>();
            int n = 0;
            for (int x = 2; x < Map.W - 2; x++)
                for (int y = 2; y < Map.H - 2; y++)
                {
                    var p = new Point(x, y);
                    if (m.T[x, y] is not (Tile.Floor or Tile.Corridor or Tile.Grass or Tile.Moss or Tile.Rubble) || !Touchable(m, p)) continue;
                    float d = (float)Math.Sqrt((x - corner.X) * (x - corner.X) + (y - corner.Y) * (y - corner.Y) * 4) * (0.85f + Rng.Int(30) / 100f);
                    if (d > reach) continue;
                    m.T[x, y] = Tile.Shallow;
                    n++;
                    if (d < reach * 0.35f && m.Neighbors(p).All(q => !m.IsWallish(q.X, q.Y))) deep.Add(p);
                }
            foreach (var p in deep) m.T[p.X, p.Y] = Tile.DeepWater;
            if (deep.Count > 0 && !Connected(m)) foreach (var p in deep) m.T[p.X, p.Y] = Tile.Shallow;
            if (n > 10)
            {
                LastDecay.Add("flood");
                m.Feeling ??= "Water has found its way in from somewhere and pooled in the low halls.";
            }
        }

        static void BreakIn(Map m)
        {
            var rooms = m.Rooms.Where(r => r.Theme is null or "cave" && r.Cells.Count >= 9 && !r.Cells.Contains(m.Up) && !r.Cells.Contains(m.Down)).ToList();
            for (int t = 0; t < 200 && rooms.Count > 0; t++)
            {
                var r = Rng.Pick(rooms);
                var f = Rng.Pick(r.Cells);
                var dir = Rng.Pick(Dirs4);
                var w = new Point(f.X + dir.X, f.Y + dir.Y);
                if (m.T[f.X, f.Y] != Tile.Floor || m.T[w.X, w.Y] != Tile.Wall) continue;
                var blob = CaveBlob(Rng.Range(7, 12), Rng.Range(4, 7));
                if (blob.Count < 12) continue;
                var anchor = blob.OrderBy(c => c.X * dir.X + c.Y * dir.Y).ThenBy(_ => Rng.Int(100)).First();
                var at = new Point(w.X + dir.X * 3, w.Y + dir.Y * 3);
                var off = new Point(at.X - anchor.X, at.Y - anchor.Y);
                var cells = blob.Select(c => new Point(c.X + off.X, c.Y + off.Y)).ToList();
                var tunnel = new[] { w, new Point(w.X + dir.X, w.Y + dir.Y), new Point(w.X + dir.X * 2, w.Y + dir.Y * 2) };
                var shell = new HashSet<Point>(tunnel);
                bool ok = cells.All(c => Inner(c) && !guard.Contains(c) && m.Neighbors(c).All(q => shell.Contains(q) || cells.Contains(q) || m.T[q.X, q.Y] is Tile.Wall or Tile.PermWall && !guard.Contains(q)))
                    && tunnel.All(c => Inner(c) && m.T[c.X, c.Y] == Tile.Wall && !guard.Contains(c));
                if (!ok) continue;
                var lair = new Room { Theme = "lair" };
                foreach (var c in cells)
                {
                    m.T[c.X, c.Y] = Rng.Chance(12) ? Tile.Rubble : Rng.Chance(10) ? Tile.Bones : Tile.Floor;
                    lair.Cells.Add(c);
                }
                foreach (var c in tunnel) m.T[c.X, c.Y] = Tile.Rubble;
                lair.R = Bounds(lair.Cells);
                m.Rooms.Add(lair);
                LastDecay.Add("break-in");
                m.Feeling ??= "Something has dug its way into these halls from the rock beyond.";
                return;
            }
        }

        static void Seep(Map m)
        {
            if (above == null) return;
            int wet = 0, hot = 0;
            for (int x = 1; x < Map.W - 1; x++)
                for (int y = 1; y < Map.H - 1; y++)
                {
                    var p = new Point(x, y);
                    if (m.T[x, y] is not (Tile.Floor or Tile.Corridor) || guard.Contains(p) || m.ShopAt(p) != null) continue;
                    if (above.T[x, y] == Tile.DeepWater && Rng.Chance(30)) { m.T[x, y] = Tile.Shallow; wet++; }
                    else if (above.T[x, y] == Tile.Lava && Rng.Chance(40))
                    {
                        m.T[x, y] = Tile.Ash;
                        hot++;
                        if (Rng.OneIn(5)) m.Lights.Add(new StaticLight { P = p, R = 2, C = new Color(255, 90, 30), I = 0.4f, Flicker = true });
                    }
                }
            if (wet > 3) m.Feeling ??= "Water drips steadily from the ceiling into shallow puddles.";
            if (hot > 3) m.Feeling ??= "The ceiling is warm, and flakes of ash drift down through cracks in it.";
        }

        static bool OneWide(Map m, Point p) =>
            m.IsWallish(p.X - 1, p.Y) && m.IsWallish(p.X + 1, p.Y) && !m.IsWallish(p.X, p.Y - 1) && !m.IsWallish(p.X, p.Y + 1) ||
            m.IsWallish(p.X, p.Y - 1) && m.IsWallish(p.X, p.Y + 1) && !m.IsWallish(p.X - 1, p.Y) && !m.IsWallish(p.X + 1, p.Y);

        static void PlaceGate(Map m)
        {
            if (m.IsBottom || m.Depth < 2 || !Rng.Chance(45)) return;
            bool Open(int x, int y) => m.Walkable(x, y) && m.T[x, y] != Tile.Chasm || m.T[x, y] is Tile.DoorClosed or Tile.DoorLocked;
            var from = Path.DistMap(m, new[] { m.Up }, Open);
            int route = from[m.Down.X, m.Down.Y];
            if (route == int.MaxValue || route < 30) return;
            var path = new List<Point> { m.Down };
            for (var p = m.Down; from[p.X, p.Y] > 0;)
            {
                var next = m.Neighbors(p).FirstOrDefault(q => from[q.X, q.Y] == from[p.X, p.Y] - 1);
                if (next == Point.Zero) return;
                path.Add(p = next);
            }
            var cands = path.Where(p => from[p.X, p.Y] > route * 35 / 100 && from[p.X, p.Y] < route * 80 / 100 && m.T[p.X, p.Y] is Tile.Corridor or Tile.DoorClosed or Tile.DoorOpen or Tile.DoorLocked && OneWide(m, p) && !guard.Contains(p)).ToList();
            Rng.Shuffle(cands);
            foreach (var g in cands.Take(10))
            {
                var keep = m.T[g.X, g.Y];
                m.T[g.X, g.Y] = Tile.Gate;
                var near = Path.DistMap(m, new[] { m.Up }, Open);
                int far = 0;
                for (int x = 1; x < Map.W - 1; x++)
                    for (int y = 1; y < Map.H - 1; y++)
                        if (Open(x, y) && near[x, y] == int.MaxValue) far++;
                if (near[m.Down.X, m.Down.Y] != int.MaxValue || far < 40) { m.T[g.X, g.Y] = keep; continue; }
                var spine = path.Where(p => near[p.X, p.Y] != int.MaxValue).ToList();
                var off = Path.DistMap(m, spine, (x, y) => Open(x, y) && near[x, y] != int.MaxValue);
                var spots = new List<(Point wall, Point stand, int score)>();
                for (int x = 2; x < Map.W - 2; x++)
                    for (int y = 2; y < Map.H - 2; y++)
                    {
                        var s = new Point(x, y);
                        if (m.T[x, y] != Tile.Floor || near[x, y] == int.MaxValue || near[x, y] < 12 || Path.Cheb(s, g) < 8 || m.ShopAt(s) != null) continue;
                        foreach (var d in Dirs4)
                        {
                            var w = new Point(x + d.X, y + d.Y);
                            if (m.T[w.X, w.Y] != Tile.Wall || guard.Contains(w)) continue;
                            spots.Add((w, s, off[x, y] * 2 + near[x, y]));
                        }
                    }
                if (spots.Count == 0) { m.T[g.X, g.Y] = keep; continue; }
                var pick = Rng.Pick(spots.OrderByDescending(s => s.score).Take(6).ToList());
                m.T[pick.wall.X, pick.wall.Y] = Tile.Lever;
                LeverGuard = pick.stand;
                var mark = m.Neighbors(g).FirstOrDefault(q => near[q.X, q.Y] != int.MaxValue && m.T[q.X, q.Y] is Tile.Floor or Tile.Corridor);
                if (mark != Point.Zero) m.Engravings[mark] = Rng.Pick(new[]
                {
                    "No lock. No keyhole. Whatever lifts it is somewhere behind you.",
                    "PULL — and an arrow, scratched pointing back the way you came.",
                    "The chains go up into the ceiling and then sideways, toward something.",
                });
                m.Feeling ??= "Somewhere, chains creak against an iron gate.";
                return;
            }
        }

        static List<Point> Meander(Point a, Point b, int depth)
        {
            if (depth == 0 || Path.Cheb(a, b) < 4) return Path.Line(a, b);
            int len = Path.Cheb(a, b);
            var mid = new Point((a.X + b.X) / 2 + Rng.Range(-len / 4, len / 4), (a.Y + b.Y) / 2 + Rng.Range(-len / 4, len / 4));
            mid = new Point(Math.Clamp(mid.X, 2, Map.W - 3), Math.Clamp(mid.Y, 2, Map.H - 3));
            var l = Meander(a, mid, depth - 1);
            l.RemoveAt(l.Count - 1);
            l.AddRange(Meander(mid, b, depth - 1));
            return l;
        }

        static bool AddRiver(Map m, Tile liquid)
        {
            bool across = Rng.Chance(70);
            int steps = across ? Rng.Range(4, 6) : 2;
            var way = new List<Point>();
            int drift = across ? Rng.Range(8, Map.H - 9) : Rng.Range(15, Map.W - 16);
            for (int i = 0; i <= steps; i++)
            {
                int a = 1 + (across ? Map.W - 3 : Map.H - 3) * i / steps;
                drift = across ? Math.Clamp(drift + Rng.Range(-7, 7), 3, Map.H - 4) : Math.Clamp(drift + Rng.Range(-14, 14), 4, Map.W - 5);
                way.Add(across ? new Point(a, drift) : new Point(drift, a));
            }
            var line = new List<Point>();
            for (int i = 0; i + 1 < way.Count; i++)
            {
                var seg = Meander(way[i], way[i + 1], 2);
                if (line.Count > 0) seg.RemoveAt(0);
                line.AddRange(seg);
            }
            int r = Rng.Range(1, 2);
            var core = new HashSet<Point>();
            var bank = new HashSet<Point>();
            foreach (var p in line)
                for (int dx = -r - 1; dx <= r + 1; dx++)
                    for (int dy = -r - 1; dy <= r + 1; dy++)
                    {
                        var q = new Point(p.X + dx, p.Y + dy);
                        int md = Math.Abs(dx) + Math.Abs(dy);
                        if (q.X < 1 || q.Y < 1 || q.X > Map.W - 2 || q.Y > Map.H - 2) continue;
                        if (md <= r) core.Add(q);
                        else if (md == r + 1) bank.Add(q);
                    }
            bank.ExceptWith(core);
            var changed = new Dictionary<Point, Tile>();
            var crossings = new List<Point>();
            foreach (var q in core)
            {
                var t = m.T[q.X, q.Y];
                if (guard.Contains(q) || t is Tile.PermWall or Tile.StairsUp or Tile.StairsDown) continue;
                if (t is not (Tile.Wall or Tile.Floor or Tile.Corridor or Tile.Grass or Tile.Moss or Tile.Rubble or Tile.DoorClosed or Tile.DoorOpen or Tile.DoorLocked or Tile.DoorBroken)) continue;
                changed[q] = t;
                m.T[q.X, q.Y] = liquid;
                if (t is Tile.Corridor or Tile.DoorClosed or Tile.DoorOpen or Tile.DoorLocked or Tile.DoorBroken) crossings.Add(q);
            }
            if (changed.Count < 30) { foreach (var (q, t) in changed) m.T[q.X, q.Y] = t; return false; }
            Tile Span() => liquid is Tile.DeepWater && Rng.Chance(45) ? Tile.Shallow : liquid is Tile.Shallow ? Tile.Shallow : Tile.Bridge;
            foreach (var q in crossings) m.T[q.X, q.Y] = Span();
            for (int tries = 0; tries < 12 && !Connected(m); tries++)
                if (!Ford(m, changed, Span())) { foreach (var (q, t) in changed) m.T[q.X, q.Y] = t; return false; }
            if (!Connected(m)) { foreach (var (q, t) in changed) m.T[q.X, q.Y] = t; return false; }
            foreach (var q in bank)
            {
                if (m.T[q.X, q.Y] is not (Tile.Floor or Tile.Corridor or Tile.Grass) || guard.Contains(q)) continue;
                if (liquid == Tile.DeepWater && Rng.Chance(75)) m.T[q.X, q.Y] = Tile.Shallow;
                else if (liquid == Tile.Lava && Rng.Chance(50)) m.T[q.X, q.Y] = Tile.Ash;
                else if (liquid == Tile.Chasm && Rng.Chance(25)) m.T[q.X, q.Y] = Tile.Rubble;
            }
            if (liquid == Tile.Lava)
                foreach (var q in changed.Keys) if (Rng.OneIn(5)) m.Lights.Add(new StaticLight { P = q, R = 2, C = new Color(255, 100, 30), I = 0.6f, Flicker = true });
            m.Feeling ??= liquid switch
            {
                Tile.DeepWater => "You hear a river rushing somewhere in the dark.",
                Tile.Lava => "A red glow seeps through the cracks. Something molten is flowing nearby.",
                Tile.Chasm => "A cold draught rises from a rift that splits the level in two.",
                _ => null,
            };
            return true;
        }

        static bool Ford(Map m, Dictionary<Point, Tile> river, Tile span)
        {
            var comp = new int[Map.W, Map.H];
            int ids = 0, bigId = 0, bigN = 0;
            for (int x = 1; x < Map.W - 1; x++)
                for (int y = 1; y < Map.H - 1; y++)
                {
                    if (comp[x, y] != 0 || !Pass(m, x, y)) continue;
                    int id = ++ids, n = 0;
                    var q = new Queue<Point>();
                    q.Enqueue(new Point(x, y));
                    comp[x, y] = id;
                    while (q.Count > 0)
                    {
                        var p = q.Dequeue();
                        n++;
                        foreach (var nb in m.Neighbors(p))
                            if (comp[nb.X, nb.Y] == 0 && Pass(m, nb.X, nb.Y)) { comp[nb.X, nb.Y] = id; q.Enqueue(nb); }
                    }
                    if (n > bigN) { bigN = n; bigId = id; }
                }
            var from = new Dictionary<Point, Point>();
            var bfs = new Queue<Point>();
            for (int x = 1; x < Map.W - 1; x++)
                for (int y = 1; y < Map.H - 1; y++)
                    if (comp[x, y] == bigId) { var p = new Point(x, y); from[p] = p; bfs.Enqueue(p); }
            while (bfs.Count > 0)
            {
                var p = bfs.Dequeue();
                foreach (var d in Dirs4)
                {
                    var nb = new Point(p.X + d.X, p.Y + d.Y);
                    if (from.ContainsKey(nb) || !Map.InB(nb)) continue;
                    if (comp[nb.X, nb.Y] != 0 && comp[nb.X, nb.Y] != bigId)
                    {
                        for (var c = p; comp[c.X, c.Y] != bigId; c = from[c]) m.T[c.X, c.Y] = span;
                        return true;
                    }
                    if (!river.ContainsKey(nb) || river[nb] == Tile.Wall || Pass(m, nb.X, nb.Y)) continue;
                    from[nb] = p;
                    bfs.Enqueue(nb);
                }
            }
            return false;
        }

        static void AddLakes(Map m, int depth)
        {
            int lakes;
            Tile liquid;
            switch (m.Branch != 0 ? -1 : depth)
            {
                case -1:
                    {
                        var dd = WorldDb.Get(m.Branch);
                        lakes = Rng.Range(dd.Lakes / 2, dd.Lakes);
                        liquid = dd.Liquid == Tile.Chasm && m.NoFall ? Tile.DeepWater : dd.Liquid;
                        break;
                    }
                case 7: lakes = 6; liquid = Tile.DeepWater; break;
                case 13: case 14: lakes = 4; liquid = Tile.Lava; break;
                case 9: case 11: lakes = 3; liquid = Tile.Chasm; break;
                case 15: lakes = 2; liquid = Tile.Lava; break;
                default:
                    lakes = Rng.Chance(45) ? Rng.Range(1, 2) : 0;
                    liquid = Rng.Chance(70) ? Tile.DeepWater : (depth >= 3 && Rng.Chance(50) ? Tile.Chasm : Tile.DeepWater);
                    break;
            }
            bool river = !(m.Branch == 0 && depth >= Rules.MaxDepth) && (lakes >= 3 ? Rng.Chance(75) : lakes > 0 && Rng.Chance(40));
            River = river && AddRiver(m, liquid);
            if (River) lakes = lakes >= 3 ? lakes / 3 : 0;
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
                        if (m.T[p.X, p.Y] is Tile.Floor or Tile.Corridor or Tile.Grass && !guard.Contains(p))
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
                    if (m.T[x, y] == Tile.Floor && !guard.Contains(new Point(x, y)) && (finalHall == null || !finalHall.R.Contains(new Point(x, y)))) floors.Add(new Point(x, y));
            if (floors.Count < 20) return false;
            var inRoom = new HashSet<Point>(m.Rooms.Where(r => r != finalHall && r.Cells.Count >= 9).SelectMany(r => r.Cells));
            bool Snug(Point p)
            {
                if (!inRoom.Contains(p) || NearExit(m, p)) return false;
                foreach (var q in m.Neighbors(p)) if (m.T[q.X, q.Y] != Tile.Floor && !m.IsWallish(q.X, q.Y)) return false;
                return m.Walkable(p.X + 1, p.Y) && m.Walkable(p.X - 1, p.Y) || m.Walkable(p.X, p.Y + 1) && m.Walkable(p.X, p.Y - 1);
            }
            var pool = finalHall != null ? floors.Where(p => p.X < 50).DefaultIfEmpty(floors.OrderBy(p => p.X).First()).ToList() : floors;
            var snug = pool.Where(Snug).ToList();
            var picks = snug.Count > 0 ? snug : pool;
            Point up = above != null ? Rng.Pick(picks.OrderBy(p => Path.Cheb(p, above.Down)).Take(3).ToList()) : Rng.Pick(picks);
            var dist = Path.DistMap(m, new[] { up }, (x, y) => Pass(m, x, y));
            m.Up = up;
            m.T[up.X, up.Y] = Tile.StairsUp;
            if (!m.IsBottom)
            {
                var far = floors.Where(p => dist[p.X, p.Y] != int.MaxValue && p != up).OrderByDescending(p => dist[p.X, p.Y]).Take(12).ToList();
                if (m.Branch == 0 && m.Depth == Rules.MaxDepth - 1)
                {
                    int span = floors.Where(p => dist[p.X, p.Y] != int.MaxValue).Max(p => dist[p.X, p.Y]);
                    var west = floors.Where(p => dist[p.X, p.Y] != int.MaxValue && dist[p.X, p.Y] >= span / 2 && p.X < 30).OrderBy(p => p.X).Take(12).ToList();
                    if (west.Count > 0) far = west;
                }
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
                    guard.Add(new Point(x, y));
                }
            m.Rooms.Add(room);
            return room;
        }

        static void SetupFinal(Map m, Room hall)
        {
            var r = hall.R;
            int sx = r.X + 12;
            for (int y = r.Y; y < r.Bottom; y++)
                if (y != r.Y + 4 && y != r.Bottom - 5) m.T[sx, y] = Tile.BoneVeil;
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
            var lich = new Monster(MonsterDb.Get("hollow_king"));
            lich.Pos = new Point(r.Right - 7, cy);
            lich.Sleeping = true;
            lich.Home = lich.Pos;
            m.Monsters.Add(lich);
            m.Feeling = "Somewhere ahead, bones rattle in the dark. A dry voice murmurs: \"Another one.\"";
            for (int x = r.X; x < r.Right; x++)
                for (int y = r.Y; y < r.Bottom; y++)
                    m.Lit[x, y] = x > sx;
            m.Engravings[new Point(r.X + 2, cy)] = "Here begins the end. Beyond this point, the dead keep court.";
        }

        static void Decorate(Map m, Room finalHall)
        {
            var rooms = m.Rooms.Where(r => r != finalHall && r.Cells.Count >= 9 && !r.Cells.Contains(m.Up) && !r.Cells.Contains(m.Down)).ToList();
            Rng.Shuffle(rooms);
            int d = m.Depth;
            var themes = new List<(string t, int w)>();
            void Add(bool ok, string t, int pct)
            {
                if (!ok) return;
                int w = Owner.Theme(t);
                for (int c = pct * w / 100; c > 0; c -= 100)
                    if (Rng.Chance(c)) themes.Add((t, w + Rng.Int(100)));
            }
            Add(d >= 2 && d <= 13, "shop", 40);
            Add(d >= 3, "oracle", 25);
            Add(true, "fountain", 45);
            Add(true, "temple", 30);
            Add(d >= 3, "crypt", 30);
            Add(true, "library", 30);
            Add(true, "armory", 25);
            Add(d >= 4, "throne", 25);
            Add(true, "garden", d == 6 ? 100 : 25);
            Add(true, "pillars", 30);
            Add(true, "brazier", 35);
            Add(d >= 4, "web", 25);
            Add(d >= 5, "statues", 20);
            Add(true, "lair", 25);
            Add(true, "flooded", 20);
            themes = themes.OrderByDescending(x => x.w).ToList();
            foreach (var (t, _) in themes)
            {
                var room = rooms.FirstOrDefault(r => r.Theme == null && Suits(m, r, t));
                if (room == null) continue;
                room.Theme = t;
                var before = room.Cells.Select(p => m.T[p.X, p.Y]).ToArray();
                int lights = m.Lights.Count;
                ApplyTheme(m, room, t);
                if (t != "shop" && !Connected(m))
                {
                    for (int i = 0; i < before.Length; i++) m.T[room.Cells[i].X, room.Cells[i].Y] = before[i];
                    m.Lights.RemoveRange(lights, m.Lights.Count - lights);
                    room.Theme = null;
                }
            }
            foreach (var r in m.Rooms.Where(r => r.Theme == "cave"))
                foreach (var p in r.Cells)
                {
                    if (m.T[p.X, p.Y] != Tile.Floor) continue;
                    int n = 0;
                    foreach (var q in m.Neighbors(p)) if (m.IsWallish(q.X, q.Y)) n++;
                    if ((d == 6 || m.Branch == 2) && Rng.Chance(14)) m.T[p.X, p.Y] = Rng.Chance(40) ? Tile.Moss : Tile.Grass;
                    else if (n >= 3 && Rng.Chance(10)) m.T[p.X, p.Y] = Tile.Rubble;
                    else if (Rng.Chance(3)) m.T[p.X, p.Y] = Tile.Grass;
                }
            int crystals = d >= 10 ? Rng.Range(3, 8) : 0;
            for (int i = 0; i < crystals; i++)
            {
                for (int t = 0; t < 50; t++)
                {
                    int x = Rng.Range(2, Map.W - 3), y = Rng.Range(2, Map.H - 3);
                    if (m.T[x, y] != Tile.Wall || guard.Contains(new Point(x, y))) continue;
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
                    for (int x = R.X; x < R.Right; x += 2)
                    {
                        if (WallAbove(m, x, R.Y)) StockRack(m, new Point(x, R.Y));
                        if (WallBelow(m, x, R.Bottom - 1)) StockRack(m, new Point(x, R.Bottom - 1));
                    }
                    m.Feeling ??= "You hear the faint ring of steel settling on steel.";
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

        static void StockRack(Map m, Point p)
        {
            if (m.T[p.X, p.Y] != Tile.Floor) return;
            m.T[p.X, p.Y] = Tile.Rack;
            m.FeatureUses[p] = Rng.Chance(55) ? 1 : 0;
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
                if (!Connected(m)) { m.T[p.X, p.Y] = Tile.Floor; continue; }
                if (light && t == Tile.Brazier) m.Lights.Add(new StaticLight { P = p, R = 5, C = new Color(255, 140, 50), I = 0.9f, Flicker = true });
            }
        }

        static void AddVault(Map m)
        {
            if (!Rng.Chance(35) || (m.Branch == 0 && m.Depth >= Rules.MaxDepth)) return;
            for (int t = 0; t < 200; t++)
            {
                int w = Rng.Range(2, 3), h = 2;
                int x0 = Rng.Range(3, Map.W - w - 4), y0 = Rng.Range(3, Map.H - h - 4);
                bool ok = true;
                for (int x = x0 - 2; x < x0 + w + 2 && ok; x++)
                    for (int y = y0 - 2; y < y0 + h + 2 && ok; y++)
                        if (!Map.InB(x, y) || m.T[x, y] != Tile.Wall || guard.Contains(new Point(x, y))) ok = false;
                if (!ok) continue;
                var room = new Room { R = new Rectangle(x0, y0, w, h), Theme = "vault", Lit = true };
                for (int x = x0; x < x0 + w; x++)
                    for (int y = y0; y < y0 + h; y++)
                    {
                        m.T[x, y] = Tile.Floor;
                        room.Cells.Add(new Point(x, y));
                    }
                bool linked = false;
                foreach (var dir in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
                {
                    var start = dir.X > 0 ? new Point(x0 + w, y0) : dir.X < 0 ? new Point(x0 - 1, y0) : dir.Y > 0 ? new Point(x0, y0 + h) : new Point(x0, y0 - 1);
                    var path = new List<Point>();
                    var p = start;
                    bool found = false;
                    for (int i = 0; i < 8; i++)
                    {
                        if (!Map.InB(p) || m.T[p.X, p.Y] == Tile.PermWall) break;
                        if (Pass(m, p.X, p.Y)) { found = true; break; }
                        if (m.T[p.X, p.Y] != Tile.Wall) break;
                        path.Add(p);
                        p = new Point(p.X + dir.X, p.Y + dir.Y);
                    }
                    if (!found || path.Count == 0) continue;
                    for (int i = 0; i < path.Count; i++) m.T[path[i].X, path[i].Y] = i == 0 ? Tile.DoorSecret : Tile.Corridor;
                    if (path.Count > 1) m.T[path[path.Count - 1].X, path[path.Count - 1].Y] = Tile.DoorSecret;
                    linked = true;
                    break;
                }
                if (!linked)
                {
                    foreach (var p in room.Cells) m.T[p.X, p.Y] = Tile.Wall;
                    continue;
                }
                m.Rooms.Add(room);
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
                    var goal = m.IsBottom ? (m.Rooms.FirstOrDefault(r => r.Theme == "sanctum") ?? m.Rooms[0]).Cells[0] : m.Down;
                    bool ok = dist[goal.X, goal.Y] != int.MaxValue;
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
                if (r.Theme is "shop" or "temple" or "throne" or "vault" or "sanctum") r.Lit = true;
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
