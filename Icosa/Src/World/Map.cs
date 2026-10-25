using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public enum Tile : byte
    {
        Wall, Floor, Corridor, DoorClosed, DoorOpen, DoorLocked, DoorSecret, DoorBroken, StairsUp, StairsDown,
        Shallow, DeepWater, Lava, Chasm, Rubble, Grass, Moss, Fountain, DryFountain, Altar, Throne, Statue, Pillar,
        Bookshelf, Sarcophagus, Brazier, Web, Bridge, Rug, OracleDie, Dais, Crystal, Mushroom, Bones, PermWall, BoneVeil, Glyph, Ash
    }

    public class TileDef
    {
        public char G;
        public Color Fg, Bg;
        public bool Walk, See;
        public string Name;
        public int GlowR;
        public Color Glow;
        public bool Feature;
        public string Desc;
    }

    public enum TrapT { Pit, SpikedPit, Arrow, Dart, Block, Teleport, Trapdoor, SleepGas, Fire, Alarm, Summon, Rune, WebTrap, BearTrap, Rust }

    public class Trap
    {
        public TrapT T;
        public bool Found;
        public Point Pos;

        public static string NameOf(TrapT t) => t switch
        {
            TrapT.Pit => "pit",
            TrapT.SpikedPit => "spiked pit",
            TrapT.Arrow => "arrow trap",
            TrapT.Dart => "poison dart trap",
            TrapT.Block => "falling block trap",
            TrapT.Teleport => "teleportation glyph",
            TrapT.Trapdoor => "trap door",
            TrapT.SleepGas => "sleeping gas trap",
            TrapT.Fire => "fire trap",
            TrapT.Alarm => "alarm plate",
            TrapT.Summon => "summoning rune",
            TrapT.Rune => "glyph of warding",
            TrapT.WebTrap => "web trap",
            TrapT.BearTrap => "bear trap",
            TrapT.Rust => "rust trap",
            _ => "trap",
        };

        public string Name => NameOf(T);

        public Color Color => T switch
        {
            TrapT.Pit or TrapT.SpikedPit or TrapT.Trapdoor => new Color(150, 110, 70),
            TrapT.Teleport or TrapT.Summon or TrapT.Rune => new Color(200, 120, 255),
            TrapT.Fire => new Color(255, 110, 40),
            TrapT.SleepGas => new Color(160, 160, 255),
            TrapT.Rust => new Color(200, 110, 60),
            TrapT.WebTrap => new Color(220, 220, 220),
            _ => new Color(220, 70, 60),
        };
    }

    public class Room
    {
        public Rectangle R;
        public List<Point> Cells = new List<Point>();
        public string Theme;
        public bool Lit;
        public int ShopId = -1;
        public Point Center => new Point(R.X + R.Width / 2, R.Y + R.Height / 2);
    }

    public class StaticLight
    {
        public Point P;
        public int R;
        public Color C;
        public float I = 1f;
        public bool Flicker;
    }

    public class Shop
    {
        public int Id;
        public string Kind;
        public string Keeper;
        public Room Room;
        public Point Door;
        public Point Inside;
        public Monster KeeperMon;
    }

    public partial class Map
    {
        public const int W = 100, H = 36;
        public int Depth;
        public readonly Tile[,] T = new Tile[W, H];
        public readonly bool[,] Seen = new bool[W, H];
        public readonly bool[,] Vis = new bool[W, H];
        public readonly bool[,] Lit = new bool[W, H];
        public readonly float[,] LR = new float[W, H], LG = new float[W, H], LB = new float[W, H];
        public readonly char[,] MemCh = new char[W, H];
        public readonly Color[,] MemFg = new Color[W, H];
        public readonly float[,] Stain = new float[W, H];
        public readonly Color[,] StainC = new Color[W, H];
        public readonly int[,] Dark = new int[W, H];
        public readonly int[,] Cloud = new int[W, H];
        public readonly Elem[,] CloudE = new Elem[W, H];
        public readonly int[,] Fire = new int[W, H];
        public readonly bool[,] Detected = new bool[W, H];
        public readonly List<Monster> Monsters = new List<Monster>();
        public readonly List<Item> Items = new List<Item>();
        public readonly Dictionary<Point, Trap> Traps = new Dictionary<Point, Trap>();
        public readonly Dictionary<Point, string> Engravings = new Dictionary<Point, string>();
        public readonly List<Room> Rooms = new List<Room>();
        public readonly List<StaticLight> Lights = new List<StaticLight>();
        public readonly List<Shop> Shops = new List<Shop>();
        public readonly Dictionary<Point, int> FeatureUses = new Dictionary<Point, int>();
        public Point Up, Down;
        public Color WallFg, WallBg, FloorFg, FloorBg;
        public float Ambient;
        public string Feeling;
        public bool Visited;
        public int[,] PlayerDist;
        public int LastWander;

        public static readonly Dictionary<Tile, TileDef> Defs = new Dictionary<Tile, TileDef>();

        static void D(Tile t, char g, Color fg, Color bg, bool walk, bool see, string name, string desc = null, bool feature = false, int glowR = 0, Color? glow = null)
        {
            Defs[t] = new TileDef { G = g, Fg = fg, Bg = bg, Walk = walk, See = see, Name = name, Desc = desc, Feature = feature, GlowR = glowR, Glow = glow ?? Color.White };
        }

        static Map()
        {
            var C = new Func<int, int, int, Color>((r, g, b) => new Color(r, g, b));
            D(Tile.Wall, '#', C(140, 130, 115), C(60, 54, 46), false, false, "stone wall");
            D(Tile.PermWall, '#', C(120, 110, 100), C(40, 36, 32), false, false, "ancient wall");
            D(Tile.Floor, '·', C(110, 105, 95), C(20, 18, 16), true, true, "floor");
            D(Tile.Corridor, '·', C(95, 90, 82), C(14, 12, 11), true, true, "passage");
            D(Tile.DoorClosed, '+', C(200, 140, 70), C(70, 45, 20), false, false, "closed door", "A heavy wooden door.");
            D(Tile.DoorOpen, '\'', C(200, 140, 70), C(30, 22, 14), true, true, "open door");
            D(Tile.DoorLocked, '+', C(220, 160, 80), C(80, 50, 22), false, false, "closed door", "A heavy wooden door.");
            D(Tile.DoorSecret, '#', C(140, 130, 115), C(60, 54, 46), false, false, "stone wall");
            D(Tile.DoorBroken, '\'', C(140, 100, 60), C(25, 20, 14), true, true, "broken door");
            D(Tile.StairsUp, '<', C(255, 240, 200), C(40, 36, 30), true, true, "staircase up", "Stairs leading up toward daylight.", true);
            D(Tile.StairsDown, '>', C(255, 240, 200), C(40, 36, 30), true, true, "staircase down", "Stairs leading down into deeper darkness.", true);
            D(Tile.Shallow, '~', C(90, 140, 210), C(20, 40, 80), true, true, "shallow water", "Knee-deep water, cold and dark.");
            D(Tile.DeepWater, '≈', C(70, 110, 220), C(10, 25, 70), false, true, "deep water", "Deep, black water. You would sink in armor.");
            D(Tile.Lava, '≈', C(255, 200, 60), C(200, 60, 10), false, true, "lava", "Molten rock. Certain death.", false, 2, C(255, 110, 30));
            D(Tile.Chasm, ' ', C(30, 30, 40), C(0, 0, 0), true, true, "chasm", "A yawning drop into the level below.");
            D(Tile.Rubble, ':', C(130, 120, 100), C(25, 22, 18), true, true, "rubble");
            D(Tile.Grass, '"', C(90, 150, 60), C(15, 25, 10), true, true, "pale cave grass");
            D(Tile.Moss, '"', C(90, 220, 170), C(10, 35, 30), true, true, "luminous moss", "Moss that glows a soft sea-green.", false, 1, C(60, 220, 160));
            D(Tile.Fountain, '{', C(110, 170, 255), C(20, 30, 60), true, true, "fountain", "A carved stone fountain. The water is clear, or seems so. (q to drink)", true);
            D(Tile.DryFountain, '{', C(120, 120, 120), C(20, 20, 20), true, true, "dry fountain", "Dry and cracked.", true);
            D(Tile.Altar, '_', C(230, 230, 255), C(40, 40, 60), true, true, "altar", "A stone altar. Items dropped here reveal their blessings and curses. Pray here to be heard more clearly.", true);
            D(Tile.Throne, 'π', C(255, 210, 60), C(60, 20, 30), true, true, "throne", "An ornate throne of a forgotten king. (E to sit)", true);
            D(Tile.Statue, 'Ω', C(200, 200, 200), C(30, 30, 30), false, true, "statue", "A lifelike statue. Its expression is one of surprise.");
            D(Tile.Pillar, 'O', C(160, 150, 140), C(50, 45, 40), false, false, "pillar");
            D(Tile.Bookshelf, '≡', C(170, 110, 60), C(50, 30, 15), false, false, "bookshelf", "Shelves of moldering books.");
            D(Tile.Sarcophagus, '∩', C(200, 190, 170), C(40, 35, 30), false, true, "sarcophagus", "A stone coffin with a carved lid.");
            D(Tile.Brazier, '¤', C(255, 160, 60), C(60, 25, 10), false, true, "brazier", "A bronze brazier full of coals that never quite go out.", false, 5, C(255, 140, 50));
            D(Tile.Web, '"', C(230, 230, 230), C(30, 30, 30), true, true, "spider web", "Sticky strands of web.");
            D(Tile.Bridge, '=', C(160, 110, 60), C(20, 15, 10), true, true, "rope bridge");
            D(Tile.Rug, '·', C(170, 60, 60), C(60, 15, 20), true, true, "faded rug");
            D(Tile.OracleDie, '◊', C(255, 255, 255), C(40, 40, 70), false, true, "Oracle Die", "A waist-high marble icosahedron set in a stone cradle. Numbers on its faces glow faintly. (E to roll it)", true, 3, C(170, 170, 255));
            D(Tile.Dais, '·', C(255, 220, 140), C(60, 45, 20), true, true, "golden dais");
            D(Tile.Crystal, '♦', C(170, 220, 255), C(30, 40, 70), false, false, "crystal formation", "Glowing crystals jut from the stone.", false, 3, C(120, 170, 255));
            D(Tile.Mushroom, '♣', C(200, 120, 230), C(25, 10, 30), false, true, "giant mushroom", "A mushroom taller than a man, gently luminous.", false, 2, C(180, 90, 255));
            D(Tile.Bones, ',', C(220, 210, 190), C(20, 18, 16), true, true, "scattered bones");
            D(Tile.BoneVeil, '║', C(255, 220, 140), C(70, 30, 30), false, false, "the Bone Veil", "A towering lattice of fused bone, carved with the names of everyone who has died in the Tomb. There is room left for yours.");
            D(Tile.Glyph, '¤', C(255, 240, 160), C(40, 35, 10), true, true, "burning glyph", "A holy glyph etched into the floor.");
            D(Tile.Ash, '·', C(80, 75, 70), C(18, 16, 15), true, true, "ash");
        }

        public Map(int depth)
        {
            Depth = depth;
        }

        public static bool InB(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public static bool InB(Point p) => InB(p.X, p.Y);
        public TileDef Def(int x, int y) => Defs[T[x, y]];
        public TileDef Def(Point p) => Defs[T[p.X, p.Y]];
        public Tile this[Point p] { get => T[p.X, p.Y]; set => T[p.X, p.Y] = value; }

        public bool BlocksSight(int x, int y) => !InB(x, y) || !Defs[T[x, y]].See;
        public bool Walkable(int x, int y) => InB(x, y) && Defs[T[x, y]].Walk;
        public bool Walkable(Point p) => Walkable(p.X, p.Y);
        public bool IsWallish(int x, int y) => !InB(x, y) || T[x, y] is Tile.Wall or Tile.PermWall or Tile.DoorSecret or Tile.BoneVeil;
        public bool IsDoor(Point p) => this[p] is Tile.DoorClosed or Tile.DoorOpen or Tile.DoorLocked or Tile.DoorBroken;

        public bool PassableFor(Monster m, int x, int y)
        {
            if (!InB(x, y)) return false;
            var t = T[x, y];
            if (m.Def.Has(MF.Phase) && t is Tile.Wall or Tile.DoorSecret or Tile.DoorClosed or Tile.DoorLocked or Tile.Bookshelf or Tile.Pillar) return true;
            if (t == Tile.DoorClosed) return m.Def.Has(MF.Hands) || m.Def.Size == Size.Large;
            if (t == Tile.DoorLocked) return m.Def.Size == Size.Large || m.Def.Id == "hollow_king";
            if (m.Def.Has(MF.Burrow) && t is Tile.Wall or Tile.Rubble) return true;
            if (t == Tile.DeepWater) return m.Def.Has(MF.Fly) || m.Def.Has(MF.Swim);
            if (t == Tile.Lava) return m.Def.Has(MF.Fly) || m.Def.Has(MF.ResFire);
            if (t == Tile.Chasm) return m.Def.Has(MF.Fly);
            return Defs[t].Walk;
        }

        public Monster MonsterAt(Point p)
        {
            foreach (var m in Monsters) if (!m.Dead && m.Pos == p) return m;
            return null;
        }

        public Actor ActorAt(Point p)
        {
            if (Dm.Player != null && Dm.Player.Pos == p && !Dm.Player.Dead) return Dm.Player;
            return MonsterAt(p);
        }

        public IEnumerable<Item> ItemsAt(Point p) => Items.Where(i => i.Pos == p);
        public Item TopItem(Point p)
        {
            Item top = null;
            foreach (var i in Items) if (i.Pos == p) top = i;
            return top;
        }

        public Trap TrapAt(Point p) => Traps.TryGetValue(p, out var t) ? t : null;

        public void Drop(Item it, Point p)
        {
            it.Pos = p;
            if (it.Stackable)
            {
                foreach (var o in Items)
                    if (o.Pos == p && o.CanStack(it))
                    {
                        o.Count += it.Count;
                        return;
                    }
            }
            Items.Add(it);
        }

        public bool Free(Point p) => Walkable(p) && ActorAt(p) == null;

        public Room RoomAt(Point p)
        {
            foreach (var r in Rooms)
                if (r.R.Contains(p) && r.Cells.Contains(p)) return r;
            return null;
        }

        public Shop ShopAt(Point p)
        {
            foreach (var s in Shops)
                if (s.Room.R.Contains(p) && s.Room.Cells.Contains(p)) return s;
            return null;
        }

        public IEnumerable<Point> Neighbors(Point p)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var q = new Point(p.X + dx, p.Y + dy);
                    if (InB(q)) yield return q;
                }
        }

        public void Bleed(Point p, Color c, float amt)
        {
            if (!InB(p) || !Defs[T[p.X, p.Y]].Walk) return;
            if (Stain[p.X, p.Y] < 0.05f) StainC[p.X, p.Y] = c;
            else StainC[p.X, p.Y] = Pal.Lerp(StainC[p.X, p.Y], c, 0.5f);
            Stain[p.X, p.Y] = Math.Min(0.85f, Stain[p.X, p.Y] + amt);
        }

        public Point RandomFloor(Func<Point, bool> ok = null, int tries = 2000)
        {
            for (int i = 0; i < tries; i++)
            {
                var p = new Point(Rng.Range(1, W - 2), Rng.Range(1, H - 2));
                if ((T[p.X, p.Y] is Tile.Floor or Tile.Corridor or Tile.Grass or Tile.Moss or Tile.Rug or Tile.Bones or Tile.Shallow or Tile.Rubble) && (ok == null || ok(p))) return p;
            }
            for (int x = 1; x < W - 1; x++)
                for (int y = 1; y < H - 1; y++)
                {
                    var p = new Point(x, y);
                    if (T[x, y] == Tile.Floor && (ok == null || ok(p))) return p;
                }
            return new Point(W / 2, H / 2);
        }

        public Point? NearestFree(Point p, int maxR = 6, bool allowItems = true)
        {
            if (Free(p) && T[p.X, p.Y] != Tile.Chasm) return p;
            for (int r = 1; r <= maxR; r++)
            {
                var cands = new List<Point>();
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var q = new Point(p.X + dx, p.Y + dy);
                        if (InB(q) && Free(q) && T[q.X, q.Y] is not (Tile.Chasm or Tile.Lava)) cands.Add(q);
                    }
                if (cands.Count > 0) return Rng.Pick(cands);
            }
            return null;
        }

        public void ComputeLight(Player p)
        {
            Array.Clear(LR, 0, LR.Length);
            Array.Clear(LG, 0, LG.Length);
            Array.Clear(LB, 0, LB.Length);
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (Lit[x, y] && Dark[x, y] <= 0)
                    {
                        LR[x, y] = 0.95f * Ambient;
                        LG[x, y] = 0.9f * Ambient;
                        LB[x, y] = 0.8f * Ambient;
                    }
                }
            foreach (var l in Lights)
            {
                float flick = l.Flicker ? 0.85f + (float)Rng.Float() * 0.15f : 1f;
                AddLight(l.P, l.R, l.C, l.I * flick);
            }
            foreach (var m in Monsters)
            {
                if (m.Dead || m.Def.GlowRadius <= 0) continue;
                AddLight(m.Pos, m.Def.GlowRadius, m.Def.GlowColor, 0.9f);
            }
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (Fire[x, y] > 0) AddLight(new Point(x, y), 2, new Color(255, 120, 40), 0.7f);
            foreach (var it in Items)
            {
                if (it.Def.Kind == IK.Light && it.Lit && it.Fuel > 0) AddLight(it.Pos, Math.Max(1, it.Def.LightRadius - 1), it.Def.LightColor, 0.8f);
                if (it.Def.Kind == IK.Heart) AddLight(it.Pos, 3, new Color(255, 80, 120), 0.9f);
            }
            if (p != null && !p.Dead)
            {
                int r = p.LightRadius;
                if (r > 0) AddLight(p.Pos, r, p.LightColor, 1.05f);
                if (p.Carrying("heart")) AddLight(p.Pos, 2, new Color(255, 80, 120), 0.5f);
            }
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (Dark[x, y] > 0)
                    {
                        LR[x, y] *= 0.05f;
                        LG[x, y] *= 0.05f;
                        LB[x, y] *= 0.05f;
                    }
                }
        }

        void AddLight(Point o, int r, Color c, float intensity)
        {
            float cr = c.R / 255f, cg = c.G / 255f, cb = c.B / 255f;
            Fov.Compute(o, r, BlocksSight, (x, y) =>
            {
                if (!InB(x, y)) return;
                float d = (float)Math.Sqrt((x - o.X) * (x - o.X) + (y - o.Y) * (y - o.Y));
                float f = 1f - d / (r + 0.75f);
                if (f <= 0) return;
                f = f * f * 0.75f + f * 0.25f;
                f *= intensity;
                LR[x, y] += cr * f;
                LG[x, y] += cg * f;
                LB[x, y] += cb * f;
            });
        }

        public float Lum(int x, int y) => Math.Max(LR[x, y], Math.Max(LG[x, y], LB[x, y]));

        public void ComputeVision(Player p)
        {
            ComputeLight(p);
            Array.Clear(Vis, 0, Vis.Length);
            if (p.Has(Status.Blind))
            {
                foreach (var q in Neighbors(p.Pos).Append(p.Pos))
                {
                    Vis[q.X, q.Y] = true;
                    Seen[q.X, q.Y] = true;
                }
                return;
            }
            var pp = p.Pos;
            Fov.Compute(pp, 60, BlocksSight, (x, y) =>
            {
                if (!InB(x, y)) return;
                int d = Math.Max(Math.Abs(x - pp.X), Math.Abs(y - pp.Y));
                if (d <= 1 || Lum(x, y) > 0.07f)
                {
                    Vis[x, y] = true;
                    Seen[x, y] = true;
                }
            });
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (Vis[x, y]) Remember(x, y);
        }

        public void Remember(int x, int y)
        {
            var it = TopItem(new Point(x, y));
            var tr = TrapAt(new Point(x, y));
            if (it != null)
            {
                MemCh[x, y] = it.Glyph;
                MemFg[x, y] = it.Color;
            }
            else if (tr != null && tr.Found)
            {
                MemCh[x, y] = '^';
                MemFg[x, y] = tr.Color;
            }
            else
            {
                var d = Def(x, y);
                MemCh[x, y] = d.G;
                MemFg[x, y] = d.Fg;
            }
        }

        public void RevealMap(bool items)
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    bool show = T[x, y] != Tile.Wall && T[x, y] != Tile.PermWall;
                    if (!show)
                        foreach (var q in Neighbors(new Point(x, y)))
                            if (T[q.X, q.Y] != Tile.Wall && T[q.X, q.Y] != Tile.PermWall && T[q.X, q.Y] != Tile.DoorSecret) { show = true; break; }
                    if (!show) continue;
                    Seen[x, y] = true;
                    var d = Def(x, y);
                    MemCh[x, y] = d.G;
                    MemFg[x, y] = d.Fg;
                }
            if (items)
                foreach (var it in Items)
                {
                    Seen[it.Pos.X, it.Pos.Y] = true;
                    MemCh[it.Pos.X, it.Pos.Y] = it.Glyph;
                    MemFg[it.Pos.X, it.Pos.Y] = it.Color;
                }
        }

        public void Smudge(Point center, int radius)
        {
            for (int x = center.X - radius; x <= center.X + radius; x++)
                for (int y = center.Y - radius; y <= center.Y + radius; y++)
                    if (InB(x, y) && !Vis[x, y] && Rng.Chance(70)) Seen[x, y] = false;
        }

        public void ComputePlayerDist(Player p)
        {
            PlayerDist = Path.DistMap(this, new[] { p.Pos }, (x, y) =>
            {
                var t = T[x, y];
                return Defs[t].Walk || t is Tile.DoorClosed or Tile.DoorLocked or Tile.DeepWater;
            });
        }

        public void TickTerrain()
        {
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (Dark[x, y] > 0) Dark[x, y]--;
                    if (Cloud[x, y] > 0) Cloud[x, y]--;
                    if (Fire[x, y] > 0)
                    {
                        Fire[x, y]--;
                        if (Fire[x, y] == 0 && T[x, y] is Tile.Grass or Tile.Web) T[x, y] = Tile.Ash;
                    }
                    if (Stain[x, y] > 0.1f && Rng.OneIn(4000)) Stain[x, y] *= 0.9f;
                }
        }
    }
}
