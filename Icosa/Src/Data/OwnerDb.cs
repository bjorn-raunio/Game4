using System.Collections.Generic;

namespace Icosa
{
    public class OwnerDef
    {
        public string Id, Name, Feeling;
        public int MinDepth, MaxDepth, Freq, Branch, KinPct;
        public MF Kin;
        public Dictionary<string, int> Themes = new Dictionary<string, int>();
        public Dictionary<string, int> Sets = new Dictionary<string, int>();
        public Dictionary<string, int> Ruin = new Dictionary<string, int>();

        public int Theme(string t) => Themes.TryGetValue(t, out var w) ? w : 100;
        public int Set(string s) => Sets.TryGetValue(s, out var w) ? w : 100;
        public int Ruins(string s) => Ruin.TryGetValue(s, out var w) ? w : 100;
    }

    public static class OwnerDb
    {
        public static readonly List<OwnerDef> All = new List<OwnerDef>();

        static Dictionary<string, int> W(string spec)
        {
            var d = new Dictionary<string, int>();
            foreach (var part in spec.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split(':');
                d[kv[0]] = int.Parse(kv[1]);
            }
            return d;
        }

        static OwnerDef O(string id, string name, int min, int max, int freq, string themes, string sets, string feeling, MF kin = MF.None, int kinPct = 0, int branch = 0, string ruin = "")
        {
            var o = new OwnerDef { Id = id, Name = name, MinDepth = min, MaxDepth = max, Freq = freq, Themes = W(themes), Sets = W(sets), Feeling = feeling, Kin = kin, KinPct = kinPct, Branch = branch, Ruin = W(ruin) };
            All.Add(o);
            return o;
        }

        static OwnerDb()
        {
            O("nobody", "no one in particular", 1, 14, 8, "", "",
                null);
            O("court", "a fallen court", 2, 14, 6,
                "throne:400 armory:250 pillars:250 brazier:200 library:150 temple:120 garden:120 crypt:40 lair:30 web:30 flooded:40",
                "chapel:300 barracks:250 treasury:300 moat_keep:300 cloister:200 ossuary:40 spider_hall:20",
                "Faded banners still hang from the walls. Someone ruled here once, and expected to again.", MF.Hands, 25, ruin: "masonry:250 cavein:120 moss:60 flood:40 lair:30");
            O("necropolis", "the honoured dead", 3, 14, 6,
                "crypt:400 temple:250 statues:250 brazier:150 pillars:150 garden:10 shop:30 throne:40 armory:40 lair:40 fountain:50",
                "ossuary:400 chapel:250 gallery:250 barracks:20 cloister:30",
                "The dead were buried here in great numbers. Not all of them agreed to it.", MF.Undead, 35, ruin: "masonry:180 moss:150 cavein:120 flood:60 lair:50");
            O("scriptorium", "a college of sages", 2, 13, 5,
                "library:450 oracle:300 fountain:150 statues:150 pillars:150 armory:30 lair:30 web:50 throne:50",
                "stacks:450 icosa_shrine:250 chapel:150 barracks:20 cellblock:40",
                "Loose pages drift along the floor, all of them numbered, none of them in order.", ruin: "masonry:150 moss:90 flood:60 cavein:80 lair:40");
            O("garrison", "a forgotten garrison", 2, 12, 5,
                "armory:450 pillars:200 brazier:200 throne:120 library:40 garden:30 temple:60 fountain:60",
                "barracks:450 cellblock:350 moat_keep:250 treasury:120 stacks:30 cloister:30",
                "Somewhere ahead, a drum keeps time for soldiers who stopped marching long ago.", MF.Hands, 35, ruin: "masonry:220 cavein:160 moss:60 lair:60");
            O("beasts", "things that den", 1, 14, 6,
                "lair:450 web:300 garden:250 flooded:150 library:20 throne:20 shop:40 temple:40 oracle:40 armory:30 pillars:50",
                "spider_hall:400 cistern:150 drowned_nave:150 chapel:30 stacks:20 treasury:30 barracks:30",
                "The air is thick with musk, and the floor with gnawed bones.", MF.Animal, 35, ruin: "masonry:30 lair:350 moss:140 cavein:140 flood:80");
            O("waterworks", "the old waterworks", 3, 14, 4,
                "flooded:400 fountain:350 garden:200 pillars:150 throne:30 library:30 armory:40 web:60",
                "cistern:450 drowned_nave:400 cloister:150 stacks:30 barracks:40",
                "Water runs somewhere inside the walls, down channels cut for it long ago.", ruin: "masonry:150 flood:400 moss:220 cavein:80 lair:40");
            O("dicewrights", "the dicewrights", 1, 14, 4,
                "oracle:450 shop:250 temple:200 statues:150 brazier:150 lair:40 web:40 crypt:60",
                "icosa_shrine:450 chapel:200 treasury:250 cellblock:40",
                "Numbers are carved over every lintel here, counting down from twenty.", ruin: "masonry:300 cavein:60 moss:60 flood:40 lair:20");
            O("belfry", "the drowned congregation", 1, 99, 1,
                "flooded:400 temple:250 fountain:200 crypt:60 throne:40 armory:40",
                "toll_bridge:300 drowned_nave:350 chapel:250 cistern:200",
                null, MF.None, 0, 1, "masonry:120 flood:350 moss:180 cavein:80");
            O("orchard", "the orchard", 1, 99, 1,
                "garden:450 lair:250 flooded:120 library:20 throne:20 armory:30 shop:40",
                "cloister:300 spider_hall:200 ossuary:150 stacks:20",
                null, MF.Animal, 25, 2, "masonry:20 moss:300 lair:250 cavein:150 flood:60");
            O("wound", "the cartographers", 1, 99, 1,
                "library:350 statues:250 oracle:250 pillars:150 garden:40 lair:60",
                "stacks:400 gallery:250 icosa_shrine:200",
                null, MF.None, 0, 3, "masonry:120 cavein:280 moss:50 flood:30");
        }
    }
}
