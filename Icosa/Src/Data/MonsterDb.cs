using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class MonsterDb
    {
        public static readonly Dictionary<string, MonsterDef> All = new Dictionary<string, MonsterDef>();
        public static List<MonsterDef> List => All.Values.ToList();

        static MonsterDef M(string id, string name, char g, Color c, string hd, int ac, int move, int depth, int freq, string attacks, MF flags, string desc, Size size = Size.Medium)
        {
            var d = new MonsterDef { Id = id, Name = name, G = g, C = c, AC = ac, Move = move, Depth = depth, Freq = freq, Attacks = Attack.ParseList(attacks), Flags = flags, Desc = desc, Size = size };
            if (hd == "1/2") { d.HalfHD = true; d.HD = 0; }
            else
            {
                int p = hd.IndexOfAny(new[] { '+', '-' });
                if (p >= 0) { d.HD = int.Parse(hd.Substring(0, p)); d.HpPlus = int.Parse(hd.Substring(p)); }
                else d.HD = int.Parse(hd);
            }
            if (d.Has(MF.Undead)) d.Flags |= MF.ResSleep | MF.ResPoison;
            if (d.Has(MF.Mindless)) d.Flags |= MF.ResSleep;
            if (d.Has(MF.Evil) && d.Alignment == Align.N) d.Alignment = Align.CE;
            All[id] = d;
            return d;
        }

        public static MonsterDef Get(string id) => All.TryGetValue(id, out var d) ? d : null;

        static readonly MF Humanoid = MF.Hands | MF.Warm;

        static MonsterDb()
        {
            var C = new Func<int, int, int, Color>((r, g, b) => new Color(r, g, b));

            M("rat", "giant rat", 'r', C(150, 110, 70), "1/2", 7, 12, 1, 10, "1d3 bite disease@5", MF.Animal | MF.Warm | MF.Coward | MF.Pack,
                "A rat the size of a terrier, its whiskers slick with sewer-wisdom. Where there is one, there is a census.").Group(3, 7).Mor(5);
            M("kobold", "kobold", 'k', C(190, 90, 60), "1/2", 7, 9, 1, 10, "1d4 hit", Humanoid | MF.Giant | MF.Evil | MF.Infra | MF.Coward,
                "Small, scaly, and deeply committed to traps they forgot the locations of. Kobolds yap in a language that is mostly grievances.").Group(3, 8).Mor(6).Al(Align.LE);
            M("goblin", "goblin", 'g', C(150, 160, 70), "1-1", 6, 9, 1, 9, "1d6 hit", Humanoid | MF.Giant | MF.Evil | MF.Infra,
                "Flat-faced and yellow-eyed, goblins live in the dark on the theory that nobody can see their teeth there.").Group(2, 6).Mor(7).Al(Align.LE);
            M("centipede", "giant centipede", 's', C(200, 160, 60), "1/2", 9, 15, 1, 6, "1d2 bite poison", MF.Animal,
                "Forty legs and one terrible idea. Its venom is weak, but it does insist.").Mor(7);
            M("jackal", "jackal", 'd', C(190, 150, 90), "1/2", 7, 12, 1, 7, "1d2 bite", MF.Animal | MF.Warm | MF.Coward | MF.Pack,
                "Lean scavengers who follow adventurers the way gulls follow ships.").Group(3, 6).Mor(6);
            M("beetle", "giant fire beetle", 'a', C(230, 90, 40), "1+2", 4, 12, 2, 6, "2d4 bite", MF.Animal,
                "Two glands above its eyes glow a stubborn orange. Miners prize them; beetles disagree.").Glows(C(255, 120, 40), 2).Drops("gland", 70);
            M("stirge", "stirge", 'x', C(200, 80, 90), "1", 8, 15, 2, 5, "1d3 bite blood@50", MF.Animal | MF.Fly | MF.Warm,
                "A feathered proboscis with a mosquito's ambition and a bird's wingspan. It latches on and drinks.").Group(1, 3).Mor(7);
            M("skeleton", "skeleton", 'Z', C(225, 220, 200), "1", 7, 12, 1, 7, "1d6 hit", MF.Undead | MF.Mindless | MF.ResCold | MF.Edged | MF.Hands | MF.NoCorpse,
                "Bones held together by somebody else's spite. Blades slide between its ribs; maces do not.").Turn(1).Mor(12).Group(1, 4);
            M("bat", "giant bat", 'b', C(140, 110, 90), "1/2", 8, 24, 1, 5, "1d2 bite", MF.Animal | MF.Fly | MF.Warm | MF.Erratic,
                "It flies as if the air were full of invisible furniture.").Group(1, 4).Mor(6);
            M("bandit", "bandit", '@', C(170, 130, 90), "1", 7, 12, 1, 6, "1d6 hit steal@20", Humanoid | MF.Evil,
                "An unemployed adventurer who has found a more direct way to acquire treasure: yours.").Group(2, 4).Mor(7).Al(Align.CE).Drops("random", 35);
            M("paper_golem", "paper golem", '\'', C(240, 235, 215), "2", 9, 12, 1, 4, "1d3 slash bleed@30", MF.Mindless | MF.VulnFire | MF.NoCorpse | MF.Hands,
                "A golem folded from the pages of dead wizards' spellbooks. Faded names drift across its body in old ink: Gorbash, Elira, Tim the Fourth.").Mor(12).Drops("scroll", 50);

            M("orc", "orc", 'o', C(120, 160, 70), "1", 6, 9, 2, 10, "1d8 hit", Humanoid | MF.Giant | MF.Orc | MF.Evil | MF.Infra,
                "Pig-snouted raiders who hold that the only bad fight is the one they lost. Their banners are mostly teeth.").Group(3, 8).Mor(8).Al(Align.LE).Drops("random", 15);
            M("orc_archer", "orc archer", 'o', C(160, 190, 90), "1", 6, 9, 3, 5, "1d6 hit", Humanoid | MF.Giant | MF.Orc | MF.Evil | MF.Infra,
                "An orc who has discovered that killing at a distance is still killing.").Shoots("1d6", "arrow", ')', C(200, 170, 120), 8, 50).Group(1, 3).Mor(7).Drops("arrows", 50);
            M("hobgoblin", "hobgoblin", 'g', C(220, 120, 50), "1+1", 5, 9, 2, 8, "1d8 hit", Humanoid | MF.Giant | MF.Evil | MF.Infra,
                "Goblins who went to military school. They march in step even when fleeing.").Group(2, 6).Mor(9).Al(Align.LE).Drops("random", 15);
            M("apprentice", "dark apprentice", '@', C(170, 110, 220), "1", 9, 12, 2, 4, "1d4 hit", Humanoid | MF.Evil,
                "A failed student of the Art who kept the robe and lost the scruples.").Casts(40, SpellId.MagicMissile, SpellId.Sleep).Mor(6).Al(Align.NE).Drops("scroll", 40);
            M("darkmantle", "darkmantle", 'q', C(90, 90, 110), "1+1", 7, 9, 2, 4, "1d4 squeeze", MF.Animal | MF.Ambush | MF.Shadowy,
                "It hangs from the ceiling like a stalactite with opinions, and it hates your torch personally.").Mor(9);
            M("shrieker", "shrieker", 'F', C(190, 140, 200), "3", 7, 0, 2, 4, "", MF.Immobile | MF.Mindless | MF.Shrieker | MF.NoCorpse,
                "A man-high mushroom that screams when light touches it. It has never been invited to a party twice.").Mor(12);
            M("piercer", "piercer", 'p', C(140, 130, 120), "3", 3, 1, 3, 4, "3d6 drop", MF.Ambush | MF.Animal,
                "Looks exactly like a stalactite. Is not.").Mor(10);
            M("acolyte", "acolyte of the Iron Crown", '@', C(190, 60, 60), "2", 5, 12, 3, 4, "1d6 hit", Humanoid | MF.Evil,
                "A shaven-headed zealot whose prayers are mostly about you, specifically.").Casts(40, SpellId.CauseLight, SpellId.CHoldPerson, SpellId.Darkness2).Mor(9).Al(Align.LE).Drops("potion", 30);
            M("zombie", "zombie", 'Z', C(130, 160, 110), "2", 8, 6, 2, 8, "1d8 hit", MF.Undead | MF.Mindless | MF.Hands,
                "Slow, relentless, and smelling of the bottom of a well. It remembers only hunger and doors.").Turn(2).Mor(12).Group(1, 4);
            M("wolf", "wolf", 'd', C(150, 150, 150), "2+2", 7, 18, 3, 6, "2d4 bite", MF.Animal | MF.Warm | MF.Pack,
                "Grey hunters who coordinate better than most adventuring parties.").Group(2, 5).Mor(8);
            M("spider", "huge spider", 's', C(140, 110, 90), "2+2", 6, 18, 3, 6, "1d6 bite poison", MF.Animal,
                "A hairy hunter that leaps rather than weaves.").Mor(8);
            M("ant", "giant ant", 'a', C(170, 50, 40), "2", 3, 18, 3, 6, "1d6 bite", MF.Animal,
                "Armored, tireless, and convinced that you are a crumb.").Group(2, 6).Mor(10);
            M("violet_fungus", "violet fungus", 'F', C(160, 80, 200), "3", 7, 1, 3, 4, "1d4 touch acid;1d4 touch acid", MF.Mindless | MF.NoCorpse,
                "Its tendrils rot flesh on contact. It moves, but only with great reluctance.").Mor(12);
            M("green_slime", "green slime", 'P', C(110, 220, 60), "2", 9, 0, 3, 3, "0 touch slime", MF.Mindless | MF.Immobile | MF.Ambush | MF.ResCold | MF.NoCorpse | MF.VulnFire,
                "A sheet of bright green ooze that turns flesh into more of itself. Burn it. Burn yourself if you have to.").Mor(12);
            M("gnoll", "gnoll", 'G', C(200, 170, 100), "2", 5, 9, 3, 8, "2d4 hit", Humanoid | MF.Giant | MF.Evil | MF.Infra,
                "Hyena-headed marauders. Their laughter is the last joke many travelers hear.").Group(2, 6).Mor(8).Al(Align.CE).Drops("random", 20);

            M("bugbear", "bugbear", 'g', C(160, 110, 60), "3+1", 5, 9, 4, 6, "2d4 hit", Humanoid | MF.Giant | MF.Evil | MF.Infra | MF.Ambush,
                "Huge, hairy, and silent as a held breath. Bugbears surprise people for a living.").Group(1, 4).Mor(9).Al(Align.CE).Drops("random", 25);
            M("ghoul", "ghoul", 'Z', C(180, 190, 140), "2", 6, 9, 4, 6, "1d3 claw paralyze;1d3 claw paralyze;1d6 bite paralyze", MF.Undead | MF.Hands | MF.Evil,
                "A gaunt thing that eats the dead and paralyzes the living to save time. Elves are immune; ghouls find this rude.").Turn(3).Group(1, 4).Mor(11);
            M("wererat", "wererat", 'r', C(150, 140, 130), "3", 6, 12, 4, 4, "1d8 bite", MF.NeedSilver | MF.Warm | MF.Hands | MF.Evil | MF.Regen,
                "A man who is sometimes a rat, or a rat who is sometimes a lawyer. Only silver or magic will cut it.").With("rat").Mor(8).Al(Align.LE);
            M("ogre", "ogre", 'O', C(180, 140, 90), "4+1", 5, 9, 4, 6, "1d10 hit", Humanoid | MF.Giant | MF.Evil,
                "Nine feet of muscle and appetite. Ogres keep treasure because it is shiny and enemies because they are crunchy.", Size.Large).Mor(8).Al(Align.CE).Drops("gold", 60);
            M("duergar", "duergar", 'h', C(130, 130, 140), "1+2", 4, 9, 4, 5, "1d8 hit", Humanoid | MF.Evil | MF.Infra,
                "Grey dwarves of the deep mines. Grim, greedy, and unpleasantly good at war.").Group(2, 4).Mor(9).Al(Align.LE).Drops("random", 25);
            M("mimic", "mimic", 'm', C(170, 120, 60), "7", 7, 3, 4, 4, "3d4 hit sticky", MF.Mimic | MF.Warm,
                "It was a chest. Then it had teeth. The lesson is that chests should not have teeth, and this one did.").Mor(10).Drops("gold", 100);
            M("wanderer", "Wanderer", '?', C(220, 220, 220), "4", 5, 12, 4, 3, "1d10 hit", MF.Morph | MF.Hands,
                "A thing that walks the deep roads between the levels, wearing whatever shape it last killed. It is never the same thing twice.").Mor(9);
            M("lizardman", "lizard man", 'l', C(80, 160, 90), "2+1", 5, 9, 5, 6, "1d3 claw;1d3 claw;1d8 bite", MF.Hands | MF.Swim,
                "Cold-blooded swamp warriors who view mammals as poorly organized meat.").Group(2, 5).Mor(9);
            M("carrion", "carrion crawler", 'C', C(150, 190, 110), "3+1", 3, 12, 5, 5, "1d2 tentacle paralyze;1d2 tentacle paralyze;1d2 bite", MF.Animal,
                "A segmented scavenger with a crown of paralyzing tentacles. It prefers its meals to hold very still.", Size.Large).Mor(9);
            M("gel_cube", "gelatinous cube", '■', C(120, 220, 210), "4", 8, 6, 5, 4, "2d4 touch paralyze", MF.Mindless | MF.Transparent | MF.ResElec | MF.ResCold | MF.NoCorpse,
                "A ten-foot cube of clear jelly that hoovers corridors clean. Bones and coins hang suspended in it like fruit in aspic.", Size.Large).Mor(12).Drops("random", 70);
            M("rust_monster", "rust monster", 'R', C(200, 110, 50), "5", 2, 18, 5, 4, "0 touch rust;0 touch rust", MF.Animal | MF.EatsMetal,
                "An armadillo with feathery antennae and a pathological hunger for iron. Fighters weep at its approach.").Mor(8);
            M("shadow", "shadow", 'W', C(70, 70, 90), "3+3", 7, 12, 5, 5, "1d4 touch strdrain", MF.Undead | MF.NeedMagic | MF.Shadowy | MF.Evil | MF.ResCold | MF.NoCorpse,
                "A patch of darkness that slipped its owner. Its touch drains strength. Ordinary steel passes through it like a rumor.").Turn(4).Mor(12).Group(1, 3);
            M("harpy", "harpy", 'B', C(200, 140, 160), "3", 7, 15, 5, 5, "1d3 claw;1d3 claw;1d6 hit", MF.Fly | MF.Hands | MF.Evil | MF.Warm,
                "Vulture-winged crones whose song tugs at your feet. Their manners are worse than their singing, and their singing is wonderful.").Casts(30, SpellId.Command).Mor(7).Group(1, 3);
            M("worg", "worg", 'd', C(70, 60, 50), "3+3", 6, 18, 5, 5, "2d4 bite", MF.Animal | MF.Warm | MF.Evil | MF.Pack,
                "Wolves who have read their own press. Goblins ride them; they consider it charity.").Group(2, 4).Mor(9);
            M("ink_wraith", "ink wraith", 'W', C(60, 90, 200), "4", 5, 12, 6, 4, "1d6 touch smudge", MF.Undead | MF.NeedSilver | MF.Evil | MF.NoCorpse | MF.Fly,
                "A drowned cartographer, all blots and wet edges. Its touch smears the map you carry in your head.").Turn(5).Mor(11);
            M("ochre_jelly", "ochre jelly", 'P', C(200, 150, 60), "6", 8, 3, 6, 4, "3d4 touch acid", MF.Mindless | MF.Splits | MF.NoCorpse | MF.ResAcid,
                "An amoeba the color of old mustard. Lightning splits it; patience does not.").Mor(12);
            M("cockatrice", "cockatrice", 'c', C(230, 220, 100), "5", 6, 15, 6, 4, "1d3 bite petrify@25", MF.Animal | MF.Fly | MF.Warm,
                "A rooster that hatched from a serpent's spite. Its peck turns flesh to stone.").Mor(7);
            M("owlbear", "owlbear", 'Y', C(160, 120, 80), "5+2", 5, 12, 6, 5, "1d6 claw;1d6 claw;2d6 bite hug@30", MF.Animal | MF.Warm,
                "Nobody knows which wizard did this, but everybody agrees they should stop.", Size.Large).Mor(10);
            M("giant_spider", "giant spider", 's', C(60, 60, 60), "4+4", 4, 15, 6, 5, "2d4 bite poison2", MF.Animal | MF.Webber,
                "Black as a closed eye. Its webs fill corridors and its venom fills graves.", Size.Large).Mor(9);

            M("troll", "troll", 'T', C(90, 150, 80), "6+6", 4, 12, 7, 6, "1d4+4 claw;1d4+4 claw;2d6 bite", MF.Regen | MF.Revives | MF.Giant | MF.Warm | MF.Hands | MF.Evil | MF.VulnFire,
                "Rubbery green and relentlessly alive. Cut it to pieces and the pieces get up. Only fire or acid finishes the argument.", Size.Large).Mor(10).Al(Align.CE);
            M("wight", "wight", 'W', C(200, 200, 170), "4+3", 5, 12, 7, 5, "1d4 touch drain", MF.Undead | MF.NeedSilver | MF.Evil | MF.Hands | MF.ResCold,
                "A barrow-king's spite in a corpse's body. Its touch drinks your experience, one life-lesson at a time.").Turn(5).Mor(11);
            M("knucklebone_golem", "knucklebone golem", '\'', C(240, 240, 250), "7", 4, 9, 7, 4, "1d20 slam", MF.Mindless | MF.NoCorpse | MF.ResPoison | MF.ResSleep,
                "Thousands of carved knucklebone dice, fused by a gambler-god's curse into a shambling giant. It rattles when it walks. Every blow is a throw of fate.", Size.Large).Mor(12).Drops("gem", 60);
            M("young_white", "young white dragon", 'D', C(230, 240, 255), "6", 3, 12, 7, 3, "1d4 claw;1d4 claw;2d8 bite", MF.Fly | MF.ResCold | MF.Warm | MF.Evil,
                "A small dragon with a cold heart and a short attention span. Still a dragon.", Size.Large).Breathes(Elem.Cold, "4d6", 6, true, "frost").Mor(9).Drops("gold", 100).Al(Align.CE);
            M("wraith_bat", "vampire bat", 'b', C(120, 50, 60), "1+1", 8, 18, 6, 3, "1d4 bite blood", MF.Animal | MF.Fly | MF.Warm | MF.Erratic,
                "A bat that has acquired standards in its diet.").Group(2, 5).Mor(7);
            M("doppelganger", "doppelganger", '@', C(200, 200, 200), "4", 5, 9, 7, 3, "1d12 hit", MF.Hands | MF.Morph,
                "A grey, faceless shapeshifter. It wears faces like you wear hats.").Mor(9);
            M("minotaur", "minotaur", 'H', C(150, 90, 50), "6+3", 6, 12, 8, 4, "2d4 gore;1d10 hit", MF.Warm | MF.Hands | MF.Evil,
                "Bull-headed, maze-born, and never lost. It always knows where you are.", Size.Large).Mor(11).Drops("random", 30);
            M("displacer", "displacer beast", 'f', C(110, 80, 160), "6", 4, 15, 8, 4, "2d4 tentacle;2d4 tentacle", MF.Displace | MF.Warm | MF.Animal | MF.Evil,
                "A six-legged panther that is never quite where it appears to be. Its pelt shimmers like heat over a road.", Size.Large).Mor(9);
            M("basilisk", "basilisk", 'c', C(80, 180, 90), "6+1", 4, 6, 8, 3, "1d10 bite", MF.Animal | MF.Warm,
                "An eight-legged lizard whose gaze turns flesh to stone. Its lair is a garden of startled statues.", Size.Large).GazeAt(AtkSp.Petrify).Mor(9);
            M("hell_hound", "hell hound", 'd', C(220, 60, 30), "5", 4, 12, 8, 4, "1d10 bite", MF.ResFire | MF.Warm | MF.Evil | MF.Pack | MF.Heat,
                "A rust-red hound that pants embers. It breathes fire the way other dogs breathe.").Breathes(Elem.Fire, "3d6", 4, false, "fire").Group(1, 3).Mor(10).Glows(C(255, 90, 20), 1);
            M("medusa", "medusa", '@', C(90, 200, 120), "6", 5, 9, 9, 3, "1d4 bite poison2", MF.Hands | MF.Evil | MF.Warm,
                "Her hair hisses. Her gaze petrifies. Her archery is, unfortunately, also excellent.").GazeAt(AtkSp.Petrify).Shoots("1d6", "arrow", ')', C(200, 170, 120), 8, 30).Mor(9).Drops("random", 60).Al(Align.LE);
            M("wraith", "wraith", 'W', C(40, 40, 50), "5+3", 4, 12, 9, 4, "1d6 touch drain", MF.Undead | MF.NeedSilver | MF.Evil | MF.Fly | MF.ResCold | MF.NoCorpse | MF.Shadowy,
                "A black-robed absence with eyes like candle stubs. It feeds on what you have learned.").Turn(7).Mor(12);
            M("mummy", "mummy", 'M', C(200, 180, 130), "6+3", 3, 6, 9, 4, "1d12 hit disease", MF.Undead | MF.Evil | MF.Hands | MF.VulnFire | MF.FearAura | MF.ResCold,
                "Wrapped in linen and spite. Its touch brings a rotting disease; its presence brings fear.").Turn(8).Mor(12);
            M("hill_giant", "hill giant", 'H', C(170, 140, 100), "8+2", 4, 12, 9, 4, "2d8 hit", MF.Giant | MF.Hands | MF.Warm | MF.Evil,
                "Twelve feet of dim cruelty. It throws boulders the way you would throw a tantrum.", Size.Large).Shoots("2d8", "boulder", '*', C(150, 140, 130), 8, 35).Mor(9).Drops("gold", 80);
            M("manticore", "manticore", 'X', C(220, 130, 60), "6+3", 4, 12, 9, 4, "1d3 claw;1d3 claw;1d8 bite", MF.Fly | MF.Warm | MF.Evil,
                "A lion with a man's face and a tail full of iron spikes it flings in volleys.", Size.Large).Shoots("1d6", "tail spike", '-', C(180, 180, 180), 8, 45, AtkSp.None, 3).Mor(9);
            M("young_black", "young black dragon", 'D', C(70, 70, 70), "7", 3, 12, 9, 3, "1d4 claw;1d4 claw;3d6 bite", MF.Fly | MF.ResAcid | MF.Warm | MF.Evil | MF.Swim,
                "It smells of swamp and old coins. Its breath is a jet of acid.", Size.Large).Breathes(Elem.Acid, "5d6", 7, false, "acid").Mor(9).Drops("gold", 100);
            M("young_green", "young green dragon", 'D', C(60, 170, 70), "7", 2, 12, 9, 3, "1d6 claw;1d6 claw;2d10 bite", MF.Fly | MF.ResPoison | MF.Warm | MF.Evil,
                "A cunning reptile that negotiates in bad faith and breathes chlorine.", Size.Large).Breathes(Elem.Poison, "5d6", 5, true, "poison gas").Mor(9).Drops("gold", 100);

            M("young_blue", "young blue dragon", 'D', C(70, 120, 240), "8", 2, 12, 10, 3, "1d6 claw;1d6 claw;3d8 bite", MF.Fly | MF.ResElec | MF.Warm | MF.Evil,
                "A vain desert wyrm with lightning in its throat.", Size.Large).Breathes(Elem.Elec, "6d6", 8, false, "lightning").Mor(9).Drops("gold", 100);
            M("wisp", "will-o'-wisp", '•', C(170, 210, 255), "9", -8, 18, 10, 3, "2d8 touch shock", MF.Fly | MF.Invis | MF.ResElec | MF.NoCorpse | MF.Evil | MF.Coward,
                "A pale light that bobs ahead of travelers, leading them into deep places. It feeds on fear and fat.").Glows(C(140, 180, 255), 2).Mor(8);
            M("umber_hulk", "umber hulk", 'U', C(120, 90, 50), "8+8", 2, 6, 10, 4, "3d4 claw;3d4 claw;2d5 bite", MF.Burrow | MF.Warm | MF.Evil,
                "A beetle-giant that chews through stone. Its four eyes confuse anyone who meets their gaze.", Size.Large).GazeAt(AtkSp.Confuse).Mor(10);
            M("drow", "drow warrior", '@', C(160, 110, 210), "2", 4, 12, 9, 6, "1d8 hit", Humanoid | MF.Evil | MF.ResSleep | MF.Infra,
                "Dark elves of the Underdark, cruel and elegant. Their crossbow bolts are tipped with sleep.").Shoots("1d3", "hand crossbow bolt", '-', C(170, 170, 200), 8, 40, AtkSp.Sleep).Casts(15, SpellId.Darkness2, SpellId.FaerieFire).Group(2, 4).Mor(9).Al(Align.CE).Drops("random", 30);
            M("drow_priestess", "drow priestess", '@', C(220, 90, 220), "5", 2, 12, 11, 3, "1d8 hit poison", Humanoid | MF.Evil | MF.ResSleep | MF.Infra,
                "A high priestess of the Spider. Her whip has snake heads; her prayers have consequences.").Casts(45, SpellId.CauseSerious, SpellId.CHoldPerson, SpellId.Darkness2, SpellId.FlameStrike).With("drow").Mor(10).Al(Align.CE).Drops("random", 60);
            M("flux_wraith", "flux wraith", '?', C(255, 90, 90), "6", 3, 12, 10, 3, "1d8 touch flux", MF.NoCorpse | MF.Phase | MF.NeedMagic,
                "A flickering red shape of raw chaos. Its touch scrambles the body: strength becomes charm, wisdom becomes speed, and nothing sits where it should.").Mor(10);
            M("black_pudding", "black pudding", 'P', C(40, 30, 40), "10", 6, 3, 11, 3, "3d8 touch acid", MF.Mindless | MF.Splits | MF.NoCorpse | MF.ResCold | MF.ResAcid,
                "A ton of black ooze that dissolves wood, metal and confidence. Blades split it into smaller, angrier puddings.", Size.Large).Mor(12);
            M("mind_flayer", "mind flayer", 'h', C(170, 110, 170), "8+4", 5, 12, 11, 3, "1d4 tentacle brain;1d4 tentacle brain", Humanoid | MF.Evil,
                "Purple-skinned and octopus-faced. It stuns you with a blast of thought and then eats the thinker.").Casts(35, SpellId.MindBlast).Mor(9).Al(Align.LE).Drops("random", 60);
            M("spectre", "spectre", 'W', C(220, 230, 240), "7+3", 2, 15, 11, 3, "1d8 touch drain", MF.Undead | MF.NeedMagic | MF.Fly | MF.Evil | MF.Phase | MF.NoCorpse | MF.ResCold,
                "A luminous ghost of terrible hunger. It walks through walls and through your memories.").Turn(9).Mor(12).Glows(C(180, 200, 230), 1);
            M("young_red", "young red dragon", 'D', C(230, 50, 30), "9", 1, 12, 11, 3, "1d8 claw;1d8 claw;3d8 bite", MF.Fly | MF.ResFire | MF.Warm | MF.Evil | MF.Heat,
                "A vain, greedy, magnificent furnace with wings. It thinks you are a snack with a vocabulary.", Size.Large).Breathes(Elem.Fire, "7d6", 6, true, "fire").Mor(10).Drops("gold", 100).Glows(C(255, 90, 30), 1);
            M("stone_golem", "stone golem", '\'', C(150, 150, 150), "12", 5, 6, 12, 3, "3d8 slam", MF.Mindless | MF.NeedMagic | MF.NoCorpse | MF.ResFire | MF.ResCold | MF.ResElec | MF.ResPoison,
                "A statue that decided to stop being polite. Spells slide off it like rain off a roof.", Size.Large).Mor(12);
            M("vampire", "vampire", 'V', C(180, 30, 50), "8+3", 1, 12, 12, 3, "1d6+4 bite drain", MF.Undead | MF.NeedMagic | MF.Regen | MF.Evil | MF.Hands | MF.ResCold,
                "An aristocrat of the grave. It drains life with a kiss and commands rats with a glance.").Turn(10).With("rat").Casts(25, SpellId.CharmPerson).Mor(11).Al(Align.CE).Drops("random", 80);
            M("vrock", "vrock", '&', C(140, 150, 110), "8", 0, 12, 12, 3, "1d4 claw;1d4 claw;1d8 talon;1d8 talon;1d6 beak", MF.Evil | MF.NeedMagic | MF.Fly | MF.ResFire | MF.ResElec,
                "A vulture-demon of the Abyss. It dances in circles, and the dance kills.", Size.Large).Mor(10);
            M("beholder", "beholder", 'e', C(200, 120, 140), "11", 0, 3, 13, 2, "2d4 bite", MF.Evil | MF.Fly | MF.Warm,
                "A floating orb of paranoia: one great eye, ten small eyes on stalks, and each eye a different way to die.", Size.Large).Casts(70, SpellId.EyeRays).Mor(11).Drops("random", 100).Al(Align.LE);
            M("purple_worm", "purple worm", 'w', C(150, 60, 180), "15", 6, 9, 13, 2, "2d12 bite swallow@30;2d4 sting poison2", MF.Animal | MF.Burrow,
                "A purple tube of appetite a hundred feet long. It swallows adventurers whole and digests them leisurely.", Size.Large).Mor(10).Drops("gem", 80);
            M("salamander", "salamander", 'N', C(240, 110, 40), "7+7", 5, 9, 13, 3, "2d6 spear fire;1d6 tail fire", MF.ResFire | MF.Evil | MF.Hands | MF.Heat,
                "A serpent-bodied creature of living flame. Its spear is red-hot; so is its temper.").Glows(C(255, 120, 40), 2).Mor(10);
            M("fire_giant", "fire giant", 'H', C(200, 60, 40), "11+3", 3, 12, 13, 3, "5d6 hit", MF.Giant | MF.Hands | MF.Warm | MF.ResFire | MF.Evil,
                "Soot-black skin and hair like a forge. It hurls boulders it heated first, for flavor.", Size.Large).Shoots("2d10", "molten boulder", '*', C(255, 120, 40), 8, 35).Mor(10).Drops("gold", 100).Al(Align.LE);
            M("efreeti", "efreeti", '&', C(240, 80, 40), "10", 2, 12, 13, 2, "3d8 hit fire", MF.ResFire | MF.Fly | MF.Evil | MF.Hands | MF.Heat,
                "A genie of the City of Brass, arrogant and smoldering. It grants wishes only to its enemies' enemies.", Size.Large).Casts(25, SpellId.WallOfFire, SpellId.Invisibility).Glows(C(255, 100, 40), 2).Mor(10).Al(Align.LE);
            M("hezrou", "hezrou", '&', C(110, 140, 90), "9", -2, 9, 13, 2, "1d3 claw;1d3 claw;4d4 bite", MF.Evil | MF.NeedMagic | MF.FearAura | MF.ResFire | MF.ResElec,
                "A toad-demon the size of a cart, wreathed in a stench that makes heroes reconsider.", Size.Large).Casts(20, SpellId.Darkness2, SpellId.Fear).Mor(10);
            M("glabrezu", "glabrezu", '&', C(200, 60, 60), "10", -4, 9, 14, 2, "2d6 pincer;2d6 pincer;1d3 claw;1d3 claw;1d4+1 bite", MF.Evil | MF.NeedMagic | MF.ResFire | MF.ResElec,
                "Four arms, two of them pincers, and a gift for twisting wishes. It tempts, then tears.", Size.Large).Casts(25, SpellId.Darkness2, SpellId.Fear, SpellId.ConeOfCold).Mor(11);
            M("balor", "balor", '&', C(230, 40, 30), "13", -2, 15, 15, 1, "2d6 sword fire;1d6 whip fire", MF.Evil | MF.NeedMagic | MF.Fly | MF.ResFire | MF.ResElec | MF.ResPoison | MF.FearAura | MF.Heat,
                "A demon lord of flame and shadow, wielding a sword of lightning and a whip of fire. Its death is an explosion; its life is worse.", Size.Large).Glows(C(255, 60, 20), 3).Mor(12);

            M("hollow_king", "the Hollow King", 'L', C(255, 230, 140), "18", -2, 12, 15, 0, "1d10 touch paralyze;1d10 touch cold", MF.Undead | MF.NeedMagic | MF.Unique | MF.Boss | MF.Evil | MF.Hands | MF.ResCold | MF.ResElec | MF.NoCorpse,
                "A lich in a moth-eaten crown, enthroned behind a towering veil of fused bone. It built this tomb nine hundred years ago to keep the Heart, and every soul who died in it now serves it. It has not lost a duel in all that time.").Turn(13)
                .Casts(55, SpellId.Fireball, SpellId.ConeOfCold, SpellId.HoldMonster, SpellId.MagicMissile, SpellId.SummonUndead, SpellId.FingerOfDeath, SpellId.LightningBolt, SpellId.Blink)
                .Glows(C(255, 220, 120), 3).Mor(12).Exp(6000).Al(Align.NE);

            M("snake", "snake", 'S', C(90, 170, 60), "1", 6, 12, 99, 0, "1d3 bite poison", MF.Animal, "A thin green snake, recently a stick.").Mor(9);
            M("familiar_cat", "black cat", 'f', C(60, 60, 70), "1", 6, 15, 99, 0, "1d2 claw;1d3 bite", MF.Animal | MF.Warm, "Sleek, aloof, and secretly pleased to see you.").Mor(8);
            M("familiar_raven", "raven", 'B', C(50, 50, 70), "1", 7, 24, 99, 0, "1d3 peck", MF.Animal | MF.Fly | MF.Warm, "It says something that sounds like your name, backwards.").Mor(8);
            M("insects", "insect swarm", 'I', C(150, 140, 60), "2", 8, 15, 99, 0, "1d4 sting", MF.Animal | MF.Fly | MF.NoCorpse, "A whining cloud of biting things.").Mor(12);
            M("onyx_dog", "onyx dog", 'd', C(50, 50, 60), "4", 4, 18, 99, 0, "2d6 bite", MF.Animal | MF.ResPoison | MF.NoCorpse, "A hound of living black stone. It never tires and never forgets a scent.").Mor(12);
            M("squire", "knight of the Deck", '@', C(200, 200, 230), "5", 2, 12, 99, 0, "1d8+2 hit;1d8+2 hit", Humanoid, "A battered knight who stepped out of a playing card. Loyal unto death, and a little confused about how they got here.").Mor(12);
            M("hammer", "spiritual hammer", ')', C(255, 240, 160), "3", 2, 18, 99, 0, "1d6+1 strike", MF.Fly | MF.Mindless | MF.NoCorpse | MF.NeedMagic, "A hammer of golden light that swings itself.").Mor(12);
            M("shopkeeper", "shopkeeper", '@', C(240, 240, 240), "12", 0, 15, 99, 0, "4d6 hit;4d6 hit", Humanoid | MF.Peaceful | MF.Shopkeeper | MF.Unique,
                "A merchant who has survived in the dungeon for decades. Ask yourself how.").Casts(30, SpellId.MagicMissile, SpellId.Fireball, SpellId.Haste).Mor(12).Al(Align.LN);
        }

        public static string PluralName(MonsterDef d)
        {
            if (d.Plural != null) return d.Plural;
            string n = d.Name;
            if (n.EndsWith("man")) return n.Substring(0, n.Length - 3) + "men";
            if (n.EndsWith("f")) return n.Substring(0, n.Length - 1) + "ves";
            if (n.EndsWith("y") && !n.EndsWith("ey")) return n.Substring(0, n.Length - 1) + "ies";
            if (n.EndsWith("s") || n.EndsWith("x") || n.EndsWith("ch")) return n + "es";
            return n + "s";
        }
    }
}
