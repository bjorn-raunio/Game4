using System;

namespace Icosa
{
    public static class Lore
    {
        static readonly string[] graffiti =
        {
            "THE DEAD KING KEEPS NO MERCY",
            "Trolls fear the flame. The flame fears the water. The water fears the cold. The cold fears only the troll.",
            "Never trust a chest that breathes.",
            "Gorbash was here. Gorbash is still here. Please send help to Gorbash.",
            "The lich counts its years in centuries. Count yours in torches.",
            "If you can read this, you are still alive. Stay that way.",
            "LOOK UP",
            "Elves ignore the ghoul's kiss. Be an elf, or be elsewhere.",
            "Silver for the wererat. Fire for the troll. Prayer for everything else.",
            "Do not drink from the fountain. (Drank from the fountain.)",
            "The 10-foot pole is mightier than the sword. Mostly in the sense that it is longer.",
            "Here lies Tim the Fourth. Tim the Fifth is on his way.",
            "Gold is experience. Experience is gold. Spend wisely; die rich.",
            "The ink wraith ate my map. I have been in this corridor for three days.",
            "Rust monsters are allergic to wooden clubs. That's a lie. They just don't care.",
            "Count your torches. Then count them again.",
            "Darkmantles hate light more than they love food.",
            "Beware the flux wraith. Last week my strength was my charm.",
            "Sit on thrones. Kings did, and look how well that went for them.",
            "The Oracle Die rolls once per pilgrim. Make it count.",
            "Bad luck is just the dungeon's way of saying hello.",
            "Behind the Bone Veil: bones, all the way down.",
            "The Heart has twenty faces. All of them are watching.",
            "Mimics like the third chest in any room. Superstition, but I'm alive.",
            "Wands of wonder are wonderful until they are not.",
            "The deeper the stair, the harder the stone. The harder the stone, the colder the dead.",
            "Pray when the end is near. Not before. The gods keep track.",
            "Here a wizard fell, asleep from his own spell. The goblins thanked him.",
            "Remember: rest to memorize. Rest in peace to forget.",
            "Halfling band: brought supper. Did not bring armor. Regret nothing.",
            "Spike the doors. The ogres can't read.",
            "KOBOLDS: 3 TRAPS AHEAD (maybe 2)",
            "Every map is a lie that tells the truth eventually.",
            "Basilisks hate mirrors almost as much as they hate you.",
            "The Veil is carved on both sides. Only one side is for the living.",
        };

        static readonly string[] rumors =
        {
            "I heard the Hollow King keeps his heart in a jar, and the Heart in his hands. Nobody knows which one beats.",
            "They say a ring of three wishes lies somewhere below the troll galleries.",
            "The dwarves of the Mines of Gilt left a forge running. It still burns.",
            "Undead can be turned. The trick is finding a god who answers.",
            "Gelatinous cubes swallow everything. Some of my best gear is inside one, somewhere.",
            "A shopkeeper once sold me a cursed sword. I am still holding it.",
            "If you see a statue that looks surprised, don't look at what it was looking at.",
            "The fountain on the third level granted a wish. Or so the bard said, and he was drunk.",
            "Never read an unknown scroll while standing next to an ogre.",
            "Rangers hunt giants. Giants hunt rangers. Everyone else is just in the way.",
            "Heroes die on level four. Legends die on level twelve.",
            "Thieves backstab. Fighters frontstab. Wizards stab from very far away.",
            "The deck of many things: never draw more than you can bury.",
            "The Heart wants to go home. Home is the dark. Don't let it.",
            "My company was eaten by a purple worm. Ask me how I escaped. (Through the other end.)",
            "The paper golems are folded from dead wizards' spellbooks. Some still remember a cantrip or two.",
            "Mind flayers find elves bland. Halflings, they say, are buttery.",
        };

        static readonly string[] epitaphs =
        {
            "Slipped.", "Should have brought a 10-foot pole.", "Thought it was a chest.", "Read the scroll anyway.",
            "Died doing what they loved: dying.", "Here lies a hero. Mostly.", "Gone to the great tavern in the sky.",
            "It was only a level-drain or two.", "Luck was cold that night.", "Forgot to memorize spells.",
            "Tried to pet the owlbear.", "Not even the gods could save this one.", "Rest in pieces.",
            "Strong arm. Thin armor.", "Their torch went out.", "Saved vs. death. Failed.",
        };

        static readonly string[] syl1 = { "Al", "Bra", "Cor", "Dra", "El", "Fen", "Gar", "Hal", "Isa", "Jor", "Kel", "Lir", "Mor", "Nym", "Or", "Per", "Quel", "Ros", "Syl", "Tor", "Ul", "Val", "Wyn", "Xan", "Yse", "Zar" };
        static readonly string[] syl2 = { "dric", "wyn", "mir", "gorn", "a", "ion", "ric", "dan", "ella", "eth", "ia", "or", "us", "en", "is", "an", "ilde", "ak", "ora", "and" };

        public static string Graffiti() => graffiti[Rng.Int(graffiti.Length)];
        public static string Rumor() => rumors[Rng.Int(rumors.Length)];
        public static string Epitaph() => epitaphs[Rng.Int(epitaphs.Length)];

        public static string RandomName(Race r)
        {
            string n = syl1[Rng.Int(syl1.Length)] + syl2[Rng.Int(syl2.Length)];
            if (r == Race.Dwarf) n += Rng.Pick(new[] { " Ironfist", " Stonebeard", " Deepdelver", "" });
            if (r == Race.Halfling) n += Rng.Pick(new[] { " Tealeaf", " Underhill", " Goodbarrel", "" });
            return n;
        }

        public static string ShopkeeperName()
        {
            string[] first = { "Pembrook", "Ozzle", "Madam Thrift", "Grumwald", "Isolde", "Fenwick", "Old Tobias", "Mirela", "Haggle-Ka", "Quince", "Bartholomew Nine-Fingers" };
            return first[Rng.Int(first.Length)];
        }

        public static string ShopTitle(string kind) => kind switch
        {
            "armory" => "Arms & Armaments",
            "apothecary" => "Potions & Tinctures",
            "scribe" => "Scrolls, Quills & Books",
            "curio" => "Curiosities & Wonders",
            "deli" => "Delicatessen of the Deep",
            _ => "General Store",
        };

        public static string DeathTale()
        {
            string[] tales =
            {
                "The corpse sighs: \"I took the left fork. Take the right.\"",
                "The corpse rasps: \"The stairs down... always farther than you think.\"",
                "The corpse whispers: \"Beyond the Veil... the King does not sleep...\"",
                "The corpse mutters: \"Should have rested. Should have memorized. Should have run.\"",
                "The corpse groans: \"" + Rumor() + "\"",
                "The corpse says: \"" + Graffiti() + "\"",
            };
            return tales[Rng.Int(tales.Length)];
        }
    }
}
