using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Icosa
{
    public static class Icosahedron
    {
        public static readonly Vector3[] V;
        public static readonly (int a, int b)[] E;
        public static readonly (int a, int b, int c)[] F;

        static Icosahedron()
        {
            float t = (1f + (float)Math.Sqrt(5)) / 2f;
            V = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < V.Length; i++) V[i] = Vector3.Normalize(V[i]);
            var edges = new List<(int, int)>();
            for (int i = 0; i < 12; i++)
                for (int j = i + 1; j < 12; j++)
                    if (Vector3.Distance(V[i], V[j]) < 1.1f) edges.Add((i, j));
            E = edges.ToArray();
            var faces = new List<(int, int, int)>();
            for (int i = 0; i < 12; i++)
                for (int j = i + 1; j < 12; j++)
                    for (int k = j + 1; k < 12; k++)
                        if (Vector3.Distance(V[i], V[j]) < 1.1f && Vector3.Distance(V[j], V[k]) < 1.1f && Vector3.Distance(V[i], V[k]) < 1.1f) faces.Add((i, j, k));
            F = faces.ToArray();
        }

        public static void Draw(Gfx g, Vector2 center, float radius, float time, Color edge, bool numbers, float spin = 1f)
        {
            var rot = Matrix.CreateRotationY(time * 0.6f * spin) * Matrix.CreateRotationX(time * 0.37f * spin + 0.4f) * Matrix.CreateRotationZ(time * 0.11f * spin);
            var pts = V.Select(v => Vector3.Transform(v, rot)).ToArray();
            Vector2 Proj(Vector3 v)
            {
                float z = 3.2f + v.Z;
                float s = radius * 3.2f / z;
                return center + new Vector2(v.X * s, -v.Y * s);
            }
            var faceOrder = F.Select((f, i) => (f, i)).OrderByDescending(x => (pts[x.f.a] + pts[x.f.b] + pts[x.f.c]).Z).ToList();
            foreach (var (f, i) in faceOrder)
            {
                var c3 = (pts[f.a] + pts[f.b] + pts[f.c]) / 3f;
                var n = Vector3.Normalize(c3);
                if (n.Z > -0.05f) continue;
                float shade = Math.Clamp(-n.Z, 0, 1);
                var ctr = Proj(c3);
                var pa = Proj(pts[f.a]);
                var pb = Proj(pts[f.b]);
                var pc = Proj(pts[f.c]);
                FillTri(g, pa, pb, pc, Color.Lerp(new Color(30, 10, 25), new Color(110, 30, 60), shade) * 0.85f);
                if (numbers)
                {
                    string num = (i + 1).ToString();
                    float sc = 0.45f + shade * 0.5f;
                    g.TextCentered(g.Font, num, ctr, Color.Lerp(new Color(120, 80, 60), new Color(255, 230, 170), shade), sc * radius / 120f * 1.6f);
                }
            }
            foreach (var (a, b) in E)
            {
                float z = (pts[a].Z + pts[b].Z) / 2f;
                float alpha = z < 0 ? 1f : 0.18f;
                g.Line(Proj(pts[a]), Proj(pts[b]), edge * alpha, z < 0 ? 2f : 1f);
            }
            foreach (var v in pts)
                if (v.Z < 0) g.Rect(Proj(v).X - 2, Proj(v).Y - 2, 4, 4, Color.Lerp(edge, Color.White, 0.5f));
        }

        static void FillTri(Gfx g, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            var pts = new[] { a, b, c }.OrderBy(p => p.Y).ToArray();
            var p0 = pts[0]; var p1 = pts[1]; var p2 = pts[2];
            for (float y = (float)Math.Ceiling(p0.Y); y <= p2.Y; y += 1f)
            {
                float xa = Interp(p0, p2, y);
                float xb = y < p1.Y ? Interp(p0, p1, y) : Interp(p1, p2, y);
                if (xa > xb) (xa, xb) = (xb, xa);
                g.Rect(xa, y, xb - xa + 1, 1, col);
            }
        }

        static float Interp(Vector2 a, Vector2 b, float y)
        {
            if (Math.Abs(b.Y - a.Y) < 0.001f) return a.X;
            return a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y);
        }
    }

    public class TitleScreen : Screen
    {
        int sel;
        float time;
        readonly string[] Options = SaveGame.Exists
            ? new[] { "Continue Your Adventure", "Roll a New Character", "Hall of Fame", "How to Play", "Quit" }
            : new[] { "Roll a New Character", "Hall of Fame", "How to Play", "Quit" };
        string notice;
        readonly List<(Vector2 p, float s, float ph)> stars = new List<(Vector2, float, float)>();

        public TitleScreen()
        {
            Synth.Ambience(0);
            var r = new Random(3);
            for (int i = 0; i < 140; i++) stars.Add((new Vector2(r.Next(1596), r.Next(900)), (float)r.NextDouble() * 1.5f + 0.5f, (float)r.NextDouble() * 6));
        }

        public override void Update(float dt) => time += dt;

        public override void Key(KeyIn k)
        {
            int mv = k.MenuMove();
            if (mv != 0) { sel = (sel + Math.Sign(mv) + Options.Length) % Options.Length; Synth.Play(Sfx.Pickup, 0.3f); return; }
            if (k.IsEnter || k.Is(' '))
            {
                switch (Options[sel])
                {
                    case "Continue Your Adventure":
                        if (SaveGame.Load())
                        {
                            Ui.Replace(new GameScreen());
                            Dm.Msg("{gold}Welcome back, " + Dm.Player.PName + ". The Tomb remembers you.{/}");
                            Synth.Play(Sfx.Stairs);
                        }
                        else notice = "{bad}The save file could not be read.{/}";
                        break;
                    case "Roll a New Character":
                        if (SaveGame.Exists) Prompts.YesNo("Abandon your saved adventure and roll a new character?", () => { SaveGame.Delete(); Synth.Play(Sfx.Dice); Ui.Replace(new CreationScreen()); });
                        else { Synth.Play(Sfx.Dice); Ui.Replace(new CreationScreen()); }
                        break;
                    case "Hall of Fame": Ui.Push(new HofScreen()); break;
                    case "How to Play": Ui.Push(HelpScreen.Make()); break;
                    case "Quit": RogueGame.Instance.QuitRequested = true; break;
                }
            }
            if (k.Is('q') || k.IsEsc) RogueGame.Instance.QuitRequested = true;
        }

        public override void Draw(Terminal t)
        {
            for (int i = 0; i < Options.Length; i++)
            {
                bool s = i == sel;
                string txt = s ? "»  " + Options[i] + "  «" : Options[i];
                t.Center(30 + i * 2, s ? "{gold}" + txt + "{/}" : "{dim}" + txt + "{/}", Pal.Text);
            }
            if (notice != null) t.Center(40, notice, Pal.Text);
            t.Center(42, "{faint}A roguelike in the spirit of Advanced Dungeons & Dragons · arrows + Enter{/}", Pal.Faint);
        }

        public override void DrawPixels(Gfx g)
        {
            foreach (var (p, s, ph) in stars)
            {
                float a = 0.3f + 0.3f * (float)Math.Sin(time * s + ph);
                g.Rect(p.X, p.Y, 2, 2, new Color(200, 190, 255) * a);
            }
            var center = new Vector2(798, 300);
            for (int r = 6; r > 0; r--) g.Rect(center.X - r * 40, center.Y - r * 40, r * 80, r * 80, new Color(80, 20, 40) * 0.02f);
            Icosahedron.Draw(g, center, 150, time, new Color(255, 120, 150), true);
            string title = "TOMB OF THE TWENTY-SIDED HEART";
            var m = g.Big.MeasureString(title);
            float glow = 0.6f + 0.4f * (float)Math.Sin(time * 1.5f);
            g.Text(g.Big, title, new Vector2(798 - m.X / 2 + 3, 492 + 3), new Color(60, 0, 20) * 0.8f);
            g.Text(g.Big, title, new Vector2(798 - m.X / 2, 492), Color.Lerp(new Color(255, 200, 120), new Color(255, 240, 200), glow));
            string sub = "— the Hollow King waits below —";
            var ms = g.Font.MeasureString(sub);
            g.Text(g.Font, sub, new Vector2(798 - ms.X / 2, 548), new Color(180, 160, 200));
        }
    }

    public class CreationScreen : Screen
    {
        enum Step { Stats, Race, Class, Align, Name, Confirm }
        Step step;
        readonly int[] stats = new int[6];
        readonly int[][] rolls = new int[6][];
        int cursor;
        int swapFrom = -1;
        Race race;
        Cls cls;
        Align align;
        string name;
        int strEx;
        float rollTime;
        float time;
        int rerolls;

        public CreationScreen()
        {
            Roll();
            name = Lore.RandomName(Race.Human);
        }

        void Roll()
        {
            for (int i = 0; i < 6; i++)
            {
                var r = new[] { Rng.D(6), Rng.D(6), Rng.D(6), Rng.D(6) };
                rolls[i] = r;
                stats[i] = r.Sum() - r.Min();
            }
            rollTime = time;
            rerolls++;
            Synth.Play(Sfx.Dice);
        }

        int Adj(int i) => stats[i] + Rules.Races[race].Adj[i];

        bool ClassOk(Cls c, out string why)
        {
            var ci = Rules.Classes[c];
            why = null;
            if (!Rules.Races[race].Classes.Contains(c)) { why = Rules.Races[race].Name + "s cannot be " + ci.Name + "s"; return false; }
            for (int i = 0; i < 6; i++)
                if (Adj(i) < ci.Min[i]) { why = "needs " + Rules.StatNames[i] + " " + ci.Min[i]; return false; }
            return true;
        }

        public override void Update(float dt) => time += dt;

        public override void Key(KeyIn k)
        {
            if (k.IsEsc)
            {
                if (step == Step.Stats) { Ui.Replace(new TitleScreen()); return; }
                step--;
                cursor = 0;
                return;
            }
            int mv = k.MenuMove();
            switch (step)
            {
                case Step.Stats:
                    if (mv != 0) { cursor = (cursor + Math.Sign(mv) + 6) % 6; return; }
                    if (k.Is('r')) { Roll(); swapFrom = -1; return; }
                    if (k.Is(' ') || (k.IsEnter && swapFrom >= 0))
                    {
                        if (swapFrom < 0) swapFrom = cursor;
                        else { (stats[swapFrom], stats[cursor]) = (stats[cursor], stats[swapFrom]); (rolls[swapFrom], rolls[cursor]) = (rolls[cursor], rolls[swapFrom]); swapFrom = -1; Synth.Play(Sfx.Pickup, 0.4f); }
                        return;
                    }
                    if (k.IsEnter || k.Is(Keys.Tab)) { step = Step.Race; cursor = 0; }
                    return;
                case Step.Race:
                    {
                        var races = Rules.Races.Keys.ToList();
                        if (mv != 0) { cursor = (cursor + Math.Sign(mv) + races.Count) % races.Count; race = races[cursor]; return; }
                        race = races[cursor];
                        if (k.IsEnter) { step = Step.Class; cursor = 0; cls = Rules.Classes.Keys.FirstOrDefault(c => ClassOk(c, out _)); cursor = Rules.Classes.Keys.ToList().IndexOf(cls); }
                        return;
                    }
                case Step.Class:
                    {
                        var classes = Rules.Classes.Keys.ToList();
                        if (mv != 0) { cursor = (cursor + Math.Sign(mv) + classes.Count) % classes.Count; cls = classes[cursor]; return; }
                        cls = classes[cursor];
                        if (k.IsEnter && ClassOk(cls, out _))
                        {
                            step = Step.Align;
                            cursor = 0;
                            align = Rules.Classes[cls].Aligns[0];
                            strEx = Adj(0) == 18 && Rules.Classes[cls].Warrior ? Rng.D(100) : 0;
                            if (strEx > 0) Synth.Play(Sfx.Dice);
                        }
                        return;
                    }
                case Step.Align:
                    {
                        var al = Rules.Classes[cls].Aligns;
                        if (mv != 0) { cursor = (cursor + Math.Sign(mv) + al.Length) % al.Length; align = al[cursor]; return; }
                        align = al[Math.Min(cursor, al.Length - 1)];
                        if (k.IsEnter) { step = Step.Name; name = Lore.RandomName(race); }
                        return;
                    }
                case Step.Name:
                    if (k.IsEnter) { if (name.Trim().Length > 0) step = Step.Confirm; return; }
                    if (k.Is(Keys.Back)) { if (name.Length > 0) name = name.Substring(0, name.Length - 1); return; }
                    if (k.Is(Keys.Tab)) { name = Lore.RandomName(race); return; }
                    if (k.Ch >= ' ' && k.Ch != '|' && name.Length < 20) name += k.Ch;
                    return;
                case Step.Confirm:
                    if (k.IsEnter || k.Is('y')) Begin();
                    return;
            }
        }

        void Begin()
        {
            Dm.Ident.Shuffle();
            Map dummy = null;
            Dm.Map = dummy;
            var p = Chargen.Make(name.Trim(), race, cls, align, stats, strEx);
            var gs = new GameScreen();
            Ui.Replace(gs);
            Dm.NewGame(p);
            Synth.Play(Sfx.Stairs);
        }

        public override void Draw(Terminal t)
        {
            t.Frame(4, 1, 125, 43, Pal.FrameHi, Pal.Panel, "Roll Up a Character");
            string[] steps = { "Abilities", "Race", "Class", "Alignment", "Name", "Begin" };
            int sx = 10;
            for (int i = 0; i < steps.Length; i++)
            {
                var c = i == (int)step ? Pal.Gold : i < (int)step ? Pal.C(150, 200, 150) : Pal.Faint;
                t.Print(sx, 3, (i + 1) + ". " + steps[i], c);
                sx += steps[i].Length + 8;
            }
            int lx = 8, ly = 6;
            t.PrintM(lx, ly, "{gold}Ability Scores{/} {dim}(4d6, drop the lowest){/}", Pal.Gold);
            for (int i = 0; i < 6; i++)
            {
                int y = ly + 2 + i * 2;
                bool sel = step == Step.Stats && i == cursor;
                if (sel) t.Fill(lx - 1, y, 50, 1, Pal.C(60, 46, 28));
                if (swapFrom == i) t.Fill(lx - 1, y, 50, 1, Pal.C(40, 60, 90));
                t.Print(lx, y, Rules.StatLong[i].PadRight(14), Pal.Text);
                float age = time - rollTime - i * 0.08f;
                bool rolling = age < 0.5f;
                int shown = rolling ? Rng.Range(3, 18) : stats[i];
                int adj = step >= Step.Race ? Rules.Races[race].Adj[i] : 0;
                int val = shown + (rolling ? 0 : adj);
                var col = val >= 16 ? Pal.Gold : val <= 7 ? Pal.C(230, 110, 90) : Pal.White;
                string vs = i == 0 && strEx > 0 && val == 18 ? "18/" + (strEx >= 100 ? "00" : strEx.ToString("00")) : val.ToString();
                t.Print(lx + 15, y, vs.PadLeft(5), col, Pal.C(36, 32, 40));
                if (adj != 0 && !rolling) t.Print(lx + 21, y, (adj > 0 ? "+" : "") + adj, adj > 0 ? Pal.Green : Pal.Red);
                if (!rolling)
                {
                    var r = rolls[i].OrderByDescending(v => v).ToArray();
                    string dice = "[" + r[0] + " " + r[1] + " " + r[2] + " {faint}" + r[3] + "{/}]";
                    t.PrintM(lx + 25, y, "{dim}" + dice + "{/}", Pal.Dim);
                }
            }
            if (step == Step.Stats)
            {
                t.PrintM(lx, ly + 15, "{yellow}r{/} reroll   {yellow}Space{/} select & swap two scores   {yellow}Enter{/} accept", Pal.Text);
                t.PrintM(lx, ly + 16, "{dim}Rolls so far: " + rerolls + ".{/}", Pal.Dim);
                t.PrintM(lx, ly + 18, "{dim}Classes these scores allow:{/}", Pal.Dim);
                int yy = ly + 19;
                var saveRace = race;
                foreach (var c in Rules.Classes.Values)
                {
                    bool any = Rules.Races.Keys.Any(r => { race = r; return ClassOk(c.Id, out _); });
                    race = saveRace;
                    t.PrintM(lx + 2, yy++, (any ? "{good}• " : "{faint}· ") + c.Name + "{/}", Pal.Text);
                }
            }
            int rx = 64, ry = 6;
            if (step == Step.Race)
            {
                var races = Rules.Races.Values.ToList();
                t.PrintM(rx, ry, "{gold}Choose your Race{/}", Pal.Gold);
                for (int i = 0; i < races.Count; i++)
                {
                    bool s = i == cursor;
                    var r = races[i];
                    string adj = string.Join(" ", Enumerable.Range(0, 6).Where(j => r.Adj[j] != 0).Select(j => (r.Adj[j] > 0 ? "+" : "") + r.Adj[j] + " " + Rules.StatNames[j]));
                    t.PrintM(rx, ry + 2 + i, (s ? "{gold}» " : "  ") + r.Name.PadRight(10) + (s ? "{/}" : "") + " {dim}" + adj + "{/}", Pal.Text);
                }
                var cur = races[cursor];
                t.PrintWrap(rx, ry + 11, 58, cur.Desc, Pal.Text);
                t.PrintWrap(rx, ry + 16, 58, "{dim}Classes:{/} " + string.Join(", ", cur.Classes.Select(c => Rules.Classes[c].Name)), Pal.Text);
            }
            if (step == Step.Class)
            {
                var classes = Rules.Classes.Values.ToList();
                t.PrintM(rx, ry, "{gold}Choose your Class{/}", Pal.Gold);
                for (int i = 0; i < classes.Count; i++)
                {
                    bool s = i == cursor;
                    bool ok = ClassOk(classes[i].Id, out var why);
                    t.PrintM(rx, ry + 2 + i, (s ? "{gold}» " : "  ") + classes[i].Name.PadRight(12) + (s ? "{/}" : "") + (ok ? " {dim}d" + classes[i].HitDie + " HD, prime " + classes[i].Prime + "{/}" : " {red}" + why + "{/}"), ok ? Pal.Text : Pal.Faint);
                }
                t.PrintWrap(rx, ry + 10, 58, classes[cursor].Desc, Pal.Text);
                if (Adj(0) == 18 && classes[cursor].Warrior) t.PrintWrap(rx, ry + 17, 58, "{gold}With 18 Strength, a warrior rolls percentile dice for exceptional strength!{/}", Pal.Text);
            }
            if (step == Step.Align)
            {
                var al = Rules.Classes[cls].Aligns;
                t.PrintM(rx, ry, "{gold}Choose your Alignment{/}", Pal.Gold);
                for (int i = 0; i < al.Length; i++)
                {
                    bool s = i == cursor;
                    t.PrintM(rx, ry + 2 + i, (s ? "{gold}» " : "  ") + Rules.AlignNames[(int)al[i]].PadRight(16) + (s ? "{/}" : "") + " {dim}serves " + Rules.Deities[(int)al[i]] + "{/}", Pal.Text);
                }
                if (strEx > 0) t.PrintM(rx, ry + 13, "{gold}You rolled percentile strength: 18/" + (strEx >= 100 ? "00" : strEx.ToString("00")) + "!{/}", Pal.Gold);
            }
            if (step == Step.Name)
            {
                t.PrintM(rx, ry, "{gold}What is your name, adventurer?{/}", Pal.Gold);
                t.Fill(rx, ry + 2, 30, 1, Pal.C(36, 32, 40));
                t.Print(rx + 1, ry + 2, name + ((int)(time * 2) % 2 == 0 ? "_" : " "), Pal.White, Pal.C(36, 32, 40));
                t.PrintM(rx, ry + 4, "{dim}Type a name · Tab for a random one · Enter to accept{/}", Pal.Dim);
            }
            if (step == Step.Confirm)
            {
                t.PrintM(rx, ry, "{gold}" + name + "{/}", Pal.Gold);
                t.PrintM(rx, ry + 1, Rules.Races[race].Name + " " + Rules.Classes[cls].Name + ", " + Rules.AlignNames[(int)align], Pal.Text);
                t.PrintM(rx, ry + 2, "{dim}Servant of{/} " + Rules.Deities[(int)align], Pal.Text);
                t.PrintWrap(rx, ry + 4, 58, "{dim}Starting gear:{/} " + string.Join(", ", Chargen.KitNames(cls)), Pal.Text);
                t.PrintWrap(rx, ry + 10, 58, "The torch sputters. The stairs go down. Somewhere below, a dead king waits on a throne of bone, and something in its hands is beating.", Pal.Text);
                t.PrintM(rx, ry + 15, "{yellow}Enter{/} to descend into the Tomb · {yellow}Esc{/} to go back", Pal.Text);
            }
            t.PrintM(8, 42, "{dim}Esc: back{/}", Pal.Dim);
        }

        public override void DrawPixels(Gfx g)
        {
            Icosahedron.Draw(g, new Vector2(1440, 120), 46, time * (time - rollTime < 0.6f ? 4f : 1f), new Color(200, 120, 160), false);
        }
    }

    public static class Chargen
    {
        public static List<string> KitNames(Cls c) => Kit(c).Select(i => i.Name()).ToList();

        static List<Item> Kit(Cls c)
        {
            var l = new List<Item>();
            Item I(string id, int n = 1) { var it = Item.Make(id, n); it.Known = true; l.Add(it); return it; }
            switch (c)
            {
                case Cls.Fighter: I("long_sword"); I("chain"); I("shield"); I("dagger"); I("heal"); break;
                case Cls.Paladin: I("long_sword"); I("splint"); I("shield"); I("holy_symbol"); I("holy_water", 2); I("heal"); break;
                case Cls.Ranger: I("long_sword"); I("long_bow"); I("arrows", 40); I("studded"); I("heal"); break;
                case Cls.Cleric: I("mace"); I("chain"); I("shield"); I("holy_symbol"); I("holy_water", 3); I("heal"); break;
                case Cls.MagicUser: I("quarterstaff"); I("robe"); I("dagger"); I("dart", 12); I("heal"); I("scroll_" + SpellId.Identify); break;
                case Cls.Thief: I("short_sword"); I("leather"); I("dart", 10); I("thieves_tools"); I("dagger"); I("heal"); break;
            }
            I("torch", 4);
            I("ration", 3);
            I("pole");
            if (c != Cls.MagicUser) I("spikes", 4);
            I("oil", 2);
            return l;
        }

        public static Player Make(string name, Race race, Cls cls, Align align, int[] stats, int strEx)
        {
            var p = new Player { PName = name, Race = race, Class = cls, Align = align };
            for (int i = 0; i < 6; i++) p.Base[i] = stats[i];
            p.StrEx = strEx;
            var ci = Rules.Classes[cls];
            int con = p.RawStat(Stat.Con);
            p.MaxHp = ci.HitDie + Rules.ConHp(con, ci.Warrior);
            if (cls == Cls.Ranger) p.MaxHp += Rng.D(8) + Rules.ConHp(con, true);
            p.MaxHp = Math.Max(3, p.MaxHp) + 6;
            p.Hp = p.MaxHp;
            p.Level = 1;
            p.Gold = Rng.Roll(3, 6) * 10;
            p.Energy = Dm.ActCost;
            foreach (var it in Kit(cls))
            {
                if (it.Def.Unknown) Dm.Ident.Learn(it.Def.Id);
                Actions_AddTo(p, it);
            }
            if (cls == Cls.MagicUser)
            {
                p.Known.Add(SpellId.MagicMissile);
                p.Known.Add(SpellId.Sleep);
                var extra = new[] { SpellId.Shield, SpellId.BurningHands, SpellId.CharmPerson, SpellId.Light, SpellId.ShockingGrasp, SpellId.FindFamiliar, SpellId.DetectMagic, SpellId.ProtEvil };
                var e1 = extra[Rng.Int(extra.Length)];
                p.Known.Add(e1);
                var e2 = extra.Where(s => s != e1).ToArray()[Rng.Int(extra.Length - 1)];
                p.Known.Add(e2);
            }
            Magic.AutoPlan(p);
            p.Memorized.AddRange(p.Plan);
            return p;
        }

        static void Actions_AddTo(Player p, Item it)
        {
            foreach (var o in p.Pack)
                if (o.CanStack(it)) { o.Count += it.Count; return; }
            var used = p.Pack.Select(i => i.Letter).ToHashSet();
            foreach (char c in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ")
                if (!used.Contains(c)) { it.Letter = c; break; }
            p.Pack.Add(it);
            var d = it.Def;
            if (d.Kind == IK.Weapon && p.Weapon == null && !(d.Has(WF.Launcher) && p.Class != Cls.Ranger) && d.Id != "dagger" && d.Id != "dart") p.Eq[Slot.Weapon] = it;
            if (d.Kind == IK.Weapon && d.Id == "long_sword" && p.Class == Cls.Ranger) p.Eq[Slot.Weapon] = it;
            if (d.Kind == IK.Armor && p.Worn(Slot.Body) == null) p.Eq[Slot.Body] = it;
            if (d.Kind == IK.Shield && p.Worn(Slot.Shield) == null) p.Eq[Slot.Shield] = it;
            if (d.Kind == IK.Ammo && p.Worn(Slot.Quiver) == null) p.Eq[Slot.Quiver] = it;
            if (d.Id == "torch" && p.Worn(Slot.Light) == null)
            {
                if (it.Count > 1)
                {
                    var one = it.Split(1);
                    foreach (char c in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ")
                        if (!p.Pack.Any(i => i.Letter == c)) { one.Letter = c; break; }
                    p.Pack.Add(one);
                    one.Lit = true;
                    p.Eq[Slot.Light] = one;
                }
                else { it.Lit = true; p.Eq[Slot.Light] = it; }
            }
        }
    }
}
