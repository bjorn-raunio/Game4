using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class Use
    {
        static Player P => Dm.Player;
        static Map M => Dm.Map;

        static void Learn(Item it, string msg = null)
        {
            if (msg != null) Dm.Msg(msg);
            if (it.Def.Unknown && Dm.Ident.Learn(it.Def.Id)) Dm.Msg("{dim}(This was " + Item.Article(it.BaseName(false)) + ".){/}");
        }

        static void Consume(Item it)
        {
            if (it.Count > 1) it.Count--;
            else Actions.RemoveFromPack(it);
        }

        public static void Quaff(Item it)
        {
            var p = P;
            Synth.Play(Sfx.Drink);
            Consume(it);
            string e = it.Def.Effect;
            switch (e)
            {
                case "heal":
                    {
                        int amt = Rng.Roll(3, 6) + 2;
                        if (p.Hp + amt > p.MaxHp) p.MaxHp++;
                        p.Hp = Math.Min(p.MaxHp, p.Hp + amt);
                        p.Remove(Status.Blind);
                        Learn(it, "{good}You feel better. (+" + amt + "){/}");
                        break;
                    }
                case "extra_heal":
                    {
                        int amt = Rng.Roll(6, 4) + 6;
                        if (p.Hp + amt > p.MaxHp) p.MaxHp += 2;
                        p.Hp = Math.Min(p.MaxHp, p.Hp + amt);
                        foreach (var s in new[] { Status.Blind, Status.Confused, Status.Poisoned, Status.Diseased, Status.Bleeding, Status.Stunned }) p.Remove(s);
                        Learn(it, "{good}You feel much better. (+" + amt + "){/}");
                        break;
                    }
                case "speed": p.Add(Status.Hasted, Rng.Range(50, 100)); Learn(it, "{yellow}You are suddenly moving much faster.{/}"); break;
                case "giant_str": p.Add(Status.GiantStr, 400, Rng.Range(19, 22)); Learn(it, "{orange}You feel strong as a giant!{/}"); break;
                case "heroism": p.Add(Status.Heroism, 250); p.Hp = Math.Min(p.MaxHp + 10, p.Hp + Rng.Roll(2, 8)); Learn(it, "{gold}You feel like a hero!{/}"); break;
                case "super_heroism": p.Add(Status.Heroism, 400); p.Hp = Math.Min(p.MaxHp + 25, p.Hp + Rng.Roll(5, 10)); Learn(it, "{gold}You feel like a legend!{/}"); break;
                case "invis": p.Add(Status.Invisible, 400); Learn(it, "{magic}Gee! All of a sudden, you can't see yourself.{/}"); break;
                case "fire_res": p.Add(Status.ResFire, 400); Learn(it, "{orange}You feel a pleasant coolness.{/}"); break;
                case "poison":
                    if (p.Wearing("a_poison")) { Dm.Msg("This tastes like slime mold juice."); it.Tried = true; Dm.Ident.Learn(it.Def.Id); break; }
                    Dm.Msg("{bad}Yecch! This stuff tastes like poison.{/}");
                    Combat.Damage(p, Rng.Roll(1, 10), Elem.Poison, null, "a potion of poison");
                    p.Add(Status.Weakened, 100, 1);
                    Learn(it);
                    break;
                case "delusion":
                    Dm.Msg("{good}You feel much better!{/}");
                    it.Tried = true;
                    break;
                case "clairvoyance": M.RevealMap(false); Learn(it, "{magic}A map of the level unrolls behind your eyes.{/}"); break;
                case "treasure":
                    M.RevealMap(true);
                    Learn(it, "{gold}You sense the presence of treasure.{/}");
                    break;
                case "esp": p.Add(Status.ESP, 400); Learn(it, "{magic}You hear the thoughts of nearby creatures.{/}"); break;
                case "sleep":
                    if (p.Race == Race.Elf && Rng.Chance(90) || p.Wearing("r_free")) { Dm.Msg("You yawn."); it.Tried = true; break; }
                    p.Add(Status.Asleep, Rng.Range(10, 20));
                    Learn(it, "{bad}You suddenly fall asleep.{/}");
                    break;
                case "confusion": p.Add(Status.Confused, Rng.Range(10, 20)); Learn(it, "{bad}Huh, what? Where am I?{/}"); break;
                case "fortune": p.Set(Status.Fortune, 300, 1); Learn(it, "{gold}You feel lucky. Every d20 seems to want to roll high.{/}"); break;
                case "misfortune": p.Set(Status.Fortune, 200, -1); Learn(it, "{bad}You feel jinxed. The dice turn cold in the dark.{/}"); break;
                case "restoration": Magic.Cast(p, SpellId.Restoration, p.Pos, p.Level); Learn(it); break;
                case "gain_level":
                    p.Xp = Math.Max(p.Xp, Rules.XpFor(p.Class, p.Level + 1));
                    Dm.GainXp(1);
                    Learn(it, "{gold}You feel more experienced.{/}");
                    break;
                case "cure_disease":
                    p.Remove(Status.Diseased); p.Remove(Status.Sliming);
                    Learn(it, "{good}You feel healthy.{/}");
                    break;
                case "levitation": p.Add(Status.Levitate, Rng.Range(50, 100)); Learn(it, "{magic}You start to float in the air!{/}"); break;
                default: Dm.Msg("This tastes like water."); break;
            }
            if (it.Def.Effect is not ("delusion")) it.Tried = !it.TypeKnown;
            Dm.PlayerActed();
        }

        public static bool ScrollNeedsItem(Item it) => it.Def.Effect is "identify" or "recharge";

        public static void Read(Item it, Item chosen = null)
        {
            var p = P;
            if (p.Has(Status.Blind)) { Dm.Msg("You can't see to read!"); return; }
            Synth.Play(Sfx.Read);
            string e = it.Def.Effect;
            Consume(it);
            switch (e)
            {
                case "identify":
                    Learn(it, "This is an identify scroll.");
                    if (Rng.Chance(15)) { foreach (var i in p.Pack) { i.Known = true; i.ChargesKnown = true; if (i.Def.Unknown) Dm.Ident.Learn(i.Def.Id); } Dm.Msg("{magic}You feel self-knowledgeable... Everything in your pack is revealed!{/}"); }
                    else if (chosen != null) Actions.IdentifyItem(chosen);
                    break;
                case "enchant_weapon":
                    {
                        var w = p.Weapon;
                        if (w == null) { Dm.Msg("Your hands twitch."); it.Tried = true; break; }
                        if (w.Ench >= 5 && Rng.Chance(60)) { Dm.Msg("{bad}Your " + w.Def.Name + " violently glows and evaporates!{/}"); Actions.RemoveFromPack(w); Learn(it); break; }
                        w.Ench++;
                        w.Cursed = false;
                        w.Erosion = 0;
                        Learn(it, "{magic}Your " + w.Def.Name + " glows blue for a moment.{/}");
                        break;
                    }
                case "enchant_armor":
                    {
                        var arms = p.Eq.Values.Where(i => i.Def.Kind is IK.Armor or IK.Shield or IK.Helm or IK.Cloak or IK.Boots or IK.Gloves).ToList();
                        if (arms.Count == 0) { Dm.Msg("Your skin glows then fades."); it.Tried = true; break; }
                        var a = Rng.Pick(arms);
                        if (a.Ench >= 4 && Rng.Chance(60)) { Dm.Msg("{bad}Your " + a.Def.Name + " violently glows and evaporates!{/}"); Actions.RemoveFromPack(a); Learn(it); break; }
                        a.Ench++;
                        a.Cursed = false;
                        a.Erosion = 0;
                        Learn(it, "{magic}Your " + a.Def.Name + " glows silver for a moment.{/}");
                        break;
                    }
                case "remove_curse":
                    {
                        int n = 0;
                        foreach (var i in p.Eq.Values.Where(i => i.Cursed).ToList()) { i.Cursed = false; i.Known = true; n++; }
                        Learn(it, n > 0 ? "{holy}You feel like someone is helping you.{/}" : "{holy}You feel like you need some help.{/}");
                        break;
                    }
                case "prot_undead": p.Add(Status.ProtUndead, 300); Learn(it, "{holy}A ward against the dead settles over you.{/}"); break;
                case "teleport": Magic.TeleportActor(p, null); Learn(it); break;
                case "magic_map": M.RevealMap(false); Learn(it, "{ink}A map coalesces in your mind, inked line by line.{/}"); break;
                case "fire":
                    Dm.Msg("{orange}The scroll erupts in a tower of flame!{/}");
                    Synth.Play(Sfx.Fire);
                    Magic.Explosion(p.Pos, 1, new Dice(1, 6), Elem.Fire, null, "a scroll of fire", true, false);
                    Learn(it);
                    break;
                case "light": Magic.Cast(p, SpellId.Light, p.Pos, 3); Learn(it); break;
                case "curse":
                    {
                        var items = p.Eq.Values.ToList();
                        if (items.Count == 0) { Dm.Msg("You feel as if you need some help."); break; }
                        for (int i = 0; i < Rng.Range(1, 3); i++) { var x = Rng.Pick(items); x.Cursed = true; }
                        Learn(it, "{bad}You feel as if you need some help.{/}");
                        break;
                    }
                case "create_monster":
                    {
                        var def = Spawn.PickMonster(M.Depth, d => d.Freq > 0 && !d.Has(MF.Immobile) && !d.Has(MF.Mimic));
                        var m = def == null ? null : Spawn.PlaceMonster(M, def, p.Pos, false);
                        if (m != null) m.Aware = 30;
                        Learn(it, "{bad}A monster appears out of thin air!{/}");
                        break;
                    }
                case "fear":
                    {
                        int n = 0;
                        foreach (var m in M.Monsters.Where(m => m.Hostile && Path.Cheb(m.Pos, p.Pos) <= 10 && !m.Def.Has(MF.Mindless) && !m.Def.Has(MF.Boss)))
                        {
                            m.Fleeing = true;
                            m.FleeTurns = 20;
                            n++;
                        }
                        Learn(it, "{holy}You hear maniacal laughter in the distance." + (n > 0 ? " Monsters flee!" : "") + "{/}");
                        break;
                    }
                case "reroll":
                    {
                        int s = Rng.Int(6);
                        var rolls = new[] { Rng.D(6), Rng.D(6), Rng.D(6), Rng.D(6) };
                        int total = rolls.Sum() - rolls.Min();
                        Dm.AddDie(6, total, 0, p.Base[s], "reroll " + Rules.StatNames[s], total > p.Base[s]);
                        Synth.Play(Sfx.Dice);
                        Dm.Msg("{gold}The scroll rolls 4d6 for your " + Rules.StatLong[s] + ": [" + string.Join(" ", rolls) + "] = " + total + " (was " + p.Base[s] + ").{/}");
                        if (total > p.Base[s]) { p.Base[s] = total; Dm.Msg("{good}You feel your " + Rules.StatLong[s].ToLower() + " improve!{/}"); }
                        else Dm.Msg("You keep the better roll. Nothing changes.");
                        Learn(it);
                        break;
                    }
                case "blank": Dm.Msg("This scroll seems to be blank."); Learn(it); break;
                case "wandering":
                    {
                        var m = Spawn.PlaceMonster(M, MonsterDb.Get("wandering"), p.Pos, false);
                        if (m != null) m.Aware = 40;
                        Learn(it, "{bad}The Wandering Monster arrives, consulting a table.{/}");
                        break;
                    }
                case "recharge":
                    Learn(it, "This is a scroll of recharging.");
                    if (chosen != null)
                    {
                        if (chosen.Def.Kind != IK.Wand && chosen.Def.Charges.IsZero) { Dm.Msg("You can't recharge that."); break; }
                        if (Rng.Chance(12)) { Dm.Msg("{bad}The " + chosen.BaseName(false) + " explodes!{/}"); Actions.RemoveFromPack(chosen); Combat.Damage(p, Rng.Roll(2, 6), Elem.Force, null, "an exploding wand"); break; }
                        chosen.Charges += Rng.Range(3, 7);
                        chosen.ChargesKnown = true;
                        Dm.Msg("{magic}Your " + chosen.BaseName(false) + " glows briefly. (" + chosen.Charges + " charges){/}");
                    }
                    break;
                case "teleport_level":
                    {
                        int dest = Math.Clamp(M.Depth + Rng.Range(-3, 3), 1, Rules.MaxDepth - 1);
                        if (dest == M.Depth) dest = Math.Max(1, dest - 1);
                        Learn(it, "{magic}You feel a wrenching sensation as the floor drops away...{/}");
                        Dm.EnterLevel(dest, Dm.Arrival.Teleport);
                        break;
                    }
            }
            Dm.PlayerActed();
        }

        public static bool CanUseSpellScroll(Item it, out string why)
        {
            var p = P;
            var sd = SpellDb.Get(it.Def.Spell);
            why = null;
            if (sd.Cleric && !p.ClericCaster) { why = "The holy script is meaningless to you."; return false; }
            if (!sd.Cleric && p.Class != Cls.MagicUser && !(p.Class == Cls.Thief && p.Level >= 10)) { why = "You can't decipher the arcane script."; return false; }
            return true;
        }

        public static void CastFromScroll(Item it, Point target, Item chosen)
        {
            var p = P;
            var sd = SpellDb.Get(it.Def.Spell);
            Consume(it);
            if (p.Class == Cls.Thief && !sd.Cleric && Rng.Chance(25))
            {
                Dm.Msg("{bad}You fumble the arcane words and the scroll crumbles!{/}");
                Dm.PlayerActed();
                return;
            }
            Dm.Msg("{magic}You read the scroll aloud: " + sd.Name + "!{/}");
            Magic.Cast(p, sd.Id, target, Math.Max(p.Level, sd.Level * 2 - 1), chosen);
            Dm.PlayerActed();
        }

        public static void CopyScroll(Item it)
        {
            var p = P;
            var sd = SpellDb.Get(it.Def.Spell);
            Consume(it);
            int chance = Rules.IntLearn(p.Stat(Stat.Int));
            int roll = Rng.Int(100) + 1;
            Dm.AddDie(100, roll, 0, chance, "learn " + sd.Name, roll <= chance);
            if (roll <= chance)
            {
                p.Known.Add(sd.Id);
                Dm.Msg("{magic}You carefully copy " + sd.Name + " into your spellbook. {dice}(d100=" + roll + " vs " + chance + "%){/}{/}");
                Synth.Play(Sfx.Spell);
            }
            else Dm.Msg("{bad}You fail to understand the spell, and the scroll crumbles as you work. {dice}(d100=" + roll + " vs " + chance + "%){/}{/}");
            Dm.PlayerActed(Dm.ActCost * 10);
        }

        public static void ReadBook(Item it)
        {
            var p = P;
            if (p.Class != Cls.MagicUser) { Dm.Msg("The pages swim with arcane symbols you cannot follow."); return; }
            it.Known = true;
            if (it.BookSpells == null || it.BookSpells.Count == 0) { Dm.Msg("The book is empty."); return; }
            int chance = Rules.IntLearn(p.Stat(Stat.Int));
            foreach (var s in it.BookSpells.ToList())
            {
                var sd = SpellDb.Get(s);
                if (p.Known.Contains(s)) { Dm.Msg("{dim}You already know " + sd.Name + ".{/}"); continue; }
                int roll = Rng.Int(100) + 1;
                Dm.AddDie(100, roll, 0, chance, "learn " + sd.Name, roll <= chance);
                if (roll <= chance) { p.Known.Add(s); Dm.Msg("{magic}You learn " + sd.Name + " (level " + sd.Level + ")!{/}"); }
                else { Dm.Msg("You cannot grasp " + sd.Name + ". The pages blur and fade."); it.BookSpells.Remove(s); }
            }
            Synth.Play(Sfx.Spell, 0.6f);
            Dm.PlayerActed(Dm.ActCost * 10);
        }

        public static bool WandNeedsTarget(Item it) => it.Def.Effect is not ("w_light" or "w_secret" or "w_create" or "w_wonder");

        public static void Zap(Item it, Point target)
        {
            var p = P;
            if (it.Charges <= 0)
            {
                if (Rng.OneIn(121)) { Dm.Msg("You wrest one last charge from the worn-out wand."); }
                else { Dm.Msg("Nothing happens."); it.ChargesKnown = true; Dm.PlayerActed(); return; }
            }
            else it.Charges--;
            Synth.Play(Sfx.Zap);
            string e = it.Def.Effect;
            int lvl = Math.Max(6, p.Level);
            switch (e)
            {
                case "w_missile":
                    {
                        var path = Path.Ray(M, p.Pos, target, 12, true);
                        Anim.Bolt(path, '*', new Color(190, 140, 255), true, 0.015f);
                        var t = path.Select(q => M.ActorAt(q)).FirstOrDefault(a => a != null && a != p);
                        if (t != null) { int d = Rng.Roll(2, 4); Dm.Msg("The magic missile hits " + (t.IsPlayer ? "you" : t.The) + ". {dice}(" + d + "){/}"); Combat.Damage(t, d, Elem.Force, p, "a magic missile"); }
                        Learn(it);
                        break;
                    }
                case "w_fire": Magic.Cast(p, SpellId.Fireball, target, 6); Learn(it); break;
                case "w_cold":
                    {
                        var cells = Magic.ConeCells(p.Pos, target, 6);
                        Anim.Burst(p.Pos, cells, Magic.ElemColor(Elem.Cold), '*', 0.4f);
                        Synth.Play(Sfx.Cold);
                        foreach (var a in Magic.ActorsIn(cells).Where(a => a != p).ToList())
                        {
                            int d = Rng.Roll(6, 6);
                            if (Dm.SavingThrow(a, Save.Wand, 0, "cold")) d /= 2;
                            if (a is Monster mm && Dm.CanSeeMonster(mm)) Dm.Msg(mm.TheCap + " is blasted by frost. {dice}(" + d + "){/}");
                            Combat.Damage(a, d, Elem.Cold, p, "frost");
                        }
                        Learn(it);
                        break;
                    }
                case "w_lightning": Magic.LightningBolt(p, target, 12, new Dice(6, 6), "lightning bolt"); Learn(it); break;
                case "w_sleep":
                case "w_slow":
                case "w_paralysis":
                case "w_teleport":
                case "w_polymorph":
                case "w_striking":
                    {
                        var path = Path.Ray(M, p.Pos, target, 12, true);
                        var col = e switch { "w_sleep" => new Color(160, 160, 255), "w_slow" => new Color(120, 150, 200), "w_paralysis" => new Color(220, 220, 120), "w_teleport" => new Color(200, 160, 255), "w_polymorph" => new Color(220, 120, 220), _ => new Color(255, 255, 255) };
                        Anim.Bolt(path, '*', col, true, 0.015f);
                        var t = path.Select(q => M.MonsterAt(q)).FirstOrDefault(a => a != null);
                        if (t == null) { Dm.Msg("The bolt dissipates harmlessly."); it.Tried = true; break; }
                        Combat.WakeUp(t);
                        switch (e)
                        {
                            case "w_sleep":
                                if (t.Def.Has(MF.ResSleep) || Dm.SavingThrow(t, Save.Wand, 0, "")) Dm.Msg(t.TheCap + " resists.");
                                else { t.Add(Status.Asleep, Rng.Range(10, 25)); Dm.Msg(t.TheCap + " falls asleep."); }
                                Learn(it);
                                break;
                            case "w_slow":
                                if (Dm.SavingThrow(t, Save.Wand, 0, "")) Dm.Msg(t.TheCap + " resists.");
                                else { t.Add(Status.Slowed, 40); Dm.Msg(t.TheCap + " seems to be moving slower."); }
                                Learn(it);
                                break;
                            case "w_paralysis":
                                if (Dm.SavingThrow(t, Save.Wand, 0, "")) Dm.Msg(t.TheCap + " resists.");
                                else { t.Add(Status.Paralyzed, Rng.Range(6, 12)); Dm.Msg(t.TheCap + " is frozen in place!"); }
                                Learn(it);
                                break;
                            case "w_teleport":
                                if (t.Def.Has(MF.Boss)) { Dm.Msg(t.TheCap + " shimmers but stays put."); break; }
                                Magic.TeleportActor(t, null);
                                Learn(it);
                                break;
                            case "w_polymorph":
                                Magic.Polymorph(t);
                                Learn(it);
                                break;
                            case "w_striking":
                                if (Dm.SavingThrow(t, Save.Wand, 0, "")) { Dm.Msg("Boing! " + t.TheCap + " resists the force bolt."); break; }
                                int d = Rng.Roll(2, 12);
                                Dm.Msg("The force bolt hits " + t.The + ". {dice}(" + d + "){/}");
                                Combat.Damage(t, d, Elem.Force, p, "a force bolt");
                                Learn(it);
                                break;
                        }
                        break;
                    }
                case "w_fear":
                    Magic.Cast(p, SpellId.Fear, target, lvl);
                    Learn(it);
                    break;
                case "w_digging":
                    {
                        var d = Path.Step(p.Pos, target);
                        if (d == Point.Zero)
                        {
                            if (M.Depth < Rules.MaxDepth - 1 && M.T[p.Pos.X, p.Pos.Y] is Tile.Floor or Tile.Corridor) { Dm.Msg("{brown}You dig a hole through the floor... you fall through!{/}"); Learn(it); Dm.EnterLevel(M.Depth + 1, Dm.Arrival.Fall); Dm.PlayerActed(); return; }
                            Dm.Msg("The floor here is too hard to dig in.");
                            break;
                        }
                        var c = p.Pos;
                        int dug = 0;
                        var path = new List<Point>();
                        for (int i = 0; i < 10; i++)
                        {
                            c = new Point(c.X + d.X, c.Y + d.Y);
                            if (!Map.InB(c) || M.T[c.X, c.Y] is Tile.PermWall or Tile.Screen) break;
                            path.Add(c);
                            if (M.T[c.X, c.Y] is Tile.Wall or Tile.DoorSecret or Tile.Rubble or Tile.Crystal or Tile.Pillar or Tile.Statue or Tile.Bookshelf or Tile.DoorClosed or Tile.DoorLocked) { M.T[c.X, c.Y] = Tile.Corridor; dug++; }
                        }
                        Anim.Bolt(path, '*', new Color(160, 130, 90), true, 0.02f);
                        Dm.Msg(dug > 0 ? "{brown}You dig through the rock.{/}" : "You dig through empty air.");
                        Learn(it);
                        break;
                    }
                case "w_wonder":
                    Wonder(target);
                    break;
                case "w_light": Magic.Cast(p, SpellId.Light, p.Pos, 5); Learn(it); break;
                case "w_secret":
                    {
                        int n = 0;
                        foreach (var c in Magic.BallCells(p.Pos, 8))
                        {
                            if (M.T[c.X, c.Y] == Tile.DoorSecret) { M.T[c.X, c.Y] = Tile.DoorClosed; n++; }
                            if (M.Traps.TryGetValue(c, out var t) && !t.Found) { t.Found = true; n++; }
                        }
                        for (int x = Math.Max(0, p.Pos.X - 8); x <= Math.Min(Map.W - 1, p.Pos.X + 8); x++)
                            for (int y = Math.Max(0, p.Pos.Y - 8); y <= Math.Min(Map.H - 1, p.Pos.Y + 8); y++)
                                if (M.T[x, y] == Tile.DoorSecret) { M.T[x, y] = Tile.DoorClosed; M.Seen[x, y] = true; M.Remember(x, y); n++; }
                        if (n > 0) Learn(it, "{magic}Hidden things are revealed!{/}");
                        else { Dm.Msg("You feel self-conscious."); it.Tried = true; }
                        break;
                    }
                case "w_create":
                    {
                        var def = Spawn.PickMonster(M.Depth, d2 => d2.Freq > 0 && !d2.Has(MF.Immobile) && !d2.Has(MF.Mimic));
                        var m = def == null ? null : Spawn.PlaceMonster(M, def, p.Pos, false);
                        if (m != null) m.Aware = 30;
                        Learn(it, "{bad}A monster appears!{/}");
                        break;
                    }
            }
            Dm.PlayerActed();
        }

        static void Wonder(Point target)
        {
            var p = P;
            int r = Rng.D(16);
            switch (r)
            {
                case 1: Dm.Msg("{magic}A stream of shimmering butterflies pours from the wand.{/}"); break;
                case 2: Dm.Msg("{magic}The wand casts a fireball!{/}"); Magic.Cast(p, SpellId.Fireball, target, 6); break;
                case 3: Dm.Msg("{magic}Lightning crackles from the tip!{/}"); Magic.LightningBolt(p, target, 12, new Dice(6, 6), "lightning"); break;
                case 4: Dm.Msg("{magic}Grass sprouts everywhere around you.{/}"); foreach (var c in Magic.BallCells(p.Pos, 3)) if (M.T[c.X, c.Y] == Tile.Floor) M.T[c.X, c.Y] = Tile.Grass; break;
                case 5: Dm.Msg("{magic}You turn invisible!{/}"); p.Add(Status.Invisible, 100); break;
                case 6: Dm.Msg("{magic}It rains gems! Mostly glass.{/}"); for (int i = 0; i < Rng.Range(3, 8); i++) M.Drop(Rng.Chance(80) ? Item.Make("glass") : Spawn.RandomItem(M.Depth, IK.Gem), M.NearestFree(p.Pos, 2) ?? p.Pos); break;
                case 7: Dm.Msg("{magic}The wand shouts: \"ROLL FOR INITIATIVE!\"{/}"); Dm.Noise(p.Pos, 30, 100); break;
                case 8: Dm.Msg("{magic}You grow larger, then smaller. Nothing else happens.{/}"); break;
                case 9: Magic.Cast(p, SpellId.Slow, target, 6); break;
                case 10: Magic.Cast(p, SpellId.Haste, p.Pos, 6); break;
                case 11: Dm.Msg("{magic}A cloud of stinking gas billows from the wand!{/}"); Magic.Cast(p, SpellId.StinkingCloud, target, 6); break;
                case 12: Dm.Msg("{magic}The wand summons a creature!{/}"); var def = Spawn.PickMonster(M.Depth, d => d.Freq > 0 && !d.Has(MF.Immobile)); if (def != null) { var m = Spawn.PlaceMonster(M, def, target, false); if (m != null) m.Aware = 30; } break;
                case 13: Dm.Msg("{magic}Everything nearby is suddenly very well lit.{/}"); Magic.Cast(p, SpellId.Light, p.Pos, 8); break;
                case 14: Dm.Msg("{magic}Your hair turns a startling shade of blue. You feel fabulous.{/}"); break;
                case 15: Magic.Cast(p, SpellId.PolymorphOther, target, 6); break;
                default: Dm.Msg("{magic}You feel a strange sense of déjà vu. The wand smirks.{/}"); Magic.TeleportActor(p, null); break;
            }
        }

        public static void Eat(Item it)
        {
            var p = P;
            int turns = 1;
            if (it.Def.Kind == IK.Corpse)
            {
                var c = it.CorpseOf;
                if (c == null) return;
                bool rotten = it.Age > 60 && it.Age > 0;
                if (c.G == '@' && !c.Has(MF.Shopkeeper) && (p.Race is Race.Human or Race.HalfElf or Race.HalfOrc))
                {
                    Dm.Msg("{bad}You cannibal! You will regret this!{/}", true);
                    p.GodAnger++;
                }
                Dm.Msg("You eat the " + c.Name + " corpse.");
                int nut = c.Size == Size.Large ? 900 : c.Size == Size.Small ? 150 : 400;
                p.Nutrition += nut;
                turns = c.Size == Size.Large ? 10 : 5;
                if (rotten && Rng.Chance(60))
                {
                    Dm.Msg("{bad}Ulch - that meat was tainted! You feel deathly sick.{/}", true);
                    p.Add(Status.Poisoned, 20);
                    Combat.Damage(p, Rng.Roll(1, 8), Elem.Poison, null, "a rotten corpse");
                }
                else if (c.Attacks.Any(a => a.Sp is AtkSp.Poison or AtkSp.Poison2) && !p.Wearing("a_poison"))
                {
                    Dm.Msg("{bad}Ecch - that must have been poisonous!{/}");
                    if (!Dm.SavingThrow(p, Save.Death, 0, "poison")) { Combat.Damage(p, Rng.Roll(1, 10), Elem.Poison, null, "a poisonous corpse"); p.Add(Status.Weakened, 60, 1); }
                }
                else if (c.Id is "cockatrice" or "basilisk")
                {
                    Dm.Msg("{bad}You feel your limbs stiffen. Oh no.{/}", true);
                    p.Add(Status.Stoning, 6);
                }
                else if (c.Id is "hill_giant" or "fire_giant" or "ogre" && Rng.Chance(c.Id == "ogre" ? 15 : 50))
                {
                    if (p.Base[0] < 18) { p.Base[0]++; Dm.Msg("{good}You feel stronger!{/}"); }
                    else if (p.IsWarrior && p.StrEx < 100) { p.StrEx = Math.Min(100, p.StrEx + Rng.Range(5, 15)); Dm.Msg("{good}You feel stronger!{/}"); }
                }
                else if (c.Id == "mind_flayer" && Rng.Chance(50)) { p.Base[1] = Math.Min(19, p.Base[1] + 1); Dm.Msg("{good}You feel smarter. Tentacularly so.{/}"); }
                else if (c.Id == "troll" && Rng.Chance(30)) { p.Add(Status.Regenerating, 300); Dm.Msg("{good}Your wounds begin to knit together on their own.{/}"); }
                else if (c.Id == "beetle" && Rng.Chance(25)) { p.Add(Status.ResFire, 500); Dm.Msg("{orange}You feel a momentary chill.{/}"); }
                else if (c.Id.StartsWith("young_") && Rng.Chance(50))
                {
                    var res = c.Id == "young_red" ? Status.ResFire : Status.ResCold;
                    p.Add(res, 2000);
                    Dm.Msg("{good}You feel " + (res == Status.ResFire ? "fireproof" : "warm and toasty") + ".{/}");
                }
                else if (c.Id == "wererat") { Dm.Msg("{bad}You feel feverish.{/}"); p.Add(Status.Diseased, 200); }
                else if (c.Id is "shrieker" or "violet_fungus") { Dm.Msg("This tastes like a mushroom."); }
                else Dm.Msg(rotten ? "That tasted a bit off." : "This " + c.Name + " corpse tastes terrible!");
                if (it.Count > 1) it.Count--;
                else if (p.Pack.Contains(it)) Actions.RemoveFromPack(it);
                else M.Items.Remove(it);
            }
            else
            {
                Consume(it);
                p.Nutrition += it.Def.Nutrition;
                turns = it.Def.Nutrition >= 600 ? 5 : it.Def.Nutrition >= 300 ? 3 : 1;
                string flavor = it.Def.Id switch
                {
                    "ration" => "That iron ration tasted like duty and salt.",
                    "std_ration" => "That food really hit the spot!",
                    "apple" => "Core dumped.",
                    "waybread" => "You eat a single bite of the waybread and are completely full. Elves.",
                    "jerky" => "Chewy. Probably venison.",
                    "cheese" => "The cheese is pungent and glorious.",
                    _ => "You finish eating.",
                };
                if (it.Def.Effect == "mushroom")
                {
                    int r = Rng.D(6);
                    if (r == 1) { p.Add(Status.Confused, Rng.Range(5, 10)); flavor = "{magenta}Whoa! Everything is so... purple.{/}"; }
                    else if (r == 2) { p.Hp = Math.Min(p.MaxHp, p.Hp + Rng.Roll(2, 4)); flavor = "{good}You feel refreshed.{/}"; }
                    else if (r == 3 && !p.Wearing("a_poison")) { Combat.Damage(p, Rng.D(6), Elem.Poison, null, "a poisonous mushroom"); flavor = "{bad}That mushroom was poisonous!{/}"; }
                    else flavor = "An earthy, nutty taste.";
                }
                Dm.Msg(flavor);
            }
            if (p.Nutrition > 4500) { p.Nutrition = 4500; Dm.Msg("{warn}You're having a hard time getting all of it down.{/}"); }
            Dm.PlayerActed(Dm.ActCost * turns);
        }

        public static void ReleaseIoun(Item it)
        {
            var p = P;
            Actions.RemoveFromPack(it);
            if (it.Count > 1) { it.Count--; var one = it.Split(1); }
            p.Iouns.Add(it);
            it.OrbitPhase = Rng.Int(12);
            Synth.Play(Sfx.Spell, 0.6f);
            Dm.Msg("{magic}You toss the " + it.Def.Name + " into the air. It begins to orbit your head!{/}");
            string desc = it.Def.Effect switch
            {
                "str" => "You feel stronger.",
                "int" => "You feel smarter.",
                "wis" => "You feel wiser.",
                "dex" => "You feel nimbler.",
                "con" => "You feel healthier.",
                "cha" => "You feel more charming.",
                "ac" => "You feel protected.",
                "regen" => "You feel your wounds begin to knit.",
                "light" => "It glows like a tiny sun.",
                _ => "",
            };
            Dm.Msg("{magic}" + desc + "{/}");
            Dm.PlayerActed();
        }

        public static bool ToolNeedsDir(Item it) => it.Def.Effect is "pole" or "spike" or "lockpick" or "mirror" or "horn" or "necklace";

        public static void Apply(Item it, Point target)
        {
            var p = P;
            var d = Path.Step(p.Pos, target);
            var adj = new Point(p.Pos.X + d.X, p.Pos.Y + d.Y);
            switch (it.Def.Effect)
            {
                case "pole":
                    {
                        Dm.Msg("You prod the floor ahead with your 10-foot pole.");
                        bool any = false;
                        for (int i = 1; i <= 2; i++)
                        {
                            var q = new Point(p.Pos.X + d.X * i, p.Pos.Y + d.Y * i);
                            if (!Map.InB(q) || !M.Walkable(q)) break;
                            var mon = M.MonsterAt(q);
                            if (mon != null && mon.Disguised) { mon.Disguised = false; Dm.Msg("{bad}The \"chest\" flinches and bares its teeth! A mimic!{/}", true); any = true; break; }
                            var t = M.TrapAt(q);
                            if (t != null)
                            {
                                t.Found = true;
                                M.Remember(q.X, q.Y);
                                any = true;
                                if (t.T is TrapT.Pit or TrapT.SpikedPit or TrapT.Arrow or TrapT.Dart or TrapT.BearTrap or TrapT.Block)
                                {
                                    Dm.Msg("{good}Your pole triggers " + Item.Article(t.Name) + "! It springs harmlessly.{/}");
                                    if (t.T is TrapT.Arrow or TrapT.Dart or TrapT.BearTrap or TrapT.Block) M.Traps.Remove(q);
                                }
                                else Dm.Msg("{good}Your pole finds " + Item.Article(t.Name) + ".{/}");
                            }
                            if (M.T[q.X, q.Y] == Tile.DoorSecret) { M.T[q.X, q.Y] = Tile.DoorClosed; any = true; }
                        }
                        foreach (var q in new[] { new Point(adj.X + d.Y, adj.Y + d.X), new Point(adj.X - d.Y, adj.Y - d.X), new Point(adj.X + d.X, adj.Y + d.Y) })
                            if (Map.InB(q) && M.T[q.X, q.Y] == Tile.DoorSecret && Rng.Chance(50)) { M.T[q.X, q.Y] = Tile.DoorClosed; M.Remember(q.X, q.Y); Dm.Msg("{good}The pole knocks hollowly on a hidden door!{/}"); any = true; }
                        if (!any) Dm.Msg("{dim}Tap, tap. Nothing suspicious.{/}");
                        Dm.PlayerActed();
                        break;
                    }
                case "rope":
                    if (p.Has(Status.Held) && p.Holder == null) { p.Remove(Status.Held); Dm.Msg("You climb out using your rope."); Dm.PlayerActed(); }
                    else Dm.Msg("You coil and uncoil the rope thoughtfully. (It helps in pits and chasms automatically.)");
                    break;
                case "spike":
                    if (M.T[adj.X, adj.Y] == Tile.DoorClosed)
                    {
                        M.T[adj.X, adj.Y] = Tile.DoorLocked;
                        if (it.Count > 1) it.Count--; else Actions.RemoveFromPack(it);
                        Dm.Msg("You hammer an iron spike into the door. It's wedged shut.");
                        Dm.Noise(p.Pos, 8, 40);
                        Dm.PlayerActed();
                    }
                    else Dm.Msg("You need a closed door to spike.");
                    break;
                case "lockpick":
                    {
                        if (M.T[adj.X, adj.Y] == Tile.DoorLocked)
                        {
                            int chance = p.Class == Cls.Thief ? p.ThiefSkills[0] + 15 : 25 + Math.Max(0, p.Stat(Stat.Dex) - 12) * 3;
                            int roll = Rng.Int(100) + 1;
                            Dm.AddDie(100, roll, 0, chance, "pick lock", roll <= chance);
                            if (roll <= chance) { M.T[adj.X, adj.Y] = Tile.DoorClosed; Dm.Msg("{good}You pick the lock.{/}"); }
                            else Dm.Msg("You fail to pick the lock.");
                            Dm.PlayerActed();
                            break;
                        }
                        var chest = M.ItemsAt(p.Pos).Concat(M.ItemsAt(adj)).FirstOrDefault(i => i.Def.Kind == IK.Chest && i.Locked);
                        if (chest != null) { Actions.OpenChest(chest); break; }
                        Dm.Msg("There's nothing to pick there.");
                        break;
                    }
                case "symbol":
                    Actions.TurnUndead();
                    break;
                case "mirror":
                    {
                        var path = Path.Ray(M, p.Pos, target, 8, true);
                        var mon = path.Select(q => M.MonsterAt(q)).FirstOrDefault(m => m != null);
                        if (mon == null) { Dm.Msg("You look as good as ever."); Dm.PlayerActed(); break; }
                        if (mon.Def.Id == "vampire") Dm.Msg(mon.TheCap + " has no reflection.");
                        else if (mon.Def.GazeSp == AtkSp.Petrify && Dm.CanSeeMonster(mon))
                        {
                            Dm.Msg("{gold}" + mon.TheCap + " catches sight of its own reflection!{/}", true);
                            if (!Dm.SavingThrow(mon, Save.Petrify, 0, ""))
                            {
                                Dm.Msg("{gold}" + mon.TheCap + " is turned to stone!{/}");
                                var pos = mon.Pos;
                                Combat.Kill(mon, p);
                                M.Items.RemoveAll(i => i.Pos == pos && i.Def.Kind == IK.Corpse);
                                M.T[pos.X, pos.Y] = Tile.Statue;
                            }
                            else Dm.Msg(mon.TheCap + " averts its eyes.");
                        }
                        else if (mon.Def.Id == "umber_hulk") { Dm.Msg(mon.TheCap + " is confused by its own gaze!"); mon.Add(Status.Confused, 8); }
                        else Dm.Msg(mon.TheCap + " is unimpressed by its reflection.");
                        Dm.PlayerActed();
                        break;
                    }
                case "deck":
                    DrawCard(it);
                    break;
                case "horn":
                    {
                        if (it.Charges <= 0) { Dm.Msg("You blow the horn, but only a wheeze comes out."); Dm.PlayerActed(); break; }
                        it.Charges--;
                        Synth.Play(Sfx.Thunder);
                        var cells = Magic.ConeCells(p.Pos, target, 6);
                        Anim.Burst(p.Pos, cells, new Color(230, 210, 160), '~', 0.4f);
                        Dm.Msg("{yellow}The horn's blast shakes the very stones!{/}");
                        foreach (var a in Magic.ActorsIn(cells).Where(a => a != p).ToList())
                        {
                            int dmg = Rng.Roll(5, 6);
                            if (Dm.SavingThrow(a, Save.Spell, 0, "")) dmg /= 2;
                            else a.Add(Status.Stunned, 2);
                            Combat.Damage(a, dmg, Elem.Force, p, "a horn blast");
                        }
                        foreach (var c in cells) if (M.T[c.X, c.Y] is Tile.Statue or Tile.Pillar or Tile.DoorClosed or Tile.DoorLocked) M.T[c.X, c.Y] = Tile.Rubble;
                        Dm.Noise(p.Pos, 40, 100);
                        Dm.PlayerActed();
                        break;
                    }
                case "tricks":
                    {
                        if (it.Charges <= 0) { Dm.Msg("The bag is empty."); break; }
                        it.Charges--;
                        var id = Rng.Pick(new[] { "rat", "jackal", "wolf", "familiar_cat", "bat", "familiar_raven" });
                        var m = Magic.SummonAt(id, p.Pos, true, 60);
                        if (m != null) Dm.Msg("{magic}You pull " + m.A + " out of the bag! It seems friendly.{/}");
                        Dm.PlayerActed();
                        break;
                    }
                case "holding":
                    Dm.Msg("You peer into the bag. It is much bigger on the inside. Your pack feels lighter.");
                    break;
                case "crystal":
                    if (it.Charges <= 0) { Dm.Msg("The crystal ball is cloudy and dark."); break; }
                    it.Charges--;
                    M.RevealMap(true);
                    p.Add(Status.Telepathy, 20);
                    Dm.Msg("{cyan}You gaze into the crystal ball. The whole level swims into view.{/}");
                    Dm.PlayerActed(Dm.ActCost * 3);
                    break;
                case "necklace":
                    if (it.Charges <= 0) { Dm.Msg("The necklace has no beads left."); break; }
                    it.Charges--;
                    Dm.Msg("{orange}You pluck a bead from the necklace and hurl it!{/}");
                    Magic.Cast(p, SpellId.Fireball, target, 6);
                    if (it.Charges <= 0) { Actions.RemoveFromPack(it); Dm.Msg("That was the last bead."); }
                    Dm.PlayerActed();
                    break;
                case "figurine":
                    {
                        if (it.Charges <= 0) { Dm.Msg("The figurine is inert."); break; }
                        if (M.Monsters.Any(m => m.Def.Id == "onyx_dog" && m.Ally)) { Dm.Msg("Your onyx dog is already at your side."); break; }
                        it.Charges--;
                        var m = Magic.SummonAt("onyx_dog", p.Pos, true, 150);
                        if (m != null) Dm.Msg("{magic}You speak the word. The figurine grows into a great hound of black stone!{/}");
                        Dm.PlayerActed();
                        break;
                    }
                case "oil":
                    {
                        var lan = p.Worn(Slot.Light) ?? p.Pack.FirstOrDefault(i => i.Def.Id == "lantern");
                        if (lan != null && lan.Def.Id == "lantern")
                        {
                            lan.Fuel = Math.Min(3000, lan.Fuel + 1500);
                            if (it.Count > 1) it.Count--; else Actions.RemoveFromPack(it);
                            Dm.Msg("You refill your lantern. (" + lan.Fuel + ")");
                            Dm.PlayerActed();
                        }
                        else Dm.Msg("You have no lantern to fill. {dim}(Throw it with t to set it alight){/}");
                        break;
                    }
                case "holy_water":
                    Dm.Msg("{dim}Throw it at an undead creature with t.{/}");
                    break;
                default:
                    if (it.Def.Kind == IK.Light) { Actions.Wear(it); break; }
                    if (it.Def.Kind == IK.Ring && it.Def.Effect == "r_wishes" && Actions.IsEquipped(it))
                    {
                        if (it.Charges <= 0 && !it.ChargesKnown) { it.Charges = 3; it.ChargesKnown = true; }
                        if (it.Charges <= 0) { Dm.Msg("The ring is cold and silent."); break; }
                        it.Charges--;
                        Dm.Ident.Learn(it.Def.Id);
                        Dm.Msg("{gold}You rub the ring. It thrums: \"Speak thy wish.\" (" + it.Charges + " left){/}");
                        Prompts.Wish();
                        break;
                    }
                    Dm.Msg("You can't think of a way to use that.");
                    break;
            }
        }

        public static void Fountain()
        {
            var p = P;
            var pos = p.Pos;
            Synth.Play(Sfx.Drink);
            int r = Rng.D(30);
            if (r <= 8) { p.Nutrition += 40; p.Hp = Math.Min(p.MaxHp, p.Hp + Rng.D(6)); Dm.Msg("{cyan}The cool draught refreshes you.{/}"); }
            else if (r <= 10) { Dm.Msg("{bad}The water is foul! You gag and vomit.{/}"); p.Nutrition -= 100; if (!p.Wearing("a_poison")) p.Add(Status.Poisoned, 5); }
            else if (r == 11)
            {
                Dm.Msg("{bad}An endless stream of snakes pours forth!{/}", true);
                for (int i = 0; i < Rng.Range(1, 4); i++) { var m = Spawn.PlaceMonster(M, MonsterDb.Get("snake"), pos, false); if (m != null) m.Aware = 30; }
            }
            else if (r == 12) { Dm.Msg("{gold}You spot a gem glittering in the fountain!{/}"); M.Drop(Spawn.RandomItem(M.Depth + 3, IK.Gem), pos); }
            else if (r == 13)
            {
                if (Rng.Chance(10 + M.Depth))
                {
                    Dm.Msg("{gold}A grateful water demon rises from the fountain! \"I am in your debt, mortal. Name your wish!\"{/}", true);
                    Prompts.Wish();
                }
                else
                {
                    Dm.Msg("{bad}A water demon bursts from the fountain, furious at being disturbed!{/}", true);
                    var def = Spawn.PickMonster(M.Depth + 2, d => d.Freq > 0 && !d.Has(MF.Immobile));
                    if (def != null) { var m = Spawn.PlaceMonster(M, def, pos, false); if (m != null) m.Aware = 30; }
                }
            }
            else if (r == 14) { int g = Rng.Range(10, 60); Dm.Msg("{gold}You find " + g + " coins at the bottom of the fountain.{/}"); p.Gold += g; }
            else if (r == 15) { p.Add(Status.SeeInvis, 300); Dm.Msg("{magic}Your vision becomes crystal clear.{/}"); }
            else if (r == 16) { var un = p.Pack.Where(i => !i.Known || !i.TypeKnown).ToList(); if (un.Count > 0) { Dm.Msg("{magic}You feel self-knowledgeable...{/}"); Actions.IdentifyItem(Rng.Pick(un)); } else Dm.Msg("The water tastes of nothing at all."); }
            else if (r == 17) { var eq = p.Eq.Values.ToList(); if (eq.Count > 0) { Rng.Pick(eq).Cursed = true; Dm.Msg("{bad}The water's surface flashes black.{/}"); } }
            else if (r == 18) { if (p.Base[2] < 18) { p.Base[2]++; Dm.Msg("{good}You feel wiser.{/}"); } else Dm.Msg("You feel serene."); }
            else if (r == 19)
            {
                var stealable = p.Pack.Where(i => !Actions.IsEquipped(i) && i.Def.Kind != IK.Heart).ToList();
                if (stealable.Count > 0)
                {
                    var s = Rng.Pick(stealable);
                    Actions.RemoveFromPack(s);
                    Dm.Msg("{bad}A water nymph rises, snatches " + s.Name() + ", and dives away!{/}", true);
                }
            }
            else Dm.Msg("The water is tepid and tastes of old copper.");
            if (Rng.OneIn(4)) { M.T[pos.X, pos.Y] = Tile.DryFountain; Dm.Msg("{dim}The fountain dries up!{/}"); }
            Dm.PlayerActed();
        }

        public static void Sit()
        {
            var p = P;
            var pos = p.Pos;
            var t = M.T[pos.X, pos.Y];
            if (t == Tile.Throne) { Throne(); return; }
            if (t == Tile.OracleDie || M.Neighbors(pos).Any(q => M.T[q.X, q.Y] == Tile.OracleDie)) { Oracle(); return; }
            Dm.Msg(t == Tile.Altar ? "You sit on the altar. That seems disrespectful." : "Having fun sitting on the floor?");
        }

        static void Throne()
        {
            var p = P;
            var pos = p.Pos;
            Dm.Msg("You sit on the throne.");
            if (!Rng.Chance(60)) { Dm.Msg("You feel somehow out of place."); Dm.PlayerActed(); return; }
            int r = Rng.D(13);
            switch (r)
            {
                case 1: { int s = Rng.Int(6); p.Drained[s]++; Dm.Msg("{bad}You feel a sudden loss of " + Rules.StatLong[s].ToLower() + ".{/}"); break; }
                case 2: { int s = Rng.Int(6); if (p.Base[s] < 18) p.Base[s]++; Dm.Msg("{good}You feel your " + Rules.StatLong[s].ToLower() + " grow!{/}"); break; }
                case 3: Dm.Msg("{bad}A charge of electricity shoots through your body!{/}", true); Synth.Play(Sfx.Thunder); Combat.Damage(p, Rng.Roll(4, 6), Elem.Elec, null, "an electric throne"); break;
                case 4: p.Hp = p.MaxHp; foreach (var s in new[] { Status.Poisoned, Status.Diseased, Status.Confused, Status.Blind }) p.Remove(s); Dm.Msg("{good}You feel much, much better!{/}"); break;
                case 5: if (p.Gold > 0) { int g = p.Gold / 2; p.Gold -= g; Dm.Msg("{bad}Your purse feels lighter. (" + g + " gold vanishes){/}"); } else Dm.Msg("You hear the clink of phantom coins."); break;
                case 6: p.Set(Status.Fortune, 400, 1); Dm.Msg("{gold}You feel your luck changing.{/}"); break;
                case 7:
                case 8:
                    {
                        Dm.Msg("{bad}A voice echoes: \"Thy audience hath been summoned, Sire!\"{/}", true);
                        var def = Spawn.PickMonster(M.Depth, d => d.Freq > 0 && d.Has(MF.Hands) && !d.Has(MF.Undead));
                        if (def != null) Spawn.PlaceGroup(M, def, pos, false, Rng.Range(2, 4));
                        foreach (var m in M.Monsters.Where(m => Path.Cheb(m.Pos, pos) < 5 && m.Hostile)) { m.Aware = 30; m.LastSeen = pos; }
                        break;
                    }
                case 9:
                    if (Rng.Chance(30)) { Dm.Msg("{gold}A voice echoes: \"Thy wish is granted, Sire!\"{/}", true); Prompts.Wish(); }
                    else Dm.Msg("A voice echoes: \"By thy imperious order, Sire...\" Nothing happens.");
                    break;
                case 10: M.RevealMap(false); Dm.Msg("{ink}Your vision clarifies; you see the layout of the kingdom.{/}"); break;
                case 11: foreach (var i in p.Pack) { i.Known = true; if (i.Def.Unknown) Dm.Ident.Learn(i.Def.Id); } Dm.Msg("{magic}You feel self-knowledgeable... your possessions are revealed.{/}"); break;
                case 12: p.Add(Status.Confused, 10); Dm.Msg("{bad}Your mind turns into a pretzel!{/}"); break;
                default: Magic.TeleportActor(p, null); break;
            }
            if (Rng.OneIn(3) && M.T[pos.X, pos.Y] == Tile.Throne) { M.T[pos.X, pos.Y] = Tile.Floor; Dm.Msg("{dim}The throne vanishes in a puff of logic.{/}"); }
            Dm.PlayerActed();
        }

        static void Oracle()
        {
            var p = P;
            Point die = M.T[p.Pos.X, p.Pos.Y] == Tile.OracleDie ? p.Pos : M.Neighbors(p.Pos).First(q => M.T[q.X, q.Y] == Tile.OracleDie);
            if (M.FeatureUses.TryGetValue(die, out var used) && used > 0) { Dm.Msg("The great die rests in its cradle, numbers dark. It has rolled for you already."); return; }
            M.FeatureUses[die] = 1;
            int r = Dm.PlayerD20(out int alt);
            Synth.Play(Sfx.Dice);
            Dm.AddDie(20, r, 0, 0, "the Oracle Die", r >= 10, r == 20, r == 1, alt);
            Dm.Msg("{white}You heave the great marble die. It tumbles across the floor, clattering like thunder... and comes to rest on {crit}" + r + "{/}.{/}", true);
            Dm.Achievements.Add("Rolled the Oracle Die (" + r + ")");
            switch (r)
            {
                case 1:
                    Dm.Msg("{fumble}CRITICAL FUMBLE. The die laughs at you.{/}", true);
                    foreach (var it in p.Eq.Values) if (Rng.Chance(50)) it.Cursed = true;
                    var l = p.Worn(Slot.Light);
                    if (l != null && l.Def.Fuel < 99999) { l.Fuel = Math.Min(l.Fuel, 5); Dm.Msg("{bad}Your light gutters.{/}"); }
                    Dm.WanderingMonster(true);
                    break;
                case 2: if (!Dm.SavingThrow(p, Save.Death, 0, "level drain")) Dm.LoseLevel(); break;
                case 3:
                    {
                        var perm = new[] { 0, 1, 2, 3, 4, 5 }.ToList();
                        Rng.Shuffle(perm);
                        p.ErrataMap = perm.ToArray();
                        p.Add(Status.Errata, 300);
                        Dm.Msg("{red}ERRATA! Your abilities are reshuffled for a while.{/}");
                        break;
                    }
                case 4: Dm.Msg("{bad}Two wandering monsters answer the roll.{/}"); Dm.WanderingMonster(true); Dm.WanderingMonster(true); break;
                case 5: p.Add(Status.Confused, 15); Dm.Msg("{bad}The numbers spin in your head.{/}"); break;
                case 6: p.Gold /= 2; Dm.Msg("{bad}The die demands tribute. Half your gold is gone.{/}"); break;
                case 7: p.Nutrition = Math.Min(p.Nutrition, 200); Dm.Msg("{bad}You feel suddenly ravenous.{/}"); break;
                case 8: Dm.Msg("The die shrugs. Nothing happens. Somehow that feels personal."); break;
                case 9: p.Hp = p.MaxHp; Dm.Msg("{good}A warm glow restores you.{/}"); break;
                case 10: Actions.GiveOrDrop(Item.Make("waybread", 2)); Dm.Msg("{good}A parcel of elven waybread appears.{/}"); break;
                case 11: { var un = p.Pack.Where(i => !i.Known || !i.TypeKnown).ToList(); if (un.Count > 0) Actions.IdentifyItem(Rng.Pick(un)); else Dm.Msg("You feel well-informed."); break; }
                case 12: M.RevealMap(true); Dm.Msg("{ink}The level inks itself across your memory.{/}"); break;
                case 13: case 14: { int s = Rng.Int(6); if (p.Base[s] < 18) { p.Base[s]++; Dm.Msg("{good}Your " + Rules.StatLong[s].ToLower() + " increases!{/}"); } else { p.MaxHp += 3; p.Hp += 3; Dm.Msg("{good}You feel tougher.{/}"); } break; }
                case 15: p.Set(Status.Fortune, 800, 1); p.Add(Status.Blessed, 300); Dm.Msg("{gold}Fortune smiles on you.{/}"); break;
                case 16: foreach (var i in p.Pack) { i.Known = true; i.ChargesKnown = true; if (i.Def.Unknown) Dm.Ident.Learn(i.Def.Id); } Dm.Msg("{magic}All your possessions are revealed to you.{/}"); break;
                case 17: p.Xp = Math.Max(p.Xp, Rules.XpFor(p.Class, p.Level + 1)); Dm.GainXp(1); break;
                case 18: { var it = Spawn.RandomItem(M.Depth + 6, Rng.Pick(new IK?[] { IK.Ring, IK.Wand, IK.Amulet, IK.Armor })); it.Known = true; Dm.Msg("{gold}Something falls from above!{/}"); Actions.GiveOrDrop(it); break; }
                case 19: { var w = p.Weapon; if (w != null) { w.Ench += 2; w.Cursed = false; w.Known = true; Dm.Msg("{gold}Your " + w.Def.Name + " blazes with blue light! (+2){/}"); } else { p.MaxHp += 6; p.Hp += 6; Dm.Msg("{gold}Your body hardens. (+6 max HP){/}"); } break; }
                case 20:
                    Dm.Msg("{crit}NATURAL TWENTY! The die blazes with golden light. A voice: \"Ask.\"{/}", true);
                    Synth.Play(Sfx.Holy);
                    Prompts.Wish();
                    break;
            }
            Dm.PlayerActed();
        }

        static readonly string[] Cards = { "Sun", "Moon", "Star", "Comet", "Throne", "Key", "Knight", "Gem", "Void", "Flames", "Skull", "Talons", "Ruin", "Euryale", "Rogue", "Jester", "Fates", "Donjon", "Balance", "Fool", "Idiot", "Vizier" };

        static void DrawCard(Item deck)
        {
            var p = P;
            string card = Rng.Pick(Cards);
            Synth.Play(Sfx.Read);
            Dm.Ident.Learn(deck.Def.Id);
            Dm.Msg("{magic}You draw a card from the deck: the {white}" + card.ToUpper() + "{/}!{/}", true);
            switch (card)
            {
                case "Sun": Dm.Msg("{gold}Radiance! You gain a level and a wondrous item.{/}"); p.Xp = Math.Max(p.Xp, Rules.XpFor(p.Class, p.Level + 1)); Dm.GainXp(1); Actions.GiveOrDrop(Spawn.RandomItem(M.Depth + 5, IK.Ring)); break;
                case "Moon": Dm.Msg("{gold}The Moon grants a wish.{/}"); Prompts.Wish(); break;
                case "Star": { int s = Rng.Int(6); p.Base[s] = Math.Min(20, p.Base[s] + 2); Dm.Msg("{gold}Your " + Rules.StatLong[s].ToLower() + " rises by 2!{/}"); break; }
                case "Comet": Dm.Msg("{gold}A comet of experience streaks through you!{/}"); Dm.GainXp(Math.Max(10, Rules.XpFor(p.Class, p.Level + 1) - p.Xp)); break;
                case "Throne": p.Gold += 1000; p.Base[5] = Math.Min(18, p.Base[5] + 2); Dm.Msg("{gold}You are granted a small keep's worth of gold and a regal bearing. (+1000 gp, +2 Cha){/}"); break;
                case "Key":
                    {
                        var arts = ItemDb.All.Values.Where(d => d.Artifact && d.Kind == IK.Weapon && d.Power > 0 && !Spawn.ArtifactsMade.Contains(d.Id)).ToList();
                        if (arts.Count > 0) { var a = Rng.Pick(arts); Spawn.ArtifactsMade.Add(a.Id); var it = new Item(a) { Known = true }; Dm.Msg("{gold}A legendary weapon appears in your hands: " + a.Name + "!{/}"); Actions.GiveOrDrop(it); }
                        else Dm.Msg("A key appears, then dissolves. All the locks are already open.");
                        break;
                    }
                case "Knight": { var k = Magic.SummonAt("squire", p.Pos, true, 0); if (k != null) Dm.Msg("{gold}A knight in battered armor appears and kneels: \"I am yours to command.\"{/}"); break; }
                case "Gem": for (int i = 0; i < Rng.Range(3, 6); i++) Actions.GiveOrDrop(Spawn.RandomItem(M.Depth + 5, IK.Gem)); Dm.Msg("{gold}Gems rain into your hands!{/}"); break;
                case "Void": Dm.Msg("{bad}THE VOID. Your soul is torn away... you stand empty and still.{/}", true); p.Add(Status.Paralyzed, 25); p.Memorized.Clear(); break;
                case "Flames":
                    {
                        Dm.Msg("{bad}FLAMES. A fiend becomes your eternal enemy, and it is here.{/}", true);
                        var def = Spawn.PickMonster(Math.Min(15, M.Depth + 4), d => d.Freq > 0 && d.Has(MF.Evil) && !d.Has(MF.Immobile));
                        if (def != null) { var m = Spawn.PlaceMonster(M, def, p.Pos, false); if (m != null) m.Aware = 99; }
                        break;
                    }
                case "Skull":
                    {
                        Dm.Msg("{bad}THE SKULL. Death itself steps out of the card to claim you.{/}", true);
                        var m = Spawn.PlaceMonster(M, MonsterDb.Get(M.Depth >= 8 ? "spectre" : "wight"), p.Pos, false);
                        if (m != null) { m.Aware = 99; m.Proper = "Death"; }
                        break;
                    }
                case "Talons":
                    {
                        var magic = p.Pack.Where(i => i.IsMagic && i.Def.Kind != IK.Heart && i != deck).ToList();
                        Rng.Shuffle(magic);
                        foreach (var it in magic.Take(3)) { Actions.RemoveFromPack(it); Dm.Msg("{bad}" + it.Name() + " vanishes!{/}"); }
                        if (magic.Count == 0) Dm.Msg("Talons rake your pack, but find nothing magical.");
                        break;
                    }
                case "Ruin": Dm.Msg("{bad}RUIN. All your gold turns to dust.{/}", true); p.Gold = 0; break;
                case "Euryale": p.Drained[4]++; p.Drained[2]++; Dm.Msg("{bad}Medusa's visage curses you. You feel frailer and more foolish.{/}"); break;
                case "Rogue":
                    {
                        var ally = M.Monsters.FirstOrDefault(m => m.Ally);
                        if (ally != null) { ally.Ally = false; ally.Aware = 50; Dm.Msg("{bad}" + ally.TheCap + " turns against you!{/}", true); }
                        else { var m = Spawn.PlaceMonster(M, MonsterDb.Get("doppelganger"), p.Pos, false); if (m != null) m.Aware = 99; Dm.Msg("{bad}A figure with your face steps out of the shadows.{/}", true); }
                        break;
                    }
                case "Jester": Dm.GainXp(Math.Max(10, (Rules.XpFor(p.Class, p.Level + 1) - p.Xp) / 2)); Dm.Msg("{yellow}The Jester laughs and tosses you some experience.{/}"); break;
                case "Fates": Dm.Msg("{gold}The Fates weave you a second chance.{/}"); Actions.GiveOrDrop(new Item(ItemDb.Get("a_second_roll")) { Known = true }); Dm.Ident.Learn("a_second_roll"); break;
                case "Donjon":
                    Dm.Msg("{bad}DONJON. You are cast down into a deeper prison.{/}", true);
                    Dm.EnterLevel(Math.Min(Rules.MaxDepth - 1, M.Depth + 2), Dm.Arrival.Teleport);
                    break;
                case "Balance":
                    p.Align = (Align)(8 - (int)p.Align);
                    Dm.Msg("{magic}Your mind turns inside out. Your alignment is now " + Rules.AlignNames[(int)p.Align] + ".{/}");
                    break;
                case "Fool": p.Xp = Math.Max(Rules.XpFor(p.Class, p.Level), p.Xp - p.Xp / 10); Dm.Msg("{bad}You feel foolish. Some experience slips away.{/}"); break;
                case "Idiot": p.Drained[1] += Rng.D(4); Dm.Msg("{bad}Your thoughts become sluggish. (Intelligence drained){/}"); break;
                case "Vizier": M.RevealMap(true); foreach (var i in p.Pack) { i.Known = true; if (i.Def.Unknown) Dm.Ident.Learn(i.Def.Id); } Dm.Msg("{gold}The Vizier whispers the answers to everything nearby.{/}"); break;
            }
            if (Rng.OneIn(4) || card is "Void" or "Donjon")
            {
                if (P.Pack.Contains(deck)) Actions.RemoveFromPack(deck);
                Dm.Msg("{magic}The deck vanishes in a puff of smoke.{/}");
            }
            if (Dm.Map != null && !Dm.GameOver) Dm.PlayerActed();
        }

        public static void GrantWish(int choice)
        {
            var p = P;
            switch (choice)
            {
                case 0: p.Gold += 5000; Dm.Msg("{gold}Coins cascade around you. (+5000 gold){/}"); Synth.Play(Sfx.Gold); break;
                case 1:
                    for (int i = 0; i < 2; i++) { p.Xp = Math.Max(p.Xp, Rules.XpFor(p.Class, p.Level + 1)); Dm.GainXp(1); }
                    break;
                case 2:
                    {
                        var arts = ItemDb.All.Values.Where(d => d.Artifact && d.Kind == IK.Weapon && d.Power > 0 && !Spawn.ArtifactsMade.Contains(d.Id)).ToList();
                        if (arts.Count == 0) { Dm.Msg("The wish finds no weapon worthy of you."); break; }
                        var pick = arts.OrderByDescending(a => a.Value).Take(4).OrderBy(_ => Rng.Int(100)).First();
                        if (p.Class == Cls.Paladin && arts.Any(a => a.Id == "holy_avenger")) pick = ItemDb.Get("holy_avenger");
                        if (p.Class == Cls.Cleric && arts.Any(a => a.Id == "disruption")) pick = ItemDb.Get("disruption");
                        if (p.Class == Cls.Ranger && arts.Any(a => a.Id == "oathbow")) pick = ItemDb.Get("oathbow");
                        Spawn.ArtifactsMade.Add(pick.Id);
                        var it = new Item(pick) { Known = true };
                        Dm.Msg("{gold}" + pick.Name + " materializes in a blaze of light!{/}");
                        Actions.GiveOrDrop(it);
                        break;
                    }
                case 3:
                    {
                        var body = p.Worn(Slot.Body);
                        if (body != null) { body.Ench += 3; body.Cursed = false; body.Known = true; body.Erosion = 0; Dm.Msg("{gold}Your " + body.Def.Name + " glows brightly! (+3){/}"); }
                        else { var c = new Item(ItemDb.Get("cloak_protection")) { Ench = 3, Known = true }; Actions.GiveOrDrop(c); Dm.Msg("{gold}A shimmering cloak settles into your hands.{/}"); }
                        break;
                    }
                case 4:
                    p.MaxHp += 10; p.Hp = p.MaxHp; p.Base[4] = Math.Min(19, p.Base[4] + 1);
                    for (int i = 0; i < 6; i++) p.Drained[i] = 0;
                    Dm.Msg("{gold}Vigor floods you. (+10 max HP, +1 Constitution, all drains restored){/}");
                    break;
                case 5:
                    foreach (var i in p.Pack) { i.Known = true; i.ChargesKnown = true; if (i.Def.Unknown) Dm.Ident.Learn(i.Def.Id); }
                    foreach (var d in ItemDb.All.Values.Where(d => d.Unknown && (d.Kind == IK.Potion || d.Kind == IK.Scroll)).OrderBy(_ => Rng.Int(100)).Take(6)) Dm.Ident.Learn(d.Id);
                    M.RevealMap(true);
                    Dm.Msg("{gold}Knowledge pours into you: your possessions, many potions and scrolls, and this whole level.{/}");
                    break;
                case 6:
                    p.Set(Status.Fortune, 3000, 1);
                    Dm.Msg("{gold}For a long while, every d20 you roll will be rolled twice, and the better kept.{/}");
                    break;
                case 7:
                    if (Dm.HeartTaken) { Dm.Msg("{gold}\"You already have it,\" the voice says, puzzled.{/}"); break; }
                    Dm.Msg("{crit}A dry voice from very far below: \"Nice try.\" Something is sent to discuss the matter with you.{/}", true);
                    var def = Spawn.PickMonster(Math.Min(15, M.Depth + 3), d => d.Freq > 0 && !d.Has(MF.Immobile));
                    if (def != null) { var m = Spawn.PlaceMonster(M, def, p.Pos, false); if (m != null) m.Aware = 99; }
                    break;
            }
        }

        public static readonly string[] WishNames =
        {
            "Wealth — five thousand gold pieces",
            "Power — gain two experience levels",
            "Arms — a legendary weapon",
            "Protection — +3 to your armor",
            "Vigor — +10 max HP, +1 Constitution, restore drains",
            "Knowledge — identify, enlighten, and map",
            "Luck — a long run of good dice",
            "The Heart — skip the dungeon entirely",
        };
    }
}
