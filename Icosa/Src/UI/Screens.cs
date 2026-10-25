using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Icosa
{
    public class SpellPrepScreen : Screen
    {
        readonly List<SpellId> rows = new List<SpellId>();
        int cursor;
        public override bool Overlay => true;

        public SpellPrepScreen()
        {
            var p = Dm.Player;
            rows.AddRange(p.Castable.OrderBy(s => SpellDb.Get(s).Level).ThenBy(s => SpellDb.Get(s).Name));
        }

        public override void Key(KeyIn k)
        {
            var p = Dm.Player;
            if (k.IsEsc || k.IsEnter) { Magic.TrimPlan(p); Ui.Pop(this); return; }
            int mv = k.MenuMove();
            if (mv != 0 && rows.Count > 0) { cursor = Math.Clamp(cursor + mv, 0, rows.Count - 1); return; }
            if (rows.Count == 0) return;
            var id = rows[cursor];
            int lvl = SpellDb.Get(id).Level;
            var slots = p.Slots;
            if (k.Is(Keys.Right) || k.Is('+') || k.Is('=') || k.Is(Keys.NumPad6) || k.Is(' '))
            {
                int used = p.Plan.Count(s => SpellDb.Get(s).Level == lvl);
                if (lvl <= slots.Length && used < slots[lvl - 1]) p.Plan.Add(id);
                else if (lvl <= slots.Length)
                {
                    int idx = p.Plan.FindIndex(s => SpellDb.Get(s).Level == lvl && s != id);
                    if (idx >= 0) { p.Plan.RemoveAt(idx); p.Plan.Add(id); }
                }
            }
            if (k.Is(Keys.Left) || k.Is('-') || k.Is(Keys.NumPad4) || k.Is(Keys.Back)) p.Plan.Remove(id);
        }

        public override void Draw(Terminal t)
        {
            var p = Dm.Player;
            int w = 96, h = Math.Min(42, rows.Count + 12);
            int x0 = (Terminal.W - w) / 2, y0 = 1;
            t.Frame(x0, y0, w, h, Pal.FrameHi, Pal.Panel, "Prepare Spells");
            var slots = p.Slots;
            string sl = string.Join("   ", slots.Select((n, i) => "L" + (i + 1) + ": " + p.Plan.Count(s => SpellDb.Get(s).Level == i + 1) + "/" + n));
            t.PrintM(x0 + 3, y0 + 2, "{dim}Slots{/}  " + sl, Pal.Text);
            t.PrintM(x0 + 3, y0 + 3, "{dim}← → or + - to change.  Changes take effect after an uninterrupted rest (R).  Enter to finish.{/}", Pal.Dim);
            if (rows.Count == 0) t.PrintM(x0 + 3, y0 + 5, "You know no spells you can cast at your level.", Pal.Text);
            int y = y0 + 5;
            int lastLvl = 0;
            for (int i = 0; i < rows.Count && y < y0 + h - 4; i++)
            {
                var sd = SpellDb.Get(rows[i]);
                if (sd.Level != lastLvl) { t.PrintM(x0 + 3, y++, "{gold}Level " + sd.Level + "{/}", Pal.Gold); lastLvl = sd.Level; }
                bool sel = i == cursor;
                if (sel) for (int xx = x0 + 1; xx < x0 + w - 1; xx++) t.SetBg(xx, y, Pal.C(60, 46, 28));
                int planned = p.Plan.Count(s => s == rows[i]);
                int mem = p.Memorized.Count(s => s == rows[i]);
                t.PrintM(x0 + 5, y, Markup.Hex(sd.C) + sd.Name + "{/}", Pal.Text);
                t.PrintM(x0 + 40, y, (planned > 0 ? "{yellow}" + new string('●', planned) + "{/}" : "{faint}·{/}") + "  {dim}ready " + mem + "{/}", Pal.Text);
                y++;
            }
            if (cursor < rows.Count)
            {
                var sd = SpellDb.Get(rows[cursor]);
                var lines = Markup.Wrap(sd.Desc, w - 8);
                for (int i = 0; i < lines.Count; i++) t.PrintM(x0 + 3, y0 + h - 3 - lines.Count + 1 + i, "{ink}" + lines[i] + "{/}", Pal.Text);
            }
        }
    }

    public class CharacterScreen : Screen
    {
        public override bool Overlay => true;

        public override void Key(KeyIn k)
        {
            if (k.IsEsc || k.IsEnter || k.Is(' ') || k.Is('@')) Ui.Pop(this);
        }

        public override void Draw(Terminal t)
        {
            var p = Dm.Player;
            int w = 121, h = 40, x0 = 6, y0 = 2;
            t.Frame(x0, y0, w, h, Pal.FrameHi, Pal.Panel, "Character Record Sheet");
            int x = x0 + 3, y = y0 + 2;
            t.PrintM(x, y++, "{gold}" + p.PName + "{/}, " + p.RaceInfo.Name + " " + p.ClassInfo.Name + " of level " + p.Level + " — " + p.Title, Pal.Text);
            t.PrintM(x, y++, "{dim}Alignment{/} " + Rules.AlignNames[(int)p.Align] + "   {dim}Deity{/} " + p.Deity, Pal.Text);
            y++;
            for (int i = 0; i < 6; i++)
            {
                var s = (Stat)i;
                string extra = s switch
                {
                    Stat.Str => "to-hit " + Sg(p.StrBonus.hit) + ", dmg " + Sg(p.StrBonus.dmg) + ", carry " + p.Capacity,
                    Stat.Int => "learn spell " + Rules.IntLearn(p.Stat(s)) + "%",
                    Stat.Wis => "magic defense " + Sg(Rules.WisSave(p.Stat(s))),
                    Stat.Dex => "AC " + Sg(Rules.DexAc(p.Stat(s))) + ", missiles " + Sg(Rules.DexMissile(p.Stat(s))),
                    Stat.Con => "HP per die " + Sg(Rules.ConHp(p.Stat(s), p.IsWarrior)),
                    _ => "prices ×" + Rules.ChaPrice(p.Stat(s)) / 100.0,
                };
                t.PrintM(x, y++, "{dim}" + Rules.StatLong[i].PadRight(13) + "{/}{white}" + p.StatText(s).PadRight(7) + "{/}{dim}" + extra + "{/}", Pal.Text);
            }
            y++;
            t.PrintM(x, y++, "{dim}Hit Points{/} " + p.Hp + "/" + p.MaxHp + "   {dim}Armor Class{/} " + p.AC + "   {dim}THAC0{/} " + p.Thac0 + "   {dim}Speed{/} " + p.Speed, Pal.Text);
            t.PrintM(x, y++, "{dim}Experience{/} " + p.Xp + (p.Level < 20 ? " (next at " + Rules.XpFor(p.Class, p.Level + 1) + ")" : "") + "   {dim}Gold{/} " + p.Gold + "   {dim}Kills{/} " + p.Kills, Pal.Text);
            y++;
            t.PrintM(x, y++, "{gold}Saving Throws{/}", Pal.Gold);
            string[] sn = { "Paralysis/Poison/Death", "Petrification/Polymorph", "Rod/Staff/Wand", "Breath Weapon", "Spell" };
            for (int i = 0; i < 5; i++) t.PrintM(x + 2, y++, "{dim}" + sn[i].PadRight(26) + "{/}" + p.SaveTarget((Save)i), Pal.Text);
            if (p.Class == Cls.Thief)
            {
                y++;
                var ts = p.ThiefSkills;
                t.PrintM(x, y++, "{gold}Thief Skills{/}  {dim}Pick Locks{/} " + ts[0] + "%  {dim}Find/Remove Traps{/} " + ts[1] + "%  {dim}Move Silently{/} " + ts[2] + "%  {dim}Hide in Shadows{/} " + ts[3] + "%  {dim}Backstab{/} ×" + Rules.BackstabMul(p.Level), Pal.Text);
            }
            int cx = x0 + 74, cy = y0 + 4;
            t.PrintM(cx, cy++, "{gold}Equipment{/}", Pal.Gold);
            foreach (Slot s in Enum.GetValues(typeof(Slot)))
            {
                var it = p.Worn(s);
                t.PrintM(cx, cy++, "{dim}" + s.ToString().PadRight(8) + "{/}" + (it == null ? "{faint}—{/}" : it.Name(false, false)), Pal.Text, 42);
            }
            if (p.Iouns.Count > 0) t.PrintM(cx, cy++, "{dim}Orbiting{/} " + string.Join(", ", p.Iouns.Select(i => i.Def.Name.Replace(" ioun stone", ""))), Pal.Text, 42);
            cy++;
            if (p.Class == Cls.MagicUser)
            {
                t.PrintM(cx, cy++, "{gold}Spellbook{/}", Pal.Gold);
                var lines = Markup.Wrap(string.Join(", ", p.Known.OrderBy(s => SpellDb.Get(s).Level).Select(s => SpellDb.Get(s).Name + " (" + SpellDb.Get(s).Level + ")")), 40);
                foreach (var l in lines.Take(8)) t.PrintM(cx, cy++, l, Pal.Text);
            }
            if (Dm.Achievements.Count > 0)
            {
                cy++;
                t.PrintM(cx, cy++, "{gold}Deeds{/}", Pal.Gold);
                foreach (var a in Dm.Achievements.Distinct().Take(6)) t.PrintM(cx, cy++, "• " + a, Pal.Text, 42);
            }
        }

        static string Sg(int v) => v >= 0 ? "+" + v : v.ToString();
    }

    public static class HelpScreen
    {
        public static Screen Make()
        {
            var l = new List<string>
            {
                "{gold}TOMB OF THE TWENTY-SIDED HEART{/}",
                "Descend fifteen levels, take the Icosahedral Heart from the Referee who guards it behind the Screen, and climb back to daylight.",
                "",
                "{gold}Movement{/}   arrows / numpad / hjklyubn · Shift+direction runs · Tab fights the nearest foe",
                "             X auto-explore · > and < use stairs (or travel to known stairs) · . or numpad-5 waits",
                "",
                "{gold}Items{/}      i inventory (select an item for options) · g or , pick up · d drop",
                "             w wield · W wear / put on / light / quiver · T take off",
                "             q quaff · r read · z zap wand · e eat · t throw · f fire launcher · a apply tool",
                "",
                "{gold}Magic{/}      c (or Z) cast a memorized spell · p prepare spells, then R rest to memorize them",
                "             Magic-users learn spells from scrolls and spellbooks. Clerics know every prayer of their level.",
                "",
                "{gold}Actions{/}    s search for secrets · R rest until healed · o open (chests, doors) · C close door · D kick",
                "             A class abilities (turn undead, lay on hands, hide, disarm) · P pray",
                "             E engage a feature: sit on a throne, roll the Oracle Die",
                "",
                "{gold}Info{/}       x look around · : look here · @ character sheet · m message log · I discoveries",
                "             S save and quit · Q quit permanently · M toggle sound · F11 or Alt+Enter fullscreen",
                "",
                "{gold}The Rules{/}  To hit, roll a d20 + modifiers ≥ your THAC0 minus the target's Armor Class. Lower AC is better.",
                "             A natural 20 always hits and deals double dice. A natural 1 always misses, and may go badly.",
                "             Saving throws: roll d20 ≥ your save target. Every roll appears in the dice tray on the right.",
                "             Gold you pick up gives you experience. Darkness is dangerous: carry light and spare torches.",
                "             Unexplored memory is drawn in ink on graph paper. Ink wraiths can smear it.",
                "",
                "{gold}Tips{/}       Elves resist sleep and ghoul paralysis. Trolls rise again unless burned with fire or acid.",
                "             Some creatures can only be harmed by silver or magic. The 10-foot pole finds traps and mimics.",
                "             Pray when you are near death, but not too often. Altars reveal curses on items dropped on them.",
                "             A wandering monster check is rolled every so often; resting is never entirely safe.",
            };
            return new TextScreen("Help", l, 110);
        }
    }

    public class DeathScreen : Screen
    {
        int page;
        readonly List<string> possessions;
        readonly string epitaph = Lore.Epitaph();

        public DeathScreen()
        {
            var p = Dm.Player;
            foreach (var it in p.Pack) { it.Known = true; it.ChargesKnown = true; if (it.Def.Unknown) Dm.Ident.Learn(it.Def.Id); }
            possessions = p.Pack.Select(i => i.Name()).ToList();
        }

        public override void Key(KeyIn k)
        {
            if (k.IsEsc || k.IsEnter || k.Is(' '))
            {
                page++;
                if (page > 1) Ui.Replace(new TitleScreen());
            }
        }

        public override void Draw(Terminal t)
        {
            var p = Dm.Player;
            if (page == 0)
            {
                string[] stone =
                {
                    "                       __________________",
                    "                      /                  \\",
                    "                     /        REST        \\",
                    "                    /          IN          \\",
                    "                   /         PEACE          \\",
                    "                  /                          \\",
                    "                  |                          |",
                    "                  |                          |",
                    "                  |                          |",
                    "                  |                          |",
                    "                  |                          |",
                    "                  |                          |",
                    "                  |                          |",
                    "                  |                          |",
                    "                  |          " + DateTime.Now.Year + "            |",
                    "                 *|     *  *  *      *       | *",
                    "        _________)/\\\\_//(\\/(/\\)/\\//\\/|_)_______",
                };
                int x0 = 30, y0 = 4;
                for (int i = 0; i < stone.Length; i++) t.Print(x0, y0 + i, stone[i], i >= 15 ? Pal.C(90, 140, 70) : Pal.C(170, 170, 160));
                void C(int row, string s, Color c) => t.Center(y0 + row, s, c, x0 + 18, 28);
                C(7, Trunc(p.PName, 24), Pal.White);
                C(8, p.Gold + " Au", Pal.Gold);
                C(9, p.ClassInfo.Name + ", lvl " + p.Level, Pal.Text);
                var cause = Markup.Wrap(Dm.DeathPhrase(), 24);
                for (int i = 0; i < Math.Min(3, cause.Count); i++) C(10 + i, cause[i], Pal.Text);
                C(13, "on dungeon level " + Dm.Depth, Pal.Text);
                t.Center(y0 + 19, "{dim}\"" + epitaph + "\"{/}", Pal.Dim);
                t.Center(y0 + 22, "Score: {gold}" + Dm.Score + "{/}   Turns: " + Dm.Turn + "   Deepest level: " + p.DeepestDepth + "   Kills: " + p.Kills + "   Real time: " + Dm.PlayTime(), Pal.Text);
                t.Center(y0 + 24, "{dim}Your possessions, now identified:{/}", Pal.Text);
                int col = 0, row = 0;
                foreach (var s in possessions.Take(24))
                {
                    t.PrintM(12 + col * 56, y0 + 26 + row, s, Pal.Text, 54);
                    row++;
                    if (row >= 12) { row = 0; col++; }
                }
                t.Center(Terminal.H - 2, "{dim}[Enter] Hall of Fame{/}", Pal.Dim);
            }
            else Hof.Draw(t, Dm.Score);
        }

        static string Trunc(string s, int n) => s.Length > n ? s.Substring(0, n) : s;
    }

    public class VictoryScreen : Screen
    {
        int page;
        float time;

        public override void Update(float dt) => time += dt;

        public override void Key(KeyIn k)
        {
            if (k.IsEsc || k.IsEnter || k.Is(' '))
            {
                page++;
                if (page > 1) Ui.Replace(new TitleScreen());
            }
        }

        public override void Draw(Terminal t)
        {
            var p = Dm.Player;
            if (page == 0)
            {
                for (int y = 0; y < 26; y++)
                    for (int x = 0; x < Terminal.W; x++)
                    {
                        float f = y / 26f;
                        var sky = Pal.Lerp(Pal.C(20, 30, 80), Pal.C(255, 160, 90), f * Math.Min(1, time / 4f));
                        t.SetBg(x, y, sky);
                    }
                int sunY = 26 - (int)Math.Min(8, time * 1.5f);
                for (int dy = -4; dy <= 4; dy++)
                    for (int dx = -9; dx <= 9; dx++)
                        if (dx * dx / 4f + dy * dy <= 16 && sunY + dy < 26 && sunY + dy >= 0) t.SetBg(Terminal.W / 2 + dx, sunY + dy, Pal.C(255, 230, 150));
                for (int x = 0; x < Terminal.W; x++) t.Fill(x, 26, 1, Terminal.H - 26, Pal.C(30, 45, 25));
                if (Dm.Won)
                {
                    t.Center(30, "{gold}V I C T O R Y{/}", Pal.Gold);
                    t.Center(32, p.PName + " the " + p.RaceInfo.Name + " " + p.ClassInfo.Name + " climbs out of the Tomb into the dawn,", Pal.White);
                    t.Center(33, "the Icosahedral Heart beating in " + "their" + " hands. Behind, very far below, a dry voice mutters: \"Again.\"", Pal.White);
                }
                else
                {
                    t.Center(30, "{gold}RETIRED{/}", Pal.Gold);
                    t.Center(32, p.PName + " climbs back into the daylight with " + p.Gold + " gold and a great many stories.", Pal.White);
                    t.Center(33, "The Heart still beats, far below. Someone else will have to roll for it.", Pal.White);
                }
                t.Center(36, "Score: {gold}" + Dm.Score + "{/}   Level " + p.Level + "   Deepest: " + p.DeepestDepth + "   Kills: " + p.Kills + "   Turns: " + Dm.Turn, Pal.Text);
                t.Center(Terminal.H - 2, "{dim}[Enter] Hall of Fame{/}", Pal.Dim);
            }
            else Hof.Draw(t, Dm.Score);
        }
    }

    public static class Hof
    {
        public class Entry
        {
            public int Score, Level, Depth;
            public string Name, Race, Class, Cause, Date;
            public bool Won;
        }

        public static readonly List<Entry> Entries = new List<Entry>();
        static string FilePath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Icosa", "halloffame.txt");

        public static void Load()
        {
            Entries.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var f = line.Split('|');
                    if (f.Length < 9) continue;
                    Entries.Add(new Entry { Score = int.Parse(f[0]), Name = f[1], Race = f[2], Class = f[3], Level = int.Parse(f[4]), Depth = int.Parse(f[5]), Cause = f[6], Date = f[7], Won = f[8] == "1" });
                }
            }
            catch { }
        }

        public static void Add()
        {
            var p = Dm.Player;
            Entries.Add(new Entry
            {
                Score = Dm.Score, Name = p.PName.Replace("|", ""), Race = p.RaceInfo.Name, Class = p.ClassInfo.Name, Level = p.Level, Depth = p.DeepestDepth,
                Cause = Dm.Won ? "escaped with the Icosahedral Heart" : Dm.DeathPhrase().Replace("|", "") + (Dm.Retired ? "" : " on level " + Dm.Depth), Date = DateTime.Now.ToString("yyyy-MM-dd"), Won = Dm.Won,
            });
            Entries.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (Entries.Count > 50) Entries.RemoveRange(50, Entries.Count - 50);
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, Entries.Select(e => string.Join("|", e.Score, e.Name, e.Race, e.Class, e.Level, e.Depth, e.Cause, e.Date, e.Won ? "1" : "0")));
            }
            catch { }
        }

        public static void Draw(Terminal t, int highlight = -1)
        {
            t.Frame(10, 2, 113, 40, Pal.FrameHi, Pal.Panel, "Hall of Fame");
            t.PrintM(14, 4, "{dim}Rank  Score   Name             Character               Lvl  Depth  Fate{/}", Pal.Dim);
            int y = 6;
            bool marked = false;
            for (int i = 0; i < Math.Min(32, Entries.Count); i++)
            {
                var e = Entries[i];
                bool me = !marked && e.Score == highlight && highlight >= 0;
                if (me) marked = true;
                var c = me ? Pal.Gold : e.Won ? Pal.C(160, 220, 160) : Pal.Text;
                string line = (i + 1).ToString().PadLeft(3) + "   " + e.Score.ToString().PadLeft(6) + "   " + Cut(e.Name, 15).PadRight(17) + Cut(e.Race + " " + e.Class, 22).PadRight(24) + e.Level.ToString().PadLeft(3) + "  " + e.Depth.ToString().PadLeft(5) + "  " + Cut(e.Cause, 40);
                t.Print(14, y++, line, c);
            }
            if (Entries.Count == 0) t.Center(10, "{dim}No one has yet braved the Tomb.{/}", Pal.Dim);
            t.Center(40, "{dim}[Enter] continue{/}", Pal.Dim);
        }

        static string Cut(string s, int n) => s.Length > n ? s.Substring(0, n) : s;
    }

    public class HofScreen : Screen
    {
        public override void Key(KeyIn k)
        {
            if (k.IsEsc || k.IsEnter || k.Is(' ')) Ui.Pop(this);
        }

        public override void Draw(Terminal t) => Hof.Draw(t);
    }
}
