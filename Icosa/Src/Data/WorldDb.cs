using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public class DungeonDef
    {
        public int Branch, Top, Bottom, Lakes, CaveChance;
        public string Name, Short, Blurb, Boss, Feeling, Slain, Site;
        public string[] Floors;
        public Color C, WallFg, WallBg, FloorFg;
        public Tile Liquid, Ring;
        public IK Prize;
    }

    public class TownDef
    {
        public string Name, Blurb;
        public string[] Shops;
        public bool Temple, Oracle, Start, Walled;
        public string Inn, Centre, Site;
        public int Branch, W, H, Stock, Folk, Watch, Houses;
    }

    public static class WorldDb
    {
        public const string WildName = "The Weald of Hollow Bells";

        public static readonly List<DungeonDef> Dungeons = new List<DungeonDef>
        {
            new DungeonDef
            {
                Branch = 0, Top = 1, Bottom = Rules.MaxDepth, Name = "the Tomb of the Twenty-Sided Heart", Short = "Tomb", Site = "lonehill", C = new Color(255, 120, 160), Ring = Tile.Statue,
                Blurb = "A stair of black basalt spirals into the hill. Its steps are numbered one to twenty, then one to twenty again, all the way down. The air rising from it smells of candle smoke and old dice.",
            },
            new DungeonDef
            {
                Branch = 1, Top = 2, Bottom = 4, Name = "the Drowned Belfry", Short = "Belfry", Site = "lake", C = new Color(110, 190, 230), Ring = Tile.Shallow,
                Floors = new[] { "The Bell-Rope Stair", "The Nave Beneath the Lake", "The Clapper's Grave" },
                WallFg = new Color(110, 150, 150), WallBg = new Color(26, 48, 54), FloorFg = new Color(90, 120, 125), Liquid = Tile.DeepWater, Lakes = 5, CaveChance = 20,
                Boss = "bellwright", Prize = IK.Amulet,
                Blurb = "A church steeple stands upside down in a black pond, spire buried in the mud and belfry gaping at the sky. Steps lead down into the bell-mouth. Now and then a bell rings once below, as if keeping count.",
                Feeling = "Somewhere below, a drowned bell strikes the hour. It is the wrong hour.",
                Slain = "The Bellwright sinks into the water, and every bell in the Belfry rings at once for the last time.",
            },
            new DungeonDef
            {
                Branch = 2, Top = 4, Bottom = 6, Name = "the Orchard of Teeth", Short = "Orchard", Site = "forest", C = new Color(230, 200, 150), Ring = Tile.Bones,
                Floors = new[] { "The Root Cellar", "The Gnawing Grove", "The Pit of First Fruit" },
                WallFg = new Color(160, 110, 90), WallBg = new Color(58, 30, 26), FloorFg = new Color(120, 95, 80), Liquid = Tile.Shallow, Lakes = 2, CaveChance = 75,
                Boss = "mother_molar", Prize = IK.Ring,
                Blurb = "Under a ring of apple trees, a root-hole widens into a stair. Cut any apple here in half and you will find a single human tooth where the core should be. The roots are warm to the touch.",
                Feeling = "You hear chewing. It is patient, and it is everywhere.",
                Slain = "Mother Molar spits out a lifetime of teeth and lies still. The orchard above will bear ordinary apples next year.",
            },
            new DungeonDef
            {
                Branch = 3, Top = 7, Bottom = 9, Name = "the Cartographer's Wound", Short = "Wound", Site = "hills", C = new Color(170, 150, 255), Ring = Tile.Crystal,
                Floors = new[] { "The Margin", "The Folded Country", "Here Be Dragons" },
                WallFg = new Color(120, 120, 190), WallBg = new Color(28, 26, 60), FloorFg = new Color(150, 135, 100), Liquid = Tile.Chasm, Lakes = 2, CaveChance = 35,
                Boss = "unfinished_map", Prize = IK.Wand,
                Blurb = "A crack in the earth with ruled, straight edges, as if someone had cut the country along a fold. Compass roses are scratched into the rock. It goes down further than any map allows.",
                Feeling = "The walls here are covered in contour lines. Some of them are still being drawn.",
                Slain = "The Unfinished Map tears down the middle. For a moment you can see every place you have ever been, and then only this one.",
            },
        };

        public static readonly List<TownDef> Towns = new List<TownDef>
        {
            new TownDef
            {
                Branch = 10, Name = "Lantern Hollow", Site = "plains", Start = true, Walled = true, W = 64, H = 24, Stock = 1, Folk = 8, Watch = 3, Houses = 8, Centre = "well",
                Temple = true, Inn = "the Wick and Waning", Shops = new[] { "general", "apothecary" },
                Blurb = "A village of lamplighters. They keep a lantern burning in every window so the dead in the Tomb can find their way home, and so they can see them coming.",
            },
            new TownDef
            {
                Branch = 11, Name = "Gallowmere Market", Site = "river", Walled = true, W = 80, H = 28, Stock = 4, Folk = 12, Watch = 4, Houses = 11, Centre = "vane",
                Inn = "the Hanged Weathercock", Shops = new[] { "armory", "scribe", "deli", "general", "apothecary" },
                Blurb = "A market town built around an old gallows that now serves as a weathervane. Everything here is for sale, including the weather.",
            },
            new TownDef
            {
                Branch = 12, Name = "the Hermitage of Nine Clocks", Site = "foothills", W = 34, H = 14, Stock = 7, Folk = 0, Houses = 1, Centre = "bell", Oracle = true, Shops = new[] { "curio" },
                Blurb = "Nine clocks tick in a hermit's window, each set to a different hour. Every one of them is right somewhere.",
            },
        };

        static readonly string[] gossip =
        {
            "The Belfry rang thirteen times last night. My grandmother says that means it's hungry.",
            "Don't eat the apples from the ring of trees in the east. Don't even look at them while they're chewing.",
            "A cartographer came through here, years ago. He said the country had a wrong fold in it and he meant to fix it. We found his compass. Just the compass.",
            "The Tomb stair has twenty steps, then twenty more. Count them on the way down. If the numbers change on the way up, run.",
            "We light the lanterns for the dead. The dead don't need the light. We need to see them.",
            "Gallowmere sells everything. My cousin bought a Tuesday there once. Rained the whole day.",
            "The hermit with the clocks rolls a great die for anyone who asks. He says it never lies. It just rounds.",
            "The Hollow King was a dice-maker before he was a king. He carved the Heart from his own luck.",
            "Bring the Heart back up into the sun and the Weald will stop ringing. That's what they say. I've never heard it stop.",
            "Wolves won't come into the village. They don't like the lanterns. Or they like them too much and are waiting.",
            "There's an altar in the chapel. Drop a thing on it and the gods will tell you if it hates you.",
            "Every road out of the Hollow leads to a hole in the ground. Funny how roads are like that.",
            "The bells in the Weald ring by themselves. Hollow bells, no clappers. Something rings them from inside.",
            "If you go down the Wound, take a map. Not to read. To give it something else to eat.",
        };

        public static string Gossip() => gossip[Rng.Int(gossip.Length)];

        public static DungeonDef Get(int branch) => Dungeons.FirstOrDefault(d => d.Branch == branch) ?? Dungeons[0];

        public static TownDef TownOf(int branch) => Towns.FirstOrDefault(t => t.Branch == branch);
        public static TownDef StartTown => Towns.First(t => t.Start);

        public static bool Holds(int branch, int depth)
        {
            if (branch == 0) return depth >= 0 && depth <= Rules.MaxDepth;
            if (branch == 99) return depth >= 1 && depth <= 20;
            if (TownOf(branch) != null) return depth == 0;
            var d = Dungeons.FirstOrDefault(x => x.Branch == branch);
            return d != null && depth >= d.Top && depth <= d.Bottom;
        }

        public static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        public static string LevelName(Map m)
        {
            if (m.Wild) return WildName;
            if (m.Town) return Cap(TownOf(m.Branch).Name);
            if (m.Encounter) return "Somewhere in the Weald";
            if (m.Branch == 0) return Rules.DepthNames[Math.Min(m.Depth, Rules.DepthNames.Length - 1)];
            var d = Get(m.Branch);
            return d.Floors[Math.Clamp(m.Depth - d.Top, 0, d.Floors.Length - 1)];
        }

        public static string LevelTag(Map m)
        {
            if (m.Wild) return "The Surface";
            if (m.Town) return "Settlement";
            if (m.Encounter) return "Encounter";
            if (m.Branch == 0) return "Level " + m.Depth;
            var d = Get(m.Branch);
            return d.Short + " " + (m.Depth - d.Top + 1) + "/" + (d.Bottom - d.Top + 1);
        }
    }
}
