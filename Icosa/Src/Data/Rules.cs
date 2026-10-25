using System;
using System.Collections.Generic;
using System.Linq;

namespace Icosa
{
    public enum Cls { Fighter, Paladin, Ranger, Cleric, MagicUser, Thief }
    public enum Race { Human, Elf, HalfElf, Dwarf, Halfling, Gnome, HalfOrc }

    public class RaceInfo
    {
        public Race Id;
        public string Name, Desc;
        public int[] Adj = new int[6];
        public int Infravision;
        public Cls[] Classes;
        public int[] Min = new int[6];
        public int[] Max = new int[6] { 18, 18, 18, 18, 18, 18 };
    }

    public class ClassInfo
    {
        public Cls Id;
        public string Name, Desc, Prime;
        public int HitDie, HpAfter9;
        public int[] Min = new int[6];
        public Align[] Aligns;
        public bool Warrior;
        public double XpMul;
    }

    public static class Rules
    {
        public static readonly string[] StatNames = { "STR", "INT", "WIS", "DEX", "CON", "CHA" };
        public static readonly string[] StatLong = { "Strength", "Intelligence", "Wisdom", "Dexterity", "Constitution", "Charisma" };
        public static readonly string[] AlignNames = { "Lawful Good", "Neutral Good", "Chaotic Good", "Lawful Neutral", "True Neutral", "Chaotic Neutral", "Lawful Evil", "Neutral Evil", "Chaotic Evil" };
        public static readonly string[] AlignShort = { "LG", "NG", "CG", "LN", "N", "CN", "LE", "NE", "CE" };
        public static readonly string[] Deities =
        {
            "Aurelion the Just", "Veylin of the Hearth", "Corvane the Unbound", "Ordo the Scale", "the Grey Wanderer",
            "Fask the Laughing", "Malgrath of the Iron Crown", "Nyx-Ula the Hungering", "Skarnoth the Red",
        };

        public static readonly Dictionary<Race, RaceInfo> Races = new Dictionary<Race, RaceInfo>();
        public static readonly Dictionary<Cls, ClassInfo> Classes = new Dictionary<Cls, ClassInfo>();

