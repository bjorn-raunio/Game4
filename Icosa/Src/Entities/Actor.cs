using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public abstract class Actor
    {
        public Point Pos;
        public int Hp, MaxHp;
        public int Energy;
        public bool Dead;
        public readonly Dictionary<Status, int> St = new Dictionary<Status, int>();
        public readonly Dictionary<Status, int> StPow = new Dictionary<Status, int>();
        public Actor Holder;

        public abstract string Name { get; }
        public abstract char Glyph { get; }
        public abstract Color Color { get; }
        public abstract int AC { get; }
        public abstract int BaseSpeed { get; }
        public abstract Size Size { get; }
        public abstract string The { get; }
        public abstract string A { get; }
        public abstract bool Resists(Elem e);
        public abstract int SaveTarget(Save s);
        public abstract bool IsUndead { get; }
        public abstract bool IsEvil { get; }

        public bool IsPlayer => this is Player;
        public string TheCap => char.ToUpper(The[0]) + The.Substring(1);

        public int Speed
        {
            get
            {
                int s = BaseSpeed;
                if (Has(Status.Hasted)) s *= 2;
                if (Has(Status.Slowed)) s = s / 2;
                return Math.Max(1, s);
            }
        }

        public bool Has(Status s) => St.TryGetValue(s, out var t) && t > 0;
        public int Turns(Status s) => St.TryGetValue(s, out var t) ? t : 0;
        public int Pow(Status s) => StPow.TryGetValue(s, out var p) ? p : 0;

        public void Add(Status s, int turns, int pow = 0)
        {
            if (turns <= 0) return;
            St[s] = Math.Max(Turns(s), turns);
            if (pow != 0) StPow[s] = Math.Max(Pow(s), pow);
        }

        public void Set(Status s, int turns, int pow)
        {
            St[s] = turns;
            StPow[s] = pow;
        }

        public void Remove(Status s)
        {
            St.Remove(s);
            StPow.Remove(s);
        }

        public bool Helpless => Has(Status.Asleep) || Has(Status.Paralyzed) || Has(Status.Stunned);
        public bool CanAct => !Helpless && !Dead;
        public float HpFrac => MaxHp <= 0 ? 0 : Math.Clamp(Hp / (float)MaxHp, 0, 1);

        public int Dist(Point p) => Math.Max(Math.Abs(p.X - Pos.X), Math.Abs(p.Y - Pos.Y));
        public int Dist(Actor a) => Dist(a.Pos);
    }

    public class Monster : Actor
    {
        public MonsterDef Def;
        public MonsterDef TrueDef;
        public string Proper;
        public bool Sleeping;
        public int Aware;
        public Point? LastSeen;
        public bool Ally;
        public bool Peaceful;
        public bool Disguised;
        public int CastCd, BreathCd, RangedCd;
        public List<Item> Inv = new List<Item>();
        public int SummonTurns;
        public bool Fleeing;
        public int FleeTurns;
        public bool MoraleChecked;
        public Point Home;
        public Point? WanderGoal;
        public int ShopId = -1;
        public bool Angry;
        public int MorphTimer;
        public int DmgPenalty;
        public bool Surprised;
        public int Generation;
        public bool Seen;
        public bool Feebleminded;
        public bool Engulfing;
        public bool Leader;
        public Queue<Point> Route;

        public Monster(MonsterDef d)
        {
            Def = d;
            TrueDef = d;
            if (d.HalfHD) MaxHp = Rng.D(4);
            else MaxHp = Math.Max(1, Rng.Roll(Math.Max(1, d.HD), 8) + d.HpPlus);
            if (d.Has(MF.Boss)) MaxHp = Math.Max(MaxHp, d.HD * 6);
            Hp = MaxHp;
            Peaceful = d.Has(MF.Peaceful);
            Disguised = d.Has(MF.Mimic);
            Energy = Rng.Int(120);
        }

        public override string Name => Proper ?? Def.Name;
        public override char Glyph => Disguised ? '■' : Def.G;
        public override Color Color => Disguised ? ItemDb.Get("chest").C : Def.C;
        public override Size Size => Def.Size;
        public override bool IsUndead => Def.Has(MF.Undead);
        public override bool IsEvil => Def.Has(MF.Evil) && !Ally;

        public override int AC
        {
            get
            {
                int ac = Def.AC;
                if (Has(Status.Shielded)) ac -= 3;
                if (Has(Status.Blessed)) ac -= 0;
                if (Has(Status.Webbed)) ac += 2;
                if (Helpless) ac += 4;
                return ac;
            }
        }

        public override int BaseSpeed => Def.Move;

        public override string The
        {
            get
            {
                if (Proper != null) return Proper;
                if (Def.Has(MF.Unique) && Def.Name.StartsWith("the ")) return Def.Name;
                if (!Dm.CanSeeMonster(this)) return "something";
                return (Ally ? "your " : "the ") + Def.Name;
            }
        }

        public override string A
        {
            get
            {
                if (Proper != null) return Proper;
                if (Def.Has(MF.Unique) && Def.Name.StartsWith("the ")) return Def.Name;
                return Item.Article(Def.Name);
            }
        }

        public override bool Resists(Elem e) => e switch
        {
            Elem.Fire => Def.Has(MF.ResFire),
            Elem.Cold => Def.Has(MF.ResCold),
            Elem.Elec => Def.Has(MF.ResElec),
            Elem.Poison => Def.Has(MF.ResPoison),
            Elem.Acid => Def.Has(MF.ResAcid),
            _ => false,
        };

        public override int SaveTarget(Save s) => Rules.MonsterSave(Def, s);

        public int HdLevel => Def.HalfHD ? 0 : Def.HD + (Def.HpPlus > 0 ? 1 : 0);
        public bool Hostile => !Ally && !Peaceful;
        public bool IsHumanoid => Def.Has(MF.Hands) && !Def.Has(MF.Undead) && Def.Size != Size.Large && !Def.Has(MF.Mindless) || Def.G == '@' || Def.Id is "goblin" or "kobold" or "orc" or "hobgoblin" or "gnoll" or "bugbear" or "lizardman" or "duergar" or "drow" or "orc_archer";
    }

    public class Player : Actor
    {
        public string PName = "Adventurer";
        public Race Race;
        public Cls Class;
        public Align Align;
        public int[] Base = new int[6];
        public int StrEx;
        public int[] Drained = new int[6];
        public int Level = 1, Xp, Gold;
        public int MaxLevelReached = 1;
        public readonly List<Item> Pack = new List<Item>();
        public readonly Dictionary<Slot, Item> Eq = new Dictionary<Slot, Item>();
        public readonly List<Item> Iouns = new List<Item>();
        public int Nutrition = 1800;
        public readonly List<SpellId> Known = new List<SpellId>();
        public readonly List<SpellId> Plan = new List<SpellId>();
        public readonly List<SpellId> Memorized = new List<SpellId>();
        public int RestTurns;
        public int PrayerTimeout = 300;
        public long LayOnHandsReady;
        public long TurnUndeadReady;
        public int Kills;
        public readonly Dictionary<string, int> KillLog = new Dictionary<string, int>();
        public int AttackFrac;
        public int Luck;
        public int[] ErrataMap;
        public int LastHpGain;
        public bool LightOut;
        public int TurnsInDark;
        public int HeartSaves;
        public bool SecondRollUsed;
        public int DeepestDepth = 1;
        public int ScarabCharges = -1;
        public int WishCharges = -1;
        public int StrangleTurns;
        public readonly List<string> Conducts = new List<string>();
        public int GodAnger;
        public bool Searching;

        public RaceInfo RaceInfo => Rules.Races[Race];
        public ClassInfo ClassInfo => Rules.Classes[Class];

        public override string Name => PName;
        public override char Glyph => '@';
        public override Color Color => Has(Status.Invisible) ? new Color(150, 150, 200) : Pal.White;
        public override Size Size => Race is Race.Halfling or Race.Gnome or Race.Dwarf ? Size.Small : Size.Medium;
        public override string The => "you";
        public override string A => "you";
        public override bool IsUndead => false;
        public override bool IsEvil => Align is Align.LE or Align.NE or Align.CE;

        public Item Weapon => Eq.TryGetValue(Slot.Weapon, out var w) ? w : null;
        public Item Worn(Slot s) => Eq.TryGetValue(s, out var w) ? w : null;
        public bool Wearing(string effect) => Eq.Values.Any(i => i.Def.Effect == effect);
        public bool WearingId(string id) => Eq.Values.Any(i => i.Def.Id == id);
        public bool HasIoun(string power) => Iouns.Any(i => i.Def.Effect == power);
        public bool Carrying(string id) => Pack.Any(i => i.Def.Id == id);
        public Item Find(string id) => Pack.FirstOrDefault(i => i.Def.Id == id);
        public bool IsWarrior => ClassInfo.Warrior;

        public int RawStat(Stat s)
        {
            int i = (int)s;
            return Math.Clamp(Base[i] + RaceInfo.Adj[i] - Drained[i], 3, 25);
        }

        public int Stat(Stat s)
        {
            if (ErrataMap != null && Has(Status.Errata)) s = (Stat)ErrataMap[(int)s];
            int v = RawStat(s);
            switch (s)
            {
                case Icosa.Stat.Str:
                    if (Wearing("ogre")) v = Math.Max(v, 18);
                    if (Wearing("str19")) v = Math.Max(v, 19);
                    if (Wearing("str22")) v = Math.Max(v, 22);
                    if (Has(Status.GiantStr)) v = Math.Max(v, Pow(Status.GiantStr));
                    if (Wearing("r_weakness")) v -= 3;
                    if (Has(Status.Weakened)) v -= Pow(Status.Weakened);
                    if (HasIoun("str")) v++;
                    break;
                case Icosa.Stat.Int: if (HasIoun("int")) v++; break;
                case Icosa.Stat.Wis: if (HasIoun("wis")) v++; break;
                case Icosa.Stat.Dex:
                    if (Wearing("dex")) v += 2;
                    if (Wearing("fumble")) v -= 4;
                    if (HasIoun("dex")) v++;
                    break;
                case Icosa.Stat.Con: if (HasIoun("con")) v++; break;
                case Icosa.Stat.Cha: if (HasIoun("cha")) v++; break;
            }
            return Math.Clamp(v, 3, 25);
        }

        public int StrExEff
        {
            get
            {
                if (Wearing("ogre") && RawStat(Icosa.Stat.Str) <= 18 && Stat(Icosa.Stat.Str) == 18) return 100;
                if (Stat(Icosa.Stat.Str) == 18 && RawStat(Icosa.Stat.Str) == 18 && IsWarrior) return StrEx;
                return 0;
            }
        }

        public string StatText(Stat s) => s == Icosa.Stat.Str ? Rules.StrText(Stat(s), StrExEff) : Stat(s).ToString();

        public (int hit, int dmg) StrBonus => Rules.StrBonus(Stat(Icosa.Stat.Str), StrExEff);

        public int Thac0 => Rules.Thac0(Class, Level);

        public override int AC
        {
            get
            {
                int ac = 10;
                var body = Worn(Slot.Body);
                if (body != null) ac = body.Def.Ac - body.Ench + body.Erosion;
                var br = Worn(Slot.Bracers);
                if (br != null && (body == null || body.Def.Id is "robe")) ac = Math.Min(ac, br.Def.Ac - br.Ench);
                if (body != null && body.Def.Id == "robe_archmagi" && Class != Cls.MagicUser) ac = 10;
                var sh = Worn(Slot.Shield);
                if (sh != null) ac -= sh.Def.Ac + sh.Ench - sh.Erosion;
                foreach (var s in new[] { Slot.Helm, Slot.Cloak, Slot.Boots, Slot.Gloves })
                {
                    var it = Worn(s);
                    if (it != null) ac -= it.Ench + (it.Def.Effect == "protection" ? 1 : 0);
                }
                foreach (var s in new[] { Slot.RingL, Slot.RingR })
                {
                    var r = Worn(s);
                    if (r != null && r.Def.Effect == "r_protection") ac -= r.Ench;
                }
                ac += Rules.DexAc(Stat(Icosa.Stat.Dex));
                if (Has(Status.Shielded)) ac -= 3;
                if (HasIoun("ac")) ac -= 1;
                if (Has(Status.Webbed)) ac += 2;
                if (Helpless) ac += 4;
                return ac;
            }
        }

        public int Capacity => Rules.Capacity(Stat(Icosa.Stat.Str), StrExEff);

        public int Load
        {
            get
            {
                int w = 0;
                foreach (var i in Pack) w += i.Weight;
                if (Carrying("bag_holding")) w = w * 2 / 3;
                return w;
            }
        }

        public int Burden
        {
            get
            {
                int cap = Capacity, l = Load;
                if (l <= cap) return 0;
                if (l <= cap * 3 / 2) return 1;
                if (l <= cap * 2) return 2;
                return 3;
            }
        }

        public override int BaseSpeed
        {
            get
            {
                int s = 12;
                if (Wearing("speed")) s = 18;
                int b = Burden;
                if (b == 1) s = s * 3 / 4;
                else if (b == 2) s = s / 2;
                else if (b >= 3) s = s / 4;
                return Math.Max(2, s);
            }
        }

        public override bool Resists(Elem e) => e switch
        {
            Elem.Fire => Has(Status.ResFire) || Wearing("r_fire") || Wearing("brilliance"),
            Elem.Cold => Has(Status.ResCold) || Wearing("r_warmth"),
            Elem.Poison => Wearing("a_poison") || Has(Status.ResFire) && false,
            _ => false,
        };

        public override int SaveTarget(Save s)
        {
            int t = Rules.SaveTarget(Class, Level, s);
            int prot = 0;
            foreach (var sl in new[] { Slot.RingL, Slot.RingR })
            {
                var r = Worn(sl);
                if (r != null && r.Def.Effect == "r_protection") prot += r.Ench;
            }
            var cl = Worn(Slot.Cloak);
            if (cl != null && cl.Def.Effect == "protection") prot += 1 + cl.Ench;
            var body = Worn(Slot.Body);
            if (body != null && body.Def.Id == "robe_archmagi") prot += 1;
            t -= prot;
            if (Race is Race.Dwarf or Race.Halfling or Race.Gnome)
            {
                int bonus = (int)(Stat(Icosa.Stat.Con) / 3.5);
                if (s == Save.Spell || s == Save.Wand) t -= bonus;
                if (s == Save.Death && Race != Race.Gnome) t -= bonus;
            }
            if (Has(Status.Prayer)) t -= 1;
            if (Has(Status.Blessed)) t -= 1;
            return t;
        }

        public bool Infravision => RaceInfo.Infravision > 0;

        public int LightRadius
        {
            get
            {
                int r = 0;
                var l = Worn(Slot.Light);
                if (l != null && l.Lit && l.Fuel > 0) r = l.Def.LightRadius;
                var w = Weapon;
                if (w != null && w.Def.LightRadius > 0) r = Math.Max(r, w.Def.LightRadius);
                var h = Worn(Slot.Helm);
                if (h != null && h.Def.LightRadius > 0) r = Math.Max(r, h.Def.LightRadius);
                if (HasIoun("light")) r = Math.Max(r, 4);
                if (Has(Status.Insight)) r = Math.Max(r, Pow(Status.Insight));
                return r;
            }
        }

        public Color LightColor
        {
            get
            {
                var l = Worn(Slot.Light);
                if (l != null && l.Lit && l.Fuel > 0) return l.Def.LightColor;
                var w = Weapon;
                if (w != null && w.Def.LightRadius > 0) return w.Def.Effect == "flame" ? new Color(255, 140, 60) : new Color(255, 240, 180);
                return new Color(255, 245, 210);
            }
        }

        public string HungerText
        {
            get
            {
                if (Nutrition > 3000) return "Satiated";
                if (Nutrition > 300) return "";
                if (Nutrition > 100) return "Hungry";
                if (Nutrition > 0) return "Weak";
                return "Fainting";
            }
        }

        public int[] Slots => Rules.Slots(Class, Level, Stat(Icosa.Stat.Wis));
        public bool Caster => Class is Cls.MagicUser or Cls.Cleric || (Class == Cls.Paladin && Level >= 9);
        public bool ClericCaster => Class is Cls.Cleric or Cls.Paladin;

        public IEnumerable<SpellId> Castable
        {
            get
            {
                var slots = Slots;
                if (Class == Cls.MagicUser) return Known.Where(s => SpellDb.Get(s).Level <= slots.Length);
                if (ClericCaster)
                {
                    int max = slots.Length;
                    if (Class == Cls.Paladin) max = Math.Min(max, 2);
                    return SpellDb.All.Values.Where(s => s.Cleric && !s.MonsterOnly && s.Level <= max).Select(s => s.Id);
                }
                return Enumerable.Empty<SpellId>();
            }
        }

        public int[] ThiefSkills
        {
            get
            {
                if (Class != Cls.Thief) return new[] { 5, 5, 5, 5 };
                var b = Rules.ThiefBase(Level);
                int dex = Stat(Icosa.Stat.Dex);
                int dm = dex >= 17 ? 10 : dex >= 16 ? 5 : dex <= 9 ? -10 : 0;
                b[0] += dm; b[1] += dm / 2; b[2] += dm / 2; b[3] += dm / 2;
                if (Race == Race.Halfling) { b[2] += 10; b[3] += 15; b[0] += 5; }
                if (Race == Race.Elf) { b[2] += 5; b[3] += 10; }
                if (Race == Race.Dwarf) { b[0] += 10; b[1] += 15; }
                if (Race == Race.Gnome) { b[0] += 5; b[1] += 10; b[2] += 5; b[3] += 5; }
                var body = Worn(Slot.Body);
                if (body != null && body.Def.Id is not ("leather" or "studded" or "elven_chain" or "robe")) for (int i = 0; i < 4; i++) b[i] = Math.Max(1, b[i] / 3);
                return b.Select(v => Math.Clamp(v, 1, 99)).ToArray();
            }
        }

        public int Stealth
        {
            get
            {
                int s = Class == Cls.Thief ? ThiefSkills[2] : 10;
                if (Race == Race.Halfling) s += 20;
                if (Race == Race.Elf) s += 10;
                if (Wearing("stealth") || Wearing("r_stealth")) s += 40;
                var body = Worn(Slot.Body);
                if (body != null && body.Def.Metal && body.Def.Id != "elven_chain") s -= 15;
                if (Has(Status.Invisible)) s += 30;
                return Math.Clamp(s, 0, 95);
            }
        }

        public bool Proficient(Item w)
        {
            if (w == null) return true;
            var d = w.Def;
            switch (Class)
            {
                case Cls.MagicUser: return d.Id is "dagger" or "silver_dagger" or "dart" or "quarterstaff" or "sling";
                case Cls.Cleric: return d.Has(WF.Blunt) || d.Id is "sling" or "quarterstaff";
                case Cls.Thief: return d.Id is "dagger" or "silver_dagger" or "dart" or "club" or "sling" or "short_sword" or "long_sword" or "broad_sword" or "scimitar" or "short_bow" or "crossbow" or "hand_axe" || d.Artifact && !d.Has(WF.TwoHand);
                default: return true;
            }
        }

        public string Title => Rules.LevelTitle(Class, Level);
        public string Deity => Rules.Deities[(int)Align];
    }
}
