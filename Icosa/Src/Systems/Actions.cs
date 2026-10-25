using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class Actions
    {
        static Player P => Dm.Player;
        static Map M => Dm.Map;

        public static char FreeLetter()
        {
            var used = P.Pack.Select(i => i.Letter).ToHashSet();
            foreach (char c in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ")
                if (!used.Contains(c)) return c;
            return '?';
        }

        public static bool AddToPack(Item it, bool msg = true)
        {
            var p = P;
            if (it.IsGold)
            {
                p.Gold += it.Count;
                Dm.GainXp(Math.Max(1, it.Count / 4), true);
                if (msg) Dm.Msg("{gold}" + it.Count + " gold piece" + (it.Count > 1 ? "s" : "") + ".{/}");
                Synth.Play(Sfx.Gold, 0.6f);
                return true;
            }
            foreach (var o in p.Pack)
                if (o.CanStack(it))
                {
                    o.Count += it.Count;
                    if (msg) Dm.Msg(o.Letter + " - " + o.Name() + ".");
                    return true;
                }
            if (p.Pack.Count >= 52) { if (msg) Dm.Msg("Your pack is full."); return false; }
            it.Letter = FreeLetter();
            p.Pack.Add(it);
            if (msg) Dm.Msg(it.Letter + " - " + it.Name() + ".");
            return true;
        }

        public static void GiveOrDrop(Item it)
        {
            if (!AddToPack(it)) M.Drop(it, P.Pos);
        }

        public static Item TakeOne(Item it)
        {
            if (it.Count <= 1)
            {
                Unequip(it);
                P.Pack.Remove(it);
                return it;
            }
            return it.Split(1);
        }

        static void Unequip(Item it)
        {
            foreach (var kv in P.Eq.Where(kv => kv.Value == it).ToList()) P.Eq.Remove(kv.Key);
        }

        public static bool IsEquipped(Item it) => P.Eq.ContainsValue(it);

        public static Slot? SlotOf(Item it) => P.Eq.Where(kv => kv.Value == it).Select(kv => (Slot?)kv.Key).FirstOrDefault();

        public static void IdentifyItem(Item it)
        {
            it.Known = true;
            it.ChargesKnown = true;
            if (it.Def.Unknown) Dm.Ident.Learn(it.Def.Id);
            Dm.Msg("{magic}" + it.Letter + " - " + it.Name() + ".{/}");
        }

        public static void ReturnToHand(Item it)
        {
            if (P.Weapon == null)
            {
                AddToPack(it, false);
                var inPack = P.Pack.FirstOrDefault(i => i.Def == it.Def) ?? it;
                P.Eq[Slot.Weapon] = inPack;
            }
            else AddToPack(it, false);
        }

        public static void LandOn(Point p)
        {
            var t = M.T[p.X, p.Y];
            if (t == Tile.Chasm) FallIntoChasm();
            else if (t == Tile.Lava && !P.Resists(Elem.Fire)) Dm.Die("falling into lava");
        }

        static void FallIntoChasm()
        {
            if (M.IsBottom || M.Outdoors) return;
            Dm.Msg("{bad}You plunge into the chasm!{/}", true);
            if (P.Carrying("rope")) Dm.Msg("You manage to slow your fall with your rope.");
            else Combat.Damage(P, Rng.Roll(1, 6), Elem.Phys, null, "a long fall");
            if (!Dm.GameOver) Dm.EnterLevel(M.Depth + 1, Dm.Arrival.Fall);
        }

        public static bool TryMove(Point d, out string prompt, out Action confirmed)
        {
            prompt = null;
            confirmed = null;
            var p = P;
            if (p.Has(Status.Confused) && Rng.Chance(50))
            {
                d = Rng.Pick(Path.Dirs);
                Dm.Msg("{dim}You stumble in confusion.{/}");
            }
            if (p.Has(Status.Feared))
            {
                var tgt0 = new Point(p.Pos.X + d.X, p.Pos.Y + d.Y);
                var mm = M.MonsterAt(tgt0);
                if (mm != null && mm.Hostile && Dm.CanSeeMonster(mm)) { Dm.Msg("{bad}You are too frightened to attack!{/}"); return false; }
            }
            if (Dm.Engulfer != null)
            {
                Dm.Msg("You attack the inside of " + Dm.Engulfer.The + "!");
                Combat.PlayerMelee(Dm.Engulfer);
                Dm.PlayerActed();
                return true;
            }
            if (p.Holder != null && !p.Holder.Dead && Path.Cheb(p.Holder.Pos, p.Pos) <= 1)
            {
                var tgt = new Point(p.Pos.X + d.X, p.Pos.Y + d.Y);
                if (tgt != p.Holder.Pos)
                {
                    if (Rng.Chance(15 + Math.Max(0, p.Stat(Stat.Str) - 10) * 5)) { Dm.Msg("You pull free!"); p.Holder = null; p.Remove(Status.Held); }
                    else { Dm.Msg("{bad}You are held fast by " + p.Holder.The + "!{/}"); Dm.PlayerActed(); return true; }
                }
            }
            else if (p.Holder != null) { p.Holder = null; p.Remove(Status.Held); }
            if (p.Has(Status.Held) && p.Holder == null)
            {
                if (Rng.Chance(25 + Math.Max(0, p.Stat(Stat.Str) - 10) * 4)) { p.Remove(Status.Held); Dm.Msg("You free yourself."); }
                else { Dm.Msg("{warn}You struggle to free yourself.{/}"); Dm.PlayerActed(); return true; }
            }
            if (p.Has(Status.Webbed))
            {
                if (Rng.Chance(20 + Math.Max(0, p.Stat(Stat.Str) - 10) * 5) || p.Wearing("r_free")) { p.Remove(Status.Webbed); Dm.Msg("You tear free of the web."); M.T[p.Pos.X, p.Pos.Y] = M.T[p.Pos.X, p.Pos.Y] == Tile.Web ? Tile.Floor : M.T[p.Pos.X, p.Pos.Y]; }
                else { Dm.Msg("{warn}You are stuck in the web.{/}"); Dm.PlayerActed(); return true; }
            }
            var to = new Point(p.Pos.X + d.X, p.Pos.Y + d.Y);
            if (!Map.InB(to)) return false;
            var mon = M.MonsterAt(to);
            if (mon != null)
            {
                var seen = Dm.Seen(mon);
                if (mon.Ally && !mon.Disguised)
                {
                    mon.Pos = p.Pos;
                    p.Pos = to;
                    Dm.Msg("You swap places with " + mon.The + ".");
                    AfterMove();
                    Dm.PlayerActed();
                    return true;
                }
                if (mon.Peaceful && seen != SeenKind.No)
                {
                    if (mon.Def.Has(MF.Shopkeeper))
                    {
                        var shop = M.Shops.FirstOrDefault(s => s.KeeperMon == mon);
                        Dm.Msg("{white}" + mon.Name + " says: \"Welcome to my " + Lore.ShopTitle(shop?.Kind ?? "general") + "! Pick up anything you'd like to buy; drop what you'd like to sell.\"{/}");
                        mon.Pos = p.Pos;
                        p.Pos = to;
                        AfterMove();
                        Dm.PlayerActed();
                        return true;
                    }
                    var mref = mon;
                    if (mon.Def.Has(MF.Townsfolk))
                    {
                        if (mon.Def.Id == "innkeeper")
                        {
                            int bed = 5 + p.Level * 3;
                            if (p.Gold < bed) { Dm.Msg("{white}" + mon.Proper + " says: \"A bed and a bowl is " + bed + " gold, friend. The floor is free, but it's outside.\"{/}"); return true; }
                            prompt = "A hot meal and a bed for the night, for " + bed + " gold?";
                            confirmed = () =>
                            {
                                p.Gold -= bed;
                                p.Hp = p.MaxHp;
                                if (p.Nutrition < 1500) p.Nutrition = 1500;
                                p.Remove(Status.Weakened);
                                if (p.Caster) Magic.Memorize(p);
                                Synth.Play(Sfx.Heal, 0.6f);
                                Dm.Msg("{good}You eat a stew of something that was probably mutton and sleep like the dead. You wake rested" + (p.Caster ? ", your spells freshly memorized" : "") + ".{/}");
                                Dm.PlayerActed(Dm.ActCost * 60);
                            };
                            return false;
                        }
                        if (mon.Def.Id == "hedge_priest" && (p.Hp < p.MaxHp || p.Has(Status.Poisoned) || p.Has(Status.Diseased) || p.Has(Status.Weakened)))
                        {
                            int fee = 10 + p.Level * 15;
                            if (p.Gold < fee) { Dm.Msg("{white}The hedge-priest looks at your wounds, then at your purse. \"The gods ask " + fee + " gold. The gods are patient, but I am hungry.\"{/}"); return true; }
                            prompt = "The hedge-priest offers healing for a donation of " + fee + " gold. Accept?";
                            confirmed = () =>
                            {
                                p.Gold -= fee;
                                p.Hp = p.MaxHp;
                                foreach (var s in new[] { Status.Poisoned, Status.Diseased, Status.Weakened, Status.Blind, Status.Confused }) p.Remove(s);
                                Synth.Play(Sfx.Holy);
                                Dm.Msg("{holy}The hedge-priest lays a warm hand on your brow. Your wounds close. \"Go carefully. Or at least go slowly.\"{/}");
                                Dm.PlayerActed();
                            };
                            return false;
                        }
                        mon.Pos = p.Pos;
                        p.Pos = to;
                        string who = mon.Proper != null ? char.ToUpper(mon.Proper[0]) + mon.Proper.Substring(1) : mon.TheCap;
                        if (mon.Def.Id == "watchman" || Rng.Chance(50)) Dm.Msg("{white}" + who + " says: \"" + (mon.Def.Id == "hedge_priest" ? "You look whole. Come back when you aren't." : mon.Def.Id == "watchman" ? "Keep your lantern lit and your blade sharp. The dead don't knock." : WorldDb.Gossip()) + "\"{/}");
                        else Dm.Msg("{dim}You step around " + who + ".{/}");
                        AfterMove();
                        Dm.PlayerActed();
                        return true;
                    }
                    prompt = "Really attack " + mon.The + "?";
                    confirmed = () => { mref.Peaceful = false; Combat.PlayerMelee(mref); Dm.PlayerActed(); };
                    return false;
                }
                Combat.PlayerMelee(mon);
                Dm.PlayerActed();
                return true;
            }
            var t = M.T[to.X, to.Y];
            switch (t)
            {
                case Tile.DoorClosed:
                    M.T[to.X, to.Y] = Tile.DoorOpen;
                    Synth.Play(Sfx.Door, 0.5f);
                    Dm.Msg("{dim}You open the door.{/}");
                    Dm.PlayerActed();
                    return true;
                case Tile.DoorLocked:
                    if (p.Class == Cls.Thief || p.Carrying("thieves_tools"))
                    {
                        PickLock(to);
                        Dm.PlayerActed();
                        return true;
                    }
                    Dm.Msg("This door is locked. {dim}(Kick it with D, or use Knock.){/}");
                    return false;
                case Tile.Gate:
                    Dm.Msg("A portcullis bars the way. {dim}It has no lock; something somewhere must raise it. (Knock will also do.){/}");
                    return false;
                case Tile.DeepWater:
                    if (p.Has(Status.Levitate) || p.Wearing("levitate")) break;
                    Dm.Msg("The water is too deep to wade.");
                    return false;
                case Tile.Lava:
                    if (p.Has(Status.Levitate) || p.Wearing("levitate")) break;
                    prompt = "Really step into the lava?";
                    confirmed = () => { p.Pos = to; Dm.Die("walking into lava"); };
                    return false;
                case Tile.Chasm:
                    if (p.Has(Status.Levitate) || p.Wearing("levitate")) break;
                    prompt = "Jump into the chasm?";
                    confirmed = () => { p.Pos = to; FallIntoChasm(); };
                    return false;
            }
            if (!M.Walkable(to) && !(t is Tile.DeepWater or Tile.Lava or Tile.Chasm))
            {
                if (t == Tile.OracleDie || t == Tile.Statue || t == Tile.Sarcophagus) Dm.Msg("{dim}There is " + Item.Article(Map.Defs[t].Name) + " in the way.{/}");
                return false;
            }
            var trap = M.TrapAt(to);
            if (trap != null && trap.Found && !p.Has(Status.Levitate))
            {
                prompt = "Really step onto the " + trap.Name + "?";
                confirmed = () => { DoStep(to); };
                return false;
            }
            DoStep(to);
            return true;
        }

        static void DoStep(Point to)
        {
            var p = P;
            var from = p.Pos;
            Dm.Travelled = M.Wild;
            p.Pos = to;
            if (p.Has(Status.Hidden) && M.Lum(to.X, to.Y) > 0.5f && !Rng.Chance(p.ThiefSkills[3])) { p.Remove(Status.Hidden); Dm.Msg("{dim}You step out of the shadows.{/}"); }
            AfterMove();
            Dm.PlayerActed();
        }

        public static void AfterMove()
        {
            var p = P;
            var pos = p.Pos;
            var t = M.T[pos.X, pos.Y];
            if ((M.Town || M.Encounter) && (pos.X == 0 || pos.Y == 0 || pos.X == Map.W - 1 || pos.Y == Map.H - 1)) { Dm.LeaveLocal(); return; }
            var trap = M.TrapAt(pos);
            if (trap != null && !p.Has(Status.Levitate)) Traps.PlayerTrigger(trap);
            if (Dm.GameOver || M != Dm.Map || p.Pos != pos) return;
            if (t == Tile.Web && !p.Wearing("r_free") && Rng.Chance(40)) { p.Add(Status.Webbed, Rng.Range(2, 4)); Dm.Msg("{warn}You are caught in a web.{/}"); }
            Traps.Search(false);
            var gold = M.ItemsAt(pos).Where(i => i.IsGold && i.Price == 0).ToList();
            foreach (var g in gold) { M.Items.Remove(g); AddToPack(g); }
            var items = M.ItemsAt(pos).ToList();
            if (items.Count == 1) Dm.Msg("You see here " + items[0].Name() + ".");
            else if (items.Count > 1) Dm.Msg("There are " + (items.Count > 4 ? "many" : "several") + " objects here. {dim}(g to pick up, : to look){/}");
            if (M.Engravings.TryGetValue(pos, out var eng)) Dm.Msg("{ink}Something is written here: \"" + eng + "\"{/}");
            var def = M.Def(pos);
            var site = M.PlaceAt(pos);
            if (def.Feature && t != Tile.StairsDown && t != Tile.StairsUp && site == null) Dm.Msg("{dim}There is " + Item.Article(def.Name) + " here.{/}");
            if (site != null && site.Dungeon) Dm.Msg("{gold}The entrance to " + site.Name + ".{/} {dim}" + site.Blurb + " (>){/}");
            else if (site != null) Dm.Msg("{gold}" + WorldDb.Cap(site.Name) + ".{/} {dim}" + site.Blurb + " (> to enter){/}");
            else if (t == Tile.StairsDown) Dm.Msg("{dim}There is a staircase down here. (>){/}");
            if (t == Tile.StairsUp) Dm.Msg("{dim}There is a staircase up here. (<){/}");
            var shop = M.ShopAt(pos);
            var prevShop = M.Shops.FirstOrDefault(s => s.Door == pos);
            if (prevShop != null && prevShop.KeeperMon != null && !prevShop.KeeperMon.Dead && prevShop.KeeperMon.Peaceful)
                Dm.Msg("{white}A bell chimes. \"Welcome to " + prevShop.Keeper + "'s " + Lore.ShopTitle(prevShop.Kind) + "!\"{/}");
            if (M.T[pos.X, pos.Y] == Tile.Chasm && !p.Has(Status.Levitate)) FallIntoChasm();
            if (!p.Has(Status.Levitate) && p.Carrying("heart") == false)
            {
                var heart = M.ItemsAt(pos).FirstOrDefault(i => i.Def.Kind == IK.Heart);
                if (heart != null) Dm.Msg("{pink}The Icosahedral Heart lies here, beating slowly. (g to take it){/}", true);
            }
        }

        static void PickLock(Point door)
        {
            var p = P;
            int chance = p.Class == Cls.Thief ? p.ThiefSkills[0] : 10 + Math.Max(0, p.Stat(Stat.Dex) - 12) * 3;
            if (p.Carrying("thieves_tools")) chance += 15;
            int roll = Rng.Int(100) + 1;
            Dm.AddDie(100, roll, 0, chance, "pick lock", roll <= chance);
            if (roll <= chance)
            {
                M.T[door.X, door.Y] = Tile.DoorClosed;
                Dm.Msg("{good}You pick the lock. {dice}(d100=" + roll + " vs " + chance + "%){/}{/}");
            }
            else Dm.Msg("You fail to pick the lock. {dice}(d100=" + roll + " vs " + chance + "%){/}");
        }

        public static void PickUpItem(Item it)
        {
            var p = P;
            if (it.Def.Kind == IK.Chest) { Dm.Msg("The chest is far too heavy to carry. {dim}(o to open it){/}"); return; }
            if (it.Def.Kind == IK.Corpse && it.Weight > p.Capacity / 2) { Dm.Msg("The " + it.Name(false) + " is too heavy to lift."); return; }
            if (p.Load + it.Weight > p.Capacity * 2) { Dm.Msg("You can't carry that much weight."); return; }
            M.Items.Remove(it);
            it.Price = 0;
            if (!AddToPack(it)) { M.Drop(it, p.Pos); return; }
            Synth.Play(Sfx.Pickup, 0.5f);
            if (it.Def.Kind == IK.Heart) TookHeart();
            if (p.Burden > 0) Dm.Msg("{warn}You are " + (p.Burden == 1 ? "burdened" : p.Burden == 2 ? "stressed" : "overloaded") + ".{/}");
        }

        static void TookHeart()
        {
            Dm.HeartTaken = true;
            Synth.Play(Sfx.Heart);
            Dm.Msg("{pink}You lift the Icosahedral Heart. It is warm, and it beats. Twenty faces turn to look at you.{/}", true);
            Dm.Msg("{pink}Somewhere far above, daylight waits. Climb.{/}");
            Dm.Achievements.Add("Took the Icosahedral Heart");
            var lich = M.Monsters.FirstOrDefault(m => m.Def.Id == "hollow_king");
            if (lich != null && !lich.Dead)
            {
                lich.Sleeping = false;
                lich.Aware = 99;
                Dm.Msg("{crit}The Hollow King's eyes flare. \"THAT,\" it says, \"is mine.\"{/}", true);
            }
        }

        public static bool Buy(Item it)
        {
            var p = P;
            int price = it.Price * it.Count * Rules.ChaPrice(p.Stat(Stat.Cha)) / 100;
            if (p.Gold < price) { Dm.Msg("{white}\"You cannot afford that. It's " + price + " gold.\"{/}"); return false; }
            p.Gold -= price;
            Dm.Msg("{gold}You pay " + price + " gold.{/}");
            PickUpItem(it);
            return true;
        }

        public static int SellPrice(Item it)
        {
            int v = it.Value * it.Count / 2;
            if (!it.TypeKnown) v /= 2;
            return Math.Max(0, v * 100 / Rules.ChaPrice(P.Stat(Stat.Cha)));
        }

        public static void Sell(Item it, int price)
        {
            RemoveFromPack(it);
            P.Gold += price;
            it.Price = Math.Max(1, it.Value);
            M.Drop(it, P.Pos);
            Synth.Play(Sfx.Gold);
            Dm.Msg("{gold}You sell " + it.Name() + " for " + price + " gold.{/}");
        }

        public static void RemoveFromPack(Item it)
        {
            Unequip(it);
            P.Pack.Remove(it);
        }

        public static bool CanRemove(Item it)
        {
            if (IsEquipped(it) && it.Cursed && it.Def.Kind != IK.Light)
            {
                it.Known = true;
                Dm.Msg("{bad}You can't. It is cursed!{/}");
                return false;
            }
            return true;
        }

        public static void Drop(Item it, int count)
        {
            if (!CanRemove(it)) return;
            Item d = count >= it.Count ? it : it.Split(count);
            if (d == it) RemoveFromPack(it);
            d.Lit = d.Def.Kind == IK.Light && d.Lit;
            if (M.T[P.Pos.X, P.Pos.Y] == Tile.Altar)
            {
                d.Known = true;
                Dm.Msg(d.Cursed ? "{bad}There is a black flash as " + d.Name() + " hits the altar.{/}" : "{dim}" + d.Name() + " lands on the altar. Nothing happens.{/}");
            }
            else Dm.Msg("You drop " + d.Name() + ".");
            M.Drop(d, P.Pos);
            Dm.PlayerActed();
        }

        public static void Wield(Item it)
        {
            var p = P;
            var cur = p.Weapon;
            if (cur == it) { Dm.Msg("You are already wielding that."); return; }
            if (cur != null && cur.Cursed) { cur.Known = true; Dm.Msg("{bad}Your " + cur.Def.Name + " is welded to your hand!{/}"); return; }
            if (it != null && it.Def.Has(WF.TwoHand) && p.Worn(Slot.Shield) != null) { Dm.Msg("You cannot wield a two-handed weapon while wearing a shield."); return; }
            if (it == null) { p.Eq.Remove(Slot.Weapon); Dm.Msg("You are empty handed."); Dm.PlayerActed(); return; }
            if (it.Count > 1 && it.Def.Kind != IK.Weapon) { }
            Unequip(it);
            p.Eq[Slot.Weapon] = it;
            Dm.Msg(it.Letter + " - " + it.Name() + " (weapon in hand).");
            if (it.Cursed) { it.Known = true; Dm.Msg("{bad}The " + it.Def.Name + " welds itself to your hand!{/}"); }
            if (!p.Proficient(it) && it.Def.Kind == IK.Weapon) Dm.Msg("{warn}You are not trained with this weapon (-4 to hit).{/}");
            if (p.Class == Cls.Cleric && it.Def.Kind == IK.Weapon && !it.Def.Has(WF.Blunt) && !it.Def.Has(WF.Launcher) && it.Def.Id != "sling") Dm.Msg("{warn}" + p.Deity + " frowns upon edged weapons.{/}");
            if (it.Def.Artifact && !it.Known) { it.Known = true; Dm.Msg("{gold}You feel the power of " + it.Def.Name + "!{/}"); }
            Dm.PlayerActed();
        }

        public static Slot? SlotFor(Item it)
        {
            switch (it.Def.Kind)
            {
                case IK.Armor: return Slot.Body;
                case IK.Shield: return Slot.Shield;
                case IK.Helm: return Slot.Helm;
                case IK.Cloak: return Slot.Cloak;
                case IK.Boots: return Slot.Boots;
                case IK.Gloves: return Slot.Gloves;
                case IK.Bracers: return Slot.Bracers;
                case IK.Girdle: return Slot.Girdle;
                case IK.Amulet: return Slot.Amulet;
                case IK.Light: return Slot.Light;
                case IK.Ring: return P.Worn(Slot.RingL) == null ? Slot.RingL : P.Worn(Slot.RingR) == null ? Slot.RingR : (Slot?)null;
                case IK.Ammo: return Slot.Quiver;
                default: return null;
            }
        }

        public static void Wear(Item it)
        {
            var p = P;
            if (it.Def.Kind == IK.Weapon) { Wield(it); return; }
            if (it.Def.Kind == IK.Ioun) { Use.ReleaseIoun(it); return; }
            var slot = SlotFor(it);
            if (slot == null) { Dm.Msg(it.Def.Kind == IK.Ring ? "You are already wearing two rings." : "You can't wear that."); return; }
            if (IsEquipped(it)) { Dm.Msg("You are already using that."); return; }
            var s = slot.Value;
            if (s == Slot.Body && p.Worn(Slot.Cloak) != null) { Dm.Msg("You must take off your cloak first."); return; }
            if (s == Slot.Shield && p.Weapon != null && p.Weapon.Def.Has(WF.TwoHand)) { Dm.Msg("You cannot use a shield while wielding a two-handed weapon."); return; }
            if (s == Slot.Light && it.Count > 1)
            {
                var one = it.Split(1);
                one.Letter = FreeLetter();
                p.Pack.Add(one);
                it = one;
            }
            var cur = p.Worn(s);
            if (cur != null)
            {
                if (cur.Cursed && s != Slot.Light) { cur.Known = true; Dm.Msg("{bad}You can't remove your " + cur.Def.Name + ". It is cursed!{/}"); return; }
                p.Eq.Remove(s);
                if (cur.Def.Kind == IK.Light) cur.Lit = false;
            }
            p.Eq[s] = it;
            int cost = Dm.ActCost;
            if (s == Slot.Body) cost = Dm.ActCost * 5;
            if (s == Slot.Light)
            {
                if (it.Fuel <= 0) { Dm.Msg("That " + it.Def.Name + " has no fuel."); p.Eq.Remove(s); return; }
                it.Lit = true;
                Dm.Msg("You light " + it.Name() + ".");
            }
            else if (s == Slot.Quiver) Dm.Msg("You ready " + it.Name() + ".");
            else Dm.Msg("You are now wearing " + it.Name() + ".");
            if (s == Slot.Body && p.Class == Cls.MagicUser && it.Def.Id is not ("robe" or "robe_archmagi" or "elven_chain")) Dm.Msg("{warn}You cannot cast spells while wearing armor!{/}");
            if (s == Slot.Body && p.Class == Cls.Thief && it.Def.Id is not ("leather" or "studded" or "elven_chain" or "robe")) Dm.Msg("{warn}Your thieving skills will suffer in this armor.{/}");
            OnEquipIdentify(it);
            if (it.Cursed) { it.Known = true; Dm.Msg("{bad}Oops; that felt deathly cold.{/}"); }
            Dm.PlayerActed(cost);
        }

        static void OnEquipIdentify(Item it)
        {
            var p = P;
            switch (it.Def.Effect)
            {
                case "r_invis": Dm.Ident.Learn(it.Def.Id); Dm.Msg("{magic}Suddenly you can't see yourself!{/}"); p.Add(Status.Invisible, 9999); break;
                case "r_see_invis": Dm.Ident.Learn(it.Def.Id); Dm.Msg("{magic}Your vision sharpens.{/}"); break;
                case "r_protection": Dm.Ident.Learn(it.Def.Id); it.Known = true; break;
                case "r_weakness": Dm.Ident.Learn(it.Def.Id); Dm.Msg("{bad}You feel weak.{/}"); break;
                case "a_strangle": Dm.Ident.Learn(it.Def.Id); Dm.Msg("{bad}It constricts your throat!{/}", true); break;
                case "a_esp": case "telepathy": Dm.Ident.Learn(it.Def.Id); Dm.Msg("{magic}You hear the whispering thoughts of nearby creatures.{/}"); break;
                case "ogre": Dm.Msg("{orange}Your hands grip with the strength of an ogre!{/}"); it.Known = true; break;
                case "str19": case "str22": Dm.Msg("{orange}Giant strength floods your body!{/}"); it.Known = true; break;
                case "speed": Dm.Msg("{yellow}You feel yourself speed up.{/}"); it.Known = true; break;
                case "opposite":
                    {
                        Dm.Msg("{bad}Your mind turns inside out! You feel a new god watching you.{/}", true);
                        p.Align = p.Align switch { Align.LG => Align.CE, Align.NG => Align.NE, Align.CG => Align.LE, Align.LN => Align.CN, Align.CN => Align.LN, Align.LE => Align.CG, Align.NE => Align.NG, Align.CE => Align.LG, _ => Align.N };
                        it.Known = true;
                        break;
                    }
                case "levitate": Dm.Msg("{magic}You float gently into the air.{/}"); it.Known = true; break;
            }
            if (it.Def.Kind is IK.Armor or IK.Shield or IK.Helm or IK.Cloak or IK.Boots or IK.Gloves && !it.Known && Rng.Chance(30))
            {
                it.Known = true;
                Dm.Msg("{dim}You get a feel for it: " + it.Name() + ".{/}");
            }
        }

        public static void TakeOff(Item it)
        {
            var p = P;
            if (!IsEquipped(it)) return;
            if (!CanRemove(it)) return;
            var s = SlotOf(it).Value;
            if (s == Slot.Body && p.Worn(Slot.Cloak) != null) { Dm.Msg("You must take off your cloak first."); return; }
            p.Eq.Remove(s);
            if (it.Def.Kind == IK.Light) { it.Lit = false; Dm.Msg("You extinguish " + it.Name() + "."); }
            else Dm.Msg("You take off " + it.Name() + ".");
            if (it.Def.Effect == "r_invis") p.Remove(Status.Invisible);
            Dm.PlayerActed(s == Slot.Body ? Dm.ActCost * 3 : Dm.ActCost);
        }

        public static void Kick(Point d)
        {
            var p = P;
            var to = new Point(p.Pos.X + d.X, p.Pos.Y + d.Y);
            var mon = M.MonsterAt(to);
            if (mon != null)
            {
                int need = p.Thac0 - mon.AC;
                int roll = Dm.PlayerD20(out _);
                if (roll + p.StrBonus.hit - 2 >= need || roll == 20)
                {
                    int dmg = Math.Max(1, Rng.D(4) + p.StrBonus.dmg);
                    Dm.Msg("You kick " + mon.The + " for " + dmg + ".");
                    Combat.WakeUp(mon);
                    Combat.Damage(mon, dmg, Elem.Phys, p, "a kick");
                }
                else { Dm.Msg("Your kick misses " + mon.The + "."); Combat.WakeUp(mon); }
                Dm.PlayerActed();
                return;
            }
            var t = M.T[to.X, to.Y];
            if (t is Tile.DoorClosed or Tile.DoorLocked)
            {
                int chance = 15 + Math.Max(0, p.Stat(Stat.Str) - 10) * 6 + (p.IsWarrior ? 10 : 0) + p.StrExEff / 10;
                int roll = Rng.Int(100) + 1;
                Dm.AddDie(100, roll, 0, chance, "kick door", roll <= chance);
                Dm.Noise(p.Pos, 12, 60);
                Synth.Play(Sfx.Door);
                if (roll <= chance) { M.T[to.X, to.Y] = Tile.DoorBroken; Dm.Msg("{warn}WHAMM! You kick the door open!{/}"); }
                else Dm.Msg("WHAMM! The door holds.");
                Dm.PlayerActed();
                return;
            }
            var chest = M.ItemsAt(to).FirstOrDefault(i => i.Def.Kind == IK.Chest);
            if (chest != null)
            {
                Dm.Noise(p.Pos, 10, 50);
                if (chest.Locked && Rng.Chance(30 + p.StrBonus.dmg * 5))
                {
                    chest.Locked = false;
                    Dm.Msg("{warn}WHAMM! You break open the lock!{/}");
                    if (chest.Contents != null)
                        foreach (var pot in chest.Contents.Where(i => i.Def.Kind == IK.Potion).ToList())
                            if (Rng.Chance(40)) { chest.Contents.Remove(pot); Dm.Msg("You hear a muffled shatter."); }
                }
                else Dm.Msg("WHAMM!");
                Dm.PlayerActed();
                return;
            }
            if (!M.Walkable(to)) { Dm.Msg("Ouch! That hurts!"); Combat.Damage(p, 1, Elem.Phys, null, "kicking a wall"); Dm.PlayerActed(); return; }
            Dm.Msg("You kick at empty space.");
            Dm.PlayerActed();
        }

        public static void Close(Point d)
        {
            var to = new Point(P.Pos.X + d.X, P.Pos.Y + d.Y);
            var t = M.T[to.X, to.Y];
            if (t == Tile.DoorBroken) { Dm.Msg("This door is broken."); return; }
            if (t != Tile.DoorOpen) { Dm.Msg("There is no open door there."); return; }
            if (M.ActorAt(to) != null || M.ItemsAt(to).Any()) { Dm.Msg("Something's in the way."); return; }
            M.T[to.X, to.Y] = Tile.DoorClosed;
            Synth.Play(Sfx.Door, 0.4f);
            Dm.Msg("{dim}You close the door.{/}");
            Dm.PlayerActed();
        }

        public static void OpenChest(Item c)
        {
            var p = P;
            if (c.Opened && (c.Contents == null || c.Contents.Count == 0)) { Dm.Msg("The chest is empty."); return; }
            if (c.Trapped && !c.TrapKnown)
            {
                int find = p.Class == Cls.Thief ? p.ThiefSkills[1] : 10;
                if (Rng.Chance(find))
                {
                    c.TrapKnown = true;
                    Dm.Msg("{warn}You notice a trap on the chest! {dim}(A to disarm, or o again to risk it){/}{/}");
                    Dm.PlayerActed();
                    return;
                }
            }
            if (c.Locked)
            {
                if (p.Class == Cls.Thief || p.Carrying("thieves_tools"))
                {
                    int chance = p.Class == Cls.Thief ? p.ThiefSkills[0] : 10 + Math.Max(0, p.Stat(Stat.Dex) - 12) * 3;
                    if (p.Carrying("thieves_tools")) chance += 15;
                    int roll = Rng.Int(100) + 1;
                    Dm.AddDie(100, roll, 0, chance, "pick lock", roll <= chance);
                    if (roll <= chance) { c.Locked = false; Dm.Msg("{good}You pick the chest's lock.{/}"); }
                    else Dm.Msg("You fail to pick the lock.");
                    Dm.PlayerActed();
                    return;
                }
                Dm.Msg("The chest is locked. {dim}(Kick it with D, or cast Knock){/}");
                return;
            }
            if (c.Trapped)
            {
                c.Trapped = false;
                Synth.Play(Sfx.Trap);
                int r = Rng.D(3);
                if (r == 1)
                {
                    Dm.Msg("{bad}A poisoned needle pricks your finger!{/}", true);
                    Combat.Damage(p, Rng.D(3), Elem.Phys, null, "a poisoned needle");
                    if (!Dm.SavingThrow(p, Save.Death, 0, "poison")) { Combat.Damage(p, Rng.Roll(2, 6), Elem.Poison, null, "a poisoned needle"); p.Add(Status.Poisoned, 15); }
                }
                else if (r == 2)
                {
                    Dm.Msg("{bad}A cloud of gas bursts from the chest!{/}", true);
                    Magic.MakeCloud(c.Pos, 1, Elem.Cold, 5, new Color(160, 160, 255));
                }
                else
                {
                    Dm.Msg("{bad}KABOOM! The chest explodes!{/}", true);
                    Synth.Play(Sfx.Fire);
                    Magic.Explosion(c.Pos, 1, new Dice(2, 6), Elem.Fire, null, "an exploding chest");
                    if (c.Contents != null) c.Contents.RemoveAll(i => i.Def.Kind is IK.Scroll or IK.Potion && Rng.Chance(60));
                }
                if (Dm.GameOver) return;
            }
            c.Opened = true;
            Synth.Play(Sfx.Door, 0.4f);
            if (c.Contents == null || c.Contents.Count == 0) Dm.Msg("You open the chest. It is empty.");
            else
            {
                Dm.Msg("You open the chest and empty it out: " + string.Join(", ", c.Contents.Select(i => i.Name())) + ".");
                foreach (var it in c.Contents) M.Drop(it, c.Pos);
                c.Contents.Clear();
            }
            Dm.PlayerActed();
        }

        public static void DisarmChest(Item c)
        {
            var p = P;
            int chance = p.Class == Cls.Thief ? p.ThiefSkills[1] : 15;
            if (p.Carrying("thieves_tools")) chance += 10;
            int roll = Rng.Int(100) + 1;
            Dm.AddDie(100, roll, 0, chance, "disarm chest", roll <= chance);
            if (roll <= chance) { c.Trapped = false; c.TrapKnown = false; Dm.Msg("{good}You disarm the chest's trap.{/}"); Dm.GainXp(3); }
            else
            {
                Dm.Msg("You fail to disarm it.");
                if (roll > 85) { c.TrapKnown = false; OpenChest(c); return; }
            }
            Dm.PlayerActed();
        }

        public static void Stairs(bool down)
        {
            var p = P;
            var t = M.T[p.Pos.X, p.Pos.Y];
            if (down)
            {
                var site = M.PlaceAt(p.Pos);
                if (t != Tile.StairsDown && site == null) { Dm.Msg("You can't go down here."); return; }
                if (site != null && !site.Dungeon)
                {
                    Synth.Play(Sfx.Door, 0.6f);
                    Dm.Msg("You walk into " + site.Name + ".");
                    Dm.EnterLevel(0, Dm.Arrival.Down, site.Branch);
                    Dm.PlayerActed();
                    return;
                }
                if (p.Burden >= 3) { Dm.Msg("You are carrying too much to climb."); return; }
                Synth.Play(Sfx.Stairs);
                var hole = M.DungeonAt(p.Pos);
                if (M.Wild && hole != null)
                {
                    var dd = WorldDb.Get(hole.Branch);
                    Dm.Msg("You descend into " + dd.Name + ".");
                    Dm.EnterLevel(dd.Top, Dm.Arrival.Down, dd.Branch);
                }
                else Dm.EnterLevel(M.Depth + 1, Dm.Arrival.Down);
                Dm.PlayerActed();
            }
            else
            {
                if (t != Tile.StairsUp) { Dm.Msg("You can't go up here."); return; }
                Synth.Play(Sfx.Stairs);
                if (M.Depth == WorldDb.Get(M.Branch).Top) Dm.EnterLevel(0, Dm.Arrival.Up, 0);
                else Dm.EnterLevel(M.Depth - 1, Dm.Arrival.Up);
                Dm.PlayerActed();
            }
        }

        public static void Search()
        {
            int n = 0;
            for (int i = 0; i < 1; i++) n += Traps.Search(true);
            Dm.PlayerActed();
        }

        public static void Rest()
        {
            var p = P;
            p.RestTurns++;
            if (M.Wild) Dm.Travelled = true;
            Traps.Search(false);
            if (p.RestTurns >= 120 && p.Caster && !p.Memorized.SequenceEqual(p.Plan))
            {
                Magic.Memorize(p);
                Dm.Msg("{magic}You finish studying and commit your spells to memory.{/}", true);
                Synth.Play(Sfx.Spell, 0.5f);
            }
            Dm.PlayerActed();
        }

        public static bool NeedsRest(Player p) => p.Hp < p.MaxHp || (p.Caster && !p.Memorized.SequenceEqual(p.Plan));

        public static void Pray()
        {
            var p = P;
            Dm.Msg("You begin praying to " + p.Deity + ".");
            bool onAltar = M.T[p.Pos.X, p.Pos.Y] == Tile.Altar;
            int major = 0, minor = 0;
            if (p.Hp < Math.Max(6, p.MaxHp / 7)) major++;
            if (p.Has(Status.Stoning) || p.Has(Status.Sliming)) major++;
            if (p.Nutrition <= 100) major++;
            if (p.Wearing("a_strangle")) major++;
            if (p.Has(Status.Diseased) || p.Has(Status.Poisoned)) minor++;
            if (p.Eq.Values.Any(i => i.Cursed)) minor++;
            int timeout = p.PrayerTimeout;
            if (onAltar) timeout = timeout * 2 / 3;
            Synth.Play(Sfx.Holy, 0.7f);
            bool ok = (major > 0 && timeout < 200) || (minor > 0 && timeout < 100) || (timeout <= 0);
            if (p.GodAnger > 0) ok = ok && Rng.Chance(30);
            if (!ok)
            {
                p.GodAnger++;
                p.PrayerTimeout += 150;
                Dm.Msg("{bad}You feel that " + p.Deity + " is displeased.{/}", true);
                if (p.GodAnger >= 3 || Rng.Chance(30))
                {
                    int r = Rng.D(3);
                    if (r == 1) { Dm.Msg("{bad}A bolt of lightning strikes you from above!{/}", true); Synth.Play(Sfx.Thunder); Combat.Damage(p, Rng.Roll(4, 6), Elem.Elec, null, "divine wrath"); }
                    else if (r == 2) { Dm.Msg("{bad}You feel foolish.{/}"); p.Drained[(int)Stat.Wis]++; }
                    else { Dm.Msg("{bad}\"Thou durst call upon me? Then die, mortal!\"{/}", true); Dm.WanderingMonster(true); }
                }
                Dm.PlayerActed(Dm.ActCost * 3);
                return;
            }
            p.PrayerTimeout = (p.ClericCaster ? 200 : 300) + Rng.Int(200);
            if (p.GodAnger > 0) p.GodAnger--;
            if (major + minor > 0)
            {
                Dm.Msg("{holy}You feel a hopeful feeling. " + p.Deity + " is listening!{/}", true);
                if (p.Hp < Math.Max(6, p.MaxHp / 7)) { p.Hp = p.MaxHp; Dm.Msg("{holy}You feel much better.{/}"); }
                if (p.Has(Status.Stoning)) { p.Remove(Status.Stoning); Dm.Msg("{holy}You feel limber.{/}"); }
                if (p.Has(Status.Sliming)) { p.Remove(Status.Sliming); Dm.Msg("{holy}The slime burns away.{/}"); }
                if (p.Nutrition <= 100) { p.Nutrition = 900; Dm.Msg("{holy}Your stomach feels content.{/}"); }
                if (p.Wearing("a_strangle"))
                {
                    var n = p.Eq.First(kv => kv.Value.Def.Effect == "a_strangle");
                    p.Eq.Remove(n.Key);
                    p.Pack.Remove(n.Value);
                    Dm.Msg("{holy}The strangling necklace vanishes!{/}");
                }
                p.Remove(Status.Diseased);
                p.Remove(Status.Poisoned);
                foreach (var it in p.Eq.Values.Where(i => i.Cursed)) { it.Cursed = false; it.Known = true; Dm.Msg("{holy}You feel as if " + p.Deity + " is helping you with your " + it.Def.Name + ".{/}"); }
            }
            else
            {
                Dm.Msg("{holy}You feel that " + p.Deity + " is well-pleased.{/}");
                p.Add(Status.Blessed, 100);
                p.PrayerTimeout = 50 + Rng.Int(100);
            }
            Synth.Play(Sfx.Holy);
            Dm.PlayerActed(Dm.ActCost * 3);
        }

        public static void TurnUndead()
        {
            var p = P;
            if (p.Class != Cls.Cleric && p.Class != Cls.Paladin) { Dm.Msg("You have no power over the dead."); return; }
            if (Dm.Turn < p.TurnUndeadReady) { Dm.Msg("You must gather your faith before turning again. (" + (p.TurnUndeadReady - Dm.Turn) + " turns)"); return; }
            int lvl = p.Class == Cls.Paladin ? p.Level - 2 : p.Level;
            if (lvl < 1) { Dm.Msg("Your faith is not yet strong enough."); return; }
            var undead = M.Monsters.Where(m => !m.Dead && m.IsUndead && m.Hostile && Path.Cheb(m.Pos, p.Pos) <= 7 && Dm.CanSeeMonster(m)).OrderBy(m => m.Def.TurnRank).ToList();
            p.TurnUndeadReady = Dm.Turn + 8;
            Dm.Msg("{holy}You present your holy symbol and call upon " + p.Deity + "!{/}");
            Synth.Play(Sfx.Holy);
            Anim.Burst(p.Pos, Magic.BallCells(p.Pos, 3), Pal.Holy, ' ', 0.4f);
            if (undead.Count == 0) { Dm.Msg("But there are no undead here."); Dm.PlayerActed(); return; }
            int bonus = p.Carrying("holy_symbol") ? 2 : 0;
            int roll = Dm.PlayerD20(out int alt);
            int count = Rng.Roll(2, 6);
            int affected = 0;
            int lowestNeed = 99;
            foreach (var m in undead)
            {
                if (affected >= count) break;
                int need = 10 + 3 * (m.Def.TurnRank - lvl);
                lowestNeed = Math.Min(lowestNeed, need);
                if (m.Def.Has(MF.Boss)) need = Math.Max(need, 20);
                if (need <= 1 && !m.Def.Has(MF.Boss))
                {
                    Dm.Msg("{holy}" + m.TheCap + " crumbles to dust before your faith!{/}");
                    Combat.Kill(m, p);
                    affected++;
                }
                else if (need <= 4 || roll + bonus >= need)
                {
                    m.Fleeing = true;
                    m.FleeTurns = Rng.Roll(3, 4) + lvl;
                    m.Add(Status.Turned, m.FleeTurns);
                    Dm.Msg("{holy}" + m.TheCap + " turns to flee!{/}");
                    affected++;
                }
            }
            Dm.AddDie(20, roll, bonus, Math.Max(1, lowestNeed), "turn undead", affected > 0, roll == 20, roll == 1, alt);
            if (affected == 0) Dm.Msg("The undead ignore your feeble faith. {dice}(d20=" + roll + Sgn(bonus) + " vs " + lowestNeed + "){/}");
            else Dm.GainXp(affected * 2);
            Dm.PlayerActed();
        }

        static string Sgn(int m) => m == 0 ? "" : m > 0 ? "+" + m : m.ToString();

        public static void LayOnHands()
        {
            var p = P;
            if (p.Class != Cls.Paladin) { Dm.Msg("Only paladins can lay on hands."); return; }
            if (Dm.Turn < p.LayOnHandsReady) { Dm.Msg("You have already laid on hands today. (" + (p.LayOnHandsReady - Dm.Turn) + " turns)"); return; }
            p.LayOnHandsReady = Dm.Turn + 1000;
            int amt = 2 * p.Level;
            p.Hp = Math.Min(p.MaxHp, p.Hp + amt);
            p.Remove(Status.Diseased);
            Synth.Play(Sfx.Heal);
            Dm.Msg("{holy}You lay hands upon yourself. Holy warmth heals " + amt + " hit points.{/}");
            Dm.PlayerActed();
        }

        public static void Hide()
        {
            var p = P;
            if (p.Class != Cls.Thief) { Dm.Msg("You lack the training to hide in shadows."); return; }
            if (M.Lum(p.Pos.X, p.Pos.Y) > 0.6f) { Dm.Msg("It is too bright here to hide. {dim}(Douse your light?){/}"); return; }
            int chance = p.ThiefSkills[3] + (M.Lum(p.Pos.X, p.Pos.Y) < 0.2f ? 25 : 0);
            int roll = Rng.Int(100) + 1;
            Dm.AddDie(100, roll, 0, chance, "hide in shadows", roll <= chance);
            if (roll <= chance) { p.Add(Status.Hidden, 30); Dm.Msg("{dim}You melt into the shadows.{/}"); }
            else Dm.Msg("You fail to find a hiding place.");
            Dm.PlayerActed();
        }

        public static int[,] ExploreMap()
        {
            var goals = new List<Point>();
            for (int x = 1; x < Map.W - 1; x++)
                for (int y = 1; y < Map.H - 1; y++)
                {
                    if (!M.Seen[x, y] || !(M.Walkable(x, y) || M.T[x, y] == Tile.DoorClosed)) continue;
                    var tr = M.TrapAt(new Point(x, y));
                    if (tr != null && tr.Found) continue;
                    bool frontier = false;
                    foreach (var q in M.Neighbors(new Point(x, y))) if (!M.Seen[q.X, q.Y]) { frontier = true; break; }
                    if (frontier) goals.Add(new Point(x, y));
                }
            foreach (var it in M.Items)
                if (M.Seen[it.Pos.X, it.Pos.Y] && M.Walkable(it.Pos) && !Visited.Contains((M.Key, it.Pos)) && it.Price == 0 && it.Def.Kind != IK.Corpse) goals.Add(it.Pos);
            if (goals.Count == 0) return null;
            return Path.DistMap(M, goals, (x, y) => (M.Seen[x, y] && (M.Walkable(x, y) || M.T[x, y] == Tile.DoorClosed) && !(M.TrapAt(new Point(x, y))?.Found ?? false) && M.T[x, y] != Tile.Chasm && M.T[x, y] != Tile.Lava));
        }

        public static readonly HashSet<(int, Point)> Visited = new HashSet<(int, Point)>();

        public static Point? NextStep(int[,] dist)
        {
            var p = P.Pos;
            if (dist == null || dist[p.X, p.Y] == int.MaxValue) return null;
            Point best = p;
            int bd = dist[p.X, p.Y];
            foreach (var q in M.Neighbors(p))
            {
                if (dist[q.X, q.Y] < bd && (M.MonsterAt(q) == null || M.MonsterAt(q).Ally || (M.MonsterAt(q).Peaceful && M.MonsterAt(q).Def.Has(MF.Townsfolk) && M.MonsterAt(q).Def.Id is not ("hedge_priest" or "innkeeper")))) { bd = dist[q.X, q.Y]; best = q; }
            }
            return best == p ? (Point?)null : best;
        }
    }
}
