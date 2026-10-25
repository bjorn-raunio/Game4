using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class Spawn
    {
        public static readonly HashSet<string> ArtifactsMade = new HashSet<string>();

        public static void Populate(Map m)
        {
            int d = m.Depth;
            foreach (var shop in m.Shops) StockShop(m, shop);
            foreach (var r in m.Rooms)
            {
                switch (r.Theme)
                {
                    case "crypt":
                        for (int i = 0; i < Rng.Range(2, 4); i++)
                        {
                            var und = PickMonster(d, def => def.Has(MF.Undead) && def.Freq > 0);
                            if (und != null) PlaceMonster(m, und, Rng.Pick(r.Cells), true);
                        }
                        for (int i = 0; i < Rng.Range(1, 3); i++) DropAt(m, RandomItem(d + 1), Rng.Pick(r.Cells));
                        break;
                    case "library":
                        for (int i = 0; i < Rng.Range(2, 4); i++) DropAt(m, RandomItem(d, Rng.Chance(20) ? IK.Book : IK.Scroll), Rng.Pick(r.Cells));
                        break;
                    case "armory":
                        for (int i = 0; i < Rng.Range(2, 5); i++) DropAt(m, RandomItem(d, Rng.Chance(50) ? IK.Weapon : IK.Armor), Rng.Pick(r.Cells));
                        break;
                    case "throne":
                        {
                            var court = PickMonster(d, def => def.Has(MF.Hands) && def.Freq > 0 && !def.Has(MF.Undead));
                            if (court != null) PlaceGroup(m, court, Rng.Pick(r.Cells), true, Rng.Range(3, 6));
                            DropAt(m, Gold(d * 3), Rng.Pick(r.Cells));
                            break;
                        }
                    case "lair":
                        {
                            var boss = PickMonster(d + 2, def => def.Freq > 0 && !def.Has(MF.Immobile));
                            if (boss != null) PlaceGroup(m, boss, Rng.Pick(r.Cells), true);
                            DropAt(m, Gold(d * 2), Rng.Pick(r.Cells));
                            DropAt(m, RandomItem(d + 2), Rng.Pick(r.Cells));
                            break;
                        }
                    case "web":
                        PlaceMonster(m, MonsterDb.Get(d >= 6 ? "giant_spider" : "spider"), Rng.Pick(r.Cells), true);
                        break;
                    case "garden":
                        if (Rng.Chance(60)) PlaceMonster(m, MonsterDb.Get(Rng.Pick(new[] { "shrieker", "violet_fungus", "shrieker" })), Rng.Pick(r.Cells), false);
                        DropAt(m, Item.Make("mushroom", Rng.Range(1, 3)), Rng.Pick(r.Cells));
                        break;
                    case "statues":
                        if (Rng.Chance(50)) PlaceMonster(m, MonsterDb.Get(d >= 8 ? "basilisk" : "cockatrice"), Rng.Pick(r.Cells), true);
                        break;
                    case "vault":
                        foreach (var p in r.Cells) DropAt(m, Gold(d * 6), p);
                        DropAt(m, RandomItem(d + 3), Rng.Pick(r.Cells));
                        break;
                }
            }

            int encounters = 4 + d / 2 + Rng.Range(0, 3);
            for (int i = 0; i < encounters; i++)
            {
                var def = PickMonster(d, x => x.Freq > 0);
                if (def == null) continue;
                var p = m.RandomFloor(q => Path.Cheb(q, m.Up) > 9 && m.ShopAt(q) == null && m.MonsterAt(q) == null);
                if (def.Has(MF.Ambush) && def.Id != "green_slime")
                {
                    p = m.RandomFloor(q => !m.Lit[q.X, q.Y] && Path.Cheb(q, m.Up) > 9 && m.ShopAt(q) == null);
                }
                PlaceGroup(m, def, p, Rng.Chance(65));
            }
            if (d >= 4 && Rng.Chance(35))
            {
                var r = m.Rooms.Where(x => x.Theme == null && x.Cells.Count > 10).OrderBy(_ => Rng.Int(100)).FirstOrDefault();
                if (r != null) PlaceMonster(m, MonsterDb.Get("mimic"), Rng.Pick(r.Cells), true);
            }

            int items = 8 + Rng.Range(0, 5);
            for (int i = 0; i < items; i++)
            {
                var p = m.RandomFloor(q => m.ShopAt(q) == null);
                DropAt(m, RandomItem(d), p);
            }
            int golds = Rng.Range(3, 7);
            for (int i = 0; i < golds; i++) DropAt(m, Gold(d), m.RandomFloor(q => m.ShopAt(q) == null));
            int chests = Rng.Range(0, 2) + (d % 3 == 0 ? 1 : 0);
            for (int i = 0; i < chests; i++)
            {
                var room = m.Rooms.Where(r => r.Theme == null || r.Theme == "cave").OrderBy(_ => Rng.Int(100)).FirstOrDefault();
                if (room == null) continue;
                DropAt(m, MakeChest(d), Rng.Pick(room.Cells));
            }

            int traps = 2 + d / 2 + Rng.Range(0, 2);
            for (int i = 0; i < traps; i++)
            {
                var p = m.RandomFloor(q => (m.T[q.X, q.Y] == Tile.Floor || m.T[q.X, q.Y] == Tile.Corridor) && Path.Cheb(q, m.Up) > 4 && m.ShopAt(q) == null && !m.Traps.ContainsKey(q));
                m.Traps[p] = new Trap { Pos = p, T = RandomTrap(d) };
            }

            if (d > 1 && d < Rules.MaxDepth && Rng.Chance(30))
            {
                var p = m.RandomFloor(q => Path.Cheb(q, m.Up) > 10);
                var corpse = new Item(ItemDb.Get("corpse")) { CorpseOf = MonsterDb.Get("bandit"), Age = -99999 };
                DropAt(m, corpse, p);
                DropAt(m, RandomItem(d + 1), p);
                DropAt(m, Gold(d * 2), p);
                m.Engravings[p] = "Here lies an adventurer. " + Lore.Epitaph();
            }
        }

        static TrapT RandomTrap(int d)
        {
            var pool = new List<TrapT> { TrapT.Pit, TrapT.Arrow, TrapT.Dart, TrapT.Alarm, TrapT.BearTrap };
            if (d >= 2) pool.AddRange(new[] { TrapT.SleepGas, TrapT.Teleport, TrapT.Trapdoor, TrapT.SpikedPit, TrapT.Rust });
            if (d >= 4) pool.AddRange(new[] { TrapT.Fire, TrapT.Block, TrapT.WebTrap, TrapT.Summon });
            if (d >= 8) pool.AddRange(new[] { TrapT.Rune, TrapT.Fire, TrapT.Summon });
            if (d >= Rules.MaxDepth - 1) pool.Remove(TrapT.Trapdoor);
            return Rng.Pick(pool);
        }

        public static MonsterDef PickMonster(int depth, Func<MonsterDef, bool> filter)
        {
            int maxDepth = depth <= 2 ? depth : depth + 1;
            var cands = MonsterDb.All.Values.Where(x => x.Depth <= maxDepth && x.Depth < 99 && !x.Has(MF.Unique) && filter(x)).ToList();
            if (cands.Count == 0) return null;
            return Rng.Weighted(cands, x =>
            {
                int w = x.Freq * 10;
                if (x.Depth > depth) w /= 6;
                int gap = depth - x.Depth;
                if (gap > 6) w /= 4;
                else if (gap > 3) w /= 2;
                else w *= 2;
                return Math.Max(1, w);
            });
        }

        public static Monster PlaceMonster(Map m, MonsterDef def, Point near, bool asleep)
        {
            var p = m.NearestFree(near, 5);
            if (p == null) return null;
            var t = m.T[p.Value.X, p.Value.Y];
            var mon = new Monster(def) { Pos = p.Value, Home = p.Value };
            if (!m.PassableFor(mon, p.Value.X, p.Value.Y) && !def.Has(MF.Immobile)) return null;
            mon.Sleeping = asleep && !def.Has(MF.Immobile);
            if (def.Has(MF.Morph)) mon.MorphTimer = Rng.Range(5, 15);
            if (def.Has(MF.Hands) && Rng.Chance(25)) mon.Inv.Add(RandomItem(m.Depth));
            m.Monsters.Add(mon);
            return mon;
        }

        public static void PlaceGroup(Map m, MonsterDef def, Point at, bool asleep, int forceCount = 0)
        {
            int n = forceCount > 0 ? forceCount : Rng.Range(def.GroupMin, def.GroupMax);
            if (forceCount == 0) n = Math.Min(n, m.Depth + 1);
            for (int i = 0; i < n; i++)
            {
                var mon = PlaceMonster(m, def, at, asleep);
                if (i == 0 && mon != null && n > 2) mon.Leader = true;
            }
            if (def.Companion != null && Rng.Chance(60))
            {
                var c = MonsterDb.Get(def.Companion);
                if (c != null) PlaceGroup(m, c, at, asleep, Rng.Range(1, 3));
            }
            if (def.Id == "goblin" && m.Depth >= 4 && Rng.Chance(30)) PlaceGroup(m, MonsterDb.Get("worg"), at, asleep, Rng.Range(1, 2));
        }

        public static void DropAt(Map m, Item it, Point p)
        {
            if (it == null) return;
            m.Drop(it, p);
        }

        public static Item Gold(int depth)
        {
            int amt = Rng.Range(5, 20) + Rng.Roll(Math.Max(1, depth), 12);
            return new Item(ItemDb.Get("gold"), amt);
        }

        public static Item MakeChest(int depth)
        {
            var c = Item.Make("chest");
            c.Contents = new List<Item>();
            int n = Rng.Range(1, 4);
            for (int i = 0; i < n; i++) c.Contents.Add(RandomItem(depth + 1));
            if (Rng.Chance(70)) c.Contents.Add(Gold(depth * 2));
            c.Locked = Rng.Chance(35);
            c.Trapped = Rng.Chance(15 + depth * 2);
            return c;
        }

        static readonly (IK kind, int w)[] KindWeights =
        {
            (IK.Potion, 18), (IK.Scroll, 18), (IK.Weapon, 9), (IK.Armor, 9), (IK.Food, 14), (IK.Light, 6), (IK.Tool, 6),
            (IK.Wand, 5), (IK.Ring, 3), (IK.Amulet, 2), (IK.Gem, 4), (IK.Book, 2), (IK.Ioun, 1), (IK.Ammo, 4),
        };

        static readonly IK[] ArmorKinds = { IK.Armor, IK.Shield, IK.Helm, IK.Cloak, IK.Boots, IK.Gloves, IK.Bracers, IK.Girdle };

        public static Item RandomItem(int depth, IK? kind = null)
        {
            IK k = kind ?? Rng.Weighted(KindWeights, x => x.w).kind;
            List<ItemDef> pool;
            if (k == IK.Armor) pool = ItemDb.All.Values.Where(d => ArmorKinds.Contains(d.Kind) && d.Freq > 0 && d.Depth <= depth).ToList();
            else if (k == IK.Scroll) pool = ItemDb.All.Values.Where(d => d.Kind == IK.Scroll && d.Freq > 0 && d.Depth <= depth).ToList();
            else pool = ItemDb.All.Values.Where(d => d.Kind == k && d.Freq > 0 && d.Depth <= depth && !d.Artifact).ToList();
            if (k == IK.Weapon && Rng.Chance(Math.Min(6, depth / 2)))
            {
                var arts = ItemDb.All.Values.Where(d => d.Artifact && d.Kind == IK.Weapon && d.Depth <= depth && !ArtifactsMade.Contains(d.Id)).ToList();
                if (arts.Count > 0)
                {
                    var a = Rng.Pick(arts);
                    ArtifactsMade.Add(a.Id);
                    var ai = new Item(a);
                    ai.Cursed = a.Power < 0;
                    return ai;
                }
            }
            if (k == IK.Book)
            {
                var b = Item.Make("spellbook");
                b.BookSpells = new List<SpellId>();
                int maxL = Math.Clamp((depth + 2) / 2, 1, 7);
                int n = Rng.Range(1, 3);
                var cands = SpellDb.All.Values.Where(s => !s.Cleric && !s.MonsterOnly && s.Level <= maxL).ToList();
                for (int i = 0; i < n && cands.Count > 0; i++)
                {
                    var s = Rng.Pick(cands);
                    cands.Remove(s);
                    b.BookSpells.Add(s.Id);
                }
                b.Title = ItemDb.BookTitle();
                return b;
            }
            if (pool.Count == 0) pool = ItemDb.All.Values.Where(d => d.Kind == IK.Food && d.Freq > 0).ToList();
            var def = Rng.Weighted(pool, d => d.Freq);
            var it = new Item(def);
            Enchant(it, depth);
            if (def.Stack)
            {
                if (def.Kind == IK.Ammo) it.Count = Rng.Range(8, 20);
                else if (def.Id == "dart") it.Count = Rng.Range(4, 12);
                else if (def.Id == "spikes") it.Count = Rng.Range(3, 8);
                else if (def.Kind == IK.Potion || def.Kind == IK.Scroll) it.Count = Rng.Chance(12) ? 2 : 1;
                else if (def.Id == "torch") it.Count = Rng.Range(1, 3);
                else if (def.Kind == IK.Tool) it.Count = Rng.Range(1, 3);
            }
            return it;
        }

        public static void Enchant(Item it, int depth)
        {
            var d = it.Def;
            if (d.Kind is IK.Weapon or IK.Armor or IK.Shield or IK.Helm or IK.Ammo && !d.Artifact)
            {
                int r = Rng.Int(100);
                int magicChance = 10 + depth * 2;
                if (r < magicChance)
                {
                    it.Ench = 1;
                    if (Rng.Chance(25 + depth * 2)) it.Ench++;
                    if (Rng.Chance(5 + depth)) it.Ench++;
                }
                else if (r < magicChance + 10)
                {
                    it.Ench = -Rng.Range(1, 3);
                    it.Cursed = true;
                }
            }
            if (d.Kind is IK.Cloak or IK.Boots or IK.Gloves && d.Magic && Rng.Chance(20)) it.Ench = Rng.Range(1, 2);
            if (d.Kind == IK.Ring)
            {
                if (d.Effect == "r_protection") it.Ench = Rng.Range(1, 3);
                if (d.Effect is "r_teleport" or "r_weakness") it.Cursed = true;
                else if (Rng.Chance(8)) it.Cursed = true;
            }
            if (d.Kind == IK.Amulet && d.Effect == "a_strangle") it.Cursed = true;
            if (d.Effect is "fumble" or "opposite") it.Cursed = true;
            if (d.Kind == IK.Wand && d.Effect == "w_wonder") it.Charges += 2;
        }

        static void StockShop(Map m, Shop s)
        {
            s.Keeper = Lore.ShopkeeperName();
            var keeper = new Monster(MonsterDb.Get("shopkeeper")) { Pos = s.Inside, Home = s.Inside, Proper = s.Keeper, ShopId = s.Id };
            keeper.Peaceful = true;
            s.KeeperMon = keeper;
            m.Monsters.Add(keeper);
            foreach (var p in s.Room.Cells)
            {
                if (p == s.Inside || m.T[p.X, p.Y] != Tile.Floor) continue;
                if (Rng.Chance(15)) continue;
                IK? kind = s.Kind switch
                {
                    "armory" => Rng.Chance(50) ? IK.Weapon : IK.Armor,
                    "apothecary" => Rng.Chance(85) ? IK.Potion : IK.Food,
                    "scribe" => Rng.Chance(80) ? IK.Scroll : IK.Book,
                    "curio" => Rng.Pick(new IK?[] { IK.Ring, IK.Wand, IK.Amulet, IK.Tool, IK.Gem }),
                    "deli" => Rng.Chance(85) ? IK.Food : IK.Light,
                    _ => null,
                };
                var it = RandomItem(m.Depth + 1, kind);
                if (it.Def.Kind == IK.Ammo) it.Count = Math.Max(it.Count, 10);
                it.Price = Math.Max(1, it.Value);
                m.Drop(it, p);
            }
            m.Feeling ??= "You hear the chime of a distant shop bell.";
        }
    }
}