        static Rules()
        {
            var all = Enum.GetValues(typeof(Cls)).Cast<Cls>().ToArray();
            Races[Race.Human] = new RaceInfo { Id = Race.Human, Name = "Human", Classes = all, Desc = "Ambitious, adaptable and short-lived. Humans can be any class and rise without limit." };
            Races[Race.Elf] = new RaceInfo { Id = Race.Elf, Name = "Elf", Adj = new[] { 0, 0, 0, 1, -1, 0 }, Infravision = 6, Classes = new[] { Cls.Fighter, Cls.Ranger, Cls.MagicUser, Cls.Thief }, Desc = "Graceful and long-lived. Infravision; 90% resistance to sleep and charm; immune to ghoul paralysis; +1 to hit with swords and bows; may notice secret doors in passing." };
            Races[Race.HalfElf] = new RaceInfo { Id = Race.HalfElf, Name = "Half-Elf", Infravision = 6, Classes = new[] { Cls.Fighter, Cls.Ranger, Cls.Cleric, Cls.MagicUser, Cls.Thief }, Desc = "Between two worlds and welcome in neither. Infravision; 30% resistance to sleep and charm; broad class choice." };
            Races[Race.Dwarf] = new RaceInfo { Id = Race.Dwarf, Name = "Dwarf", Adj = new[] { 0, 0, 0, 0, 1, -1 }, Infravision = 6, Classes = new[] { Cls.Fighter, Cls.Thief, Cls.Cleric }, Desc = "Stout, stubborn miners. Infravision; saves against magic and poison improved by Constitution; +1 to hit orcs and goblins; giants and ogres suffer -4 to hit them; spot traps in stonework." };
            Races[Race.Halfling] = new RaceInfo { Id = Race.Halfling, Name = "Halfling", Adj = new[] { -1, 0, 0, 1, 0, 0 }, Classes = new[] { Cls.Fighter, Cls.Thief }, Desc = "Small, quiet, and fond of second breakfasts. Saves improved by Constitution; +3 to hit with slings and thrown weapons; very stealthy." };
            Races[Race.Gnome] = new RaceInfo { Id = Race.Gnome, Name = "Gnome", Adj = new[] { 0, 1, -1, 0, 0, 0 }, Infravision = 6, Classes = new[] { Cls.Fighter, Cls.Thief, Cls.Cleric }, Desc = "Inventive tinkerers of the hills. Infravision; saves against magic improved by Constitution; +1 to hit kobolds and goblins; giants suffer -4 to hit them." };
            Races[Race.HalfOrc] = new RaceInfo { Id = Race.HalfOrc, Name = "Half-Orc", Adj = new[] { 1, 0, 0, 0, 1, -2 }, Infravision = 6, Classes = new[] { Cls.Fighter, Cls.Cleric, Cls.Thief }, Desc = "Brawny and scarred, distrusted by all. Infravision; strong and tough." };

            Classes[Cls.Fighter] = new ClassInfo { Id = Cls.Fighter, Name = "Fighter", HitDie = 10, HpAfter9 = 3, Warrior = true, XpMul = 1.0, Prime = "STR", Min = new[] { 9, 0, 0, 0, 7, 0 }, Aligns = (Align[])Enum.GetValues(typeof(Align)), Desc = "Masters of arms and armor. Exceptional strength (18/xx); extra attacks at 7th and 13th level; against foes of less than 1 HD, a fighter attacks once per level each round." };
            Classes[Cls.Paladin] = new ClassInfo { Id = Cls.Paladin, Name = "Paladin", HitDie = 10, HpAfter9 = 3, Warrior = true, XpMul = 1.25, Prime = "STR, WIS", Min = new[] { 12, 9, 13, 0, 9, 17 }, Aligns = new[] { Align.LG }, Desc = "Holy warriors of law and good. +2 to all saves; immune to disease; lay on hands once a day; turn undead as a cleric two levels lower; detect evil at will. Must remain Lawful Good." };
            Classes[Cls.Ranger] = new ClassInfo { Id = Cls.Ranger, Name = "Ranger", HitDie = 8, HpAfter9 = 2, Warrior = true, XpMul = 1.2, Prime = "STR, INT, WIS", Min = new[] { 13, 13, 14, 0, 14, 0 }, Aligns = new[] { Align.LG, Align.NG, Align.CG }, Desc = "Wilderness hunters. Two hit dice at first level; +1 damage per level against giant-class creatures (goblins, orcs, ogres, trolls, giants...); rarely surprised; superb archers." };
            Classes[Cls.Cleric] = new ClassInfo { Id = Cls.Cleric, Name = "Cleric", HitDie = 8, HpAfter9 = 2, XpMul = 1.0, Prime = "WIS", Min = new[] { 0, 0, 9, 0, 0, 0 }, Aligns = (Align[])Enum.GetValues(typeof(Align)), Desc = "Servants of a god. Wear any armor; use only blunt weapons without penalty; turn undead; cast clerical spells and know every prayer of each level they can cast. High Wisdom grants bonus spells." };
            Classes[Cls.MagicUser] = new ClassInfo { Id = Cls.MagicUser, Name = "Magic-User", HitDie = 4, HpAfter9 = 1, XpMul = 1.3, Prime = "INT", Min = new[] { 0, 9, 0, 6, 0, 0 }, Aligns = (Align[])Enum.GetValues(typeof(Align)), Desc = "Frail scholars of the Art. No armor; dagger, dart and staff only. Learn spells from scrolls and books; memorize them by resting. At the end, the most powerful of all." };
            Classes[Cls.Thief] = new ClassInfo { Id = Cls.Thief, Name = "Thief", HitDie = 6, HpAfter9 = 2, XpMul = 0.8, Prime = "DEX", Min = new[] { 0, 0, 0, 9, 0, 0 }, Aligns = new[] { Align.LN, Align.N, Align.CN, Align.LE, Align.NE, Align.CE, Align.NG, Align.CG }, Desc = "Experts in locks, traps and shadows. Backstab unaware foes for double damage (more at higher level); pick locks; find and remove traps; move silently and hide in shadows. Leather armor only for skills." };
        }

