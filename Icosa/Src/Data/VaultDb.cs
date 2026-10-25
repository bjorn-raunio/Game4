using System.Collections.Generic;

namespace Icosa
{
    public class VaultDef
    {
        public string Id, Theme, Feeling, Text;
        public int MinDepth, MaxDepth, Freq, Branch = -1;
        public bool Lit;
        public string[] Rows;
    }

    public static class VaultDb
    {
        public static readonly List<VaultDef> All = new List<VaultDef>();

        static VaultDef V(string id, int min, int max, int freq, string theme, bool lit, params string[] rows)
        {
            var v = new VaultDef { Id = id, MinDepth = min, MaxDepth = max, Freq = freq, Theme = theme, Lit = lit, Rows = rows };
            All.Add(v);
            return v;
        }

        static VaultDb()
        {
            V("moat_keep", 3, 14, 6, "keep", true,
                "...........",
                ".~~~~~~~~~.",
                ".~#######~.",
                ".~#M.$.*#~.",
                ".~#..c..#~.",
                ".~###+###~.",
                ".~~~~=~~~~.",
                "...........").Feeling = "You hear water lapping against stone walls.";

            V("cellblock", 2, 12, 6, "prison", false,
                ".................",
                ".###.###.###.###.",
                ".#m#.#*#.#b#.#$#.",
                ".#+#.#+#.#L#.#L#.",
                "....^............").Text = "Day 4,113. They say the King counts us every night. He always gets a different number.";

            V("chapel", 1, 15, 5, "chapel", true,
                "B.........B",
                "...O...O...",
                ".RRRRARRRR.",
                "...O.E.O...",
                "B.........B").Text = "Twenty faces has the Heart, and nineteen of them are lying.";

            V("ossuary", 3, 15, 6, "crypt", false,
                ".C.C.C.C.",
                "b.......b",
                ".C.C.C.C.",
                "..b.u.b..",
                ".C.C.C.C.",
                "b...!...b");

            V("stacks", 2, 15, 5, "library", false,
                "KKKKK.KKKKK",
                ".....*.....",
                ".KKK.K.KKK.",
                ".K!..E..*K.",
                ".KKK.K.KKK.",
                "...........").Text = "OVERDUE. — stamped into the floor, in letters a foot high";

            V("drowned_nave", 1, 15, 4, "flooded", false,
                "..----------..",
                ".O-O-O-O-O-O-.",
                ".-----~~-----.",
                ".O-O-O-O-O-O-.",
                "..----$-----..");

            V("gallery", 5, 15, 4, "statues", true,
                ".T.T.T.T.T.",
                "...........",
                ".T...!...T.",
                "...........",
                ".T.T.T.T.T.").Feeling = "You feel watched by many unblinking eyes.";

            V("treasury", 5, 14, 3, "treasury", true,
                "...........",
                ".#########.",
                ".#$.$.$.c#.",
                ".#...!...#.",
                ".####x####.",
                ".....M.....").Feeling = "You have a feeling that someone has hidden treasure nearby.";

            V("icosa_shrine", 1, 15, 4, "shrine", true,
                "   .....   ",
                "  ..O.O..  ",
                " ..O...O.. ",
                "..O..F..O..",
                " ..O.E.O.. ",
                "  ..O.O..  ",
                "   .....   ").Text = "Twenty pillars were planned. The masons ran out of faith at ten.";

            V("barracks", 2, 10, 5, "barracks", false,
                ".............",
                ".R.R.R.R.R.R.",
                ".............",
                "..g...g...g..",
                ".............",
                ".:.*.:.:.$.:.");

            V("cistern", 4, 15, 4, "cistern", false,
                ".............",
                ".~~~~~=~~~~~.",
                ".~~~~~=~~~~~.",
                ".=====*=====.",
                ".~~~~~=~~~~~.",
                ".~~~~~=~~~~~.",
                ".............").Feeling = "Somewhere, water drips into a great deal of water.";

            V("cloister", 2, 15, 4, "garden", true,
                "...........",
                ".####+####.",
                ".#GGGGGGG#.",
                ".+GGGFGGG+.",
                ".#GGGGGGG#.",
                ".####+####.",
                "...........");

            V("spider_hall", 4, 13, 3, "web", false,
                "..w....w..",
                ".w.ww.w.w.",
                "..w.!..w..",
                ".w.w.ww.w.",
                "..w....w..");

            V("toll_bridge", 1, 4, 3, "belfry", false,
                ".......",
                "~~~=~~~",
                "~~~=~~~",
                "~~~=~~~",
                "...m...").Branch = 1;
        }
    }
}
