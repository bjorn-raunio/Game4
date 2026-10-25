using System;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class Traps
    {
        static Player P => Dm.Player;
        static Map M => Dm.Map;

        public static void PlayerTrigger(Trap t)
        {
            var p = P;
            bool wasFound = t.Found;
            t.Found = true;
            M.Remember(t.Pos.X, t.Pos.Y);
            if (p.Has(Status.Levitate) || p.Wearing("levitate"))
            {
                if (t.T is TrapT.Pit or TrapT.SpikedPit or TrapT.Trapdoor or TrapT.BearTrap) { Dm.Msg("You float over " + Item.Article(t.Name) + "."); return; }
            }
            if (!wasFound)
            {
                int dex = p.Stat(Stat.Dex);
                int avoid = p.Class == Cls.Thief ? 15 + p.Level * 2 : 5;
                if (dex >= 16) avoid += 5;
                if (Rng.Chance(avoid))
                {
                    Dm.Msg("{good}You notice " + Item.Article(t.Name) + " just in time and step aside!{/}");
                    return;
                }
            }
            Synth.Play(Sfx.Trap);
            Dm.Interrupted = true;
            switch (t.T)
            {
                case TrapT.Pit:
                case TrapT.SpikedPit:
                    Dm.Msg("{bad}You fall into a " + t.Name + "!{/}", true);
                    Combat.Damage(p, Rng.Roll(1, 6) + (t.T == TrapT.SpikedPit ? Rng.Roll(1, 6) : 0), Elem.Phys, null, "falling into a pit");
                    if (t.T == TrapT.SpikedPit && Rng.Chance(30) && !Dm.SavingThrow(p, Save.Death, 0, "poison spikes")) p.Add(Status.Poisoned, 10);
                    if (!p.Carrying("rope")) p.Add(Status.Held, Rng.Range(2, 5));
                    else Dm.Msg("You climb out with your rope.");
                    break;
                case TrapT.Arrow:
                    {
                        int need = 17 - p.AC;
                        int r = Rng.D(20);
                        if (r >= need) { Dm.Msg("{bad}An arrow shoots out at you!{/}"); Combat.Damage(p, Rng.Roll(1, 6), Elem.Phys, null, "an arrow trap"); }
                        else Dm.Msg("An arrow shoots out at you, but misses!");
                        if (Rng.Chance(40)) M.Drop(Item.Make("arrows"), p.Pos);
                        break;
                    }
                case TrapT.Dart:
                    Dm.Msg("{bad}A little dart shoots out at you!{/}");
                    Combat.Damage(p, Rng.Roll(1, 3), Elem.Phys, null, "a poison dart");
                    if (!Dm.SavingThrow(p, Save.Death, 0, "poison")) { Dm.Msg("{bad}The dart was poisoned!{/}"); p.Add(Status.Poisoned, Rng.Range(10, 20)); }
                    break;
                case TrapT.Block:
                    if (p.Worn(Slot.Helm) != null) { Dm.Msg("{bad}A stone block falls on your head! Your helm absorbs some of the blow.{/}"); Combat.Damage(p, Rng.Roll(1, 6), Elem.Phys, null, "a falling block"); }
                    else { Dm.Msg("{bad}A stone block falls on your head!{/}", true); Combat.Damage(p, Rng.Roll(2, 6), Elem.Phys, null, "a falling block"); }
                    break;
                case TrapT.Teleport:
                    Dm.Msg("{magic}A glyph flares beneath your feet!{/}");
                    Magic.TeleportActor(p, null);
                    break;
                case TrapT.Trapdoor:
                    if (M.NoFall) { Dm.Msg("A trap door opens, but it is jammed shut."); break; }
                    Dm.Msg("{bad}A trap door opens up under you!{/}", true);
                    Combat.Damage(p, Rng.Roll(1, 4), Elem.Phys, null, "a fall");
                    Dm.EnterLevel(M.Depth + 1, Dm.Arrival.Fall);
                    break;
                case TrapT.SleepGas:
                    Dm.Msg("{bad}A cloud of gas billows out around you!{/}");
                    if ((p.Race == Race.Elf && Rng.Chance(90)) || p.Wearing("r_free")) Dm.Msg("You feel drowsy, but shake it off.");
                    else if (!Dm.SavingThrow(p, Save.Death, 0, "sleeping gas")) { Dm.Msg("{bad}You fall asleep.{/}"); p.Add(Status.Asleep, Rng.Range(5, 15)); }
                    break;
                case TrapT.Fire:
                    Dm.Msg("{bad}A tower of flame erupts from the floor!{/}", true);
                    Synth.Play(Sfx.Fire);
                    Anim.Burst(p.Pos, Magic.BallCells(p.Pos, 1), new Color(255, 110, 30), '^');
                    Combat.Damage(p, p.Resists(Elem.Fire) ? Rng.Roll(1, 6) : Rng.Roll(2, 6), Elem.Fire, null, "a fire trap");
                    break;
                case TrapT.Alarm:
                    Dm.Msg("{warn}A loud bell rings out! Everything on the level heard that.{/}", true);
                    Dm.Noise(p.Pos, 99, 100);
                    foreach (var m in M.Monsters.Where(m => m.Hostile)) { m.Aware = 30; m.LastSeen = p.Pos; }
                    break;
                case TrapT.Summon:
                    Dm.Msg("{magic}A rune flares and you hear the sound of something arriving.{/}", true);
                    for (int i = 0; i < Rng.Range(1, 3); i++)
                    {
                        var def = Spawn.PickMonster(M.Depth, d => d.Freq > 0 && !d.Has(MF.Immobile) && !d.Has(MF.Mimic));
                        if (def != null) { var m = Spawn.PlaceMonster(M, def, p.Pos, false); if (m != null) m.Aware = 30; }
                    }
                    M.Traps.Remove(t.Pos);
                    break;
                case TrapT.Rune:
                    Dm.Msg("{bad}A glyph of warding explodes!{/}", true);
                    Synth.Play(Sfx.Thunder);
                    Combat.Damage(p, Rng.Roll(3, 6), Elem.Elec, null, "a glyph of warding");
                    M.Traps.Remove(t.Pos);
                    break;
                case TrapT.WebTrap:
                    Dm.Msg("{bad}A web drops over you!{/}");
                    M.T[p.Pos.X, p.Pos.Y] = Tile.Web;
                    if (!p.Wearing("r_free")) p.Add(Status.Webbed, Rng.Range(3, 7));
                    break;
                case TrapT.BearTrap:
                    Dm.Msg("{bad}A bear trap closes on your foot!{/}", true);
                    Combat.Damage(p, Rng.Roll(2, 4), Elem.Phys, null, "a bear trap");
                    p.Add(Status.Held, Rng.Range(4, 8));
                    break;
                case TrapT.Rust:
                    {
                        Dm.Msg("{cyan}A gush of water hits you!{/}");
                        var arm = p.Eq.Values.Where(i => i.Def.Metal && i.Def.Kind is IK.Armor or IK.Helm or IK.Weapon or IK.Shield).ToList();
                        if (arm.Count > 0)
                        {
                            var it = Rng.Pick(arm);
                            if (it.Erosion < 3 && !(it.Ench > 0 && Rng.Chance(50))) { it.Erosion++; Dm.Msg("{warn}Your " + it.Def.Name + " rusts!{/}"); }
                        }
                        break;
                    }
            }
        }

        public static void MonsterTrigger(Monster m, Trap t)
        {
            bool vis = Dm.CanSeeMonster(m);
            switch (t.T)
            {
                case TrapT.Pit:
                case TrapT.SpikedPit:
                case TrapT.BearTrap:
                    if (vis) Dm.Msg(m.TheCap + " is caught in " + Item.Article(t.Name) + "!");
                    m.Add(Status.Webbed, 3);
                    Combat.Damage(m, Rng.Roll(1, 6), Elem.Phys, null, "trap", true);
                    break;
                case TrapT.Teleport:
                    if (vis) Dm.Msg(m.TheCap + " steps on a glyph and vanishes!");
                    Magic.TeleportActor(m, null);
                    break;
                case TrapT.Fire:
                    if (vis) Dm.Msg("Flames erupt around " + m.The + "!");
                    Combat.Damage(m, Rng.Roll(2, 6), Elem.Fire, null, "trap", true);
                    break;
                case TrapT.Arrow:
                case TrapT.Dart:
                    Combat.Damage(m, Rng.Roll(1, 6), Elem.Phys, null, "trap", true);
                    break;
                default:
                    return;
            }
            if (vis) { t.Found = true; M.Remember(t.Pos.X, t.Pos.Y); }
        }

        public static int Search(bool active)
        {
            var p = P;
            int found = 0;
            int bonus = p.Class == Cls.Thief ? p.ThiefSkills[1] / 3 : 0;
            if (p.Race == Race.Dwarf) bonus += 10;
            foreach (var q in M.Neighbors(p.Pos))
            {
                if (M.T[q.X, q.Y] == Tile.DoorSecret)
                {
                    int chance = (active ? 30 : 4) + bonus + (p.Race == Race.Elf ? (active ? 20 : 30) : 0) + (p.Race == Race.HalfElf ? 10 : 0);
                    if (Rng.Chance(chance))
                    {
                        M.T[q.X, q.Y] = Tile.DoorClosed;
                        M.Remember(q.X, q.Y);
                        Dm.Msg("{good}You find a hidden door!{/}", true);
                        found++;
                    }
                }
                var t = M.TrapAt(q);
                if (t != null && !t.Found)
                {
                    int chance = (active ? 25 : 3) + bonus + (p.Race == Race.Dwarf && t.T is TrapT.Pit or TrapT.Block or TrapT.Trapdoor ? 20 : 0);
                    if (Rng.Chance(chance))
                    {
                        t.Found = true;
                        M.Remember(q.X, q.Y);
                        Dm.Msg("{good}You find " + Item.Article(t.Name) + ".{/}", true);
                        found++;
                    }
                }
                var mim = M.MonsterAt(q);
                if (mim != null && mim.Disguised && active && Rng.Chance(25 + bonus))
                {
                    mim.Disguised = false;
                    Dm.Msg("{bad}You find a mimic!{/}", true);
                    found++;
                }
            }
            return found;
        }

        public static bool Disarm(Trap t)
        {
            var p = P;
            int chance = p.Class == Cls.Thief ? p.ThiefSkills[1] : 10 + Math.Max(0, p.Stat(Stat.Dex) - 12) * 3;
            if (p.Carrying("thieves_tools")) chance += 10;
            int roll = Rng.Int(100) + 1;
            Dm.AddDie(100, roll, 0, chance, "disarm trap", roll <= chance);
            if (roll <= chance)
            {
                M.Traps.Remove(t.Pos);
                Dm.Msg("{good}You disarm the " + t.Name + ". {dice}(d100=" + roll + " vs " + chance + "%){/}{/}");
                Dm.GainXp(2 + M.Depth);
                if (t.T == TrapT.Arrow) M.Drop(new Item(ItemDb.Get("arrows"), Rng.Range(4, 10)), t.Pos);
                if (t.T == TrapT.Dart) M.Drop(new Item(ItemDb.Get("dart"), Rng.Range(3, 8)), t.Pos);
                return true;
            }
            Dm.Msg("You fail to disarm the " + t.Name + ". {dice}(d100=" + roll + " vs " + chance + "%){/}");
            if (roll > 90)
            {
                Dm.Msg("{bad}You set it off!{/}");
                var old = p.Pos;
                p.Pos = t.Pos;
                PlayerTrigger(t);
                if (!Dm.GameOver && M == Dm.Map && p.Pos == t.Pos && t.T is not (TrapT.Teleport or TrapT.Trapdoor)) p.Pos = old;
            }
            return false;
        }
    }
}