        public static readonly int[] XpTable =
        {
            0, 0, 30, 70, 150, 300, 550, 950, 1600, 2600, 4000, 6000, 8500, 11500, 15000, 19000, 24000, 30000, 37000, 45000, 54000,
        };

        public static int XpFor(Cls c, int level)
        {
            if (level <= 1) return 0;
            if (level >= XpTable.Length) return int.MaxValue;
            return (int)(XpTable[level] * Classes[c].XpMul);
        }

        public static int Thac0(Cls c, int level)
        {
            switch (c)
            {
                case Cls.Fighter:
                case Cls.Paladin:
                case Cls.Ranger:
                    return Math.Max(1, 21 - level);
                case Cls.Cleric:
                    return Math.Max(4, 20 - ((level - 1) / 3) * 2);
                case Cls.Thief:
                    return Math.Max(6, 20 - ((level - 1) / 2));
                default:
                    return Math.Max(8, 20 - ((level - 1) / 3));
            }
        }

        static readonly int[][] SaveFighter =
        {
            new[] { 14, 15, 16, 17, 17 }, new[] { 13, 14, 15, 16, 16 }, new[] { 11, 12, 13, 13, 14 }, new[] { 10, 11, 12, 12, 13 },
            new[] { 8, 9, 10, 9, 11 }, new[] { 7, 8, 9, 8, 10 }, new[] { 5, 6, 7, 5, 8 }, new[] { 4, 5, 6, 4, 7 }, new[] { 3, 4, 5, 4, 6 },
        };
        static readonly int[][] SaveCleric =
        {
            new[] { 10, 13, 14, 16, 15 }, new[] { 9, 12, 13, 15, 14 }, new[] { 7, 10, 11, 13, 12 }, new[] { 6, 9, 10, 12, 11 },
            new[] { 5, 8, 9, 11, 10 }, new[] { 4, 7, 8, 10, 9 }, new[] { 2, 5, 6, 8, 7 },
        };
        static readonly int[][] SaveMage =
        {
            new[] { 14, 13, 11, 15, 12 }, new[] { 13, 11, 9, 13, 10 }, new[] { 11, 9, 7, 11, 8 }, new[] { 10, 7, 5, 9, 6 }, new[] { 8, 5, 3, 7, 4 },
        };
        static readonly int[][] SaveThief =
        {
            new[] { 13, 12, 14, 16, 15 }, new[] { 12, 11, 12, 15, 13 }, new[] { 11, 10, 10, 14, 11 }, new[] { 10, 9, 8, 13, 9 }, new[] { 9, 8, 6, 12, 7 }, new[] { 8, 7, 4, 11, 5 },
        };

        public static int SaveTarget(Cls c, int level, Save s)
        {
            level = Math.Max(1, level);
            int i = (int)s;
            switch (c)
            {
                case Cls.Fighter:
                case Cls.Paladin:
                case Cls.Ranger:
                    {
                        int row = Math.Min(SaveFighter.Length - 1, (level - 1) / 2);
                        int v = SaveFighter[row][i];
                        if (c == Cls.Paladin) v -= 2;
                        return v;
                    }
                case Cls.Cleric:
                    return SaveCleric[Math.Min(SaveCleric.Length - 1, (level - 1) / 3)][i];
                case Cls.MagicUser:
                    return SaveMage[Math.Min(SaveMage.Length - 1, (level - 1) / 5)][i];
                default:
                    return SaveThief[Math.Min(SaveThief.Length - 1, (level - 1) / 4)][i];
            }
        }

        public static int MonsterSave(MonsterDef d, Save s)
        {
            int lvl = d.HalfHD ? 0 : d.HD + (d.HpPlus > 0 ? 1 : 0);
            if (lvl <= 0) return new[] { 16, 17, 18, 20, 19 }[(int)s];
            return SaveTarget(Cls.Fighter, lvl, s);
        }

