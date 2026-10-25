using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public enum Size { Small, Medium, Large }
    public enum Elem { Phys, Fire, Cold, Elec, Acid, Poison, Magic, Holy, Necro, Force }
    public enum Save { Death, Petrify, Wand, Breath, Spell }
    public enum Stat { Str, Int, Wis, Dex, Con, Cha }
    public enum Align { LG, NG, CG, LN, N, CN, LE, NE, CE }

    public enum AtkSp
    {
        None, Poison, Poison2, Paralyze, Drain, Disease, Rust, Acid, Fire, Cold, Shock, Petrify, Engulf, BloodDrain,
        StrDrain, StealGold, Confuse, Sleep, Stun, BrainEat, Slime, Hug, Swallow, Sticky, Smudge, Errata, Bleed
    }

    [Flags]
    public enum MF : long
    {
        None = 0,
        Undead = 1L << 0,
        Evil = 1L << 1,
        Animal = 1L << 2,
        Mindless = 1L << 3,
        Regen = 1L << 4,
        Fly = 1L << 5,
        Swim = 1L << 6,
        Invis = 1L << 7,
        Warm = 1L << 8,
        Giant = 1L << 9,
        Orc = 1L << 10,
        Glow = 1L << 11,
        Mimic = 1L << 12,
        Splits = 1L << 13,
        Phase = 1L << 14,
        ResFire = 1L << 15,
        ResCold = 1L << 16,
        ResPoison = 1L << 17,
        ResElec = 1L << 18,
        ResSleep = 1L << 19,
        NeedMagic = 1L << 20,
        NeedSilver = 1L << 21,
        Unique = 1L << 22,
        Coward = 1L << 23,
        Immobile = 1L << 24,
        Burrow = 1L << 25,
        Erratic = 1L << 26,
        Webber = 1L << 27,
        Ambush = 1L << 28,
        Revives = 1L << 29,
        Displace = 1L << 30,
        Shrieker = 1L << 31,
        Hands = 1L << 32,
        Infra = 1L << 33,
        Edged = 1L << 34,
        VulnFire = 1L << 35,
        NoCorpse = 1L << 36,
        Peaceful = 1L << 37,
        Shopkeeper = 1L << 38,
        Transparent = 1L << 39,
        FearAura = 1L << 40,
        Morph = 1L << 41,
        Boss = 1L << 42,
        Heat = 1L << 43,
        ResAcid = 1L << 44,
        Shadowy = 1L << 45,
        Pack = 1L << 46,
        Ally = 1L << 47,
        EatsMetal = 1L << 48,
    }

    public enum Status
    {
        Asleep, Paralyzed, Confused, Blind, Hasted, Slowed, Invisible, Poisoned, Blessed, Shielded, Feared, Webbed,
        GiantStr, Heroism, ResFire, ESP, MirrorImage, Held, Stoning, Diseased, ProtEvil, Charmed, Turned, Silenced,
        Stunned, Sliming, Darkness, Engulfed, Sanctuary, Prayer, Bleeding, Levitate, ResCold, SeeInvis, Weakened,
        Swallowed, FireShield, Globe, Errata, Rested, Telepathy, Hidden, ProtMissiles, Regenerating, Spiritual, Insight, Fortune, BladeBarrier, ProtUndead
    }

    public enum Slot { Weapon, Shield, Body, Helm, Cloak, Boots, Gloves, Bracers, Girdle, RingL, RingR, Amulet, Light, Quiver }

    public enum IK { Weapon, Ammo, Armor, Shield, Helm, Cloak, Boots, Gloves, Bracers, Girdle, Ring, Amulet, Potion, Scroll, Wand, Book, Food, Light, Tool, Gold, Gem, Misc, Corpse, Chest, Heart, Ioun }

    public enum Target { Self, Single, Bolt, Ball, Cone, Dir, Area, Touch, Location, Item }

    public class Attack
    {
        public Dice Dmg;
        public string Verb;
        public AtkSp Sp;
        public int SpChance = 100;

        public static List<Attack> ParseList(string s)
        {
            var list = new List<Attack>();
            if (string.IsNullOrWhiteSpace(s)) return list;
            foreach (var part in s.Split(';'))
            {
                var bits = part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (bits.Length == 0) continue;
                var a = new Attack { Dmg = Dice.Parse(bits[0]), Verb = bits.Length > 1 ? bits[1] : "hit" };
                if (bits.Length > 2)
                {
                    string sp = bits[2];
                    int pct = 100;
                    int at = sp.IndexOf('@');
                    if (at >= 0)
                    {
                        pct = int.Parse(sp.Substring(at + 1));
                        sp = sp.Substring(0, at);
                    }
                    a.Sp = sp switch
                    {
                        "poison" => AtkSp.Poison,
                        "poison2" => AtkSp.Poison2,
                        "paralyze" => AtkSp.Paralyze,
                        "drain" => AtkSp.Drain,
                        "disease" => AtkSp.Disease,
                        "rust" => AtkSp.Rust,
                        "acid" => AtkSp.Acid,
                        "fire" => AtkSp.Fire,
                        "cold" => AtkSp.Cold,
                        "shock" => AtkSp.Shock,
                        "petrify" => AtkSp.Petrify,
                        "engulf" => AtkSp.Engulf,
                        "blood" => AtkSp.BloodDrain,
                        "strdrain" => AtkSp.StrDrain,
                        "steal" => AtkSp.StealGold,
                        "confuse" => AtkSp.Confuse,
                        "sleep" => AtkSp.Sleep,
                        "stun" => AtkSp.Stun,
                        "brain" => AtkSp.BrainEat,
                        "slime" => AtkSp.Slime,
                        "hug" => AtkSp.Hug,
                        "swallow" => AtkSp.Swallow,
                        "sticky" => AtkSp.Sticky,
                        "smudge" => AtkSp.Smudge,
                        "errata" => AtkSp.Errata,
                        "bleed" => AtkSp.Bleed,
                        _ => AtkSp.None,
                    };
                    a.SpChance = pct;
                }
                list.Add(a);
            }
            return list;
        }
    }

    public class RangedAtk
    {
        public Dice Dmg;
        public string Name;
        public char Glyph;
        public Color Color;
        public int Range;
        public AtkSp Sp;
        public int Count = 1;
        public int Chance = 40;
    }

    public class BreathAtk
    {
        public Elem Elem;
        public Dice Dmg;
        public int Range;
        public bool Cone;
        public int Chance = 30;
        public string Name;
    }

    public class MonsterDef
    {
        public string Id, Name, Plural, Desc;
        public char G;
        public Color C;
        public int HD, HpPlus;
        public bool HalfHD;
        public int AC, Move, Depth, Freq, Morale = 9, TurnRank;
        public Size Size = Size.Medium;
        public MF Flags;
        public List<Attack> Attacks;
        public RangedAtk Ranged;
        public BreathAtk Breath;
        public List<SpellId> Spells = new List<SpellId>();
        public int CastChance = 35;
        public int GroupMin = 1, GroupMax = 1;
        public string Companion;
        public int GlowRadius;
        public Color GlowColor;
        public int XpOverride;
        public Elem Gaze = Elem.Phys;
        public AtkSp GazeSp = AtkSp.None;
        public string LeavesItem;
        public int LeavesChance;
        public int RegenRate = 3;
        public int Infravision = 6;
        public Align Alignment = Align.N;

        public bool Has(MF f) => (Flags & f) != 0;

        public float HdValue => HalfHD ? 0.5f : HD + (HpPlus > 0 ? 0.25f : 0);

        public int Thac0
        {
            get
            {
                if (HalfHD) return 20;
                int t = 20 - HD - (HpPlus >= 3 ? 1 : 0);
                return Math.Max(4, t);
            }
        }

        public int Xp
        {
            get
            {
                if (XpOverride > 0) return XpOverride;
                float h = HalfHD ? 0.5f : HD + HpPlus / 4f;
                int specials = 0;
                foreach (var a in Attacks) if (a.Sp != AtkSp.None) specials++;
                if (Ranged != null) specials++;
                if (Breath != null) specials += 2;
                if (Spells.Count > 0) specials += 2;
                if (GazeSp != AtkSp.None) specials += 2;
                if (Has(MF.Regen)) specials++;
                if (Has(MF.NeedMagic) || Has(MF.NeedSilver)) specials++;
                return (int)Math.Round(h * h * 3 + h * 4 + 2 + specials * Math.Max(1, h) * 2);
            }
        }

        public MonsterDef Group(int min, int max) { GroupMin = min; GroupMax = max; return this; }
        public MonsterDef With(string companion) { Companion = companion; return this; }
        public MonsterDef Shoots(string dmg, string name, char g, Color c, int range, int chance = 40, AtkSp sp = AtkSp.None, int count = 1)
        {
            Ranged = new RangedAtk { Dmg = Dice.Parse(dmg), Name = name, Glyph = g, Color = c, Range = range, Chance = chance, Sp = sp, Count = count };
            return this;
        }
        public MonsterDef Breathes(Elem e, string dmg, int range, bool cone, string name, int chance = 30)
        {
            Breath = new BreathAtk { Elem = e, Dmg = Dice.Parse(dmg), Range = range, Cone = cone, Name = name, Chance = chance };
            return this;
        }
        public MonsterDef Casts(int chance, params SpellId[] spells) { Spells.AddRange(spells); CastChance = chance; return this; }
        public MonsterDef Glows(Color c, int r) { GlowColor = c; GlowRadius = r; Flags |= MF.Glow; return this; }
        public MonsterDef Turn(int rank) { TurnRank = rank; return this; }
        public MonsterDef Mor(int m) { Morale = m; return this; }
        public MonsterDef Sz(Size s) { Size = s; return this; }
        public MonsterDef GazeAt(AtkSp sp) { GazeSp = sp; return this; }
        public MonsterDef Drops(string item, int chance) { LeavesItem = item; LeavesChance = chance; return this; }
        public MonsterDef Al(Align a) { Alignment = a; return this; }
        public MonsterDef Exp(int xp) { XpOverride = xp; return this; }
        public MonsterDef Plur(string p) { Plural = p; return this; }
    }

    [Flags]
    public enum WF
    {
        None = 0,
        Blunt = 1,
        TwoHand = 2,
        Thrown = 4,
        Launcher = 8,
        Silver = 16,
        Returns = 32,
        Light = 64,
    }

    public class ItemDef
    {
        public string Id, Name, Plural, Desc;
        public IK Kind;
        public char G;
        public Color C;
        public int Wt, Value, Freq, Depth;
        public Dice Dmg, DmgL;
        public int Ac;
        public WF WFlags;
        public string Ammo;
        public string Launcher;
        public int Range = 1;
        public bool Stack;
        public bool Magic;
        public bool Unknown;
        public string Effect;
        public Dice Charges;
        public int LightRadius;
        public int Fuel;
        public int Nutrition;
        public int Power;
        public bool Metal;
        public string ArmorClass;
        public int SpellLevel;
        public SpellId Spell;
        public bool Artifact;
        public Color LightColor = new Color(255, 190, 110);

        public bool IsWeapon => Kind == IK.Weapon;
        public bool Has(WF f) => (WFlags & f) != 0;
        public bool Equippable => Kind is IK.Weapon or IK.Armor or IK.Shield or IK.Helm or IK.Cloak or IK.Boots or IK.Gloves or IK.Bracers or IK.Girdle or IK.Ring or IK.Amulet or IK.Light;
    }

    public enum SpellId
    {
        None,
        MagicMissile, Sleep, BurningHands, ShockingGrasp, Shield, CharmPerson, Light, Identify, DetectMagic, HoldPortal, ProtEvil, FindFamiliar,
        Web, Invisibility, MirrorImage, Knock, StinkingCloud, AcidArrow, Strength, RayEnfeeble, Darkness, Scare,
        Fireball, LightningBolt, Haste, HoldPerson, DispelMagic, Slow, Blink, ProtMissiles, MonsterSummon,
        IceStorm, DimensionDoor, Fear, WallOfFire, PolymorphOther, CharmMonster, FireShield,
        ConeOfCold, Cloudkill, HoldMonster, Teleport, AnimateDead, Passwall, Feeblemind,
        Disintegrate, DeathSpell, Globe, ChainLightning,
        FingerOfDeath, PowerWordStun, DelayedBlast,
        IncendiaryCloud, MassCharm,
        MeteorSwarm, PowerWordKill, TimeStop,
        CureLight, Bless, CProtEvil, CLight, Command, DetectEvil, Sanctuary,
        CHoldPerson, Silence, SpiritualHammer, SlowPoison, FindTraps, ResistFire, Augury,
        CureDisease, Prayer, RemoveCurse, CDispel, CreateFood, CAnimateDead, SpeakWithDead, ContinualLight, GlyphWarding,
        CureSerious, NeutralizePoison, SticksToSnakes, Divination, ProtEvil10,
        CureCritical, FlameStrike, InsectPlague, DispelEvil, Commune, TrueSeeing,
        Heal, BladeBarrier, WordOfRecall, FindPath, Harm,
        HolyWord, Earthquake, Restoration, Regenerate,
        CauseLight, CauseSerious, Darkness2, FaerieFire, MindBlast, SummonUndead, EyeRays
    }

    public class SpellDef
    {
        public SpellId Id;
        public string Name;
        public int Level;
        public bool Cleric;
        public Target Tgt;
        public int Range = 8;
        public string Desc;
        public Color C;
        public bool Offensive;
        public bool MonsterOnly;
    }
}
