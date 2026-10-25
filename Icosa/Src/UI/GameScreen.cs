using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Icosa
{
    public class GameScreen : Screen
    {
        enum Auto { None, Run, Rest, Explore, Travel, Search }
        Auto auto;
        Map autoMap;
        Point runDir;
        float autoTimer;
        int autoSteps;
        Point travelGoal;
        float endTimer;
        const int OY = 1;
        const int SX = 101;

        static Player P => Dm.Player;
        static Map M => Dm.Map;
        public bool Busy => auto != Auto.None;

        public override void Update(float dt)
        {
            if (Dm.GameOver)
            {
                auto = Auto.None;
                if (!Anim.Blocking)
                {
                    endTimer += dt;
                    if (endTimer > 1.4f) Ui.Replace(Dm.Won || Dm.Retired ? new VictoryScreen() : new DeathScreen());
                }
                return;
            }
            if (Ui.Top == this && !Anim.Blocking && (Ui.Click || Ui.RightClick) && Ui.MouseCell is Point mc && mc.X < Map.W && mc.Y >= OY && mc.Y < OY + Map.H)
            {
                bool right = Ui.RightClick;
                Ui.Click = Ui.RightClick = false;
                var cell = new Point(mc.X, mc.Y - OY);
                if (auto != Auto.None) StopAuto();
                else if (right || !(M.Seen[cell.X, cell.Y] || M.Vis[cell.X, cell.Y])) Ui.Push(new TextScreen("Look", new[] { TargetScreen.Describe(cell, true) }, 70));
                else ClickCell(cell);
            }
            if (auto != Auto.None && !Anim.Blocking && Ui.Top == this)
            {
                autoTimer -= dt;
                if (autoTimer <= 0)
                {
                    autoTimer = auto == Auto.Rest || auto == Auto.Search ? 0.004f : 0.035f;
                    int n = auto == Auto.Rest || auto == Auto.Search ? 6 : 1;
                    for (int i = 0; i < n && auto != Auto.None; i++) StepAuto();
                }
            }
        }

        void StopAuto() => auto = Auto.None;

        void BeginCommand()
        {
            Dm.Interrupted = false;
            Dm.LogMark = Dm.Log.Count;
        }

        void StepAuto()
        {
            var p = P;
            if (Dm.GameOver) { StopAuto(); return; }
            if (Dm.Interrupted && autoSteps > 0) { StopAuto(); return; }
            if (autoSteps == 0) autoMap = M;
            else if (autoMap != M) { StopAuto(); return; }
            int before = Dm.Log.Count;
            Dm.Interrupted = false;
            autoSteps++;
            switch (auto)
            {
                case Auto.Run:
                    {
                        var to = new Point(p.Pos.X + runDir.X, p.Pos.Y + runDir.Y);
                        if (!M.Walkable(to) || M.ActorAt(to) != null || (M.TrapAt(to)?.Found ?? false) || HostileInView()) { StopAuto(); return; }
                        var oldPos = p.Pos;
                        Actions.TryMove(runDir, out _, out _);
                        if (p.Pos == oldPos || M.ItemsAt(p.Pos).Any() || M.Def(p.Pos).Feature || M.IsDoor(p.Pos) || Junction()) StopAuto();
                        break;
                    }
                case Auto.Rest:
                    if (HostileInView()) { Dm.Msg("{warn}You cannot rest with enemies in view.{/}"); StopAuto(); return; }
                    if (!Actions.NeedsRest(p) || autoSteps > 3000) { Dm.Msg("{dim}You finish resting.{/}"); StopAuto(); return; }
                    if (p.Nutrition < 150) { Dm.Msg("{warn}You are too hungry to rest.{/}"); StopAuto(); return; }
                    Actions.Rest();
                    break;
                case Auto.Search:
                    if (autoSteps > 10 || HostileInView()) { StopAuto(); return; }
                    Actions.Search();
                    break;
                case Auto.Explore:
                    {
                        if (HostileInView()) { if (autoSteps == 1) Dm.Msg("{warn}Not with enemies in view!{/}"); StopAuto(); return; }
                        Actions.Visited.Add((M.Key, p.Pos));
                        var dist = Actions.ExploreMap();
                        var next = Actions.NextStep(dist);
                        if (next == null)
                        {
                            bool lockedKnown = false;
                            for (int lx = 0; lx < Map.W && !lockedKnown; lx++) for (int ly = 0; ly < Map.H; ly++) if (M.Seen[lx, ly] && M.T[lx, ly] == Tile.DoorLocked) { lockedKnown = true; break; }
                            if (!M.IsBottom && M.Seen[M.Down.X, M.Down.Y]) Dm.Msg(M.Wild ? "{dim}Explored. Press > to travel to the Tomb.{/}" : M.Town ? "{dim}Explored. Press > to walk to the south gate.{/}" : "{dim}Explored. Press > to travel to the stairs down.{/}");
                            else if (lockedKnown) Dm.Msg("{warn}Everything you can reach is explored. Locked doors remain — kick them (D) or pick them.{/}");
                            else Dm.Msg("{warn}Everything you can reach is explored. Search (s) dead ends and odd walls for secret doors.{/}");
                            StopAuto();
                            return;
                        }
                        var d = new Point(next.Value.X - p.Pos.X, next.Value.Y - p.Pos.Y);
                        var old = p.Pos;
                        if (!Actions.TryMove(d, out _, out _) || p.Pos == old) { if (M.T[next.Value.X, next.Value.Y] != Tile.DoorOpen) StopAuto(); }
                        if (M.ItemsAt(p.Pos).Any(i => !i.IsGold)) { Actions.Visited.Add((M.Key, p.Pos)); StopAuto(); }
                        break;
                    }
                case Auto.Travel:
                    {
                        if (p.Pos == travelGoal) { StopAuto(); return; }
                        if (HostileInView() && autoSteps > 1) { StopAuto(); return; }
                        var dist = Path.DistMap(M, new[] { travelGoal }, (x, y) => M.Seen[x, y] && (M.Walkable(x, y) || M.T[x, y] == Tile.DoorClosed) && !(M.TrapAt(new Point(x, y))?.Found ?? false) && M.T[x, y] != Tile.Chasm);
                        var next = Actions.NextStep(dist);
                        if (next == null) { Dm.Msg("You can't find a way there."); StopAuto(); return; }
                        var old = p.Pos;
                        Actions.TryMove(new Point(next.Value.X - p.Pos.X, next.Value.Y - p.Pos.Y), out _, out _);
                        if (p.Pos == old && M.T[next.Value.X, next.Value.Y] != Tile.DoorOpen) StopAuto();
                        break;
                    }
            }
            if (Dm.GameOver || Dm.Interrupted) StopAuto();
        }

        bool HostileInView() => M.Monsters.Any(m => !m.Dead && m.Hostile && !m.Disguised && Dm.CanSeeMonster(m) && !(m.Def.Has(MF.Immobile) && Path.Cheb(m.Pos, P.Pos) > 2));

        bool Junction()
        {
            if (M.T[P.Pos.X, P.Pos.Y] != Tile.Corridor) return false;
            int open = M.Neighbors(P.Pos).Count(q => M.Walkable(q) && (q.X == P.Pos.X || q.Y == P.Pos.Y));
            return open > 2;
        }

        public override void Key(KeyIn k)
        {
            if (Dm.GameOver) return;
            if (auto != Auto.None) { StopAuto(); return; }
            BeginCommand();
            var p = P;
            if (RogueGame.Wizard && k.Ctrl)
            {
                switch (k.Ch)
                {
                    case 'd': if (!M.IsBottom) { Dm.EnterLevel(M.Depth + 1, Dm.Arrival.Down); Dm.UpdateVision(); } return;
                    case 'm': M.RevealMap(true); Dm.UpdateVision(); return;
                    case 'e': if (M.Wild) Wild.Begin(M.T[p.Pos.X, p.Pos.Y]); return;
                    case 'b': { var bd = WorldDb.Get(M.Branch % 3 + 1); Dm.EnterLevel(bd.Top, Dm.Arrival.Down, bd.Branch); Dm.UpdateVision(); return; }
                    case 'u': if (M.Outdoors) return; Dm.EnterLevel(M.Depth == WorldDb.Get(M.Branch).Top ? 0 : M.Depth - 1, Dm.Arrival.Up, M.Depth == WorldDb.Get(M.Branch).Top ? 0 : M.Branch); Dm.UpdateVision(); return;
                    case 'h': p.Hp = p.MaxHp; p.Nutrition = 1500; return;
                    case 'l': p.Xp = Rules.XpFor(p.Class, p.Level + 1); Dm.GainXp(1); return;
                    case 'i': Actions.GiveOrDrop(Spawn.RandomItem(M.Depth + 3)); return;
                }
            }
            if (k.Is(Keys.F2) || k.Is('M')) { Synth.Muted = !Synth.Muted; Synth.Ambience(M.Outdoors ? 0 : M.Depth); Dm.Msg("{dim}Sound " + (Synth.Muted ? "off" : "on") + ".{/}"); return; }
            if (!p.Has(Status.Hidden) || true) { }
            if (k.Dir(out var d, out bool run))
            {
                if (!k.IsWait) p.RestTurns = 0;
                if (run) { runDir = d; auto = Auto.Run; autoSteps = 0; autoTimer = 0; return; }
                if (!Actions.TryMove(d, out var prompt, out var confirmed) && prompt != null) Prompts.YesNo(prompt, () => confirmed());
                return;
            }
            if (k.IsWait) { Actions.Rest(); return; }
            if (k.Ch != 'R' && k.Ch != '.' && k.Ch != 's') p.RestTurns = 0;
            switch (k.Ch)
            {
                case 's': auto = Auto.Search; autoSteps = 0; return;
                case 'R': auto = Auto.Rest; autoSteps = 0; return;
                case 'X': auto = Auto.Explore; autoSteps = 0; return;
                case '>':
                    if (M.T[p.Pos.X, p.Pos.Y] == Tile.StairsDown || M.PlaceAt(p.Pos) != null) Actions.Stairs(true);
                    else if (!M.IsBottom && M.Seen[M.Down.X, M.Down.Y]) { travelGoal = M.Down; auto = Auto.Travel; autoSteps = 0; }
                    else Dm.Msg("You don't know where the stairs down are.");
                    return;
                case '<':
                    if (M.T[p.Pos.X, p.Pos.Y] == Tile.StairsUp) Actions.Stairs(false);
                    else if (M.Town && WorldDb.TownOf(M.Branch).Start && p.Pos == M.Up) Prompts.YesNo("Hang up your pack and retire to a quiet life in Lantern Hollow? (This ends your adventure.)", () => Dm.Retire());
                    else if (M.Seen[M.Up.X, M.Up.Y]) { travelGoal = M.Up; auto = Auto.Travel; autoSteps = 0; }
                    else Dm.Msg("You don't know where the stairs up are.");
                    return;
                case 'g':
                case ',': PickUp(); return;
                case 'i': Inventory(); return;
                case 'd': DoDrop(); return;
                case 'w': DoWield(); return;
                case 'W': DoWear(); return;
                case 'P': Prompts.YesNo("Are you sure you want to pray to " + p.Deity + "?", () => Actions.Pray()); return;
                case 'T': DoTakeOff(); return;
                case 'q': DoQuaff(); return;
                case 'r': DoRead(); return;
                case 'z': DoZap(); return;
                case 'e': DoEat(); return;
                case 't': DoThrow(); return;
                case 'f': DoFire(); return;
                case 'a': DoApply(); return;
                case 'c':
                case 'Z': DoCast(); return;
                case 'p': Ui.Push(new SpellPrepScreen()); return;
                case '#': Prompts.YesNo("Are you sure you want to pray to " + p.Deity + "?", () => Actions.Pray()); return;
                case 'A': DoAbility(); return;
                case 'o': DoOpen(); return;
                case 'C': Prompts.Direction("Close in which direction?", dd => Actions.Close(dd)); return;
                case 'D': Prompts.Direction("Kick in which direction?", dd => Actions.Kick(dd)); return;
                case 'x':
                case ';': Ui.Push(new TargetScreen("Look:", 99, _ => { }, true)); return;
                case ':': LookHere(); return;
                case '@': Ui.Push(new CharacterScreen()); return;
                case 'm': ShowLog(); return;
                case '?': Ui.Push(HelpScreen.Make()); return;
                case 'I': ShowDiscoveries(); return;
                case 'E': Use.Sit(); return;
                case 'S':
                    Prompts.YesNo("Save the game and quit to the title screen?", () =>
                    {
                        if (SaveGame.Save()) { Ui.Replace(new TitleScreen()); }
                        else Dm.Msg("{bad}The game could not be saved!{/}");
                    });
                    return;
                case 'Q': Prompts.YesNo("Really quit? Your character will be lost forever.", () => { Dm.Die("quit"); }); return;
            }
            if (k.Is(Keys.Tab)) { FightNearest(); return; }
        }

        void ClickCell(Point cell)
        {
            var p = P;
            BeginCommand();
            if (cell == p.Pos)
            {
                if (M.ItemsAt(p.Pos).Any()) PickUp();
                else if (M.T[p.Pos.X, p.Pos.Y] == Tile.StairsDown) Actions.Stairs(true);
                else if (M.T[p.Pos.X, p.Pos.Y] == Tile.StairsUp) Key(new KeyIn { Ch = '<' });
                else Actions.Rest();
                return;
            }
            if (Path.Cheb(cell, p.Pos) == 1)
            {
                p.RestTurns = 0;
                if (!Actions.TryMove(new Point(cell.X - p.Pos.X, cell.Y - p.Pos.Y), out var prompt, out var confirmed) && prompt != null) Prompts.YesNo(prompt, () => confirmed());
                return;
            }
            var mon = M.MonsterAt(cell);
            if (mon != null && mon.Hostile && Dm.CanSeeMonster(mon) && p.Weapon != null && p.Weapon.Def.Has(WF.Launcher))
            {
                TargetScreen.LastTarget = mon;
                DoFire();
                return;
            }
            travelGoal = cell;
            auto = Auto.Travel;
            autoSteps = 0;
            autoTimer = 0;
        }

        void FightNearest()
        {
            var p = P;
            var adj = M.Monsters.Where(m => !m.Dead && m.Hostile && Dm.CanSeeMonster(m) && Path.Cheb(m.Pos, p.Pos) == 1).OrderBy(m => m.Hp).FirstOrDefault();
            if (adj != null) { Actions.TryMove(new Point(adj.Pos.X - p.Pos.X, adj.Pos.Y - p.Pos.Y), out _, out _); return; }
            var near = M.Monsters.Where(m => !m.Dead && m.Hostile && Dm.CanSeeMonster(m)).OrderBy(m => Path.Cheb(m.Pos, p.Pos)).FirstOrDefault();
            if (near == null) { Dm.Msg("There is nothing to fight."); return; }
            var dist = Path.DistMap(M, new[] { near.Pos }, (x, y) => M.Walkable(x, y) || (x == near.Pos.X && y == near.Pos.Y) || M.T[x, y] == Tile.DoorClosed, 30);
            var next = Actions.NextStep(dist);
            if (next != null) Actions.TryMove(new Point(next.Value.X - p.Pos.X, next.Value.Y - p.Pos.Y), out _, out _);
        }

        void PickUp()
        {
            var p = P;
            var items = M.ItemsAt(p.Pos).ToList();
            if (items.Count == 0) { Dm.Msg("There is nothing here to pick up."); return; }
            var shop = M.ShopAt(p.Pos);
            bool shopOpen = shop != null && shop.KeeperMon != null && !shop.KeeperMon.Dead && shop.KeeperMon.Peaceful;
            void Take(List<Item> list)
            {
                var queue = new Queue<Item>(list);
                bool any = false;
                void Next()
                {
                    while (queue.Count > 0)
                    {
                        var it = queue.Dequeue();
                        if (it.Price > 0 && shopOpen)
                        {
                            int price = it.Price * it.Count * Rules.ChaPrice(p.Stat(Stat.Cha)) / 100;
                            Prompts.YesNo(shop.Keeper + ": \"" + it.Name(true) + "? For you, " + price + " gold.\" Buy it?", () => { if (Actions.Buy(it)) { any = true; } Next(); }, () => Next());
                            return;
                        }
                        it.Price = 0;
                        Actions.PickUpItem(it);
                        any = true;
                    }
                    if (any) Dm.PlayerActed();
                }
                Next();
            }
            if (items.Count == 1) { Take(items); return; }
            var menu = items.Select((it, i) => new MenuItem { Key = (char)('a' + i % 26), Text = it.Name(), Tag = it }).ToList();
            Ui.Push(new MenuScreen("Pick up what?", menu, sel => Take(sel.Select(m => (Item)m.Tag).ToList()), mi => ((Item)mi.Tag).Describe()));
        }

        void Inventory()
        {
            var p = P;
            if (p.Pack.Count == 0) { Dm.Msg("You are not carrying anything."); return; }
            var items = Prompts.InventoryItems(p.Pack);
            var title = "Inventory  {dim}(weight " + p.Load + "/" + p.Capacity +(p.Burden > 0 ? ", " + (p.Burden == 1 ? "burdened" : p.Burden == 2 ? "stressed" : "overloaded") : "") + "){/}";
            Ui.Push(new MenuScreen(title, items, mi => ItemActions((Item)mi.Tag), mi => ((Item)mi.Tag).Describe()) { Width = 64 });
        }

        void ItemActions(Item it)
        {
            var acts = new List<MenuItem>();
            var k = it.Def.Kind;
            bool eq = Actions.IsEquipped(it);
            void Add(char key, string text, Action a) => acts.Add(new MenuItem { Key = key, Text = text, Tag = a });
            if (k == IK.Weapon) Add('w', eq ? "Unwield" : "Wield", () => { if (eq) Actions.Wield(null); else Actions.Wield(it); });
            if (k is IK.Armor or IK.Shield or IK.Helm or IK.Cloak or IK.Boots or IK.Gloves or IK.Bracers or IK.Girdle or IK.Ring or IK.Amulet or IK.Light or IK.Ammo)
                Add(eq ? 'T' : 'W', eq ? (k == IK.Light ? "Extinguish" : "Take off") : (k == IK.Light ? "Light it" : k == IK.Ammo ? "Ready in quiver" : "Wear / put on"), () => { if (eq) Actions.TakeOff(it); else Actions.Wear(it); });
            if (k == IK.Ioun) Add('a', "Release it to orbit", () => Use.ReleaseIoun(it));
            if (k == IK.Potion) Add('q', "Quaff", () => Use.Quaff(it));
            if (k == IK.Scroll || k == IK.Book) Add('r', "Read", () => ReadItem(it));
            if (k == IK.Wand) Add('z', "Zap", () => ZapItem(it));
            if (k is IK.Food or IK.Corpse) Add('e', "Eat", () => Use.Eat(it));
            if (k is IK.Tool or IK.Light || (k == IK.Ring && it.Def.Effect == "r_wishes")) Add('a', "Apply / use", () => ApplyItem(it));
            if (it.Def.Has(WF.Thrown) || k is IK.Weapon or IK.Potion or IK.Gem or IK.Ammo || it.Def.Effect is "oil" or "holy_water") Add('t', "Throw", () => ThrowItem(it));
            if (k == IK.Ammo) Add('f', "Fire", () => FireWith(it));
            Add('d', "Drop", () => DropItem(it));
            Add('x', "Examine", () => Ui.Push(new TextScreen(it.Name(), new[] { it.Describe() }, 70)));
            Ui.Push(new MenuScreen(it.Letter + " - " + it.Name(), acts, mi => { BeginCommand(); ((Action)mi.Tag)(); }) { Width = 50 });
        }

        void DoDrop()
        {
            Prompts.ChooseItem("Drop what?", _ => true, DropItem);
        }

        void DropItem(Item it)
        {
            var p = P;
            var shop = M.ShopAt(p.Pos);
            if (shop != null && shop.KeeperMon != null && !shop.KeeperMon.Dead && shop.KeeperMon.Peaceful && it.Def.Kind != IK.Heart && Actions.CanRemove(it))
            {
                int price = Actions.SellPrice(it);
                if (price > 0)
                {
                    Prompts.YesNo(shop.Keeper + " offers " + price + " gold for " + it.Name() + ". Sell it?", () => { Actions.Sell(it, price); Dm.PlayerActed(); }, () => Actions.Drop(it, it.Count));
                    return;
                }
            }
            if (it.Def.Kind == IK.Heart)
            {
                Prompts.YesNo("Really put down the Icosahedral Heart?", () => Actions.Drop(it, it.Count));
                return;
            }
            Actions.Drop(it, it.Count);
        }

        void DoWield()
        {
            var p = P;
            var list = p.Pack.Where(i => i.Def.Kind == IK.Weapon || i.Def.Effect == "pole").ToList();
            var items = Prompts.InventoryItems(list);
            items.Insert(0, new MenuItem { Key = '-', Text = "your bare hands", Tag = null });
            Ui.Push(new MenuScreen("Wield what?", items, mi => Actions.Wield(mi.Tag as Item), mi => (mi.Tag as Item)?.Describe()));
        }

        void DoWear() => Prompts.ChooseItem("Wear or put on what?", i => (i.Def.Equippable && i.Def.Kind != IK.Weapon && !Actions.IsEquipped(i)) || i.Def.Kind is IK.Ioun or IK.Ammo, Actions.Wear);
        void DoTakeOff() => Prompts.ChooseItem("Take off what?", i => Actions.IsEquipped(i) && Actions.SlotOf(i) != Slot.Weapon, Actions.TakeOff);

        void DoQuaff()
        {
            var p = P;
            var t = M.T[p.Pos.X, p.Pos.Y];
            if (t == Tile.Fountain)
            {
                Prompts.YesNo("Drink from the fountain?", () => Use.Fountain(), () => Prompts.ChooseItem("Quaff what?", i => i.Def.Kind == IK.Potion, Use.Quaff));
                return;
            }
            Prompts.ChooseItem("Quaff what?", i => i.Def.Kind == IK.Potion, Use.Quaff);
        }

        void DoRead() => Prompts.ChooseItem("Read what?", i => i.Def.Kind is IK.Scroll or IK.Book, ReadItem);

        void ReadItem(Item it)
        {
            var p = P;
            if (it.Def.Kind == IK.Book) { Use.ReadBook(it); return; }
            if (it.Def.Effect == "spell")
            {
                if (!Use.CanUseSpellScroll(it, out var why)) { Dm.Msg(why); return; }
                var sd = SpellDb.Get(it.Def.Spell);
                if (p.Class == Cls.MagicUser && !sd.Cleric && !p.Known.Contains(sd.Id))
                {
                    var opts = new List<MenuItem>
                    {
                        new MenuItem { Key = 'c', Text = "Cast " + sd.Name + " now", Tag = 0 },
                        new MenuItem { Key = 'l', Text = "Copy it into your spellbook {dim}(" + Rules.IntLearn(p.Stat(Stat.Int)) + "% chance){/}", Tag = 1 },
                    };
                    Prompts.Menu(it.Name(), opts, mi =>
                    {
                        if ((int)mi.Tag == 1) Use.CopyScroll(it);
                        else WithSpellTarget(sd.Id, (tg, ch) => Use.CastFromScroll(it, tg, ch));
                    });
                    return;
                }
                WithSpellTarget(sd.Id, (tg, ch) => Use.CastFromScroll(it, tg, ch));
                return;
            }
            if (Use.ScrollNeedsItem(it))
            {
                Func<Item, bool> filter = it.Def.Effect == "identify" ? (i => i != it && (!i.Known || !i.TypeKnown)) : (i => i.Def.Kind == IK.Wand || !i.Def.Charges.IsZero);
                var cands = p.Pack.Where(filter).ToList();
                if (cands.Count == 0) { Use.Read(it, null); return; }
                if (!it.TypeKnown) Dm.Msg("{magic}As you read the scroll, it asks you to choose an item.{/}");
                var menu = new MenuScreen(it.Def.Effect == "identify" ? "Identify what?" : "Recharge what?", Prompts.InventoryItems(cands), mi => Use.Read(it, (Item)mi.Tag), mi => ((Item)mi.Tag).Describe());
                Ui.Push(menu);
                return;
            }
            Use.Read(it);
        }

        void WithSpellTarget(SpellId id, Action<Point, Item> go)
        {
            var p = P;
            var sd = SpellDb.Get(id);
            switch (sd.Tgt)
            {
                case Target.Self: go(p.Pos, null); break;
                case Target.Item:
                    Prompts.ChooseItem("Identify which item?", i => !i.Known || !i.TypeKnown, it => go(p.Pos, it), true);
                    break;
                case Target.Cone:
                case Target.Dir:
                case Target.Touch:
                    Prompts.Direction(sd.Name + ": which direction?", d => go(new Point(p.Pos.X + d.X * Math.Max(1, sd.Range), p.Pos.Y + d.Y * Math.Max(1, sd.Range)), null), sd.Tgt == Target.Dir);
                    break;
                default:
                    int range = id == SpellId.Teleport ? 99 : sd.Range;
                    Prompts.Target(sd.Name + ":", range, tg => go(tg, null));
                    break;
            }
        }

        void DoZap() => Prompts.ChooseItem("Zap which wand?", i => i.Def.Kind == IK.Wand, ZapItem);

        void ZapItem(Item it)
        {
            if (!Use.WandNeedsTarget(it) && it.TypeKnown) { Use.Zap(it, P.Pos); return; }
            if (it.Def.Effect == "w_digging" && it.TypeKnown)
            {
                Prompts.Direction("Dig in which direction?", d => Use.Zap(it, new Point(P.Pos.X + d.X * 5, P.Pos.Y + d.Y * 5)), true);
                return;
            }
            Prompts.Target("Zap " + it.Name(false) + " at:", 12, tg => Use.Zap(it, tg));
        }

        void DoEat()
        {
            var p = P;
            var corpse = M.ItemsAt(p.Pos).FirstOrDefault(i => i.Def.Kind is IK.Corpse or IK.Food);
            if (corpse != null)
            {
                Prompts.YesNo("There is " + corpse.Name() + " here; eat it?", () => Use.Eat(corpse), () => Prompts.ChooseItem("Eat what?", i => i.Def.Kind is IK.Food or IK.Corpse, Use.Eat));
                return;
            }
            Prompts.ChooseItem("Eat what?", i => i.Def.Kind is IK.Food or IK.Corpse, Use.Eat);
        }

        void DoThrow() => Prompts.ChooseItem("Throw what?", i => i.Def.Has(WF.Thrown) || i.Def.Kind is IK.Potion or IK.Weapon or IK.Gem or IK.Ammo || i.Def.Effect is "oil" or "holy_water", ThrowItem);

        void ThrowItem(Item it)
        {
            if (Actions.IsEquipped(it) && it.Cursed) { it.Known = true; Dm.Msg("{bad}It is welded to your hand!{/}"); return; }
            int range = it.Def.Range > 1 ? it.Def.Range : Math.Max(2, P.Stat(Stat.Str) / 3);
            Prompts.Target("Throw " + it.Name(false) + ":", range, tg =>
            {
                var one = Actions.TakeOne(it);
                int shots = it.Def.Id == "dart" ? 3 : it.Def.Id is "dagger" or "silver_dagger" ? 2 : 1;
                Combat.Missile(P, tg, one, null, range);
                for (int s = 1; s < shots; s++)
                {
                    var tgtMon = M.MonsterAt(tg);
                    if (tgtMon == null || tgtMon.Dead || !P.Pack.Contains(it) || it.Count < 1) break;
                    if (Actions.IsEquipped(it) && it.Count <= 1) break;
                    Combat.Missile(P, tg, Actions.TakeOne(it), null, range);
                }
                Dm.PlayerActed();
            });
        }

        void DoFire()
        {
            var p = P;
            var launcher = p.Weapon != null && p.Weapon.Def.Has(WF.Launcher) ? p.Weapon : null;
            var q = p.Worn(Slot.Quiver);
            if (launcher != null)
            {
                if (q == null || q.Def.Launcher != launcher.Def.Ammo) q = p.Pack.FirstOrDefault(i => i.Def.Kind == IK.Ammo && i.Def.Launcher == launcher.Def.Ammo);
                if (q == null) { Dm.Msg("You have no " + launcher.Def.Ammo + "s for your " + launcher.Def.Name + "."); return; }
                p.Eq[Slot.Quiver] = q;
                FireWith(q);
                return;
            }
            var spareLauncher = p.Pack.FirstOrDefault(i => i.Def.Has(WF.Launcher) && p.Pack.Any(a => a.Def.Kind == IK.Ammo && a.Def.Launcher == i.Def.Ammo));
            if (spareLauncher != null && (q == null || q.Def.Kind == IK.Ammo))
            {
                Dm.Msg("You ready your " + spareLauncher.Def.Name + ".");
                Actions.Wield(spareLauncher);
                return;
            }
            if (q == null || !q.Def.Has(WF.Thrown)) q = p.Pack.FirstOrDefault(i => i.Def.Has(WF.Thrown) && !Actions.IsEquipped(i) && i.Def.Kind == IK.Weapon);
            if (q == null) { Dm.Msg("You have nothing ready to fire. {dim}(Wield a bow or sling, or quiver some darts){/}"); return; }
            ThrowItem(q);
        }

        void FireWith(Item ammo)
        {
            var p = P;
            var launcher = p.Weapon != null && p.Weapon.Def.Has(WF.Launcher) && p.Weapon.Def.Ammo == ammo.Def.Launcher ? p.Weapon : null;
            if (launcher == null) { Dm.Msg("You need to wield the right launcher to fire that."); return; }
            int shots = launcher.Def.Id.Contains("bow") && p.IsWarrior ? 2 : 1;
            Prompts.Target("Fire " + ammo.Name(false) + ":", launcher.Def.Range, tg =>
            {
                for (int s = 0; s < shots; s++)
                {
                    if (!p.Pack.Contains(ammo) || ammo.Count < 1) break;
                    if (s > 0) { var m = M.MonsterAt(tg); if (m == null || m.Dead) break; }
                    var one = Actions.TakeOne(ammo);
                    Combat.Missile(p, tg, one, launcher, launcher.Def.Range);
                }
                if (!p.Pack.Contains(ammo)) { p.Eq.Remove(Slot.Quiver); Dm.Msg("{warn}You are out of " + ammo.Def.Name + "s.{/}"); }
                Dm.PlayerActed();
            });
        }

        void DoApply() => Prompts.ChooseItem("Apply what?", i => i.Def.Kind is IK.Tool or IK.Light or IK.Ioun || (i.Def.Kind == IK.Ring && i.Def.Effect == "r_wishes"), ApplyItem);

        void ApplyItem(Item it)
        {
            if (it.Def.Kind == IK.Ioun) { Use.ReleaseIoun(it); return; }
            if (it.Def.Kind == IK.Light) { if (Actions.IsEquipped(it)) Actions.TakeOff(it); else Actions.Wear(it); return; }
            if (it.Def.Effect is "mirror" or "horn" or "necklace")
            {
                Prompts.Target("Use " + it.Def.Name + " toward:", 10, tg => Use.Apply(it, tg));
                return;
            }
            if (Use.ToolNeedsDir(it))
            {
                Prompts.Direction("Use " + it.Def.Name + " in which direction?", d => Use.Apply(it, new Point(P.Pos.X + d.X, P.Pos.Y + d.Y)));
                return;
            }
            Use.Apply(it, P.Pos);
        }

        void DoCast()
        {
            var p = P;
            if (!p.Caster) { Dm.Msg("You don't know any spells."); return; }
            if (p.Class == Cls.MagicUser)
            {
                var body = p.Worn(Slot.Body);
                if (body != null && body.Def.Id is not ("robe" or "robe_archmagi" or "elven_chain")) { Dm.Msg("{warn}You cannot cast spells while wearing armor.{/}"); return; }
                if (p.Worn(Slot.Shield) != null) { Dm.Msg("{warn}You cannot cast spells while carrying a shield.{/}"); return; }
            }
            if (p.Has(Status.Silenced)) { Dm.Msg("You cannot speak the words!"); return; }
            if (p.Memorized.Count == 0) { Dm.Msg("You have no spells memorized. {dim}(Prepare with p, then rest with R){/}"); return; }
            var groups = p.Memorized.GroupBy(s => s).OrderBy(g => SpellDb.Get(g.Key).Level).ThenBy(g => SpellDb.Get(g.Key).Name).ToList();
            var items = new List<MenuItem>();
            char key = 'a';
            int lastLvl = 0;
            foreach (var g in groups)
            {
                var sd = SpellDb.Get(g.Key);
                if (sd.Level != lastLvl) { items.Add(new MenuItem { Header = true, Text = "Level " + sd.Level }); lastLvl = sd.Level; }
                items.Add(new MenuItem { Key = key++, Text = Markup.Hex(sd.C) + sd.Name + "{/}" + (g.Count() > 1 ? " ×" + g.Count() : ""), Tag = g.Key });
            }
            Ui.Push(new MenuScreen("Cast which spell?", items, mi =>
            {
                var id = (SpellId)mi.Tag;
                WithSpellTarget(id, (tg, ch) =>
                {
                    if (!p.Memorized.Remove(id)) return;
                    int lvl = p.Class == Cls.Paladin ? Math.Max(1, p.Level - 8) : p.Level;
                    Magic.Cast(p, id, tg, lvl, ch);
                    if (p.Has(Status.Invisible) && SpellDb.Get(id).Offensive && !p.Wearing("r_invis")) { p.Remove(Status.Invisible); Dm.Msg("{dim}You become visible.{/}"); }
                    if (p.Has(Status.Sanctuary) && SpellDb.Get(id).Offensive) p.Remove(Status.Sanctuary);
                    Dm.PlayerActed();
                });
            }, mi => mi.Tag is SpellId s ? SpellDb.Get(s).Desc : null));
        }

        void DoAbility()
        {
            var p = P;
            var items = new List<MenuItem>();
            if (p.Class is Cls.Cleric or Cls.Paladin) items.Add(new MenuItem { Key = 't', Text = "Turn Undead" + (Dm.Turn < p.TurnUndeadReady ? " {dim}(recovering){/}" : ""), Tag = "turn" });
            if (p.Class == Cls.Paladin)
            {
                items.Add(new MenuItem { Key = 'l', Text = "Lay on Hands (heal " + 2 * p.Level + ")" + (Dm.Turn < p.LayOnHandsReady ? " {dim}(used today){/}" : ""), Tag = "lay" });
                items.Add(new MenuItem { Key = 'e', Text = "Detect Evil", Tag = "evil" });
            }
            if (p.Class == Cls.Thief) items.Add(new MenuItem { Key = 'h', Text = "Hide in Shadows (" + p.ThiefSkills[3] + "%)", Tag = "hide" });
            items.Add(new MenuItem { Key = 'd', Text = "Disarm a trap" + (p.Class == Cls.Thief ? " (" + p.ThiefSkills[1] + "%)" : ""), Tag = "disarm" });
            items.Add(new MenuItem { Key = 'p', Text = "Pray to " + p.Deity, Tag = "pray" });
            Prompts.Menu("Abilities", items, mi =>
            {
                switch ((string)mi.Tag)
                {
                    case "turn": Actions.TurnUndead(); break;
                    case "lay": Actions.LayOnHands(); break;
                    case "evil": Magic.Cast(p, SpellId.DetectEvil, p.Pos, p.Level); Dm.PlayerActed(); break;
                    case "hide": Actions.Hide(); break;
                    case "pray": Prompts.YesNo("Are you sure you want to pray?", () => Actions.Pray()); break;
                    case "disarm":
                        {
                            var chest = M.ItemsAt(p.Pos).FirstOrDefault(i => i.Def.Kind == IK.Chest && i.Trapped && i.TrapKnown);
                            if (chest != null) { Actions.DisarmChest(chest); break; }
                            Prompts.Direction("Disarm in which direction?", d =>
                            {
                                var q = new Point(p.Pos.X + d.X, p.Pos.Y + d.Y);
                                var tr = M.TrapAt(q);
                                if (tr == null || !tr.Found) { Dm.Msg("You know of no trap there."); return; }
                                Traps.Disarm(tr);
                                Dm.PlayerActed();
                            });
                            break;
                        }
                }
            });
        }

        void DoOpen()
        {
            var p = P;
            var chest = M.ItemsAt(p.Pos).FirstOrDefault(i => i.Def.Kind == IK.Chest);
            if (chest != null) { Actions.OpenChest(chest); return; }
            Prompts.Direction("Open in which direction?", d =>
            {
                var q = new Point(p.Pos.X + d.X, p.Pos.Y + d.Y);
                var mim = M.MonsterAt(q);
                if (mim != null && mim.Disguised) { Actions.TryMove(d, out _, out _); return; }
                var c = M.ItemsAt(q).FirstOrDefault(i => i.Def.Kind == IK.Chest);
                if (c != null) { Actions.OpenChest(c); return; }
                if (M.T[q.X, q.Y] is Tile.DoorClosed or Tile.DoorLocked) { Actions.TryMove(d, out _, out _); return; }
                Dm.Msg("There is nothing there to open.");
            });
        }

        void LookHere()
        {
            var p = P;
            var lines = new List<string>();
            var def = M.Def(p.Pos);
            lines.Add("You are standing on " + Item.Article(def.Name) + ".");
            if (def.Desc != null) lines.Add("{dim}" + def.Desc + "{/}");
            if (M.Engravings.TryGetValue(p.Pos, out var e)) lines.Add("{ink}Something is written here: \"" + e + "\"{/}");
            var items = M.ItemsAt(p.Pos).ToList();
            if (items.Count > 0) { lines.Add(""); lines.Add("Things that are here:"); foreach (var it in items) lines.Add("  " + it.Name()); }
            Ui.Push(new TextScreen("Here", lines, 70));
        }

        void ShowLog()
        {
            var lines = Dm.Log.Skip(Math.Max(0, Dm.Log.Count - 300)).Select(l => l.Text + (l.Count > 1 ? " {dim}(x" + l.Count + "){/}" : "")).ToList();
            var ts = new TextScreen("Message History", lines, 110);
            Ui.Push(ts);
            for (int i = 0; i < 400; i++) ts.Key(new KeyIn { Key = Keys.PageDown });
        }

        void ShowDiscoveries()
        {
            var lines = new List<string>();
            foreach (var kind in new[] { IK.Potion, IK.Scroll, IK.Wand, IK.Ring, IK.Amulet })
            {
                var known = ItemDb.All.Values.Where(d => d.Kind == kind && d.Unknown && Dm.Ident.IsKnown(d.Id)).ToList();
                if (known.Count == 0) continue;
                lines.Add("{gold}" + kind + "s{/}");
                foreach (var d in known) lines.Add("  " + Dm.Ident.Appearance(d.Id) + " → " + d.Name);
            }
            if (lines.Count == 0) lines.Add("You haven't discovered anything yet.");
            Ui.Push(new TextScreen("Discoveries", lines, 70));
        }

        public override void Draw(Terminal t)
        {
            DrawHeader(t);
            DrawMap(t);
            Anim.Draw(t, 0, OY);
            DrawMessages(t);
            DrawSidebar(t);
            if (Ui.Top == this && Ui.MouseCell is Point mc && mc.X < Map.W && mc.Y >= OY && mc.Y < OY + Map.H)
            {
                var cell = new Point(mc.X, mc.Y - OY);
                t.SetBg(mc.X, mc.Y, Pal.Lerp(t.Bg[mc.X, mc.Y], Pal.C(200, 170, 90), 0.35f));
                if (M.Seen[cell.X, cell.Y] || M.Vis[cell.X, cell.Y])
                {
                    string desc = TargetScreen.Describe(cell, false).Split('\n')[0];
                    int len = Math.Min(94, Markup.Len(desc) + 2);
                    t.Fill(2, 37, len, 1, Pal.C(40, 30, 15));
                    t.PrintM(3, 37, desc, Pal.Hi, 92);
                }
            }
        }

        void DrawHeader(Terminal t)
        {
            t.Fill(0, 0, 100, 1, Pal.C(22, 17, 12));
            var name = WorldDb.LevelName(M);
            var here = M.PlaceAt(P.Pos);
            if (M.Wild && here == null) name += " {dim}·{/} " + M.Def(P.Pos).Name;
            if (here != null) name += " {dim}·{/} " + WorldDb.Cap(here.Name);
            string right = "{dim}Turn{/} " + Dm.Turn + "   {dim}? help{/}";
            t.PrintM(1, 0, "{gold}" + WorldDb.LevelTag(M) + "{/} {dim}·{/} " + name + (Dm.HeartTaken ? "  {pink}♥ The Heart beats. Climb!{/}" : ""), Pal.Text, 96 - Markup.Len(right));
            t.PrintM(99 - Markup.Len(right), 0, right, Pal.Text);
        }

        static Color Shimmer(Color c, int x, int y, float speed, float amt)
        {
            float s = (float)Math.Sin(Ui.Time * speed + x * 0.7f + y * 1.3f) * 0.5f + 0.5f;
            return Pal.Mul(c, 1f - amt + s * amt * 2);
        }

        void DrawMap(Terminal t)
        {
            var p = P;
            float time = Ui.Time;
            for (int x = 0; x < Map.W; x++)
                for (int y = 0; y < Map.H; y++)
                {
                    int sx = x, sy = y + OY;
                    var tile = M.T[x, y];
                    var def = Map.Defs[tile];
                    if (M.Vis[x, y])
                    {
                        Color fg = def.Fg, bg = def.Bg;
                        char g = def.G;
                        if (tile is Tile.Wall or Tile.DoorSecret or Tile.PermWall) { fg = M.WallFg; bg = M.WallBg; }
                        else if (tile is Tile.Floor or Tile.Corridor) { fg = M.FloorFg; }
                        if (tile is Tile.Shallow or Tile.DeepWater) { fg = Shimmer(fg, x, y, 2.2f, 0.18f); bg = Shimmer(bg, x + 3, y, 1.6f, 0.15f); if (tile == Tile.DeepWater && (int)(time * 2 + x * 3 + y * 7) % 9 == 0) g = '~'; }
                        if (tile == Tile.Lava) { fg = Shimmer(fg, x, y, 3f, 0.25f); bg = Shimmer(bg, x, y + 2, 2.3f, 0.3f); }
                        if (tile == Tile.Moss || tile == Tile.Mushroom || tile == Tile.Crystal) fg = Shimmer(fg, x, y, 1.2f, 0.12f);
                        if (tile == Tile.Brazier) fg = Shimmer(fg, x, y, 9f, 0.2f);
                        if (tile == Tile.OracleDie) fg = Pal.Lerp(Pal.White, Pal.C(170, 170, 255), (float)Math.Sin(time * 2) * 0.5f + 0.5f);
                        if (tile == Tile.Chasm) { fg = Pal.C(20, 20, 30); bg = Pal.C(2, 2, 6); }
                        if (tile == Tile.StairsDown && M.Wild && M.DungeonAt(new Point(x, y)) is Place hole) fg = Pal.Lerp(WorldDb.Get(hole.Branch).C, Pal.White, (float)Math.Sin(time * 2.5f) * 0.25f + 0.25f);
                        if (tile == Tile.Bell) fg = Shimmer(fg, x, y, 1.5f, 0.15f);
                        float lr = M.LR[x, y], lg = M.LG[x, y], lb = M.LB[x, y];
                        float lum = Math.Max(lr, Math.Max(lg, lb));
                        float flick = 1f + 0.04f * (float)Math.Sin(time * 11 + x * 1.7f + y * 2.3f) * Math.Min(1, lum);
                        float norm = lum > 1.2f ? 1.2f / lum : 1f;
                        float kr = Math.Clamp(0.12f + lr * norm * 0.95f, 0.12f, 1.25f) * flick;
                        float kg = Math.Clamp(0.12f + lg * norm * 0.95f, 0.12f, 1.25f) * flick;
                        float kb = Math.Clamp(0.14f + lb * norm * 0.95f, 0.14f, 1.25f) * flick;
                        if (def.GlowR > 0 && tile != Tile.Chasm) { kr = Math.Max(kr, 0.9f); kg = Math.Max(kg, 0.9f); kb = Math.Max(kb, 0.9f); }
                        fg = Pal.Mul(fg, kr, kg, kb);
                        bg = Pal.Mul(bg, kr, kg, kb);
                        if (M.Stain[x, y] > 0.02f && Map.Defs[tile].Walk)
                        {
                            var sc = Pal.Mul(M.StainC[x, y], Math.Min(1, kr + 0.2f));
                            bg = Pal.Lerp(bg, sc, M.Stain[x, y] * 0.7f);
                            fg = Pal.Lerp(fg, sc, M.Stain[x, y] * 0.4f);
                        }
                        if (M.Cloud[x, y] > 0)
                        {
                            var cc = Magic.ElemColor(M.CloudE[x, y] == Elem.Magic ? Elem.Poison : M.CloudE[x, y] == Elem.Cold ? Elem.Force : M.CloudE[x, y]);
                            bg = Pal.Lerp(bg, Pal.Mul(cc, 0.6f), 0.45f + 0.1f * (float)Math.Sin(time * 3 + x + y));
                            g = '░';
                            fg = Pal.Mul(cc, 0.9f);
                        }
                        if (M.Fire[x, y] > 0)
                        {
                            bg = Pal.Lerp(Pal.C(150, 40, 0), Pal.C(220, 110, 20), (float)Math.Sin(time * 14 + x * 3 + y) * 0.5f + 0.5f);
                            g = (int)(time * 10 + x + y) % 2 == 0 ? '^' : '*';
                            fg = Pal.C(255, 230, 120);
                        }
                        var tr = M.TrapAt(new Point(x, y));
                        if (tr != null && tr.Found) { g = '^'; fg = tr.Color; }
                        var it = M.TopItem(new Point(x, y));
                        if (it != null && M.Fire[x, y] <= 0)
                        {
                            g = it.Glyph;
                            fg = Pal.Mul(it.Color, Math.Max(0.55f, Math.Min(1.1f, (kr + kg + kb) / 2.2f)));
                            if (it.Def.Kind == IK.Heart) fg = Pal.Lerp(Pal.C(255, 60, 110), Pal.White, (float)Math.Sin(time * 4) * 0.3f + 0.3f);
                            if (it.Price > 0) bg = Pal.Lerp(bg, Pal.C(60, 50, 10), 0.4f);
                        }
                        t.Put(sx, sy, g, fg, bg);
                    }
                    else if (M.Seen[x, y])
                    {
                        char g = M.MemCh[x, y];
                        Color ink = Pal.Lerp(Pal.Ink, M.MemFg[x, y], 0.18f);
                        Color bg = Pal.Paper;
                        if (tile is Tile.Wall or Tile.PermWall or Tile.DoorSecret)
                        {
                            bg = Pal.C(30, 40, 72);
                            ink = Pal.C(70, 90, 140);
                        }
                        else if (g == '·') ink = Pal.C(55, 75, 125);
                        if (tile is Tile.Road or Tile.Bridge) ink = Pal.C(150, 110, 70);
                        if (tile == Tile.TownSite) ink = Pal.C(230, 190, 110);
                        if (g == '>' || g == '<') ink = Pal.C(200, 220, 255);
                        t.Put(sx, sy, g, ink, bg);
                        t.Paper[sx, sy] = true;
                    }
                    else t.Put(sx, sy, ' ', Pal.Black, Pal.Black);
                }

            foreach (var m in M.Monsters)
            {
                if (m.Dead) continue;
                var s = Dm.Seen(m);
                if (s == SeenKind.No) continue;
                int sx = m.Pos.X, sy = m.Pos.Y + OY;
                switch (s)
                {
                    case SeenKind.Visible:
                        {
                            var c = m.Color;
                            if (m.Def.Has(MF.Shadowy)) c = Pal.Lerp(c, Pal.C(20, 20, 30), 0.3f);
                            if (m.Has(Status.Invisible) || m.Def.Has(MF.Invis)) c = Pal.Lerp(c, Pal.C(60, 60, 90), 0.5f);
                            if (m.Ally) t.SetBg(sx, sy, Pal.Lerp(t.Bg[sx, sy], Pal.C(20, 70, 30), 0.6f));
                            if (m.Def.Has(MF.Boss)) c = Pal.Lerp(c, Pal.White, (float)Math.Sin(time * 3) * 0.3f + 0.3f);
                            if (m.Disguised && Dm.Map.TopItem(m.Pos) == null) c = Pal.Mul(c, Math.Clamp(M.Lum(m.Pos.X, m.Pos.Y) + 0.3f, 0.4f, 1.1f));
                            t.Put(sx, sy, m.Glyph, c);
                            if (m.Def.Has(MF.Transparent)) t.SetBg(sx, sy, Pal.Lerp(t.Bg[sx, sy], m.Def.C, 0.35f));
                            break;
                        }
                    case SeenKind.Faint:
                        t.SetBg(sx, sy, Pal.Lerp(t.Bg[sx, sy], m.Def.C, 0.18f));
                        break;
                    case SeenKind.Infra:
                        t.Put(sx, sy, m.Def.G, Pal.C(200, 40, 30));
                        break;
                    case SeenKind.Esp:
                        t.Put(sx, sy, m.Def.G, Pal.C(200, 90, 220));
                        break;
                }
            }

            if (!p.Dead)
            {
                var pc = p.Color;
                if (Dm.HeartTaken && p.Carrying("heart")) pc = Pal.Lerp(pc, Pal.C(255, 80, 130), (float)Math.Sin(time * 4.5f) * 0.35f + 0.35f);
                if (p.Has(Status.Hidden)) pc = Pal.C(100, 100, 120);
                t.Put(p.Pos.X, p.Pos.Y + OY, '@', pc);
                if (p.Has(Status.MirrorImage))
                {
                    int n = p.Pow(Status.MirrorImage);
                    for (int i = 0; i < n; i++)
                    {
                        double a = time * 1.3 + i * Math.PI * 2 / n;
                        int ix = p.Pos.X + (int)Math.Round(Math.Cos(a)), iy = p.Pos.Y + (int)Math.Round(Math.Sin(a));
                        if (Map.InB(ix, iy) && M.ActorAt(new Point(ix, iy)) == null && M.Walkable(ix, iy)) t.Put(ix, iy + OY, '@', Pal.C(140, 140, 200));
                    }
                }
                for (int i = 0; i < p.Iouns.Count; i++)
                {
                    double a = time * 1.8 + i * Math.PI * 2 / Math.Max(1, p.Iouns.Count);
                    int ix = p.Pos.X + (int)Math.Round(Math.Cos(a) * 1.4), iy = p.Pos.Y + (int)Math.Round(Math.Sin(a) * 0.9);
                    if (Map.InB(ix, iy) && M.ActorAt(new Point(ix, iy)) == null && !M.BlocksSight(ix, iy)) t.Put(ix, iy + OY, '•', p.Iouns[i].Def.C);
                }
            }
        }

        void DrawMessages(Terminal t)
        {
            t.Fill(0, 37, 100, 8, Pal.C(8, 7, 6));
            for (int x = 0; x < 100; x++) t.Put(x, 37, '─', Pal.C(60, 48, 30));
            var lines = new List<(string text, bool fresh)>();
            for (int i = Dm.Log.Count - 1; i >= 0 && lines.Count < 7; i--)
            {
                var l = Dm.Log[i];
                string txt = l.Text + (l.Count > 1 ? " {dim}(x" + l.Count + "){/}" : "");
                var wrapped = Markup.Wrap(txt, 97);
                for (int j = wrapped.Count - 1; j >= 0 && lines.Count < 7; j--) lines.Insert(0, (wrapped[j], i >= Dm.LogMark));
            }
            for (int i = 0; i < lines.Count; i++)
            {
                var (text, fresh) = lines[i];
                t.PrintM(1, 38 + i, text, fresh ? Pal.Text : Pal.Dim, 98);
                if (!fresh) for (int x = 1; x < 99; x++) t.Fg[x, 38 + i] = Pal.Lerp(t.Fg[x, 38 + i], Pal.C(30, 28, 25), 0.45f);
            }
        }

        void DrawSidebar(Terminal t)
        {
            var p = P;
            int x = SX, w = Terminal.W - SX;
            t.Fill(SX - 1, 0, w + 1, Terminal.H, Pal.Panel);
            for (int y = 0; y < Terminal.H; y++) t.Put(SX - 1, y, '│', Pal.C(70, 55, 35));
            int row = 0;
            t.PrintM(x + 1, row++, "{gold}" + p.PName + "{/}", Pal.Gold, w - 2);
            t.PrintM(x + 1, row++, p.RaceInfo.Name + " " + p.ClassInfo.Name + " {dim}(" + Rules.AlignShort[(int)p.Align] + "){/}", Pal.Text, w - 2);
            t.PrintM(x + 1, row++, "{dim}Lvl{/} " + p.Level + " {dim}" + p.Title + "{/}", Pal.Text, w - 2);
            row++;
            float hf = p.HpFrac;
            var hcol = hf > 0.6f ? Pal.C(40, 120, 40) : hf > 0.3f ? Pal.C(150, 120, 20) : Pal.C(150, 30, 25);
            t.Print(x + 1, row, "HP", Pal.Text);
            t.Bar(x + 4, row, w - 6, hf, hcol, Pal.C(35, 20, 18), p.Hp + " / " + p.MaxHp);
            row++;
            int need = Rules.XpFor(p.Class, p.Level + 1), have = Rules.XpFor(p.Class, p.Level);
            float xf = need == int.MaxValue ? 1 : (p.Xp - have) / (float)Math.Max(1, need - have);
            t.Print(x + 1, row, "XP", Pal.Text);
            t.Bar(x + 4, row, w - 6, xf, Pal.C(60, 60, 130), Pal.C(20, 20, 35), p.Xp + (need == int.MaxValue ? "" : " / " + need));
            row++;
            var light = p.Worn(Slot.Light);
            if (light != null && light.Def.Fuel < 99999)
            {
                float lf = light.Fuel / (float)Math.Max(1, light.Def.Id == "lantern" ? 3000 : light.Def.Fuel);
                t.Print(x + 1, row, "Lt", Pal.Text);
                t.Bar(x + 4, row, w - 6, lf, light.Lit ? Pal.C(150, 100, 30) : Pal.C(60, 60, 60), Pal.C(30, 22, 12), light.Def.Name + " " + light.Fuel);
            }
            else t.PrintM(x + 1, row, "Lt {dim}" + (p.LightRadius > 0 ? "radius " + p.LightRadius : "{red}none — darkness{/}") + "{/}", Pal.Text, w - 2);
            row += 2;
            t.PrintM(x + 1, row++, "{dim}AC{/} " + p.AC.ToString().PadRight(4) + "{dim}THAC0{/} " + p.Thac0.ToString().PadRight(4) + "{dim}Spd{/} " + p.Speed, Pal.Hi);
            for (int i = 0; i < 6; i += 2)
            {
                string a = StatCell(p, (Stat)i), b = StatCell(p, (Stat)(i + 1));
                t.PrintM(x + 1, row++, a + "  " + b, Pal.Text);
            }
            t.PrintM(x + 1, row++, "{dim}Gold{/} {gold}" + p.Gold + "{/}   " + (M.Wild ? "{dim}Land{/} " + Wild.WildWord(Wild.Wildness(M, p.Pos)) : "{dim}Depth{/} " + (M.Outdoors ? "surface" : M.Depth + "/" + p.DeepestDepth)), Pal.Text);
            var wpn = p.Weapon;
            t.PrintM(x + 1, row++, "{dim}Wield{/} " + (wpn == null ? "bare hands" : wpn.Name(false, false)), Pal.Text, w - 2);
            var st = StatusList(p);
            var stLines = Markup.Wrap(st, w - 2);
            for (int i = 0; i < Math.Min(3, stLines.Count); i++) t.PrintM(x + 1, row++, stLines[i], Pal.Text, w - 2);
            if (p.Caster)
            {
                var slots = p.Slots;
                int total = slots.Sum();
                string sp = "{dim}Spells{/} " + p.Memorized.Count + "/" + total + " ";
                var names = p.Memorized.GroupBy(s => s).Select(g => Abbrev(SpellDb.Get(g.Key).Name) + (g.Count() > 1 ? "×" + g.Count() : ""));
                var spl = Markup.Wrap(sp + "{magic}" + string.Join(" ", names) + "{/}", w - 2);
                for (int i = 0; i < Math.Min(2, spl.Count); i++) t.PrintM(x + 1, row++, spl[i], Pal.Text, w - 2);
            }
            row = Math.Max(row, 19);
            Section(t, row++, "In View");
            var vis = M.Monsters.Where(m => !m.Dead && Dm.Seen(m) != SeenKind.No && !m.Disguised && Dm.Seen(m) != SeenKind.Faint).OrderBy(m => Path.Cheb(m.Pos, p.Pos)).Take(9).ToList();
            if (vis.Count == 0) t.PrintM(x + 1, row++, "{faint}nothing{/}", Pal.Faint);
            foreach (var m in vis)
            {
                var s = Dm.Seen(m);
                var gc = s == SeenKind.Infra ? Pal.C(200, 40, 30) : s == SeenKind.Esp ? Pal.C(200, 90, 220) : m.Def.C;
                t.Put(x + 1, row, m.Def.G, gc);
                string name = s == SeenKind.Infra ? "warm shape" : m.Proper ?? m.Def.Name;
                if (name.Length > 15) name = name.Substring(0, 15);
                t.Print(x + 3, row, name, m.Ally ? Pal.Green : m.Peaceful ? Pal.Silver : Pal.Text);
                if (s == SeenKind.Visible)
                {
                    float f = m.HpFrac;
                    var bc = m.Ally ? Pal.C(40, 110, 40) : f > 0.6f ? Pal.C(120, 40, 30) : f > 0.3f ? Pal.C(150, 100, 20) : Pal.C(200, 40, 30);
                    t.Bar(x + 19, row, 8, f, bc, Pal.C(30, 20, 18));
                    string tag = m.Sleeping || m.Has(Status.Asleep) ? "z" : m.Fleeing || m.Has(Status.Turned) ? "!" : m.Helpless ? "-" : "";
                    if (tag.Length > 0) t.Print(x + 28, row, tag, Pal.Yellow);
                }
                row++;
            }
            row = Math.Max(row, 31);
            Section(t, row++, "The Dice");
            for (int i = Dm.Dice.Count - 1; i >= 0 && row < Terminal.H - 1; i--)
            {
                var d = Dm.Dice[i];
                DrawDie(t, x + 1, row++, d, w - 2, i == Dm.Dice.Count - 1);
            }
            t.PrintM(x + 1, Terminal.H - 1, "{faint}i inv · c cast · ? help{/}", Pal.Faint);
        }

        static string Abbrev(string name)
        {
            var parts = name.Replace("'", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return name.Length > 7 ? name.Substring(0, 6) + "." : name;
            return string.Concat(parts.Select(s => char.IsDigit(s[0]) ? s : s.Length <= 2 || s == "from" ? s.Substring(0, 1).ToLower() : s.Substring(0, 1).ToUpper()));
        }

        static string StatCell(Player p, Stat s)
        {
            int raw = p.RawStat(s), eff = p.Stat(s);
            string col = eff > raw ? "{good}" : eff < raw ? "{bad}" : "{white}";
            if (p.Has(Status.Flux)) col = "{red}";
            return "{dim}" + Rules.StatNames[(int)s] + "{/} " + col + p.StatText(s).PadRight(6) + "{/}";
        }

        void Section(Terminal t, int row, string title)
        {
            int x = SX;
            int w = Terminal.W - SX;
            for (int i = 0; i < w; i++) t.Put(x + i, row, '─', Pal.C(80, 62, 38));
            t.Print(x + (w - title.Length - 2) / 2, row, " " + title + " ", Pal.FrameHi);
        }

        static void DrawDie(Terminal t, int x, int y, DieRoll d, int w, bool newest)
        {
            float age = Ui.Time - d.Born;
            bool rolling = age < 0.45f && age >= 0;
            int shown = rolling ? Rng.D(d.Sides) : d.Result;
            Color numCol = d.Crit ? Pal.Gold : d.Fumble ? Pal.C(255, 60, 60) : d.Success == true ? Pal.C(140, 230, 140) : d.Success == false ? Pal.C(230, 120, 100) : Pal.White;
            if (rolling) numCol = Pal.White;
            var dieBg = d.Crit && !rolling ? Pal.C(90, 70, 10) : d.Fumble && !rolling ? Pal.C(90, 15, 15) : Pal.C(36, 32, 40);
            string ds = "d" + d.Sides;
            t.Print(x, y, ds.PadLeft(4), Pal.Dim);
            string num = shown.ToString().PadLeft(3);
            t.Print(x + 4, y, " " + num + " ", numCol, dieBg);
            string rest = "";
            if (!rolling)
            {
                if (d.Mod != 0) rest += (d.Mod > 0 ? "+" : "") + d.Mod + " ";
                if (d.Target > 0 && d.Sides == 20) rest += "≥" + d.Target + " ";
                if (d.Target > 0 && d.Sides == 100) rest += "≤" + d.Target + " ";
                if (d.Alt > 0) rest += "(" + d.Alt + ") ";
            }
            rest += d.Label;
            if (rest.Length > w - 10) rest = rest.Substring(0, w - 10);
            t.Print(x + 10, y, rest, newest ? Pal.Text : Pal.Dim);
        }

        static string StatusList(Player p)
        {
            var s = new List<string>();
            string h = p.HungerText;
            if (h.Length > 0) s.Add((p.Nutrition <= 100 ? "{bad}" : p.Nutrition <= 300 ? "{warn}" : "{good}") + h + "{/}");
            if (p.Burden > 0) s.Add("{warn}" + (p.Burden == 1 ? "Burdened" : p.Burden == 2 ? "Stressed" : "Overloaded") + "{/}");
            void A(Status st, string name, string col) { if (p.Has(st)) s.Add("{" + col + "}" + name + "{/}"); }
            A(Status.Poisoned, "Poisoned", "bad"); A(Status.Diseased, "Diseased", "bad"); A(Status.Confused, "Confused", "bad"); A(Status.Blind, "Blind", "bad");
            A(Status.Paralyzed, "Paralyzed", "bad"); A(Status.Asleep, "Asleep", "bad"); A(Status.Stunned, "Stunned", "bad"); A(Status.Feared, "Afraid", "bad");
            A(Status.Stoning, "STONING", "red"); A(Status.Sliming, "SLIMING", "red"); A(Status.Held, "Held", "warn"); A(Status.Webbed, "Webbed", "warn");
            A(Status.Bleeding, "Bleeding", "bad"); A(Status.Slowed, "Slow", "bad"); A(Status.Weakened, "Weak", "bad"); A(Status.Flux, "Flux", "red");
            A(Status.Hasted, "Fast", "good"); A(Status.Invisible, "Invisible", "magic"); A(Status.Blessed, "Blessed", "holy"); A(Status.Shielded, "Shield", "magic");
            A(Status.MirrorImage, "Images", "magic"); A(Status.GiantStr, "Giant Str", "orange"); A(Status.Heroism, "Heroic", "gold"); A(Status.ResFire, "Res Fire", "orange");
            A(Status.ESP, "ESP", "magic"); A(Status.ProtEvil, "Prot Evil", "holy"); A(Status.Sanctuary, "Sanctuary", "holy"); A(Status.Prayer, "Prayer", "holy");
            A(Status.Levitate, "Levitating", "magic"); A(Status.FireShield, "Fire Shield", "orange"); A(Status.Globe, "Globe", "magic"); A(Status.Hidden, "Hidden", "dim");
            A(Status.ProtMissiles, "Prot Missiles", "magic"); A(Status.Regenerating, "Regen", "good"); A(Status.BladeBarrier, "Blades", "white"); A(Status.ProtUndead, "Prot Undead", "holy");
            if (p.Has(Status.Fortune)) s.Add(p.Pow(Status.Fortune) > 0 ? "{gold}Fortune{/}" : "{bad}Jinxed{/}");
            if (Dm.Engulfer != null) s.Add("{red}" + (p.Has(Status.Swallowed) ? "Swallowed" : "Engulfed") + "{/}");
            return string.Join(" ", s);
        }
    }
}