        static readonly int[][] MageSlots =
        {
            new int[0], new[] { 1 }, new[] { 2 }, new[] { 2, 1 }, new[] { 3, 2 }, new[] { 4, 2, 1 }, new[] { 4, 2, 2 }, new[] { 4, 3, 2, 1 },
            new[] { 4, 3, 3, 2 }, new[] { 4, 3, 3, 2, 1 }, new[] { 4, 4, 3, 2, 2 }, new[] { 4, 4, 4, 3, 3 }, new[] { 4, 4, 4, 4, 4, 1 },
            new[] { 5, 5, 5, 4, 4, 2 }, new[] { 5, 5, 5, 4, 4, 2, 1 }, new[] { 5, 5, 5, 5, 5, 2, 1 }, new[] { 5, 5, 5, 5, 5, 3, 2, 1 },
            new[] { 5, 5, 5, 5, 5, 3, 3, 2 }, new[] { 5, 5, 5, 5, 5, 3, 3, 2, 1 }, new[] { 5, 5, 5, 5, 5, 3, 3, 3, 1 }, new[] { 5, 5, 5, 5, 5, 4, 3, 3, 2 },
        };
        static readonly int[][] ClericSlots =
        {
            new int[0], new[] { 1 }, new[] { 2 }, new[] { 2, 1 }, new[] { 3, 2 }, new[] { 3, 3, 1 }, new[] { 3, 3, 2 }, new[] { 3, 3, 2, 1 },
            new[] { 3, 3, 3, 2 }, new[] { 4, 4, 3, 2, 1 }, new[] { 4, 4, 3, 3, 2 }, new[] { 5, 4, 4, 3, 2, 1 }, new[] { 6, 5, 5, 3, 2, 2 },
            new[] { 6, 6, 6, 4, 2, 2 }, new[] { 6, 6, 6, 5, 3, 2, 1 }, new[] { 7, 7, 7, 5, 4, 2, 1 }, new[] { 7, 7, 7, 6, 5, 3, 1 },
            new[] { 8, 8, 8, 6, 5, 3, 1 }, new[] { 8, 8, 8, 7, 6, 4, 1 }, new[] { 9, 9, 9, 7, 6, 4, 2 }, new[] { 9, 9, 9, 8, 7, 5, 2 },
        };

        public static int[] Slots(Cls c, int level, int wis)
        {
            level = Math.Clamp(level, 0, 20);
            if (c == Cls.MagicUser) return (int[])MageSlots[level].Clone();
            if (c == Cls.Cleric)
            {
                var s = (int[])ClericSlots[level].Clone();
                if (s.Length > 0)
                {
                    if (wis >= 13) s[0]++;
                    if (wis >= 14) s[0]++;
                    if (wis >= 15 && s.Length > 1) s[1]++;
                    if (wis >= 16 && s.Length > 1) s[1]++;
                    if (wis >= 17 && s.Length > 2) s[2]++;
                    if (wis >= 18 && s.Length > 3) s[3]++;
                }
                return s;
            }
            if (c == Cls.Paladin && level >= 9) return level >= 13 ? new[] { 2, 1 } : new[] { 1 };
            return new int[0];
        }

        public static int[] ThiefBase(int level)
        {
            level = Math.Max(1, level);
            int l = level - 1;
            return new[]
            {
                Math.Min(99, 25 + l * 5),
                Math.Min(99, 20 + l * 5),
                Math.Min(99, 15 + l * 6),
                Math.Min(99, 10 + l * 6),
            };
        }

        public static int BackstabMul(int level) => level >= 13 ? 5 : level >= 9 ? 4 : level >= 5 ? 3 : 2;

        public static (int hit, int dmg) StrBonus(int str, int ex)
        {
            if (str <= 3) return (-3, -1);
            if (str <= 5) return (-2, -1);
            if (str <= 7) return (-1, 0);
            if (str <= 15) return (0, 0);
            if (str == 16) return (0, 1);
            if (str == 17) return (1, 1);
            if (str == 18)
            {
                if (ex <= 0) return (1, 2);
                if (ex <= 50) return (1, 3);
                if (ex <= 75) return (2, 3);
                if (ex <= 90) return (2, 4);
                if (ex <= 99) return (2, 5);
                return (3, 6);
            }
            if (str == 19) return (3, 7);
            if (str == 20) return (3, 8);
            if (str == 21) return (4, 9);
            if (str == 22) return (4, 10);
            if (str == 23) return (5, 11);
            if (str == 24) return (6, 12);
            return (7, 14);
        }

