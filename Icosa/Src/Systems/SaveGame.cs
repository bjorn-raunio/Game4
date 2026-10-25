using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class SaveGame
    {
        const int Magic = 0x1C05A;
        const int Version = 3;

        static string FilePath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Icosa", "save.dat");
        public static bool Disabled;
        public static bool Exists => !Disabled && File.Exists(FilePath);

        public static void Delete()
        {
            if (Disabled) return;
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }

        public static bool Save()
        {
            if (Disabled || Dm.Player == null || Dm.GameOver || Dm.Map == null) return false;
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath));
                string tmp = FilePath + ".tmp";
                using (var fs = File.Create(tmp))
                using (var w = new BinaryWriter(fs))
                {
                    w.Write(Magic);
                    w.Write(Version);
                    WriteWorld(w);
                }
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Load()
        {
            try
            {
                using (var fs = File.OpenRead(FilePath))
                using (var r = new BinaryReader(fs))
                {
                    if (r.ReadInt32() != Magic || r.ReadInt32() != Version) return false;
                    ReadWorld(r);
                }
                Delete();
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void WriteWorld(BinaryWriter w)
        {
            w.Write(Dm.Turn);
            w.Write(Dm.Tick);
            w.Write(Dm.HeartTaken);
            w.Write(Dm.WanderCounter);
            w.Write(DateTime.Now.Ticks - Dm.StartTicks);
            WriteStrings(w, Dm.Achievements);
            WriteStrings(w, Spawn.ArtifactsMade.ToList());
            w.Write(Dm.Ident.Looks.Count);
            foreach (var kv in Dm.Ident.Looks) { w.Write(kv.Key); w.Write(kv.Value); WriteColor(w, Dm.Ident.LookColors.TryGetValue(kv.Key, out var c) ? c : Color.White); }
            WriteStrings(w, Dm.Ident.Known.ToList());
            var msgs = Dm.Log.Skip(Math.Max(0, Dm.Log.Count - 150)).ToList();
            w.Write(msgs.Count);
            foreach (var m in msgs) { w.Write(m.Text); w.Write(m.Count); w.Write(m.Turn); }
            w.Write(Actions.Visited.Count);
            foreach (var (d, p) in Actions.Visited) { w.Write(d); WritePoint(w, p); }
            w.Write(Dm.Levels.Count);
            foreach (var kv in Dm.Levels) { w.Write(kv.Key); WriteMap(w, kv.Value); }
            w.Write(Dm.Map.Depth);
            WritePlayer(w, Dm.Player);
            w.Write(Dm.Engulfer != null ? Dm.Map.Monsters.IndexOf(Dm.Engulfer) : -1);
            w.Write(Dm.Player.Holder is Monster hm ? Dm.Map.Monsters.IndexOf(hm) : -1);
        }

        static void ReadWorld(BinaryReader r)
        {
            Dm.Turn = r.ReadInt64();
            Dm.Tick = r.ReadInt32();
            Dm.HeartTaken = r.ReadBoolean();
            Dm.WanderCounter = r.ReadInt32();
            Dm.StartTicks = DateTime.Now.Ticks - r.ReadInt64();
            Dm.Achievements.Clear();
            Dm.Achievements.AddRange(ReadStrings(r));
            Spawn.ArtifactsMade.Clear();
            foreach (var s in ReadStrings(r)) Spawn.ArtifactsMade.Add(s);
            Dm.Ident.Shuffle();
            Dm.Ident.Looks.Clear();
            Dm.Ident.LookColors.Clear();
            int nl = r.ReadInt32();
            for (int i = 0; i < nl; i++)
            {
                string k = r.ReadString(), v = r.ReadString();
                var c = ReadColor(r);
                Dm.Ident.Looks[k] = v;
                Dm.Ident.LookColors[k] = c;
            }
            Dm.Ident.Known.Clear();
            foreach (var s in ReadStrings(r)) Dm.Ident.Known.Add(s);
            Dm.Log.Clear();
            int nm = r.ReadInt32();
            for (int i = 0; i < nm; i++) Dm.Log.Add(new LogMsg { Text = r.ReadString(), Count = r.ReadInt32(), Turn = r.ReadInt64() });
            Dm.LogMark = Dm.Log.Count;
            Actions.Visited.Clear();
            int nv = r.ReadInt32();
            for (int i = 0; i < nv; i++) { int d = r.ReadInt32(); Actions.Visited.Add((d, ReadPoint(r))); }
            Dm.Levels.Clear();
            int nlev = r.ReadInt32();
            for (int i = 0; i < nlev; i++) { int d = r.ReadInt32(); Dm.Levels[d] = ReadMap(r); }
            Dm.Map = Dm.Levels[r.ReadInt32()];
            Dm.Player = ReadPlayer(r);
            int eng = r.ReadInt32();
            Dm.Engulfer = eng >= 0 && eng < Dm.Map.Monsters.Count ? Dm.Map.Monsters[eng] : null;
            int hold = r.ReadInt32();
            Dm.Player.Holder = hold >= 0 && hold < Dm.Map.Monsters.Count ? Dm.Map.Monsters[hold] : null;
            Dm.GameOver = Dm.Won = Dm.Retired = false;
            Dm.KilledBy = "";
            Dm.Dice.Clear();
            Dm.KnownHostiles.Clear();
            Anim.Clear();
            Dm.UpdateVision();
            Dm.Map.ComputePlayerDist(Dm.Player);
            Synth.Ambience(Dm.Map.Depth);
        }

        static void WriteStrings(BinaryWriter w, List<string> l)
        {
            w.Write(l.Count);
            foreach (var s in l) w.Write(s ?? "");
        }

        static List<string> ReadStrings(BinaryReader r)
        {
            int n = r.ReadInt32();
            var l = new List<string>(n);
            for (int i = 0; i < n; i++) l.Add(r.ReadString());
            return l;
        }

        static void WritePoint(BinaryWriter w, Point p) { w.Write(p.X); w.Write(p.Y); }
        static Point ReadPoint(BinaryReader r) => new Point(r.ReadInt32(), r.ReadInt32());
        static void WriteColor(BinaryWriter w, Color c) => w.Write(c.PackedValue);
        static Color ReadColor(BinaryReader r) => new Color { PackedValue = r.ReadUInt32() };

        static void WriteNullableString(BinaryWriter w, string s)
        {
            w.Write(s != null);
            if (s != null) w.Write(s);
        }

        static string ReadNullableString(BinaryReader r) => r.ReadBoolean() ? r.ReadString() : null;

        static void WriteStatuses(BinaryWriter w, Actor a)
        {
            w.Write(a.St.Count);
            foreach (var kv in a.St) { w.Write((int)kv.Key); w.Write(kv.Value); w.Write(a.Pow(kv.Key)); }
        }

        static void ReadStatuses(BinaryReader r, Actor a)
        {
            a.St.Clear();
            a.StPow.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var s = (Status)r.ReadInt32();
                int t = r.ReadInt32(), p = r.ReadInt32();
                a.St[s] = t;
                if (p != 0) a.StPow[s] = p;
            }
        }

        static void WriteItem(BinaryWriter w, Item it)
        {
            w.Write(it.Def.Id);
            w.Write(it.Count);
            w.Write(it.Ench);
            w.Write(it.Cursed);
            w.Write(it.Known);
            w.Write(it.Charges);
            w.Write(it.ChargesKnown);
            w.Write(it.Fuel);
            w.Write(it.Lit);
            w.Write(it.Letter);
            WritePoint(w, it.Pos);
            w.Write(it.Locked);
            w.Write(it.Trapped);
            w.Write(it.TrapKnown);
            w.Write(it.Opened);
            w.Write(it.Price);
            WriteNullableString(w, it.CorpseOf?.Id);
            w.Write(it.Age);
            w.Write(it.Erosion);
            WriteNullableString(w, it.Title);
            w.Write(it.Tried);
            w.Write(it.MagicSensed);
            w.Write(it.OrbitPhase);
            w.Write(it.BookSpells?.Count ?? -1);
            if (it.BookSpells != null) foreach (var s in it.BookSpells) w.Write((int)s);
            w.Write(it.Contents?.Count ?? -1);
            if (it.Contents != null) foreach (var c in it.Contents) WriteItem(w, c);
        }

        static Item ReadItem(BinaryReader r)
        {
            var def = ItemDb.Get(r.ReadString()) ?? ItemDb.Get("glass");
            var it = new Item(def)
            {
                Count = r.ReadInt32(),
                Ench = r.ReadInt32(),
                Cursed = r.ReadBoolean(),
                Known = r.ReadBoolean(),
                Charges = r.ReadInt32(),
                ChargesKnown = r.ReadBoolean(),
                Fuel = r.ReadInt32(),
                Lit = r.ReadBoolean(),
                Letter = r.ReadChar(),
                Pos = ReadPoint(r),
                Locked = r.ReadBoolean(),
                Trapped = r.ReadBoolean(),
                TrapKnown = r.ReadBoolean(),
                Opened = r.ReadBoolean(),
                Price = r.ReadInt32(),
            };
            var corpse = ReadNullableString(r);
            if (corpse != null) it.CorpseOf = MonsterDb.Get(corpse);
            it.Age = r.ReadInt32();
            it.Erosion = r.ReadInt32();
            it.Title = ReadNullableString(r);
            it.Tried = r.ReadBoolean();
            it.MagicSensed = r.ReadBoolean();
            it.OrbitPhase = r.ReadInt32();
            int nb = r.ReadInt32();
            if (nb >= 0)
            {
                it.BookSpells = new List<SpellId>();
                for (int i = 0; i < nb; i++) it.BookSpells.Add((SpellId)r.ReadInt32());
            }
            int nc = r.ReadInt32();
            if (nc >= 0)
            {
                it.Contents = new List<Item>();
                for (int i = 0; i < nc; i++) it.Contents.Add(ReadItem(r));
            }
            return it;
        }

        static void WriteItems(BinaryWriter w, List<Item> items)
        {
            w.Write(items.Count);
            foreach (var it in items) WriteItem(w, it);
        }

        static List<Item> ReadItems(BinaryReader r)
        {
            int n = r.ReadInt32();
            var l = new List<Item>(n);
            for (int i = 0; i < n; i++) l.Add(ReadItem(r));
            return l;
        }

        static void WriteMonster(BinaryWriter w, Monster m)
        {
            w.Write(m.Def.Id);
            w.Write(m.TrueDef.Id);
            WriteNullableString(w, m.Proper);
            WritePoint(w, m.Pos);
            w.Write(m.Hp);
            w.Write(m.MaxHp);
            w.Write(m.Energy);
            WriteStatuses(w, m);
            w.Write(m.Sleeping);
            w.Write(m.Aware);
            w.Write(m.LastSeen.HasValue);
            if (m.LastSeen.HasValue) WritePoint(w, m.LastSeen.Value);
            w.Write(m.Ally);
            w.Write(m.Peaceful);
            w.Write(m.Disguised);
            w.Write(m.CastCd);
            w.Write(m.BreathCd);
            w.Write(m.RangedCd);
            WriteItems(w, m.Inv);
            w.Write(m.SummonTurns);
            w.Write(m.Fleeing);
            w.Write(m.FleeTurns);
            w.Write(m.MoraleChecked);
            WritePoint(w, m.Home);
            w.Write(m.ShopId);
            w.Write(m.Angry);
            w.Write(m.MorphTimer);
            w.Write(m.DmgPenalty);
            w.Write(m.Generation);
            w.Write(m.Seen);
            w.Write(m.Feebleminded);
            w.Write(m.Leader);
        }

        static Monster ReadMonster(BinaryReader r)
        {
            var def = MonsterDb.Get(r.ReadString()) ?? MonsterDb.Get("rat");
            var tdef = MonsterDb.Get(r.ReadString()) ?? def;
            var m = new Monster(def) { TrueDef = tdef };
            m.Proper = ReadNullableString(r);
            m.Pos = ReadPoint(r);
            m.Hp = r.ReadInt32();
            m.MaxHp = r.ReadInt32();
            m.Energy = r.ReadInt32();
            ReadStatuses(r, m);
            m.Sleeping = r.ReadBoolean();
            m.Aware = r.ReadInt32();
            if (r.ReadBoolean()) m.LastSeen = ReadPoint(r);
            m.Ally = r.ReadBoolean();
            m.Peaceful = r.ReadBoolean();
            m.Disguised = r.ReadBoolean();
            m.CastCd = r.ReadInt32();
            m.BreathCd = r.ReadInt32();
            m.RangedCd = r.ReadInt32();
            m.Inv = ReadItems(r);
            m.SummonTurns = r.ReadInt32();
            m.Fleeing = r.ReadBoolean();
            m.FleeTurns = r.ReadInt32();
            m.MoraleChecked = r.ReadBoolean();
            m.Home = ReadPoint(r);
            m.ShopId = r.ReadInt32();
            m.Angry = r.ReadBoolean();
            m.MorphTimer = r.ReadInt32();
            m.DmgPenalty = r.ReadInt32();
            m.Generation = r.ReadInt32();
            m.Seen = r.ReadBoolean();
            m.Feebleminded = r.ReadBoolean();
            m.Leader = r.ReadBoolean();
            return m;
        }

        static void WriteMap(BinaryWriter w, Map m)
        {
            w.Write(m.Depth);
            for (int x = 0; x < Map.W; x++)
                for (int y = 0; y < Map.H; y++)
                {
                    w.Write((byte)m.T[x, y]);
                    w.Write(m.Seen[x, y]);
                    w.Write(m.Lit[x, y]);
                    w.Write(m.MemCh[x, y]);
                    WriteColor(w, m.MemFg[x, y]);
                    w.Write(m.Stain[x, y]);
                    WriteColor(w, m.StainC[x, y]);
                    w.Write(m.Dark[x, y]);
                    w.Write(m.Cloud[x, y]);
                    w.Write((byte)m.CloudE[x, y]);
                    w.Write(m.Fire[x, y]);
                }
            w.Write(m.Monsters.Count(x => !x.Dead));
            foreach (var mon in m.Monsters.Where(x => !x.Dead)) WriteMonster(w, mon);
            WriteItems(w, m.Items);
            w.Write(m.Traps.Count);
            foreach (var t in m.Traps.Values) { WritePoint(w, t.Pos); w.Write((int)t.T); w.Write(t.Found); }
            w.Write(m.Engravings.Count);
            foreach (var kv in m.Engravings) { WritePoint(w, kv.Key); w.Write(kv.Value); }
            w.Write(m.Rooms.Count);
            foreach (var room in m.Rooms)
            {
                w.Write(room.R.X); w.Write(room.R.Y); w.Write(room.R.Width); w.Write(room.R.Height);
                WriteNullableString(w, room.Theme);
                w.Write(room.Lit);
                w.Write(room.ShopId);
                w.Write(room.Cells.Count);
                foreach (var c in room.Cells) WritePoint(w, c);
            }
            w.Write(m.Lights.Count);
            foreach (var l in m.Lights) { WritePoint(w, l.P); w.Write(l.R); WriteColor(w, l.C); w.Write(l.I); w.Write(l.Flicker); }
            var alive = m.Monsters.Where(x => !x.Dead).ToList();
            w.Write(m.Shops.Count);
            foreach (var s in m.Shops)
            {
                w.Write(s.Id);
                w.Write(s.Kind);
                WriteNullableString(w, s.Keeper);
                w.Write(m.Rooms.IndexOf(s.Room));
                WritePoint(w, s.Door);
                WritePoint(w, s.Inside);
                w.Write(s.KeeperMon != null ? alive.IndexOf(s.KeeperMon) : -1);
            }
            w.Write(m.FeatureUses.Count);
            foreach (var kv in m.FeatureUses) { WritePoint(w, kv.Key); w.Write(kv.Value); }
            WritePoint(w, m.Up);
            WritePoint(w, m.Down);
            WriteColor(w, m.WallFg);
            WriteColor(w, m.WallBg);
            WriteColor(w, m.FloorFg);
            WriteColor(w, m.FloorBg);
            w.Write(m.Ambient);
            WriteNullableString(w, m.Feeling);
            w.Write(m.Visited);
        }

        static Map ReadMap(BinaryReader r)
        {
            var m = new Map(r.ReadInt32());
            for (int x = 0; x < Map.W; x++)
                for (int y = 0; y < Map.H; y++)
                {
                    m.T[x, y] = (Tile)r.ReadByte();
                    m.Seen[x, y] = r.ReadBoolean();
                    m.Lit[x, y] = r.ReadBoolean();
                    m.MemCh[x, y] = r.ReadChar();
                    m.MemFg[x, y] = ReadColor(r);
                    m.Stain[x, y] = r.ReadSingle();
                    m.StainC[x, y] = ReadColor(r);
                    m.Dark[x, y] = r.ReadInt32();
                    m.Cloud[x, y] = r.ReadInt32();
                    m.CloudE[x, y] = (Elem)r.ReadByte();
                    m.Fire[x, y] = r.ReadInt32();
                }
            int nm = r.ReadInt32();
            for (int i = 0; i < nm; i++) m.Monsters.Add(ReadMonster(r));
            m.Items.AddRange(ReadItems(r));
            int nt = r.ReadInt32();
            for (int i = 0; i < nt; i++)
            {
                var t = new Trap { Pos = ReadPoint(r), T = (TrapT)r.ReadInt32(), Found = r.ReadBoolean() };
                m.Traps[t.Pos] = t;
            }
            int ne = r.ReadInt32();
            for (int i = 0; i < ne; i++) m.Engravings[ReadPoint(r)] = r.ReadString();
            int nr = r.ReadInt32();
            for (int i = 0; i < nr; i++)
            {
                var room = new Room { R = new Rectangle(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32()) };
                room.Theme = ReadNullableString(r);
                room.Lit = r.ReadBoolean();
                room.ShopId = r.ReadInt32();
                int nc = r.ReadInt32();
                for (int j = 0; j < nc; j++) room.Cells.Add(ReadPoint(r));
                m.Rooms.Add(room);
            }
            int nli = r.ReadInt32();
            for (int i = 0; i < nli; i++) m.Lights.Add(new StaticLight { P = ReadPoint(r), R = r.ReadInt32(), C = ReadColor(r), I = r.ReadSingle(), Flicker = r.ReadBoolean() });
            int ns = r.ReadInt32();
            for (int i = 0; i < ns; i++)
            {
                var s = new Shop { Id = r.ReadInt32(), Kind = r.ReadString(), Keeper = ReadNullableString(r) };
                int ri = r.ReadInt32();
                s.Room = ri >= 0 && ri < m.Rooms.Count ? m.Rooms[ri] : new Room();
                s.Door = ReadPoint(r);
                s.Inside = ReadPoint(r);
                int ki = r.ReadInt32();
                s.KeeperMon = ki >= 0 && ki < m.Monsters.Count ? m.Monsters[ki] : null;
                m.Shops.Add(s);
            }
            int nf = r.ReadInt32();
            for (int i = 0; i < nf; i++) m.FeatureUses[ReadPoint(r)] = r.ReadInt32();
            m.Up = ReadPoint(r);
            m.Down = ReadPoint(r);
            m.WallFg = ReadColor(r);
            m.WallBg = ReadColor(r);
            m.FloorFg = ReadColor(r);
            m.FloorBg = ReadColor(r);
            m.Ambient = r.ReadSingle();
            m.Feeling = ReadNullableString(r);
            m.Visited = r.ReadBoolean();
            return m;
        }

        static void WritePlayer(BinaryWriter w, Player p)
        {
            w.Write(p.PName);
            w.Write((int)p.Race);
            w.Write((int)p.Class);
            w.Write((int)p.Align);
            for (int i = 0; i < 6; i++) { w.Write(p.Base[i]); w.Write(p.Drained[i]); }
            w.Write(p.StrEx);
            w.Write(p.Level);
            w.Write(p.Xp);
            w.Write(p.Gold);
            w.Write(p.MaxLevelReached);
            WritePoint(w, p.Pos);
            w.Write(p.Hp);
            w.Write(p.MaxHp);
            w.Write(p.Energy);
            WriteStatuses(w, p);
            WriteItems(w, p.Pack);
            w.Write(p.Eq.Count);
            foreach (var kv in p.Eq) { w.Write((int)kv.Key); w.Write(p.Pack.IndexOf(kv.Value)); }
            WriteItems(w, p.Iouns);
            w.Write(p.Nutrition);
            foreach (var list in new[] { p.Known, p.Plan, p.Memorized })
            {
                w.Write(list.Count);
                foreach (var s in list) w.Write((int)s);
            }
            w.Write(p.RestTurns);
            w.Write(p.PrayerTimeout);
            w.Write(p.LayOnHandsReady);
            w.Write(p.TurnUndeadReady);
            w.Write(p.Kills);
            w.Write(p.KillLog.Count);
            foreach (var kv in p.KillLog) { w.Write(kv.Key); w.Write(kv.Value); }
            w.Write(p.AttackFrac);
            w.Write(p.ErrataMap != null);
            if (p.ErrataMap != null) foreach (var v in p.ErrataMap) w.Write(v);
            w.Write(p.HeartSaves);
            w.Write(p.SecondRollUsed);
            w.Write(p.DeepestDepth);
            w.Write(p.GodAnger);
            w.Write(p.TurnsInDark);
        }

        static Player ReadPlayer(BinaryReader r)
        {
            var p = new Player { PName = r.ReadString(), Race = (Race)r.ReadInt32(), Class = (Cls)r.ReadInt32(), Align = (Align)r.ReadInt32() };
            for (int i = 0; i < 6; i++) { p.Base[i] = r.ReadInt32(); p.Drained[i] = r.ReadInt32(); }
            p.StrEx = r.ReadInt32();
            p.Level = r.ReadInt32();
            p.Xp = r.ReadInt32();
            p.Gold = r.ReadInt32();
            p.MaxLevelReached = r.ReadInt32();
            p.Pos = ReadPoint(r);
            p.Hp = r.ReadInt32();
            p.MaxHp = r.ReadInt32();
            p.Energy = r.ReadInt32();
            ReadStatuses(r, p);
            p.Pack.AddRange(ReadItems(r));
            int ne = r.ReadInt32();
            for (int i = 0; i < ne; i++)
            {
                var slot = (Slot)r.ReadInt32();
                int idx = r.ReadInt32();
                if (idx >= 0 && idx < p.Pack.Count) p.Eq[slot] = p.Pack[idx];
            }
            p.Iouns.AddRange(ReadItems(r));
            p.Nutrition = r.ReadInt32();
            foreach (var list in new[] { p.Known, p.Plan, p.Memorized })
            {
                int n = r.ReadInt32();
                for (int i = 0; i < n; i++) list.Add((SpellId)r.ReadInt32());
            }
            p.RestTurns = r.ReadInt32();
            p.PrayerTimeout = r.ReadInt32();
            p.LayOnHandsReady = r.ReadInt64();
            p.TurnUndeadReady = r.ReadInt64();
            p.Kills = r.ReadInt32();
            int nk = r.ReadInt32();
            for (int i = 0; i < nk; i++) p.KillLog[r.ReadString()] = r.ReadInt32();
            p.AttackFrac = r.ReadInt32();
            if (r.ReadBoolean())
            {
                p.ErrataMap = new int[6];
                for (int i = 0; i < 6; i++) p.ErrataMap[i] = r.ReadInt32();
            }
            p.HeartSaves = r.ReadInt32();
            p.SecondRollUsed = r.ReadBoolean();
            p.DeepestDepth = r.ReadInt32();
            p.GodAnger = r.ReadInt32();
            p.TurnsInDark = r.ReadInt32();
            return p;
        }
    }
}
