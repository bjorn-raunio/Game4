using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Icosa
{
    public class MenuItem
    {
        public char Key;
        public string Text;
        public Color Color = Pal.Text;
        public object Tag;
        public bool Enabled = true;
        public bool Header;
        public bool Selected;
        public string Right;
    }

    public class MenuScreen : Screen
    {
        readonly string title;
        readonly List<MenuItem> items;
        readonly Action<MenuItem> pick;
        readonly Action<List<MenuItem>> pickMany;
        readonly Func<MenuItem, string> describe;
        readonly string footer;
        int cursor;
        int scroll;
        public int Width = 64;
        public bool CloseOnPick = true;

        public override bool Overlay => true;

        public MenuScreen(string title, List<MenuItem> items, Action<MenuItem> pick, Func<MenuItem, string> describe = null, string footer = null)
        {
            this.title = title;
            this.items = items;
            this.pick = pick;
            this.describe = describe;
            this.footer = footer;
            cursor = items.FindIndex(i => !i.Header && i.Enabled);
            if (cursor < 0) cursor = items.FindIndex(i => !i.Header);
            if (cursor < 0) cursor = 0;
            if (describe != null) Width = 56;
        }

        public MenuScreen(string title, List<MenuItem> items, Action<List<MenuItem>> pickMany, Func<MenuItem, string> describe = null)
            : this(title, items, (Action<MenuItem>)null, describe, "Space/letters toggle · Enter confirm · , all · Esc cancel")
        {
            this.pickMany = pickMany;
        }

        int Visible => Math.Min(items.Count, 34);
        int drawX0, drawY0;

        public override void Update(float dt)
        {
            if (Ui.Top != this || Ui.MouseCell is not Point mc) return;
            int row = mc.Y - drawY0 - 2;
            bool inside = mc.X > drawX0 && mc.X < drawX0 + Width - 1 && row >= 0 && row < Visible;
            int idx = row + scroll;
            if (inside && idx < items.Count && !items[idx].Header)
            {
                if (Ui.MouseMoved) cursor = idx;
                if (Ui.Click)
                {
                    Ui.Click = false;
                    cursor = idx;
                    if (pickMany != null) items[idx].Selected = !items[idx].Selected;
                    else if (items[idx].Enabled) Choose(items[idx]);
                }
            }
            else if (Ui.RightClick) { Ui.RightClick = false; Ui.Pop(this); }
        }

        public override void Key(KeyIn k)
        {
            if (k.IsEsc) { Ui.Pop(this); return; }
            int mv = k.MenuMove();
            if (mv != 0) { Move(mv); return; }
            if (pickMany != null)
            {
                if (k.Is(',')) { bool all = items.Where(i => !i.Header && i.Enabled).All(i => i.Selected); foreach (var i in items.Where(i => !i.Header && i.Enabled)) i.Selected = !all; return; }
                if (k.IsEnter)
                {
                    var sel = items.Where(i => i.Selected).ToList();
                    if (sel.Count == 0 && cursor >= 0 && cursor < items.Count && !items[cursor].Header) sel.Add(items[cursor]);
                    Ui.Pop(this);
                    if (sel.Count > 0) pickMany(sel);
                    return;
                }
                if (k.Is(' ') && cursor >= 0 && cursor < items.Count && !items[cursor].Header) { items[cursor].Selected = !items[cursor].Selected; Move(1); return; }
                var mi = items.FirstOrDefault(i => i.Key == k.Ch && k.Ch != '\0' && !i.Header && i.Enabled);
                if (mi != null) mi.Selected = !mi.Selected;
                return;
            }
            if (k.IsEnter)
            {
                if (cursor >= 0 && cursor < items.Count && !items[cursor].Header && items[cursor].Enabled) Choose(items[cursor]);
                return;
            }
            if (k.Ch != '\0')
            {
                var mi = items.FirstOrDefault(i => i.Key == k.Ch && !i.Header);
                if (mi != null && mi.Enabled) Choose(mi);
            }
        }

        void Choose(MenuItem mi)
        {
            if (CloseOnPick) Ui.Pop(this);
            pick?.Invoke(mi);
        }

        void Move(int d)
        {
            if (items.Count == 0) return;
            int n = cursor;
            int step = Math.Sign(d);
            for (int i = 0; i < Math.Abs(d); i++)
            {
                int c = n;
                do { c += step; } while (c >= 0 && c < items.Count && items[c].Header);
                if (c < 0 || c >= items.Count) break;
                n = c;
            }
            cursor = n;
            if (cursor < scroll) scroll = cursor;
            if (cursor >= scroll + Visible) scroll = cursor - Visible + 1;
            if (scroll > 0 && cursor > 0 && items[cursor - 1].Header && cursor - 1 < scroll) scroll = cursor - 1;
        }

        public override void Draw(Terminal t)
        {
            int w = Width;
            int h = Visible + 4 + (footer != null ? 1 : 0);
            int dw = describe != null ? 40 : 0;
            int x0 = (Terminal.W - w - dw) / 2, y0 = Math.Max(1, (Terminal.H - h) / 2 - 1);
            drawX0 = x0;
            drawY0 = y0;
            t.Frame(x0, y0, w, h, Pal.FrameHi, Pal.Panel, title);
            for (int i = 0; i < Visible; i++)
            {
                int idx = i + scroll;
                if (idx >= items.Count) break;
                var it = items[idx];
                int y = y0 + 2 + i;
                if (it.Header)
                {
                    t.PrintM(x0 + 2, y, "{gold}" + it.Text + "{/}", Pal.Gold, w - 4);
                    continue;
                }
                bool sel = idx == cursor;
                if (sel) for (int xx = x0 + 1; xx < x0 + w - 1; xx++) t.SetBg(xx, y, Pal.C(60, 46, 28));
                var col = it.Enabled ? it.Color : Pal.Faint;
                string prefix = it.Key != '\0' ? it.Key + (pickMany != null ? (it.Selected ? " + " : " - ") : " - ") : "  ";
                t.Print(x0 + 2, y, prefix, it.Enabled ? Pal.Yellow : Pal.Faint);
                int maxText = w - 6 - (it.Right != null ? it.Right.Length + 1 : 0);
                t.PrintM(x0 + 2 + prefix.Length, y, it.Enabled ? it.Text : Markup.Strip(it.Text), col, maxText - prefix.Length + 2);
                if (it.Right != null) t.PrintM(x0 + w - 2 - Markup.Len(it.Right), y, it.Right, Pal.Dim);
            }
            if (items.Count > Visible)
            {
                if (scroll > 0) t.Put(x0 + w - 2, y0 + 1, '▲', Pal.Dim);
                if (scroll + Visible < items.Count) t.Put(x0 + w - 2, y0 + h - 2, '▼', Pal.Dim);
            }
            if (footer != null) t.PrintM(x0 + 2, y0 + h - 2, "{dim}" + footer + "{/}", Pal.Dim, w - 4);
            if (describe != null && cursor >= 0 && cursor < items.Count && !items[cursor].Header)
            {
                string d = describe(items[cursor]);
                if (!string.IsNullOrEmpty(d))
                {
                    int dx = x0 + w + 1;
                    var lines = Markup.Wrap(d, dw - 4);
                    int dh = Math.Max(5, lines.Count + 4);
                    t.Frame(dx, y0, dw - 1, dh, Pal.Frame, Pal.Panel);
                    for (int i = 0; i < lines.Count; i++) t.PrintM(dx + 2, y0 + 2 + i, lines[i], Pal.Text);
                }
            }
        }
    }

    public class YesNoScreen : Screen
    {
        readonly string q;
        readonly Action yes, no;
        public override bool Overlay => true;

        public YesNoScreen(string q, Action yes, Action no)
        {
            this.q = q;
            this.yes = yes;
            this.no = no;
        }

        public override void Key(KeyIn k)
        {
            if (k.Is('y') || k.Is('Y')) { Ui.Pop(this); yes?.Invoke(); }
            else if (k.Is('n') || k.Is('N') || k.IsEsc) { Ui.Pop(this); no?.Invoke(); }
        }

        public override void Draw(Terminal t)
        {
            int w = Math.Max(30, Markup.Len(q) + 6);
            int x0 = (100 - w) / 2, y0 = 16;
            t.Frame(x0, y0, w, 5, Pal.FrameHi, Pal.Panel);
            t.PrintM(x0 + 3, y0 + 2, q + " {yellow}[y/n]{/}", Pal.Hi);
        }
    }

    public class DirectionScreen : Screen
    {
        readonly string prompt;
        readonly Action<Point> pick;
        readonly bool allowSelf;
        public override bool Overlay => true;

        public DirectionScreen(string prompt, Action<Point> pick, bool allowSelf)
        {
            this.prompt = prompt;
            this.pick = pick;
            this.allowSelf = allowSelf;
        }

        public override void Key(KeyIn k)
        {
            if (k.IsEsc) { Ui.Pop(this); return; }
            if (k.Dir(out var d, out _)) { Ui.Pop(this); pick(d); return; }
            if (allowSelf && (k.IsWait || k.Is('>') || k.Is('<'))) { Ui.Pop(this); pick(Point.Zero); }
        }

        public override void Draw(Terminal t)
        {
            string s = prompt + " {dim}(direction" + (allowSelf ? ", . for here" : "") + ", Esc cancels){/}";
            t.Fill(0, 37, 100, 1, Pal.C(40, 30, 15));
            t.PrintM(1, 37, s, Pal.Hi);
        }
    }

    public class TargetScreen : Screen
    {
        readonly string prompt;
        readonly Action<Point> pick;
        readonly int range;
        readonly bool look;
        Point cur;
        readonly List<Monster> targets;
        int tIdx;
        public static Monster LastTarget;
        public override bool Overlay => true;
        string detail;

        public TargetScreen(string prompt, int range, Action<Point> pick, bool look = false)
        {
            this.prompt = prompt;
            this.pick = pick;
            this.range = range;
            this.look = look;
            var p = Dm.Player;
            targets = Dm.Map.Monsters.Where(m => !m.Dead && Dm.Seen(m) != SeenKind.No && !m.Disguised && (look || m.Hostile)).OrderBy(m => Path.Cheb(m.Pos, p.Pos)).ToList();
            cur = p.Pos;
            if (LastTarget != null && targets.Contains(LastTarget)) { tIdx = targets.IndexOf(LastTarget); cur = LastTarget.Pos; }
            else if (targets.Count > 0 && (!look || targets.Any(m => m.Hostile))) { tIdx = 0; cur = targets[0].Pos; }
        }

        public override void Key(KeyIn k)
        {
            if (k.IsEsc) { if (detail != null) { detail = null; return; } Ui.Pop(this); return; }
            if (k.Dir(out var d, out bool run))
            {
                int n = run ? 5 : 1;
                cur = new Point(Math.Clamp(cur.X + d.X * n, 0, Map.W - 1), Math.Clamp(cur.Y + d.Y * n, 0, Map.H - 1));
                detail = null;
                return;
            }
            if (k.Is(Keys.Tab) || k.Is('+') || k.Is('=') || (k.Is(' ') && !look))
            {
                if (targets.Count > 0) { tIdx = (tIdx + (k.Shift ? targets.Count - 1 : 1)) % targets.Count; cur = targets[tIdx].Pos; detail = null; }
                return;
            }
            if (k.Is('-'))
            {
                if (targets.Count > 0) { tIdx = (tIdx + targets.Count - 1) % targets.Count; cur = targets[tIdx].Pos; }
                return;
            }
            if (k.IsEnter || k.Is('f') || k.Is('t') || k.Is('z') || k.Is('.') || k.Is('c') || (look && k.Is('x')))
            {
                if (look)
                {
                    detail = Describe(cur, true);
                    return;
                }
                var m = Dm.Map.MonsterAt(cur);
                if (m != null) LastTarget = m;
                Ui.Pop(this);
                pick(cur);
            }
        }

        public static string Describe(Point c, bool full)
        {
            var map = Dm.Map;
            var p = Dm.Player;
            if (!Map.InB(c)) return "";
            if (c == p.Pos) return "{white}You{/}: " + p.PName + " the " + p.Title + ".";
            if (!map.Seen[c.X, c.Y] && !map.Vis[c.X, c.Y])
            {
                var mm = map.MonsterAt(c);
                if (mm != null && Dm.Seen(mm) != SeenKind.No) return "You sense " + mm.A + " there.";
                return "{dim}Unexplored.{/}";
            }
            var parts = new List<string>();
            var m = map.MonsterAt(c);
            if (m != null && Dm.Seen(m) != SeenKind.No)
            {
                if (m.Disguised) parts.Add(Markup.Hex(ItemDb.Get("chest").C) + "a chest{/}");
                else
                {
                    var s = Dm.Seen(m);
                    string st = MonsterState(m);
                    string name = Markup.Hex(m.Def.C) + (m.Proper ?? Item.Article(m.Def.Name)) + "{/}";
                    if (s == SeenKind.Infra) name = "{red}a warm shape (infravision){/}";
                    if (s == SeenKind.Esp) name = "{magenta}a mind (telepathy): " + m.Def.Name + "{/}";
                    parts.Add(name + (st.Length > 0 ? " {dim}(" + st + "){/}" : ""));
                    if (full && s != SeenKind.Infra)
                    {
                        var d = m.Def;
                        string hd = d.HalfHD ? "½" : d.HD + (d.HpPlus > 0 ? "+" + d.HpPlus : d.HpPlus < 0 ? d.HpPlus.ToString() : "");
                        parts.Add("\n\n{gold}" + d.Name.ToUpper() + "{/}\n{dim}HD{/} " + hd + "  {dim}AC{/} " + d.AC + "  {dim}Move{/} " + d.Move + "\"  {dim}Size{/} " + d.Size + "  {dim}Morale{/} " + d.Morale +
                            "\n{dim}Attacks:{/} " + (d.Attacks.Count == 0 ? "none" : string.Join(", ", d.Attacks.Select(a => a.Dmg + " " + a.Verb + (a.Sp != AtkSp.None ? " (" + a.Sp.ToString().ToLower() + ")" : "")))) +
                            (d.Ranged != null ? "\n{dim}Ranged:{/} " + d.Ranged.Name + " " + d.Ranged.Dmg : "") +
                            (d.Breath != null ? "\n{dim}Breath:{/} " + d.Breath.Name + " " + d.Breath.Dmg : "") +
                            (d.Spells.Count > 0 ? "\n{dim}Spells:{/} " + string.Join(", ", d.Spells.Where(sp => sp != SpellId.EyeRays).Select(sp => SpellDb.Get(sp).Name).DefaultIfEmpty("eye rays")) : "") +
                            (d.GazeSp != AtkSp.None ? "\n{dim}Gaze:{/} " + d.GazeSp.ToString().ToLower() : "") +
                            Traits(d) + "\n\n{ink}" + d.Desc + "{/}");
                    }
                }
            }
            if (map.Vis[c.X, c.Y] || map.Seen[c.X, c.Y])
            {
                var items = map.ItemsAt(c).ToList();
                if (items.Count == 1) parts.Add(items[0].Name());
                else if (items.Count > 1) parts.Add(items[0].Name() + " and " + (items.Count - 1) + " more");
                var tr = map.TrapAt(c);
                if (tr != null && tr.Found) parts.Add("{red}" + Item.Article(tr.Name) + "{/}");
                var td = map.Def(c);
                if (map.T[c.X, c.Y] == Tile.DoorLocked && map.Seen[c.X, c.Y]) parts.Add(td.Name);
                else parts.Add((parts.Count > 0 ? "on " : "") + Item.Article(td.Name));
                if (full && td.Desc != null && m == null) parts.Add("\n{dim}" + td.Desc + "{/}");
                if (map.Engravings.TryGetValue(c, out var eng) && full) parts.Add("\n{ink}Written here: \"" + eng + "\"{/}");
            }
            return string.Join(", ", parts).Replace(", \n", "\n");
        }

        static string Traits(MonsterDef d)
        {
            var t = new List<string>();
            if (d.Has(MF.Undead)) t.Add("undead");
            if (d.Has(MF.NeedMagic)) t.Add("hit only by magic");
            if (d.Has(MF.NeedSilver)) t.Add("hit only by silver or magic");
            if (d.Has(MF.Regen)) t.Add("regenerates");
            if (d.Has(MF.Revives)) t.Add("rises again unless burned");
            if (d.Has(MF.Fly)) t.Add("flies");
            if (d.Has(MF.Edged)) t.Add("resists edged weapons");
            if (d.Has(MF.Splits)) t.Add("splits");
            if (d.Has(MF.ResFire)) t.Add("fire-immune");
            if (d.Has(MF.ResCold)) t.Add("cold-immune");
            if (d.Has(MF.ResElec)) t.Add("shock-immune");
            if (d.Has(MF.VulnFire)) t.Add("vulnerable to fire");
            if (d.Has(MF.Giant)) t.Add("giant-class");
            if (d.Has(MF.Burrow)) t.Add("burrows");
            if (d.Has(MF.Displace)) t.Add("displaced");
            return t.Count == 0 ? "" : "\n{dim}Traits:{/} " + string.Join(", ", t);
        }

        public static string MonsterState(Monster m)
        {
            var s = new List<string>();
            if (m.Ally) s.Add("ally");
            else if (m.Peaceful) s.Add("peaceful");
            if (m.Sleeping || m.Has(Status.Asleep)) s.Add("asleep");
            if (m.Has(Status.Paralyzed)) s.Add("paralyzed");
            if (m.Has(Status.Stunned)) s.Add("stunned");
            if (m.Fleeing || m.Has(Status.Turned)) s.Add("fleeing");
            if (m.Has(Status.Webbed)) s.Add("stuck");
            if (m.Has(Status.Slowed)) s.Add("slowed");
            if (m.Has(Status.Hasted)) s.Add("hasted");
            if (m.Has(Status.Confused)) s.Add("confused");
            float f = m.HpFrac;
            if (!m.Ally && !m.Peaceful)
            {
                if (f < 0.25f) s.Add("almost dead");
                else if (f < 0.5f) s.Add("badly wounded");
                else if (f < 0.85f) s.Add("wounded");
            }
            return string.Join(", ", s);
        }

        public override void Draw(Terminal t)
        {
            var p = Dm.Player;
            if (!look)
            {
                foreach (var q in Path.Line(p.Pos, cur).Skip(1))
                {
                    if (q == cur) break;
                    if (Path.Cheb(p.Pos, q) > range) break;
                    t.SetBg(q.X, q.Y + 1, Pal.Lerp(t.Bg[q.X, q.Y + 1], Pal.C(200, 180, 80), 0.35f));
                }
            }
            bool blink = (Ui.Time * 3) % 1 < 0.6f;
            var cb = Path.Cheb(p.Pos, cur) > range && !look ? Pal.C(160, 40, 40) : Pal.C(220, 190, 80);
            t.SetBg(cur.X, cur.Y + 1, blink ? cb : Pal.Lerp(t.Bg[cur.X, cur.Y + 1], cb, 0.4f));
            t.Fill(0, 37, 100, 1, Pal.C(40, 30, 15));
            string hint = look ? "{dim}(arrows move, Tab cycles, Enter details, Esc exits){/}" : "{dim}(arrows/Tab to aim, Enter/f to fire, Esc cancels){/}";
            t.PrintM(1, 37, prompt + " " + hint, Pal.Hi, 99);
            string desc = Describe(cur, false);
            t.Fill(0, 38, 100, 1, Pal.C(24, 18, 10));
            t.PrintM(1, 38, desc.Split('\n')[0], Pal.Text, 98);
            if (detail != null)
            {
                var lines = Markup.Wrap(detail, 56);
                int h = Math.Min(30, lines.Count + 4);
                int x0 = cur.X < 50 ? 40 : 2;
                t.Frame(x0, 3, 60, h, Pal.FrameHi, Pal.Panel, "Lore");
                for (int i = 0; i < Math.Min(lines.Count, h - 4); i++) t.PrintM(x0 + 2, 5 + i, lines[i], Pal.Text);
            }
        }
    }

    public class TextScreen : Screen
    {
        readonly string title;
        readonly List<string> lines;
        int scroll;
        public override bool Overlay => true;
        readonly int w;

        public TextScreen(string title, IEnumerable<string> lines, int width = 96)
        {
            this.title = title;
            w = width;
            this.lines = new List<string>();
            foreach (var l in lines) this.lines.AddRange(Markup.Wrap(l, w - 6));
        }

        public override void Update(float dt)
        {
            if (Ui.Top == this && (Ui.Click || Ui.RightClick)) { Ui.Click = Ui.RightClick = false; Ui.Pop(this); }
        }

        public override void Key(KeyIn k)
        {
            int mv = k.MenuMove();
            if (mv != 0) { scroll = Math.Clamp(scroll + mv, 0, Math.Max(0, lines.Count - 38)); return; }
            if (k.IsEsc || k.IsEnter || k.Is(' ') || k.Is('q')) Ui.Pop(this);
        }

        public override void Draw(Terminal t)
        {
            int h = Math.Min(43, lines.Count + 4);
            int x0 = (Terminal.W - w) / 2, y0 = (Terminal.H - h) / 2;
            t.Frame(x0, y0, w, h, Pal.FrameHi, Pal.Panel, title);
            for (int i = 0; i < h - 4 && i + scroll < lines.Count; i++) t.PrintM(x0 + 3, y0 + 2 + i, lines[i + scroll], Pal.Text);
            if (lines.Count > h - 4) t.PrintM(x0 + w - 18, y0 + h - 1, " {dim}↑↓ scroll{/} ", Pal.Dim);
        }
    }

    public static class Prompts
    {
        public static void YesNo(string q, Action yes, Action no = null) => Ui.Push(new YesNoScreen(q, yes, no));

        public static void Menu(string title, List<MenuItem> items, Action<MenuItem> pick, Func<MenuItem, string> describe = null) => Ui.Push(new MenuScreen(title, items, pick, describe));

        public static void Direction(string prompt, Action<Point> pick, bool allowSelf = false) => Ui.Push(new DirectionScreen(prompt, pick, allowSelf));

        public static void Target(string prompt, int range, Action<Point> pick) => Ui.Push(new TargetScreen(prompt, range, pick));

        public static void ChooseItem(string title, Func<Item, bool> filter, Action<Item> pick, bool showAllIfNone = false)
        {
            var p = Dm.Player;
            var list = p.Pack.Where(filter).ToList();
            if (list.Count == 0 && showAllIfNone) list = p.Pack.ToList();
            if (list.Count == 0) { Dm.Msg("You don't have anything appropriate."); return; }
            Ui.Push(new MenuScreen(title, InventoryItems(list), mi => pick((Item)mi.Tag), mi => mi.Tag is Item it ? it.Describe() : null));
        }

        public static List<MenuItem> InventoryItems(IEnumerable<Item> items)
        {
            var order = new[] { IK.Heart, IK.Weapon, IK.Ammo, IK.Armor, IK.Shield, IK.Helm, IK.Cloak, IK.Boots, IK.Gloves, IK.Bracers, IK.Girdle, IK.Amulet, IK.Ring, IK.Ioun, IK.Wand, IK.Potion, IK.Scroll, IK.Book, IK.Food, IK.Corpse, IK.Light, IK.Tool, IK.Gem, IK.Chest, IK.Misc, IK.Gold };
            var result = new List<MenuItem>();
            foreach (var g in items.GroupBy(i => Group(i.Def.Kind)).OrderBy(g => Array.IndexOf(order, g.First().Def.Kind)))
            {
                result.Add(new MenuItem { Header = true, Text = GroupName(g.Key) });
                foreach (var it in g.OrderBy(i => i.Letter))
                {
                    var slot = Actions.SlotOf(it);
                    string eq = slot == null ? "" : slot switch
                    {
                        Slot.Weapon => " {cyan}(weapon in hand){/}",
                        Slot.Light => " {cyan}(in use){/}",
                        Slot.Quiver => " {cyan}(quivered){/}",
                        Slot.RingL => " {cyan}(on left hand){/}",
                        Slot.RingR => " {cyan}(on right hand){/}",
                        _ => " {cyan}(being worn){/}",
                    };
                    result.Add(new MenuItem { Key = it.Letter, Text = it.Name() + eq, Tag = it, Color = it.Def.Kind == IK.Heart ? Pal.Pink : Pal.Text, Right = it.Weight > 0 ? it.Weight + "#" : null });
                }
            }
            return result;
        }

        static IK Group(IK k) => k switch
        {
            IK.Shield or IK.Helm or IK.Cloak or IK.Boots or IK.Gloves or IK.Bracers or IK.Girdle => IK.Armor,
            IK.Corpse => IK.Food,
            _ => k,
        };

        static string GroupName(IK k) => k switch
        {
            IK.Weapon => "Weapons",
            IK.Ammo => "Ammunition",
            IK.Armor => "Armor",
            IK.Amulet => "Amulets",
            IK.Ring => "Rings",
            IK.Ioun => "Ioun Stones",
            IK.Wand => "Wands",
            IK.Potion => "Potions",
            IK.Scroll => "Scrolls",
            IK.Book => "Spellbooks",
            IK.Food => "Comestibles",
            IK.Light => "Light Sources",
            IK.Tool => "Tools",
            IK.Gem => "Gems",
            IK.Heart => "The Artifact",
            _ => "Other",
        };

        public static void Wish()
        {
            var items = Use.WishNames.Select((n, i) => new MenuItem { Key = (char)('a' + i), Text = n, Tag = i, Color = Pal.Gold }).ToList();
            var screen = new MenuScreen("Speak Thy Wish", items, mi => Use.GrantWish((int)mi.Tag));
            Ui.Push(screen);
        }
    }
}