        public static int Capacity(int str, int ex)
        {
            int baseCap = 1000 + Math.Max(0, str - 10) * 100;
            if (str < 10) baseCap = 1000 - (10 - str) * 80;
            if (str == 18) baseCap += ex * 3;
            if (str > 18) baseCap += 300 + (str - 18) * 200;
            return Math.Max(400, baseCap);
        }

        public static int DexAc(int dex)
        {
            if (dex <= 3) return 4;
            if (dex == 4) return 3;
            if (dex == 5) return 2;
            if (dex == 6) return 1;
            if (dex <= 14) return 0;
            if (dex == 15) return -1;
            if (dex == 16) return -2;
            if (dex == 17) return -3;
            return -4;
        }

        public static int DexMissile(int dex)
        {
            if (dex <= 3) return -3;
            if (dex == 4) return -2;
            if (dex == 5) return -1;
            if (dex <= 15) return 0;
            if (dex == 16) return 1;
            if (dex == 17) return 2;
            return 3;
        }

        public static int ConHp(int con, bool warrior)
        {
            if (con <= 3) return -2;
            if (con <= 6) return -1;
            if (con <= 14) return 0;
            if (con == 15) return 1;
            if (con == 16) return 2;
            if (con == 17) return warrior ? 3 : 2;
            if (con == 18) return warrior ? 4 : 2;
            return warrior ? 5 : 2;
        }

        public static int WisSave(int wis)
        {
            if (wis <= 3) return -3;
            if (wis == 4) return -2;
            if (wis <= 7) return -1;
            if (wis <= 14) return 0;
            return Math.Min(4, wis - 14);
        }

        public static int IntLearn(int intel)
        {
            if (intel <= 9) return 35;
            if (intel <= 12) return 45;
            if (intel <= 14) return 55;
            if (intel <= 16) return 65;
            if (intel == 17) return 75;
            if (intel == 18) return 85;
            return 95;
        }

        public static int ChaPrice(int cha)
        {
            if (cha <= 5) return 150;
            if (cha <= 7) return 130;
            if (cha <= 10) return 110;
            if (cha <= 14) return 100;
            if (cha <= 16) return 90;
            if (cha == 17) return 80;
            return 70;
        }

        public static string StrText(int str, int ex)
        {
            if (str == 18 && ex > 0) return ex >= 100 ? "18/00" : "18/" + ex.ToString("00");
            return str.ToString();
        }

        public static string LevelTitle(Cls c, int level)
        {
            string[] t = c switch
            {
                Cls.Fighter => new[] { "Veteran", "Warrior", "Swordsman", "Hero", "Swashbuckler", "Myrmidon", "Champion", "Superhero", "Lord" },
                Cls.Paladin => new[] { "Gallant", "Keeper", "Protector", "Defender", "Warder", "Guardian", "Chevalier", "Justiciar", "Paladin" },
                Cls.Ranger => new[] { "Runner", "Strider", "Scout", "Courser", "Tracker", "Guide", "Pathfinder", "Ranger", "Ranger Knight" },
                Cls.Cleric => new[] { "Acolyte", "Adept", "Priest", "Curate", "Perfect", "Canon", "Lama", "Patriarch", "High Priest" },
                Cls.MagicUser => new[] { "Prestidigitator", "Evoker", "Conjurer", "Theurgist", "Thaumaturgist", "Magician", "Enchanter", "Warlock", "Wizard" },
                _ => new[] { "Rogue", "Footpad", "Cutpurse", "Robber", "Burglar", "Filcher", "Sharper", "Magsman", "Master Thief" },
            };
            return t[Math.Clamp(level - 1, 0, t.Length - 1)];
        }

        public static readonly string[] DepthNames =
        {
            "", "The Antechamber", "The Upper Halls", "The Kobold Warrens", "The Mines of Gilt", "The Ossuary", "The Fungal Deep",
            "The Drowned Temple", "The Troll Galleries", "The Shattered Vaults", "The Underdark Gate", "The Spider Courts",
            "The Mind's Abyss", "The Ember Stair", "The Burning Deep", "The Hollow Throne",
        };

        public const int MaxDepth = 15;
    }
}
