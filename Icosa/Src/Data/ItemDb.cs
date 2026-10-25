using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class ItemDb
    {
        public static readonly Dictionary<string, ItemDef> All = new Dictionary<string, ItemDef>();
        public static ItemDef Get(string id) => All.TryGetValue(id, out var d) ? d : null;

        public static readonly string[] PotionLooks =
        {
            "bubbling crimson", "murky green", "smoky", "glittering violet", "milky", "amber", "inky black", "effervescent pink",
            "cloudy blue", "oily", "golden", "silver-flecked", "brackish", "luminous", "orange", "steaming", "icy", "clotted",
            "rainbow-swirled", "tea-colored", "viscous yellow", "fizzy white", "dark ruby", "chartreuse", "sea-green",
        };

        public static readonly string[] ScrollLooks =
        {
            "XOR THAN ZUN", "ELBIB YLOH", "DAIYEN FOOELS", "VERR YED HORRE", "KIRJE", "PRATYAVAYAH", "ANDOVA BEGARIN",
            "JUYED AWK YACC", "NR 9", "VE FORBRYDERNE", "HACKEM MUCHE", "GARVEN DEH", "READ ME", "ETAOIN SHRDLU",
            "LOREM IPSUM", "THARR", "YUM YUM", "MAPIRO MAHAMA DIROMAT", "TEMOV", "FOOBIE BLETCH", "ZELGO MER", "DUAM XNAHT",
            "KO BATE", "VELOX NEB", "IHTFP", "SNIFFLE GORP", "OBELISK IN BLUE", "TWELVE SIDES SING",
        };

        public static readonly string[] WandLooks =
        {
            "oak", "ebony", "bone", "glass", "copper", "iron", "crystal", "silver", "jade", "marble", "pine", "redwood",
            "brass", "ivory", "tin", "platinum", "runed", "twisted", "uranium", "balsa", "spiked", "forked",
        };

        public static readonly string[] RingLooks =
        {
            "ruby", "jade", "moonstone", "tiger eye", "onyx", "opal", "sapphire", "topaz", "wooden", "iron", "twisted gold",
            "black onyx", "engagement", "coral", "agate", "pearl", "granite", "bone", "ivory", "obsidian", "emerald", "wire",
        };

        public static readonly string[] AmuletLooks =
        {
            "circular", "triangular", "pyramidal", "square", "concave", "hexagonal", "octagonal", "spiral", "star-shaped", "oval", "icosahedral",
        };

        static ItemDef I(string id, string name, IK kind, char g, Color c, int wt, int value, int freq, int depth, string desc)
        {
            var d = new ItemDef { Id = id, Name = name, Kind = kind, G = g, C = c, Wt = wt, Value = value, Freq = freq, Depth = depth, Desc = desc };
            All[id] = d;
            return d;
        }

        static ItemDef W(string id, string name, string dmg, string dmgL, int wt, int value, int freq, WF flags, string desc, bool metal = true)
        {
            var d = I(id, name, IK.Weapon, ')', metal ? new Color(190, 200, 210) : new Color(170, 130, 80), wt, value, freq, 1, desc);
            d.Dmg = Dice.Parse(dmg);
            d.DmgL = Dice.Parse(dmgL);
            d.WFlags = flags;
            d.Metal = metal;
            return d;
        }

        static ItemDef A(string id, string name, IK kind, int ac, int wt, int value, int freq, string desc, bool metal, char g = '[')
        {
            var col = kind switch
            {
                IK.Shield => new Color(170, 150, 120),
                IK.Helm => new Color(180, 180, 190),
                _ => metal ? new Color(180, 185, 195) : new Color(160, 120, 80)
            };
            var d = I(id, name, kind, g, col, wt, value, freq, 1, desc);
            d.Ac = ac;
            d.Metal = metal;
            return d;
        }

        static ItemDef P(string id, string name, int value, int freq, int depth, string desc)
        {
            var d = I(id, name, IK.Potion, '!', Color.White, 10, value, freq, depth, desc);
            d.Unknown = true;
            d.Stack = true;
            d.Magic = true;
            d.Effect = id;
            return d;
        }

        static ItemDef S(string id, string name, int value, int freq, int depth, string desc)
        {
            var d = I(id, name, IK.Scroll, '?', new Color(235, 230, 210), 3, value, freq, depth, desc);
            d.Unknown = true;
            d.Stack = true;
            d.Magic = true;
            d.Effect = id;
            return d;
        }

        static ItemDef Wd(string id, string name, string charges, int value, int freq, int depth, string desc)
        {
            var d = I(id, name, IK.Wand, '/', Color.White, 7, value, freq, depth, desc);
            d.Unknown = true;
            d.Magic = true;
            d.Effect = id;
            d.Charges = Dice.Parse(charges);
            return d;
        }

        static ItemDef R(string id, string name, int value, int freq, int depth, string desc)
        {
            var d = I(id, name, IK.Ring, '=', Color.White, 1, value, freq, depth, desc);
            d.Unknown = true;
            d.Magic = true;
            d.Effect = id;
            return d;
        }

        static ItemDef Am(string id, string name, int value, int freq, int depth, string desc)
        {
            var d = I(id, name, IK.Amulet, '"', Color.White, 2, value, freq, depth, desc);
            d.Unknown = true;
            d.Magic = true;
            d.Effect = id;
            return d;
        }

        static ItemDef Io(string id, string look, string power, Color c, int value, string desc)
        {
            var d = I(id, look + " ioun stone", IK.Ioun, '•', c, 1, value, 1, 5, desc);
            d.Magic = true;
            d.Effect = power;
            return d;
        }

        static ItemDef MagicW(string id, string name, string baseId, int ench, int value, int depth, string effect, string desc, int freq = 1)
        {
            var b = All[baseId];
            var d = I(id, name, IK.Weapon, ')', new Color(240, 220, 140), b.Wt, value, freq, depth, desc);
            d.Dmg = b.Dmg;
            d.DmgL = b.DmgL;
            d.WFlags = b.WFlags;
            d.Metal = b.Metal;
            d.Magic = true;
            d.Power = ench;
            d.Effect = effect;
            d.Artifact = true;
            return d;
        }

        static ItemDb()
        {
            var steel = new Color(190, 200, 210);
            var wood = new Color(170, 130, 80);

            W("dagger", "dagger", "1d4", "1d3", 10, 2, 8, WF.Thrown | WF.Light, "A short blade. Good for throwing, stabbing, and buttering.").Range = 6;
            W("silver_dagger", "silver dagger", "1d4", "1d3", 10, 30, 3, WF.Thrown | WF.Silver | WF.Light, "A dagger of pure silver. Werecreatures and wights fear its kiss.").Range = 6;
            W("short_sword", "short sword", "1d6", "1d8", 35, 8, 6, WF.Light, "A stout stabbing blade.");
            W("long_sword", "long sword", "1d8", "1d12", 60, 15, 7, WF.None, "The adventurer's classic. Balanced, reliable, and narratively appropriate.");
            W("broad_sword", "broad sword", "2d4", "1d6+1", 75, 10, 4, WF.None, "A wide, heavy chopping blade.");
            W("bastard_sword", "bastard sword", "2d4", "2d8", 100, 25, 3, WF.TwoHand, "A hand-and-a-half sword, here wielded in both.");
            W("two_handed_sword", "two-handed sword", "1d10", "3d6", 250, 30, 3, WF.TwoHand, "Six feet of steel. Devastating against large foes.");
            W("scimitar", "scimitar", "1d8", "1d8", 40, 15, 4, WF.None, "A curved slashing sword from the desert lands.");
            W("hand_axe", "hand axe", "1d6", "1d4", 50, 1, 5, WF.Thrown, "A small axe, balanced for throwing.").Range = 5;
            W("battle_axe", "battle axe", "1d8", "1d8", 75, 5, 4, WF.None, "A broad axe for broad problems.");
            W("mace", "mace", "1d6+1", "1d6", 100, 8, 6, WF.Blunt, "A flanged iron club. Favored by clerics, who may not shed blood with edges.");
            W("morning_star", "morning star", "2d4", "1d6+1", 125, 5, 4, WF.Blunt, "A spiked ball on a haft. Subtle as a sermon.");
            W("flail", "flail", "1d6+1", "2d4", 150, 3, 4, WF.Blunt, "A spiked weight on a chain.");
            W("war_hammer", "war hammer", "1d4+1", "1d4", 50, 1, 4, WF.Blunt | WF.Thrown, "A short hammer with a spike behind.").Range = 4;
            W("club", "club", "1d6", "1d3", 30, 1, 5, WF.Blunt, "A heavy stick with ambitions.", false);
            W("quarterstaff", "quarterstaff", "1d6", "1d6", 50, 1, 5, WF.Blunt | WF.TwoHand, "A stout wooden staff, the wizard's walking companion.", false);
            W("spear", "spear", "1d6", "1d8", 50, 1, 5, WF.Thrown, "A long point on a long stick.", false).Range = 6;
            W("halberd", "halberd", "1d10", "2d6", 175, 9, 3, WF.TwoHand, "An axe-blade, a spike, and a hook on a pole. A committee of a weapon.");
            W("dart", "dart", "1d3", "1d2", 1, 1, 5, WF.Thrown | WF.Light, "A small weighted dart.").Range = 8;
            All["dart"].Stack = true;
            All["dagger"].Stack = false;
            var sb = W("short_bow", "short bow", "1d2", "1d2", 50, 15, 4, WF.Launcher | WF.TwoHand, "A light hunting bow.", false); sb.Ammo = "arrow"; sb.Range = 10;
            var lb = W("long_bow", "long bow", "1d2", "1d2", 100, 60, 3, WF.Launcher | WF.TwoHand, "A tall bow of yew. In practiced hands, death at a distance.", false); lb.Ammo = "arrow"; lb.Range = 14;
            var cb = W("crossbow", "light crossbow", "1d2", "1d2", 50, 12, 3, WF.Launcher | WF.TwoHand, "A mechanical bow that anyone can aim.", false); cb.Ammo = "bolt"; cb.Range = 12;
            var sl = W("sling", "sling", "1d2", "1d2", 5, 1, 4, WF.Launcher, "A leather strap and a good arm.", false); sl.Ammo = "bullet"; sl.Range = 10;

            var ar = I("arrows", "arrow", IK.Ammo, ')', wood, 1, 1, 6, 1, "A fletched arrow."); ar.Dmg = Dice.Parse("1d6"); ar.DmgL = Dice.Parse("1d6"); ar.Launcher = "arrow"; ar.Stack = true;
            var bo = I("bolts", "crossbow bolt", IK.Ammo, ')', steel, 1, 1, 4, 1, "A stubby iron-headed bolt."); bo.Dmg = Dice.Parse("1d4+1"); bo.DmgL = Dice.Parse("1d6+1"); bo.Launcher = "bolt"; bo.Stack = true;
            var bu = I("bullets", "sling bullet", IK.Ammo, ')', new Color(150, 150, 160), 1, 1, 4, 1, "A lead sling bullet."); bu.Dmg = Dice.Parse("1d4+1"); bu.DmgL = Dice.Parse("1d6+1"); bu.Launcher = "bullet"; bu.Stack = true;
            var sar = I("silver_arrows", "silver arrow", IK.Ammo, ')', new Color(220, 225, 235), 1, 10, 1, 4, "An arrow tipped with silver."); sar.Dmg = Dice.Parse("1d6"); sar.DmgL = Dice.Parse("1d6"); sar.Launcher = "arrow"; sar.Stack = true; sar.WFlags = WF.Silver;

            A("leather", "leather armor", IK.Armor, 8, 150, 5, 6, "Boiled and shaped leather. Quiet and light.", false);
            A("studded", "studded leather", IK.Armor, 7, 200, 15, 5, "Leather reinforced with iron studs.", false);
            A("ring_mail", "ring mail", IK.Armor, 7, 250, 30, 4, "Iron rings sewn to leather.", true);
            A("scale", "scale mail", IK.Armor, 6, 400, 45, 4, "Overlapping metal scales.", true);
            A("chain", "chain mail", IK.Armor, 5, 300, 75, 5, "Interlocking iron links. The dungeon standard.", true);
            A("splint", "splint mail", IK.Armor, 4, 400, 80, 3, "Vertical metal strips over chain.", true);
            A("banded", "banded mail", IK.Armor, 4, 350, 90, 3, "Overlapping horizontal bands of steel.", true);
            A("plate", "plate mail", IK.Armor, 3, 450, 400, 2, "Full plate. You clank like a dropped kitchen.", true);
            var ec = A("elven_chain", "elven chain mail", IK.Armor, 5, 150, 900, 1, "Mithral links fine as fabric. Wizards may cast in it; thieves may sneak in it.", true); ec.Depth = 5; ec.Magic = true;
            var dp = A("dwarven_plate", "dwarven plate", IK.Armor, 2, 500, 1500, 1, "Plate forged under the mountain. It fits like a grudge.", true); dp.Depth = 8; dp.Magic = true;
            A("robe", "robe", IK.Armor, 10, 30, 3, 2, "A simple robe, embroidered with stars that are mostly in the wrong places.", false);
            var ra = A("robe_archmagi", "robe of the archmagi", IK.Armor, 5, 30, 5000, 1, "A robe woven of spell-thread. Grants a magic user AC 5 and +1 to saves.", false); ra.Depth = 9; ra.Magic = true;
            A("shield", "shield", IK.Shield, 1, 100, 10, 6, "A wooden shield bound with iron.", false, '[');
            A("large_shield", "large shield", IK.Shield, 2, 150, 20, 2, "A tall kite shield.", true, '[');
            A("helm", "helm", IK.Helm, 0, 45, 10, 5, "An iron helm. It turns aside the worst blows: critical hits against you are rarer.", true, '[');
            var ht = A("helm_telepathy", "helm of telepathy", IK.Helm, 0, 45, 2000, 1, "A circlet-helm that hums with the thoughts of nearby creatures.", true, '['); ht.Magic = true; ht.Depth = 6; ht.Effect = "telepathy";
            var hb = A("helm_brilliance", "helm of brilliance", IK.Helm, 0, 45, 5000, 1, "Studded with gems that glow. Grants fire resistance and light.", true, '['); hb.Magic = true; hb.Depth = 10; hb.Effect = "brilliance"; hb.LightRadius = 2;
            var ho = A("helm_opposite", "helm of opposite alignment", IK.Helm, 0, 45, 1, 1, "Its touch rewrites your soul. Cursed, inevitably.", true, '['); ho.Magic = true; ho.Depth = 4; ho.Effect = "opposite";
            A("cloak", "cloak", IK.Cloak, 0, 10, 1, 4, "A heavy traveling cloak.", false, '[');
            var cp = A("cloak_protection", "cloak of protection", IK.Cloak, 1, 10, 1500, 2, "A cloak that turns blows and spells alike.", false, '['); cp.Magic = true; cp.Depth = 2; cp.Effect = "protection";
            var ce = A("cloak_elvenkind", "cloak of elvenkind", IK.Cloak, 0, 10, 1200, 1, "Grey-green and shifting. You blend into shadows.", false, '['); ce.Magic = true; ce.Depth = 3; ce.Effect = "stealth";
            var cd = A("cloak_displacement", "cloak of displacement", IK.Cloak, 0, 10, 3000, 1, "Made from a displacer beast's hide. You seem to stand a foot from where you are.", false, '['); cd.Magic = true; cd.Depth = 7; cd.Effect = "displace";
            var bs = A("boots_speed", "boots of speed", IK.Boots, 0, 20, 5000, 1, "Winged boots. You move as if the world were slow.", false, '['); bs.Magic = true; bs.Depth = 7; bs.Effect = "speed";
            var be = A("boots_elvenkind", "boots of elvenkind", IK.Boots, 0, 15, 1000, 1, "Soft boots that make no sound at all.", false, '['); be.Magic = true; be.Depth = 3; be.Effect = "stealth";
            var bl = A("boots_levitation", "boots of levitation", IK.Boots, 0, 15, 2000, 1, "You float an inch above the ground. Pits and water are beneath you, literally.", false, '['); bl.Magic = true; bl.Depth = 5; bl.Effect = "levitate";
            A("boots", "boots", IK.Boots, 0, 20, 1, 3, "Sturdy leather boots.", false, '[');
            A("gauntlets", "gauntlets", IK.Gloves, 0, 25, 5, 3, "Iron gauntlets.", true, '[');
            var go = A("gauntlets_ogre", "gauntlets of ogre power", IK.Gloves, 0, 25, 3000, 1, "Your strength becomes 18/00. Doors fear you.", true, '['); go.Magic = true; go.Depth = 5; go.Effect = "ogre";
            var gd = A("gauntlets_dex", "gauntlets of dexterity", IK.Gloves, 0, 10, 1500, 1, "Supple gloves that make your hands quick.", false, '['); gd.Magic = true; gd.Depth = 3; gd.Effect = "dex";
            var gf = A("gauntlets_fumbling", "gauntlets of fumbling", IK.Gloves, 0, 10, 1, 1, "They look like gauntlets of dexterity. They are not.", false, '['); gf.Magic = true; gf.Depth = 2; gf.Effect = "fumble";
            var br6 = A("bracers6", "bracers of defense AC 6", IK.Bracers, 6, 10, 2000, 1, "Silver bracers that ward the unarmored as well as chain mail.", true, '['); br6.Magic = true; br6.Depth = 3;
            var br4 = A("bracers4", "bracers of defense AC 4", IK.Bracers, 4, 10, 4000, 1, "Silver bracers that ward the unarmored as well as banded mail.", true, '['); br4.Magic = true; br4.Depth = 6;
            var br2 = A("bracers2", "bracers of defense AC 2", IK.Bracers, 2, 10, 8000, 1, "Silver bracers that ward the unarmored better than plate.", true, '['); br2.Magic = true; br2.Depth = 10;
            var gh = A("girdle_hill", "girdle of hill giant strength", IK.Girdle, 0, 20, 3500, 1, "A wide belt of giant's hide. Strength 19.", false, '['); gh.Magic = true; gh.Depth = 6; gh.Effect = "str19";
            var gfi = A("girdle_fire", "girdle of fire giant strength", IK.Girdle, 0, 20, 7000, 1, "A belt of red scales, warm to touch. Strength 22.", false, '['); gfi.Magic = true; gfi.Depth = 11; gfi.Effect = "str22";

            P("heal", "healing", 50, 16, 1, "Heals 2d4+2 hit points and cures blindness.");
            P("extra_heal", "extra-healing", 200, 6, 4, "Heals 6d4 hit points and cures much else.");
            P("speed", "speed", 150, 4, 3, "Doubles your speed for a while.");
            P("giant_str", "giant strength", 200, 4, 3, "Your muscles swell with the strength of a hill giant.");
            P("heroism", "heroism", 150, 4, 2, "You fight as a hero: +2 to hit and temporary hit points.");
            P("invis", "invisibility", 150, 4, 2, "You fade from sight.");
            P("fire_res", "fire resistance", 100, 4, 2, "Flames lick at you without appetite.");
            P("poison", "poison", 1, 5, 1, "Deadly. Or at least sickening.");
            P("delusion", "delusion", 1, 3, 1, "You feel wonderful. Nothing has changed.");
            P("clairvoyance", "clairvoyance", 150, 4, 2, "Visions of the level unfold behind your eyes.");
            P("treasure", "treasure finding", 100, 3, 2, "You sense the location of every object on the level.");
            P("esp", "ESP", 100, 3, 3, "You hear the thoughts of nearby creatures.");
            P("sleep", "sleep", 1, 3, 1, "Drowsiness overwhelms you.");
            P("confusion", "confusion", 1, 3, 1, "Everything is suddenly very interesting and in the wrong order.");
            P("fortune", "fortune", 300, 3, 3, "Liquid luck. For a while, every d20 you roll is rolled twice and the better kept.");
            P("misfortune", "misfortune", 1, 2, 3, "Liquid bad luck. Every d20 is rolled twice and the worse kept.");
            P("restoration", "restoration", 800, 2, 6, "Restores drained levels and lost ability points.");
            P("gain_level", "gain level", 1500, 1, 7, "You feel more experienced.");
            P("super_heroism", "super-heroism", 400, 2, 6, "As heroism, but more so.");
            P("cure_disease", "cure disease", 100, 3, 2, "Purges disease, rot, and sliming.");
            P("levitation", "levitation", 150, 2, 3, "You float gently upward.");

            S("identify", "identify", 60, 18, 1, "Reveals the true nature of an item.");
            S("enchant_weapon", "enchant weapon", 200, 6, 1, "Adds +1 to your wielded weapon.");
            S("enchant_armor", "enchant armor", 200, 6, 1, "Adds +1 to a piece of worn armor.");
            S("remove_curse", "remove curse", 100, 6, 1, "Lifts curses from your equipped items.");
            S("prot_undead", "protection from undead", 150, 3, 3, "Undead cannot touch you for a time.");
            S("teleport", "teleportation", 150, 5, 1, "Teleports you somewhere else on the level.");
            S("magic_map", "magic mapping", 150, 5, 2, "Inks the full map of the level onto your memory.");
            S("fire", "fire", 50, 4, 1, "A burst of flame centered on the reader.");
            S("light", "light", 30, 6, 1, "Lights up the area around you.");
            S("curse", "curse", 1, 3, 2, "Curses your equipment. Someone has a sense of humor.");
            S("create_monster", "create monster", 1, 4, 1, "Calls a monster. Rarely what you wanted.");
            S("fear", "fear", 100, 4, 2, "A dreadful voice booms: monsters nearby flee.");
            S("reroll", "reroll", 500, 2, 3, "Reroll 4d6 drop lowest on one of your abilities, and keep the better.");
            S("blank", "blank paper", 1, 3, 1, "This scroll is blank.");
            S("wandering", "wandering monster", 1, 2, 3, "Summons the Wandering Monster itself. Please do not read.");
            S("recharge", "recharging", 300, 2, 4, "Restores charges to a wand. May explode.");
            S("teleport_level", "level teleportation", 400, 1, 5, "Teleports you to another dungeon level.");

            Wd("w_missile", "magic missiles", "3d4+2", 150, 5, 1, "Fires bolts of magical force (2d4).");
            Wd("w_fire", "fire", "2d4+2", 400, 2, 5, "Shoots a fireball (6d6).");
            Wd("w_cold", "frost", "2d4+2", 400, 2, 5, "Shoots a cone of cold (6d6).");
            Wd("w_lightning", "lightning", "2d4+2", 400, 2, 5, "Shoots a bouncing lightning bolt (6d6).");
            Wd("w_sleep", "sleep", "2d4+3", 200, 3, 2, "Sends creatures to sleep.");
            Wd("w_slow", "slow monster", "2d4+3", 150, 3, 2, "Slows a creature.");
            Wd("w_fear", "fear", "2d4+3", 150, 3, 2, "Sends creatures fleeing.");
            Wd("w_paralysis", "paralyzation", "2d4+2", 300, 2, 4, "Paralyzes a creature that fails its save.");
            Wd("w_digging", "digging", "2d4+4", 200, 3, 2, "Bores a tunnel through solid rock.");
            Wd("w_striking", "striking", "2d4+3", 150, 4, 1, "Smites a creature with force (2d12).");
            Wd("w_wonder", "wonder", "3d4+3", 200, 3, 2, "Does something. Probably.");
            Wd("w_teleport", "teleport other", "2d4+2", 300, 2, 4, "Sends a creature elsewhere on the level.");
            Wd("w_light", "illumination", "3d4+5", 50, 4, 1, "Lights up the surroundings.");
            Wd("w_secret", "secret door detection", "3d4+5", 100, 3, 1, "Reveals hidden doors and traps nearby.");
            Wd("w_polymorph", "polymorph", "2d4+1", 300, 2, 6, "Changes a creature into another creature.");
            Wd("w_create", "create monster", "2d4+3", 1, 2, 2, "Creates monsters. Why would anyone make this?");

            R("r_protection", "protection", 500, 5, 1, "Improves armor class and saving throws by its bonus.");
            R("r_regen", "regeneration", 1500, 2, 5, "Your wounds close as you watch. You also get very hungry.");
            R("r_invis", "invisibility", 1000, 2, 4, "You are invisible while wearing it.");
            R("r_fire", "fire resistance", 600, 3, 3, "Flames treat you as a guest.");
            R("r_free", "free action", 1000, 2, 4, "Nothing can hold or paralyze you.");
            R("r_sustenance", "sustenance", 500, 2, 2, "You barely need to eat.");
            R("r_teleport", "teleportation", 1, 2, 2, "You teleport at random. Cursed, usually.");
            R("r_weakness", "weakness", 1, 2, 2, "Saps your strength. Cursed.");
            R("r_see_invis", "see invisible", 600, 2, 3, "Reveals invisible creatures.");
            R("r_mind", "mind shielding", 600, 2, 3, "Protects your mind from charm, confusion, and psionic blasts.");
            R("r_keen", "keen fortune", 2000, 2, 4, "Your critical hits come on a 19 as well as a 20.");
            R("r_wishes", "three wishes", 20000, 1, 9, "A plain ring that thrums with possibility. Rub it to wish.");
            R("r_warmth", "warmth", 400, 2, 2, "Protects you from cold.");
            R("r_stealth", "stealth", 600, 2, 2, "Your footsteps make no sound.");

            Am("a_closure", "periapt of wound closure", 1000, 3, 2, "Your wounds stop bleeding and close faster.");
            Am("a_health", "periapt of health", 1000, 3, 2, "Protects you from disease.");
            Am("a_poison", "periapt of proof against poison", 1000, 3, 3, "Protects you from poison.");
            Am("a_scarab", "scarab of protection", 3000, 2, 5, "Absorbs level drain and death magic.");
            Am("a_strangle", "necklace of strangulation", 1, 2, 3, "Tightens around your throat. Deadly.");
            Am("a_second_roll", "amulet of the second roll", 5000, 1, 6, "When you die, the dice are rolled again. Once.");
            Am("a_esp", "amulet of ESP", 1500, 2, 4, "You hear the thoughts of nearby creatures.");

            Io("ioun_str", "pale blue", "str", new Color(150, 190, 255), 2000, "A rhomboid ioun stone. +1 Strength while it orbits you.");
            Io("ioun_int", "scarlet and blue", "int", new Color(220, 80, 140), 2000, "A spherical ioun stone. +1 Intelligence while it orbits you.");
            Io("ioun_wis", "incandescent blue", "wis", new Color(90, 140, 255), 2000, "A spherical ioun stone. +1 Wisdom while it orbits you.");
            Io("ioun_dex", "deep red", "dex", new Color(200, 40, 50), 2000, "A spherical ioun stone. +1 Dexterity while it orbits you.");
            Io("ioun_con", "pink", "con", new Color(255, 150, 190), 2000, "A rhomboid ioun stone. +1 Constitution while it orbits you.");
            Io("ioun_cha", "pink and green", "cha", new Color(200, 230, 150), 2000, "A spherical ioun stone. +1 Charisma while it orbits you.");
            Io("ioun_ac", "dusty rose", "ac", new Color(220, 160, 160), 3000, "A prism ioun stone. +1 to AC while it orbits you.");
            Io("ioun_regen", "pearly white", "regen", new Color(245, 245, 240), 4000, "A spindle ioun stone. Regenerates 1 hit point every few turns.");
            Io("ioun_light", "incandescent yellow", "light", new Color(255, 240, 120), 1000, "A glowing ioun stone. It lights your way.");

            var ration = I("ration", "iron ration", IK.Food, '%', new Color(190, 140, 90), 20, 5, 12, 1, "Hard bread, salt pork, and a hard-boiled sense of duty.");
            ration.Nutrition = 900; ration.Stack = true;
            var std = I("std_ration", "standard ration", IK.Food, '%', new Color(200, 160, 100), 20, 3, 6, 1, "Fresh(ish) food for a day or two.");
            std.Nutrition = 650; std.Stack = true;
            var apple = I("apple", "apple", IK.Food, '%', new Color(220, 50, 40), 2, 1, 5, 1, "A crisp red apple. Suspiciously unbruised.");
            apple.Nutrition = 120; apple.Stack = true;
            var wb = I("waybread", "elven waybread", IK.Food, '%', new Color(230, 220, 170), 5, 30, 2, 3, "A thin cake wrapped in leaves. One bite fills a grown man's stomach.");
            wb.Nutrition = 1000; wb.Stack = true;
            var jerky = I("jerky", "strip of jerky", IK.Food, '%', new Color(150, 80, 50), 2, 1, 4, 1, "Dried meat of uncertain heritage.");
            jerky.Nutrition = 200; jerky.Stack = true;
            var cheese = I("cheese", "wheel of cheese", IK.Food, '%', new Color(240, 210, 90), 15, 6, 3, 1, "A dense, pungent wheel of cave-aged cheese.");
            cheese.Nutrition = 500; cheese.Stack = true;
            var mush = I("mushroom", "cave mushroom", IK.Food, '%', new Color(200, 170, 220), 1, 1, 3, 1, "A pale mushroom. Probably edible. Probably.");
            mush.Nutrition = 80; mush.Stack = true; mush.Effect = "mushroom";
            var corpse = I("corpse", "corpse", IK.Corpse, '%', new Color(160, 60, 50), 400, 0, 0, 99, "Recently alive.");
            corpse.Nutrition = 300;
            var gland = I("gland", "fire beetle gland", IK.Light, '•', new Color(255, 120, 40), 1, 5, 0, 99, "A glowing gland from a fire beetle. It will shine for several days.");
            gland.LightRadius = 3; gland.Fuel = 900; gland.Stack = true; gland.LightColor = new Color(255, 120, 60);

            var torch = I("torch", "torch", IK.Light, '(', new Color(200, 140, 70), 30, 1, 10, 1, "A stick wrapped in pitch-soaked rags. It burns for about fifteen hundred turns.");
            torch.LightRadius = 5; torch.Fuel = 1500; torch.Stack = true;
            var lantern = I("lantern", "hooded lantern", IK.Light, '(', new Color(240, 200, 90), 50, 7, 4, 1, "A brass lantern with shutters. Refill it with flasks of oil.");
            lantern.LightRadius = 7; lantern.Fuel = 2500; lantern.Metal = true; lantern.LightColor = new Color(255, 215, 140);
            var candle = I("everburning", "everburning candle", IK.Light, '(', new Color(255, 250, 220), 2, 800, 1, 4, "A candle that never shortens. Its flame is cool and pale.");
            candle.LightRadius = 4; candle.Fuel = 999999; candle.Magic = true; candle.LightColor = new Color(220, 230, 255);

            var oil = I("oil", "flask of oil", IK.Tool, '!', new Color(200, 170, 80), 10, 2, 7, 1, "Lamp oil. Refills a lantern, or, lit and thrown, burns your enemies (2d6).");
            oil.Stack = true; oil.Effect = "oil"; oil.WFlags = WF.Thrown; oil.Range = 6;
            var hw = I("holy_water", "flask of holy water", IK.Tool, '!', new Color(200, 220, 255), 10, 25, 4, 1, "Blessed water in a glass vial. Burns the undead and the fiendish (2d4).");
            hw.Stack = true; hw.Effect = "holy_water"; hw.WFlags = WF.Thrown; hw.Range = 6;
            var pole = I("pole", "10-foot pole", IK.Tool, '(', new Color(170, 130, 80), 80, 1, 4, 1, "The most important tool in the history of adventuring. Apply it to prod the floor ahead for traps, mimics, and doubts.");
            pole.Effect = "pole";
            var rope = I("rope", "50' coil of rope", IK.Tool, '(', new Color(190, 170, 120), 75, 1, 3, 1, "Hemp rope. Apply it to climb out of pits at once, or to descend a chasm safely.");
            rope.Effect = "rope";
            var spikes = I("spikes", "iron spike", IK.Tool, '(', new Color(150, 150, 160), 5, 1, 4, 1, "Apply to spike a closed door shut. Monsters will have to break it down.");
            spikes.Stack = true; spikes.Effect = "spike"; spikes.Metal = true;
            var tools = I("thieves_tools", "set of thieves' tools", IK.Tool, '(', new Color(160, 160, 170), 10, 30, 3, 1, "Picks, wires, and tiny mirrors. Required for lockpicking by thieves; helpful for anyone.");
            tools.Effect = "lockpick"; tools.Metal = true;
            var sym = I("holy_symbol", "holy symbol", IK.Tool, '(', new Color(230, 210, 120), 5, 25, 2, 1, "A silver symbol of your faith. Clerics and paladins who carry it turn undead more surely.");
            sym.Effect = "symbol"; sym.Metal = true;
            var mirror = I("mirror", "steel mirror", IK.Tool, '(', new Color(210, 220, 230), 5, 10, 3, 1, "A polished steel mirror. Apply it toward a creature with a petrifying gaze, and pray.");
            mirror.Effect = "mirror"; mirror.Metal = true;
            var deck = I("deck", "deck of many things", IK.Tool, '(', new Color(230, 200, 255), 1, 10000, 1, 5, "A deck of ivory plaques. Each card drawn changes everything. Each card drawn could end everything.");
            deck.Effect = "deck"; deck.Magic = true;
            var horn = I("horn", "horn of blasting", IK.Tool, '(', new Color(220, 190, 120), 20, 3000, 1, 6, "A horn that shatters stone and eardrums: a cone of sonic force.");
            horn.Effect = "horn"; horn.Magic = true; horn.Charges = Dice.Parse("1d4+2");
            var bot = I("bag_tricks", "bag of tricks", IK.Tool, '(', new Color(180, 140, 100), 5, 2500, 1, 4, "Reach in and pull out a small, furry, angry ally.");
            bot.Effect = "tricks"; bot.Magic = true; bot.Charges = Dice.Parse("2d4+2");
            var boh = I("bag_holding", "bag of holding", IK.Tool, '(', new Color(150, 110, 80), 15, 5000, 1, 4, "Bigger on the inside. While carried, your pack weighs much less.");
            boh.Effect = "holding"; boh.Magic = true;
            var ball = I("crystal_ball", "crystal ball", IK.Tool, '(', new Color(170, 220, 255), 70, 4000, 1, 7, "Gaze into it to see the whole level and everything upon it.");
            ball.Effect = "crystal"; ball.Magic = true; ball.Charges = Dice.Parse("1d4+2");
            var nec = I("necklace_missiles", "necklace of missiles", IK.Tool, '(', new Color(255, 160, 60), 5, 4000, 1, 5, "Beads of orange fire on a chain. Each is a fireball waiting to be thrown.");
            nec.Effect = "necklace"; nec.Magic = true; nec.Charges = Dice.Parse("1d4+3");
            var fig = I("figurine", "onyx dog figurine", IK.Tool, '(', new Color(60, 60, 70), 5, 3000, 1, 5, "A carved onyx dog. Speak the word and it becomes a hound of black stone, loyal for a time.");
            fig.Effect = "figurine"; fig.Magic = true; fig.Charges = Dice.Parse("1d3+2");

            var book = I("spellbook", "spellbook", IK.Book, '+', new Color(150, 120, 220), 50, 300, 3, 1, "A book of magic-user spells.");
            book.Magic = true;

            I("gold", "gold piece", IK.Gold, '$', new Color(255, 210, 60), 0, 1, 0, 1, "Gold. In this dungeon, it is also experience.");

            string[] gemNames = { "ruby", "emerald", "sapphire", "diamond", "amethyst", "garnet", "topaz", "opal", "black pearl", "jet", "bloodstone", "star sapphire" };
            int[] gemValues = { 1000, 1000, 1000, 5000, 100, 100, 500, 1000, 500, 100, 50, 5000 };
            Color[] gemColors = { new Color(230, 30, 60), new Color(40, 200, 90), new Color(50, 90, 240), new Color(240, 250, 255), new Color(170, 80, 210), new Color(170, 30, 40), new Color(250, 200, 60), new Color(220, 240, 230), new Color(60, 50, 80), new Color(30, 30, 30), new Color(110, 40, 40), new Color(80, 120, 255) };
            for (int i = 0; i < gemNames.Length; i++)
            {
                var g = I("gem_" + i, gemNames[i], IK.Gem, '*', gemColors[i], 1, gemValues[i], gemValues[i] >= 5000 ? 1 : gemValues[i] >= 1000 ? 2 : 4, gemValues[i] >= 1000 ? 4 : 1, "A precious stone.");
                g.Stack = true;
            }
            var glass = I("glass", "worthless piece of glass", IK.Gem, '*', new Color(150, 220, 150), 1, 0, 4, 1, "Glass. It looked like a gem in the dark.");
            glass.Stack = true;

            var chest = I("chest", "chest", IK.Chest, '■', new Color(170, 120, 60), 350, 10, 0, 1, "A wooden chest bound with iron.");
            var heart = I("heart", "Icosahedral Heart", IK.Heart, '◊', new Color(255, 80, 120), 10, 50000, 0, 99, "A fist-sized twenty-sided gem, warm and beating. Each face shows a number; each number is a fate. While you carry it, death must roll against you.");
            heart.Magic = true; heart.Artifact = true;

            MagicW("flame_tongue", "Flame Tongue", "long_sword", 1, 5000, 6, "flame", "A long sword whose blade bursts into flame at a word. Burns for 1d6 extra and lights the way. Trolls hate it in particular.");
            MagicW("frost_brand", "Frost Brand", "broad_sword", 3, 6000, 8, "frost", "A frost-rimed broad sword. Cold strikes for 1d6 extra.");
            MagicW("sharpness", "Sword of Sharpness", "long_sword", 2, 7000, 9, "sharp", "So sharp it parts thought. A roll of 19 or 20 deals triple damage.");
            MagicW("dragonslayer", "Dragonslayer", "two_handed_sword", 2, 8000, 8, "dragonslayer", "A blade quenched in dragon blood. Triple damage against dragons.");
            MagicW("holy_avenger", "Holy Avenger", "long_sword", 2, 10000, 10, "holy", "In a paladin's hand, it is +5 and a beacon. In anyone else's, merely very good.");
            MagicW("disruption", "Mace of Disruption", "mace", 1, 7000, 7, "disruption", "Undead struck by this mace must save or be blasted to dust.");
            MagicW("dwarven_thrower", "Dwarven Thrower", "war_hammer", 3, 6000, 6, "thrower", "Thrown, it deals double damage (triple to giants) and returns to your hand.");
            MagicW("vorpal", "Vorpal Sword", "long_sword", 3, 15000, 12, "vorpal", "Snicker-snack. On a natural 20, it takes the head.");
            MagicW("nine_lives", "Nine Lives Stealer", "long_sword", 2, 9000, 9, "ninelives", "An evil blade. A natural 20 steals the life of its victim outright.");
            MagicW("oathbow", "Oathbow", "long_bow", 3, 8000, 8, "oath", "An elven bow that whispers vengeance. Arrows fly true and hit hard.");
            MagicW("berserk_sword", "sword of berserking", "long_sword", -2, 1, 3, "berserk", "Cursed. Once drawn, it cannot be put away, and you will not retreat.");
            MagicW("sun_blade", "Sun Blade", "bastard_sword", 2, 9000, 10, "sun", "A hilt of golden light and a blade like sunrise. Undead take double damage.");
            All["sun_blade"].WFlags = WF.None;
            All["sun_blade"].LightRadius = 3;
            All["flame_tongue"].LightRadius = 2;

            foreach (var id in SpellDb.All.Keys)
            {
                var sd = SpellDb.All[id];
                if (sd.MonsterOnly) continue;
                var d = I("scroll_" + id, "scroll of " + sd.Name, IK.Scroll, '?', sd.Cleric ? new Color(255, 245, 190) : new Color(200, 180, 255), 3, 100 * sd.Level, sd.Level <= 3 ? 3 : sd.Level <= 5 ? 2 : 1, Math.Max(1, sd.Level * 2 - 2), "A spell scroll: " + sd.Desc);
                d.Spell = id;
                d.SpellLevel = sd.Level;
                d.Magic = true;
                d.Effect = "spell";
            }
        }

        public static string BookTitle()
        {
            string[] a = { "Grimoire", "Codex", "Tome", "Folio", "Manual", "Libram", "Book", "Treatise" };
            string[] b = { "the Violet Hour", "Seven Candles", "Unlikely Geometries", "the Drowned Sage", "Moths", "Borrowed Lightning", "Patient Fire", "Quiet Doors", "the Twelfth Moon", "Inkless Words" };
            return a[Rng.Int(a.Length)] + " of " + b[Rng.Int(b.Length)];
        }
    }
}
