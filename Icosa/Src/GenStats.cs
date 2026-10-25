using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class GenStats
    {
        class Row
        {
            public string Name;
            public int Branch, Depth, Fails;
            public List<double[]> S = new List<double[]>();
        }

        static readonly string[] Cols = { "ms", "rooms", "sets", "floor%", "route", "reach", "dead", "loop%", "unrch", "mons", "near", "items", "detour", "farIt%", "upRm%", "gate%", "lockOK", "upOff", "land%", "river%" };

        public static void Run(string[] args)
        {
            int gi = Array.IndexOf(args, "--genstats");
            int n = gi + 1 < args.Length && int.TryParse(args[gi + 1], out var k) ? k : 20;
            string file = gi + 2 < args.Length && !args[gi + 2].StartsWith("--") ? args[gi + 2] : "genstats.txt";
            int si = Array.IndexOf(args, "--seed");
            int seed0 = si >= 0 && si + 1 < args.Length ? int.Parse(args[si + 1]) : 1;
            var rows = new List<Row>();
            for (int d = 1; d <= Rules.MaxDepth; d++) rows.Add(new Row { Name = "Tomb " + d, Branch = 0, Depth = d });
            foreach (var dd in WorldDb.Dungeons.Where(x => x.Branch != 0))
                for (int d = dd.Top; d <= dd.Bottom; d++) rows.Add(new Row { Name = dd.Short + " " + d, Branch = dd.Branch, Depth = d });
            var sets = new Dictionary<string, int>();
            var errors = new List<string>();
            var dumps = new List<string>();
            var owners = new Dictionary<string, Dictionary<string, int>>();
            var history = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
                foreach (var row in rows)
                {
                    if (row == rows[0]) Dm.Levels.Clear();
                    int seed = seed0 + i * 7919 + row.Branch * 131 + row.Depth;
                    Rng.Init(seed);
                    Spawn.ArtifactsMade.Clear();
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        var m = Gen.Generate(row.Depth, row.Branch);
                        double ms = sw.Elapsed.TotalMilliseconds;
                        foreach (var s in Gen.LastSets) sets[s] = sets.GetValueOrDefault(s) + 1;
                        foreach (var s in Gen.LastDecay) history[s] = history.GetValueOrDefault(s) + 1;
                        var ok = owners.TryGetValue(Gen.Owner.Id, out var tally) ? tally : owners[Gen.Owner.Id] = new Dictionary<string, int>();
                        foreach (var r in m.Rooms) if (r.Theme is string th && th != "cave") ok[th] = ok.GetValueOrDefault(th) + 1;
                        ok["#levels"] = ok.GetValueOrDefault("#levels") + 1;
                        row.S.Add(Measure(m, ms));
                        Dm.Levels[m.Key] = m;
                        if (row.S[^1][8] > 0 || row.S[^1][16] == 0)
                        {
                            row.Fails++;
                            if (dumps.Count < 4) dumps.Add($"{row.Name} seed {seed}, sets {string.Join(",", Gen.LastSets)}:\n{Ascii(m)}");
                        }
                    }
                    catch (Exception e)
                    {
                        row.Fails++;
                        if (errors.Count < 20) errors.Add($"{row.Name} seed {seed}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                    }
                }
            var sb = new StringBuilder();
            sb.AppendLine($"genstats: {n} levels per row, base seed {seed0}");
            sb.AppendLine("route = steps up->down, reach = farthest step from up, dead = dead ends, loop% = corridor/door cells on a cycle,");
            sb.AppendLine("unrch = walkable cells unreachable from up, near = monsters within 10 steps of up, detour = mean item distance off the route, farIt% = items 12+ off route");
            sb.AppendLine();
            sb.Append($"{"level",-12}{"bad",5}");
            foreach (var c in Cols) sb.Append($"{c,8}");
            sb.AppendLine();
            foreach (var row in rows)
            {
                sb.Append($"{row.Name,-12}{row.Fails,5}");
                for (int c = 0; c < Cols.Length; c++)
                {
                    var vals = row.S.Select(s => s[c]).Where(v => !double.IsNaN(v)).ToList();
                    sb.Append(vals.Count == 0 ? $"{"-",8}" : $"{vals.Average(),8:0.0}");
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("set pieces placed:");
            foreach (var v in VaultDb.All) sb.AppendLine($"  {v.Id,-14}{sets.GetValueOrDefault(v.Id),6}");
            sb.AppendLine();
            sb.AppendLine("history (builders' masonry, then decay events):");
            foreach (var (ev, v) in history.OrderByDescending(x => x.Value)) sb.AppendLine($"  {ev,-14}{v,6}");
            sb.AppendLine();
            sb.AppendLine("owners (levels, then room themes per level, most common first):");
            foreach (var (id, t) in owners.OrderByDescending(x => x.Value["#levels"]))
            {
                int lv = t["#levels"];
                sb.AppendLine($"  {id,-12}{lv,5}  " + string.Join(" ", t.Where(x => x.Key != "#levels").OrderByDescending(x => x.Value).Take(8).Select(x => $"{x.Key} {x.Value / (double)lv:0.00}")));
            }
            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("exceptions:");
                foreach (var e in errors) sb.AppendLine(e);
            }
            if (dumps.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("levels with unreachable cells (X):");
                foreach (var dm in dumps) sb.AppendLine(dm);
            }
            File.WriteAllText(file, sb.ToString());
            Console.Write(sb.ToString());
        }

        public static void World(string[] args)
        {
            int gi = Array.IndexOf(args, "--worldgen");
            int n = gi + 1 < args.Length && int.TryParse(args[gi + 1], out var k) ? k : 20;
            string file = gi + 2 < args.Length && !args[gi + 2].StartsWith("--") ? args[gi + 2] : "worldgen.txt";
            int si = Array.IndexOf(args, "--seed");
            int seed0 = si >= 0 && si + 1 < args.Length ? int.Parse(args[si + 1]) : 1;
            var tiles = new Dictionary<Tile, double>();
            var sites = new Dictionary<string, Dictionary<string, int>>();
            var dist = new Dictionary<string, List<int>>();
            var dumps = new List<string>();
            var errors = new List<string>();
            double ms = 0, att = 0, fords = 0, filled = 0, lakes = 0, rivers = 0, valleys = 0;
            var wild = new int[5];
            int ok = 0;
            Wild.FailWhy.Clear();
            for (int i = 0; i < n; i++)
            {
                Rng.Init(seed0 + i);
                try
                {
                    var sw = Stopwatch.StartNew();
                    var m = Wild.Generate();
                    ms += sw.Elapsed.TotalMilliseconds;
                    ok++;
                    att += Wild.Attempts;
                    fords += Wild.Fords;
                    valleys += Wild.Valleys;
                    for (int x = 1; x < Map.W - 1; x++)
                        for (int y = 1; y < Map.H - 1; y++)
                            if (m.Walkable(x, y)) wild[Math.Min(4, (int)Wild.Wildness(m, new Point(x, y)))]++;
                    filled += Wild.Filled;
                    lakes += Wild.LakeCells;
                    rivers += Wild.RiverCells;
                    for (int x = 1; x < Map.W - 1; x++)
                        for (int y = 1; y < Map.H - 1; y++)
                            tiles[m.T[x, y]] = tiles.GetValueOrDefault(m.T[x, y]) + 1;
                    var home = m.Places[0].At;
                    foreach (var pl in m.Places)
                    {
                        var t = sites.TryGetValue(pl.Name, out var d) ? d : sites[pl.Name] = new Dictionary<string, int>();
                        foreach (var q in m.Neighbors(pl.At).Concat(m.Neighbors(pl.At).SelectMany(m.Neighbors)).Distinct())
                        {
                            string name = Map.Defs[m.T[q.X, q.Y]].Name;
                            t[name] = t.GetValueOrDefault(name) + 1;
                        }
                        (dist.TryGetValue(pl.Name, out var l) ? l : dist[pl.Name] = new List<int>()).Add(Math.Max(Math.Abs(pl.At.X - home.X), Math.Abs(pl.At.Y - home.Y) * 2));
                    }
                    if (dumps.Count < 4) dumps.Add($"seed {seed0 + i}:\n{Overland(m)}");
                }
                catch (Exception e)
                {
                    if (errors.Count < 20) errors.Add($"seed {seed0 + i}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine($"worldgen: {ok}/{n} overlands, base seed {seed0}, {ms / Math.Max(1, ok):0.0} ms, {att / Math.Max(1, ok):0.00} attempts");
            sb.AppendLine($"per map: lake cells {lakes / Math.Max(1, ok):0.0}, river cells {rivers / Math.Max(1, ok):0.0}, fords {fords / Math.Max(1, ok):0.0}, hidden valleys {valleys / Math.Max(1, ok):0.00}, cells walled off {filled / Math.Max(1, ok):0.0}");
            sb.AppendLine("walkable land by wildness 0-1/1-2/2-3/3-4/4+: " + string.Join(" ", wild.Select(v => $"{100.0 * v / Math.Max(1, wild.Sum()):0}%")));
            sb.AppendLine("failed attempts: " + string.Join(", ", Wild.FailWhy.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value}")));
            sb.AppendLine();
            double total = tiles.Values.Sum();
            sb.AppendLine("terrain:");
            foreach (var (t, v) in tiles.OrderByDescending(x => x.Value)) sb.AppendLine($"  {Map.Defs[t].Name,-16}{100 * v / total,6:0.0}%");
            sb.AppendLine();
            sb.AppendLine("sites (mean distance from home, then terrain within 2 squares, share of 24 cells):");
            foreach (var (name, t) in sites)
            {
                double all = t.Values.Sum();
                sb.AppendLine($"  {name,-38}{dist[name].Average(),5:0}  " + string.Join(" ", t.OrderByDescending(x => x.Value).Take(5).Select(x => $"{x.Key} {100 * x.Value / all:0}%")));
            }
            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("exceptions:");
                foreach (var e in errors) sb.AppendLine(e);
            }
            sb.AppendLine();
            sb.AppendLine("^ mountain  n hills  . plains  f forest  F wildwood  \" marsh  ~ water  s ford  = road  # bridge  T town  > dungeon  b bell");
            foreach (var d in dumps) sb.AppendLine(d);
            File.WriteAllText(file, sb.ToString());
        }

        static string Overland(Map m)
        {
            var sb = new StringBuilder();
            for (int y = 0; y < Map.H; y++)
            {
                for (int x = 0; x < Map.W; x++)
                    sb.Append(m.T[x, y] switch
                    {
                        Tile.Mountain => '^',
                        Tile.Hills => 'n',
                        Tile.Plains => '.',
                        Tile.Forest => 'f',
                        Tile.Wildwood => 'F',
                        Tile.Marsh => '"',
                        Tile.DeepWater => '~',
                        Tile.Shallow => 's',
                        Tile.Road => '=',
                        Tile.Bridge => '#',
                        Tile.TownSite => 'T',
                        Tile.StairsDown => '>',
                        Tile.Bell => 'b',
                        _ => '?',
                    });
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static double[] Measure(Map m, double ms)
        {
            Spawn.Chart(m);
            int pass = 0, unreach = 0;
            for (int x = 0; x < Map.W; x++)
                for (int y = 0; y < Map.H; y++)
                {
                    if (!Spawn.Pass(m, x, y)) continue;
                    pass++;
                    if (Spawn.Reach[x, y] == int.MaxValue) unreach++;
                }
            int Rch(Point p) => Spawn.Reach[p.X, p.Y] == int.MaxValue ? -1 : Spawn.Reach[p.X, p.Y];
            int Off(Point p) => Spawn.Detour[p.X, p.Y] == int.MaxValue ? 0 : Spawn.Detour[p.X, p.Y];
            int route = m.IsBottom ? Spawn.ReachMax : Math.Max(0, Rch(m.Down));
            var cut = Cuts(m);
            int choke = 0, cyc = 0;
            for (int x = 1; x < Map.W - 1; x++)
                for (int y = 1; y < Map.H - 1; y++)
                {
                    if (m.T[x, y] is not (Tile.Corridor or Tile.DoorClosed or Tile.DoorOpen or Tile.DoorLocked or Tile.DoorSecret or Tile.DoorBroken)) continue;
                    choke++;
                    if (!cut[x * Map.H + y]) cyc++;
                }
            var mons = m.Monsters.Where(x => !x.Peaceful).ToList();
            var items = m.Items.Where(i => m.ShopAt(i.Pos) == null).ToList();
            return new double[]
            {
                ms, m.Rooms.Count, Gen.LastSets.Count, 100.0 * pass / ((Map.W - 2) * (Map.H - 2)), route, Spawn.ReachMax,
                Spawn.DeadEnds(m).Count, choke == 0 ? 0 : 100.0 * cyc / choke, unreach,
                mons.Count, mons.Count(x => Rch(x.Pos) >= 0 && Rch(x.Pos) <= 10),
                items.Count, items.Count == 0 ? 0 : items.Average(i => Off(i.Pos)), items.Count == 0 ? 0 : 100.0 * items.Count(i => Off(i.Pos) >= 12) / items.Count,
                m.RoomAt(m.Up) != null && m.Neighbors(m.Up).All(q => m.T[q.X, q.Y] is not (Tile.Corridor or Tile.DoorClosed or Tile.DoorOpen or Tile.DoorLocked)) ? 100 : 0,
                Find(m, Tile.Gate) != null ? 100 : 0, LockOk(m),
                Gen.Above(m.Depth, m.Branch) is Map a ? Path.Cheb(m.Up, a.Down) : double.NaN,
                Gen.Holes(Gen.Above(m.Depth, m.Branch)) is var holes && holes.Count > 0 ? 100.0 * holes.Count(h => Spawn.Pass(m, h.X, h.Y) || m.Neighbors(h).Any(q => Spawn.Pass(m, q.X, q.Y))) / holes.Count : double.NaN,
                Gen.River ? 100 : 0,
            };
        }

        static Point? Find(Map m, Tile t)
        {
            for (int x = 0; x < Map.W; x++)
                for (int y = 0; y < Map.H; y++)
                    if (m.T[x, y] == t) return new Point(x, y);
            return null;
        }

        static double LockOk(Map m)
        {
            if (Find(m, Tile.Gate) == null) return double.NaN;
            var lever = Find(m, Tile.Lever);
            if (lever == null) return 0;
            var d = Path.DistMap(m, new[] { m.Up }, (x, y) => m.Walkable(x, y) && m.T[x, y] != Tile.Chasm || m.T[x, y] is Tile.DoorClosed or Tile.DoorLocked);
            bool reach = m.Neighbors(lever.Value).Any(q => d[q.X, q.Y] != int.MaxValue);
            bool blocked = d[m.Down.X, m.Down.Y] == int.MaxValue;
            return reach && blocked ? 100 : 0;
        }

        static string Ascii(Map m)
        {
            var sb = new StringBuilder();
            for (int y = 0; y < Map.H; y++)
            {
                for (int x = 0; x < Map.W; x++)
                {
                    var t = m.T[x, y];
                    char g = Spawn.Pass(m, x, y) && Spawn.Reach[x, y] == int.MaxValue ? 'X' : t switch
                    {
                        Tile.Wall or Tile.PermWall => '#',
                        Tile.DoorSecret => 's',
                        Tile.DoorLocked => 'L',
                        Tile.Floor or Tile.Corridor or Tile.Rug or Tile.Dais => '.',
                        Tile.DeepWater => 'W',
                        Tile.Lava => 'V',
                        Tile.Chasm => 'H',
                        Tile.Mushroom => 'P',
                        Tile.Crystal => 'c',
                        Tile.Statue => 'T',
                        Tile.Brazier => 'B',
                        Tile.Sarcophagus => 'C',
                        Tile.Bookshelf => 'K',
                        _ => Map.Defs[t].G > 127 ? '?' : Map.Defs[t].G,
                    };
                    sb.Append(g);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static bool[] Cuts(Map m)
        {
            int n = Map.W * Map.H;
            var disc = new int[n];
            var low = new int[n];
            var par = new int[n];
            var it = new int[n];
            var kids = new int[n];
            var cut = new bool[n];
            Array.Fill(disc, -1);
            int t = 0;
            var st = new Stack<int>();
            for (int s = 0; s < n; s++)
            {
                if (disc[s] >= 0 || !Spawn.Pass(m, s / Map.H, s % Map.H)) continue;
                disc[s] = low[s] = t++;
                par[s] = -1;
                st.Push(s);
                while (st.Count > 0)
                {
                    int u = st.Peek();
                    if (it[u] < 8)
                    {
                        var dir = Path.Dirs[it[u]++];
                        int x = u / Map.H + dir.X, y = u % Map.H + dir.Y;
                        if (!Map.InB(x, y) || !Spawn.Pass(m, x, y)) continue;
                        int v = x * Map.H + y;
                        if (disc[v] < 0)
                        {
                            par[v] = u;
                            kids[u]++;
                            disc[v] = low[v] = t++;
                            st.Push(v);
                        }
                        else if (v != par[u]) low[u] = Math.Min(low[u], disc[v]);
                    }
                    else
                    {
                        st.Pop();
                        int p = par[u];
                        if (p < 0) continue;
                        low[p] = Math.Min(low[p], low[u]);
                        if (par[p] >= 0 && low[u] >= disc[p]) cut[p] = true;
                    }
                }
                if (kids[s] > 1) cut[s] = true;
            }
            return cut;
        }
    }
}
