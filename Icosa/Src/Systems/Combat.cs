using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class Combat
    {
        static Player P => Dm.Player;

        public static int AttacksThisRound(Player p)
        {
            if (!p.IsWarrior) return 1;
            int lvl = p.Level;
            if (p.Class != Cls.Fighter) lvl -= 2;
            if (lvl >= 13) return 2;
            if (lvl >= 7)
            {
                p.AttackFrac ^= 1;
                return p.AttackFrac == 1 ? 2 : 1;
            }
            return 1;
        }

        public static bool IsWeakling(Monster m) => m.Def.HalfHD || m.Def.HD == 0 || (m.Def.HD == 1 && m.Def.HpPlus < 0);

        public static void PlayerMelee(Monster target)
        {
            var p = P;
            if (p.Has(Status.Sanctuary)) { p.Remove(Status.Sanctuary); Dm.Msg("{dim}Your sanctuary is broken.{/}"); }
            if (p.Has(Status.Invisible) && !p.Wearing("r_invis")) { p.Remove(Status.Invisible); Dm.Msg("{dim}You become visible as you attack.{/}"); }
            int attacks = AttacksThisRound(p);
            if (p.Class == Cls.Fighter && p.Level >= 2 && IsWeakling(target)) attacks = Math.Max(attacks, p.Level);
            Monster cur = target;
            for (int i = 0; i < attacks && !p.Dead; i++)
            {
                if (cur == null || cur.Dead)
                {
                    if (p.Class != Cls.Fighter) break;
                    cur = Dm.Map.Monsters.FirstOrDefault(m => !m.Dead && m.Hostile && Path.Cheb(m.Pos, p.Pos) == 1 && IsWeakling(m) && Dm.CanSeeMonster(m));
                    if (cur == null) break;
                    Dm.Msg("{dim}You sweep onward to " + cur.The + ".{/}");
                }
                SwingAt(cur, i > 0);
            }
            Dm.Noise(p.Pos, 6, 40);
        }

        static void SwingAt(Monster t, bool followup)
        {
            var p = P;
            var w = p.Weapon;
            bool launcherInMelee = w != null && w.Def.Has(WF.Launcher);
            if (t.Disguised)
            {
                t.Disguised = false;
                Dm.Msg("{bad}The chest was a mimic!{/}", true);
            }
            if (t.Def.Has(MF.Displace) && Rng.Chance(30))
            {
                Dm.Msg("You strike at the displaced image of " + t.The + "!");
                Synth.Play(Sfx.Miss);
                WakeUp(t);
                return;
            }
            bool backstab = p.Class == Cls.Thief && !followup && (t.Sleeping || t.Helpless || t.Aware <= 0 || p.Has(Status.Invisible) || p.Has(Status.Hidden)) && !t.Def.Has(MF.Undead) && !(w != null && w.Def.Has(WF.TwoHand));
            int mod = p.StrBonus.hit;
            if (w != null && !launcherInMelee) mod += w.Ench - w.Erosion / 2;
            if (w != null && w.Def.Effect == "holy" && p.Class == Cls.Paladin) mod += 3;
            if (!p.Proficient(w) || launcherInMelee) mod -= 4;
            if (p.Race == Race.Elf && w != null && w.Def.Id.Contains("sword")) mod += 1;
            if ((p.Race == Race.Dwarf || p.Race == Race.HalfOrc && false) && t.Def.Has(MF.Orc)) mod += 1;
            if ((p.Race == Race.Dwarf || p.Race == Race.Gnome) && (t.Def.Id is "goblin" or "hobgoblin" or "kobold")) mod += 1;
            if (p.Has(Status.Blessed)) mod += 1;
            if (p.Has(Status.Prayer)) mod += 1;
            if (p.Has(Status.Heroism)) mod += 2;
            if (p.Has(Status.Feared)) mod -= 2;
            if (p.Has(Status.Blind)) mod -= 4;
            if (p.Nutrition <= 100) mod -= 1;
            if (t.Sleeping || t.Helpless) mod += 4;
            if (backstab) mod += 4;
            if (Dm.Seen(t) == SeenKind.No) mod -= 4;
            int thac0 = p.Thac0;
            int need = thac0 - t.AC;
            int roll = Dm.PlayerD20(out int alt);
            int critOn = p.Wearing("r_keen") || (w != null && w.Def.Effect == "sharp") ? 19 : 20;
            bool fumble = roll == 1;
            bool crit = roll >= critOn && roll + mod >= need;
            bool hit = !fumble && (roll == 20 || roll + mod >= need);
            Dm.AddDie(20, roll, mod, need, "hit " + t.Name, hit, crit, fumble, alt);
            WakeUp(t);
            if (!hit)
            {
                if (fumble)
                {
                    Synth.Play(Sfx.Fumble);
                    int f = Rng.D(6);
                    if (f == 1 && w != null && !w.Cursed && !w.Def.Has(WF.TwoHand))
                    {
                        Dm.Msg("{fumble}Natural 1! Your " + w.Def.Name + " slips from your grasp!{/}", true);
                        p.Eq.Remove(Slot.Weapon);
                        p.Pack.Remove(w);
                        Dm.Map.Drop(w, p.Pos);
                    }
                    else if (f == 2)
                    {
                        Dm.Msg("{fumble}Natural 1! You stumble and lose your footing.{/}", true);
                        p.Energy -= 60;
                    }
                    else Dm.Msg("{fumble}Natural 1!{/} You swing wildly and miss " + t.The + ".");
                }
                else
                {
                    Dm.Msg("You miss " + t.The + ". {dice}(" + roll + Sgn(mod) + " vs " + need + "){/}");
                    Synth.Play(Sfx.Miss);
                }
                return;
            }
            if (t.Def.Has(MF.NeedMagic) && (w == null || (w.Ench <= 0 && !w.Def.Artifact)))
            {
                Dm.Msg("Your " + (w == null ? "fist passes" : w.Def.Name + " passes") + " harmlessly through " + t.The + ". {dim}(Requires a magic weapon){/}");
                return;
            }
            if (t.Def.Has(MF.NeedSilver) && (w == null || (!w.Def.Has(WF.Silver) && w.Ench <= 0 && !w.Def.Artifact)))
            {
                Dm.Msg("Your blow does not harm " + t.The + ". {dim}(Requires silver or magic){/}");
                return;
            }
            Dice dice = w == null || launcherInMelee ? new Dice(1, 2) : (t.Size == Size.Large ? w.Def.DmgL : w.Def.Dmg);
            int dmg = dice.Roll();
            if (crit) dmg += dice.Roll();
            int mul = 1;
            if (backstab) mul = Rules.BackstabMul(p.Level);
            dmg *= mul;
            dmg += p.StrBonus.dmg;
            if (w != null && !launcherInMelee) dmg += w.Ench - w.Erosion;
            if (w != null && w.Def.Effect == "holy" && p.Class == Cls.Paladin) dmg += 3;
            if (p.Class == Cls.Ranger && t.Def.Has(MF.Giant)) dmg += p.Level;
            if (p.Has(Status.Prayer)) dmg += 1;
            if (t.Def.Has(MF.Edged) && (w == null || !w.Def.Has(WF.Blunt))) dmg = Math.Max(1, dmg / 2);
            string extra = "";
            Elem elem = Elem.Phys;
            if (w != null)
            {
                switch (w.Def.Effect)
                {
                    case "flame":
                        if (!t.Resists(Elem.Fire)) { int f = Rng.D(6); if (t.Def.Has(MF.Regen) || t.Def.Has(MF.VulnFire)) f *= 2; dmg += f; extra = " {orange}The flames sear!{/}"; }
                        break;
                    case "frost":
                        if (!t.Resists(Elem.Cold)) { dmg += Rng.D(6); extra = " {cyan}Frost bites!{/}"; }
                        break;
                    case "dragonslayer":
                        if (t.Def.G == 'D') { dmg *= 3; extra = " {gold}The Dragonslayer sings!{/}"; }
                        break;
                    case "sun":
                        if (t.IsUndead) { dmg *= 2; extra = " {holy}Sunlight blazes!{/}"; }
                        break;
                    case "sharp":
                        if (roll >= 19) { dmg *= 3; extra = " {crit}A terrible wound!{/}"; }
                        break;
                    case "disruption":
                        if (t.IsUndead && !t.Def.Has(MF.Boss) && !Dm.SavingThrow(t, Save.Spell, 0, "disruption"))
                        {
                            Dm.Msg("{holy}The mace of disruption blasts " + t.The + " to dust!{/}");
                            dmg = t.Hp + 10;
                        }
                        break;
                    case "vorpal":
                        if (roll == 20)
                        {
                            if (t.Def.Has(MF.Boss) || t.Def.G is 'P' or 'F' or '■') dmg *= 3;
                            else { Dm.Msg("{crit}Snicker-snack! The vorpal blade takes " + t.The + "'s head!{/}"); dmg = t.Hp + 100; }
                        }
                        break;
                    case "ninelives":
                        if (roll == 20 && !t.Def.Has(MF.Boss) && !Dm.SavingThrow(t, Save.Death, 0, "nine lives"))
                        {
                            Dm.Msg("{darkred}The Nine Lives Stealer drinks the life of " + t.The + "!{/}");
                            dmg = t.Hp + 100;
                        }
                        break;
                }
            }
            dmg = Math.Max(1, dmg);
            string verb = crit ? "{crit}CRITICAL! You smite{/}" : backstab ? "{crit}You backstab{/}" : "You hit";
            if (crit) Synth.Play(Sfx.Crit); else Synth.Play(Sfx.Hit);
            Dm.Msg(verb + " " + t.The + (backstab ? " (×" + mul + ")" : "") + " for " + dmg + "." + extra + " {dice}(" + roll + Sgn(mod) + " vs " + need + "){/}");
            Anim.Flash(t.Pos, crit ? Pal.Gold : new Color(200, 40, 30), 0.15f);
            if (t.Def.Has(MF.EatsMetal) && w != null && w.Def.Metal)
            {
                if (w.Ench > 0 && Rng.Chance(50)) { }
                else if (w.Erosion < 3) { w.Erosion++; Dm.Msg("{warn}Your " + w.Def.Name + " corrodes on contact!{/}"); }
            }
            if (t.Def.Has(MF.Splits) && t.Hp > dmg + 1 && w != null && !w.Def.Has(WF.Blunt) && t.Hp >= 4) Split(t);
            Damage(t, dmg, elem, p, "you");
            if (!t.Dead && p.Has(Status.FireShield) && false) { }
        }

        public static void Split(Monster t)
        {
            var spot = Dm.Map.NearestFree(t.Pos, 1);
            if (spot == null || spot == t.Pos) return;
            var half = t.Hp / 2;
            t.Hp -= half;
            var clone = new Monster(t.Def) { Pos = spot.Value, Aware = 20, Hp = half, MaxHp = half };
            clone.Generation = t.Generation + 1;
            Dm.Map.Monsters.Add(clone);
            if (Dm.CanSeeMonster(t)) Dm.Msg("{warn}" + t.TheCap + " divides in two!{/}");
        }

        public static void WakeUp(Monster m)
        {
            if (m.Sleeping || m.Has(Status.Asleep))
            {
                m.Sleeping = false;
                m.Remove(Status.Asleep);
            }
            m.Aware = Math.Max(m.Aware, 30);
            m.LastSeen = P.Pos;
            if (m.Peaceful && !m.Def.Has(MF.Shopkeeper)) m.Peaceful = false;
        }

        static string Sgn(int m) => m == 0 ? "" : m > 0 ? "+" + m : m.ToString();

        public static int Damage(Actor t, int dmg, Elem e, Actor src, string cause, bool silent = false)
        {
            if (dmg <= 0 || t.Dead) return 0;
            if (t is Monster m)
            {
                if (e != Elem.Phys && e != Elem.Holy && e != Elem.Magic && e != Elem.Force && m.Resists(e))
                {
                    if (!silent && Dm.CanSeeMonster(m)) Dm.Msg(m.TheCap + " seems unharmed.");
                    return 0;
                }
                if (e == Elem.Fire && m.Def.Has(MF.VulnFire)) dmg *= 2;
                if (e == Elem.Holy && !m.IsUndead && !m.Def.Has(MF.Evil)) dmg /= 2;
                if (e == Elem.Elec && m.Def.Has(MF.Splits) && m.Hp > dmg + 2) Split(m);
                m.Hp -= dmg;
                m.Sleeping = false;
                m.Remove(Status.Asleep);
                if (src != null && src.IsPlayer) WakeUp(m);
                Bleed(m, dmg);
                if (m.Hp <= 0) Kill(m, src);
                else if (src != null && src.IsPlayer || src is Monster sm && sm.Ally) Morale(m);
                return dmg;
            }
            var p = (Player)t;
            if (e != Elem.Phys && p.Resists(e)) dmg = Math.Max(1, dmg / 2);
            if (e == Elem.Poison && p.Wearing("a_poison")) dmg = 0;
            if (dmg <= 0) return 0;
            p.Hp -= dmg;
            p.RestTurns = 0;
            Dm.Interrupted = true;
            Synth.Play(Sfx.Hurt, Math.Min(1f, 0.4f + dmg / 20f));
            Anim.Flash(p.Pos, new Color(255, 30, 20), 0.25f);
            Dm.Map.Bleed(p.Pos, Pal.Blood, 0.15f);
            if (p.Has(Status.Asleep) && e != Elem.Poison) p.Remove(Status.Asleep);
            if (p.Hp <= 0)
            {
                string killer = cause;
                if (src is Monster km) killer = km.A;
                Dm.Die(killer);
            }
            else if (p.Hp < p.MaxHp / 4 && p.Hp + dmg >= p.MaxHp / 4) Dm.Msg("{bad}*** LOW HIT POINTS ***{/}", true);
            return dmg;
        }

        static void Bleed(Monster m, int dmg)
        {
            Color c = m.IsUndead ? new Color(120, 115, 100) : m.Def.G is 'P' or 'F' or '■' ? new Color(90, 160, 40) : m.Def.G == '&' ? new Color(60, 10, 30) : m.Def.G == '\'' ? new Color(90, 90, 90) : Pal.Blood;
            Dm.Map.Bleed(m.Pos, c, Math.Min(0.4f, 0.05f + dmg * 0.02f));
            if (dmg >= 6 && Rng.Chance(40))
            {
                var q = Rng.Pick(Dm.Map.Neighbors(m.Pos).ToList());
                Dm.Map.Bleed(q, c, 0.1f);
            }
        }

        static void Morale(Monster m)
        {
            if (m.Dead || m.Fleeing || m.MoraleChecked || m.Def.Has(MF.Mindless) || m.IsUndead || m.Ally) return;
            if (m.Hp > m.MaxHp / 2) return;
            m.MoraleChecked = true;
            int roll = Rng.Roll(2, 6);
            if (roll > m.Def.Morale)
            {
                m.Fleeing = true;
                m.FleeTurns = Rng.Range(8, 20);
                if (Dm.CanSeeMonster(m)) Dm.Msg("{warn}" + m.TheCap + " breaks and flees!{/} {dice}(morale " + roll + " > " + m.Def.Morale + "){/}");
            }
        }

        public static void Kill(Monster m, Actor killer)
        {
            if (m.Dead) return;
            m.Dead = true;
            var map = Dm.Map;
            bool vis = Dm.CanSeeMonster(m) || Dm.Map.Vis[m.Pos.X, m.Pos.Y];
            if (killer != null && killer.IsPlayer)
            {
                if (m.Def.Has(MF.Boss)) { }
                else if (m.IsUndead || m.Def.Has(MF.Mindless)) Dm.Msg("{good}You destroy " + m.The + "!{/}");
                else Dm.Msg("{good}You kill " + m.The + "!{/}");
            }
            else if (vis) Dm.Msg(m.TheCap + (m.IsUndead ? " is destroyed!" : " dies!"));
            Synth.Play(Sfx.Kill, 0.7f);
            if (Dm.Engulfer == m)
            {
                Dm.Engulfer = null;
                P.Remove(Status.Engulfed);
                P.Remove(Status.Swallowed);
                Dm.Msg("You are released!");
            }
            if (P.Holder == m) P.Holder = null;
            if (m.Hostile || m.Def.Has(MF.Shopkeeper))
            {
                int xp = m.Def.Xp;
                if (m.Summoned()) xp /= 4;
                if (m.Generation > 0) xp /= (1 + m.Generation);
                if (killer != null && (killer.IsPlayer || killer is Monster am && am.Ally)) Dm.GainXp(Math.Max(1, xp));
                P.Kills++;
                P.KillLog.TryGetValue(m.Def.Name, out var c);
                P.KillLog[m.Def.Name] = c + 1;
            }
            foreach (var it in m.Inv) map.Drop(it, m.Pos);
            m.Inv.Clear();
            var d = m.Def;
            if (d.LeavesItem != null && Rng.Chance(d.LeavesChance) && !m.Summoned())
            {
                Item drop = d.LeavesItem switch
                {
                    "gold" => Spawn.Gold(map.Depth * 2 + m.HdLevel * 3),
                    "random" => Spawn.RandomItem(map.Depth),
                    "scroll" => Spawn.RandomItem(map.Depth, IK.Scroll),
                    "potion" => Spawn.RandomItem(map.Depth, IK.Potion),
                    "gem" => Spawn.RandomItem(map.Depth + 2, IK.Gem),
                    "arrows" => new Item(ItemDb.Get("arrows"), Rng.Range(3, 10)),
                    _ => ItemDb.Get(d.LeavesItem) != null ? Item.Make(d.LeavesItem) : null,
                };
                if (drop != null) map.Drop(drop, m.Pos);
            }
            if (!d.Has(MF.NoCorpse) && !m.Summoned() && (Rng.Chance(45) || d.Has(MF.Revives)) && map.Walkable(m.Pos) && map.T[m.Pos.X, m.Pos.Y] != Tile.Chasm)
            {
                var corpse = new Item(ItemDb.Get("corpse")) { CorpseOf = d };
                map.Drop(corpse, m.Pos);
            }
            if (d.Has(MF.Shopkeeper))
            {
                foreach (var s in map.Shops.Where(s => s.KeeperMon == m))
                    foreach (var it in map.Items.Where(i => s.Room.Cells.Contains(i.Pos))) it.Price = 0;
                Dm.Msg("{dim}The shop is now unattended.{/}");
            }
            if (d.Id == "hollow_king")
            {
                Dm.Msg("{gold}The Hollow King crumples before its throne. Its crown rolls across the floor and comes to rest at your feet.{/}", true);
                Dm.Msg("{gold}\"Nine hundred years,\" it rasps as it fades. \"Keep it, then. See how long it keeps you.\"{/}");
                Dm.Achievements.Add("Destroyed the Hollow King");
                Synth.Play(Sfx.Thunder);
                for (int i = 0; i < 6; i++) map.Drop(Spawn.RandomItem(16), map.NearestFree(m.Pos, 3) ?? m.Pos);
                map.Drop(Spawn.Gold(200), m.Pos);
                for (int x = 0; x < Map.W; x++)
                    for (int y = 0; y < Map.H; y++)
                        if (map.T[x, y] == Tile.BoneVeil) map.T[x, y] = Tile.Rubble;
            }
            var lair = WorldDb.Dungeons.FirstOrDefault(x => x.Boss == d.Id);
            if (lair != null)
            {
                Dm.Msg("{gold}" + lair.Slain + "{/}", true);
                Dm.Achievements.Add("Conquered " + lair.Name);
                Synth.Play(Sfx.Thunder);
            }
            if (d.Id == "beholder" || d.Id == "balor" || d.Id.StartsWith("young_")) Dm.Achievements.Add("Slew " + m.A);
            if (d.Id == "balor")
            {
                Dm.Msg("{orange}The balor explodes in a roar of flame!{/}", true);
                Magic.Explosion(m.Pos, 2, new Dice(6, 6), Elem.Fire, null, "a balor's death throes");
            }
        }

        public static bool Summoned(this Monster m) => m.SummonTurns > 0;

        public static void MonsterMelee(Monster m, Actor t)
        {
            if (t.IsPlayer) MonsterAttacksPlayer(m);
            else if (t is Monster tm) MonsterAttacksMonster(m, tm);
        }

        static void MonsterAttacksMonster(Monster m, Monster t)
        {
            bool vis = Dm.CanSeeMonster(m) || Dm.CanSeeMonster(t);
            foreach (var a in m.Def.Attacks)
            {
                if (t.Dead) break;
                int need = m.Def.Thac0 - t.AC;
                int roll = Rng.D(20);
                if (roll + (m.Has(Status.Blessed) ? 1 : 0) >= need || roll == 20)
                {
                    int dmg = Math.Max(1, a.Dmg.Roll());
                    if (t.Def.Has(MF.NeedMagic) && !m.Def.Has(MF.NeedMagic) && m.Def.Id != "hammer") { if (vis) Dm.Msg(m.TheCap + "'s attack passes through " + t.The + "."); continue; }
                    if (vis) Dm.Msg(m.TheCap + " " + a.Verb + "s " + t.The + ".");
                    if (a.Sp == AtkSp.Paralyze && !t.Def.Has(MF.Undead) && !Dm.SavingThrow(t, Save.Death, 0, "")) t.Add(Status.Paralyzed, Rng.Range(2, 5));
                    if (a.Sp == AtkSp.Sleep && !t.Def.Has(MF.ResSleep) && !Dm.SavingThrow(t, Save.Death, 0, "")) t.Add(Status.Asleep, Rng.Range(3, 7));
                    Damage(t, dmg, a.Sp switch { AtkSp.Fire => Elem.Fire, AtkSp.Cold => Elem.Cold, AtkSp.Shock => Elem.Elec, AtkSp.Acid => Elem.Acid, _ => Elem.Phys }, m, m.A);
                    if (t.Dead) break;
                    if (!t.Ally && !t.Hostile) t.Peaceful = false;
                }
                else if (vis) Dm.Msg(m.TheCap + " misses " + t.The + ".");
            }
        }

        static void MonsterAttacksPlayer(Monster m)
        {
            var p = P;
            if (m.Disguised) { m.Disguised = false; Dm.Msg("{bad}The chest was a mimic!{/}", true); }
            if (p.Has(Status.Sanctuary) && !Dm.SavingThrow(m, Save.Spell, 0, "sanctuary"))
            {
                if (Dm.CanSeeMonster(m)) Dm.Msg(m.TheCap + " hesitates, unable to attack you.");
                return;
            }
            if (p.Has(Status.ProtUndead) && m.IsUndead)
            {
                if (Dm.CanSeeMonster(m)) Dm.Msg(m.TheCap + " recoils from your holy ward.");
                return;
            }
            string name = Dm.CanSeeMonster(m) ? m.TheCap : "It";
            foreach (var a in m.Def.Attacks)
            {
                if (p.Dead || m.Dead) break;
                if (a.Verb == "drop")
                {
                    if (m.Def.Id == "piercer" && m.Turns(Status.Rested) > 0) continue;
                }
                if (p.Has(Status.MirrorImage) && Rng.Chance(100 * p.Pow(Status.MirrorImage) / (p.Pow(Status.MirrorImage) + 1)))
                {
                    int left = p.Pow(Status.MirrorImage) - 1;
                    Dm.Msg(name + " strikes one of your mirror images, which vanishes!" + (left > 0 ? " (" + left + " left)" : ""));
                    if (left <= 0) p.Remove(Status.MirrorImage);
                    else p.StPow[Status.MirrorImage] = left;
                    continue;
                }
                if (p.Wearing("displace") && Rng.Chance(25))
                {
                    Dm.Msg(name + " strikes at your displaced image and misses.");
                    continue;
                }
                int mod = 0;
                if (p.Has(Status.Invisible)) mod -= 4;
                if ((p.Has(Status.ProtEvil)) && m.IsEvil) mod -= 2;
                if ((p.Race == Race.Dwarf || p.Race == Race.Gnome || p.Race == Race.Halfling) && m.Def.Has(MF.Giant) && m.Size == Size.Large) mod -= 4;
                if (m.Has(Status.Blessed)) mod += 1;
                if (m.Has(Status.Weakened)) mod -= 1;
                if (m.Has(Status.Prayer)) mod -= 1;
                if (p.Has(Status.Prayer)) mod -= 1;
                if (m.Feebleminded) mod -= 2;
                int need = m.Def.Thac0 - p.AC;
                int roll = Rng.D(20);
                bool hit = roll == 20 || (roll != 1 && roll + mod >= need);
                if (!hit)
                {
                    Dm.Msg(name + " misses. {dice}(" + roll + Sgn(mod) + " vs " + need + "){/}");
                    continue;
                }
                int dmg = a.Dmg.Roll();
                bool crit = roll == 20 && !(p.Worn(Slot.Helm) != null && Rng.Chance(50));
                if (crit) dmg += a.Dmg.Roll();
                if (m.DmgPenalty > 0) dmg = dmg * (100 - m.DmgPenalty) / 100;
                if (m.Def.Id == "knucklebone_golem")
                {
                    Dm.AddDie(20, dmg, 0, 0, "knucklebone golem slam", false, dmg == 20, dmg == 1);
                }
                string verb = a.Verb switch
                {
                    "hit" => "hits",
                    "bite" => "bites",
                    "claw" => "claws",
                    "touch" => "touches",
                    "sting" => "stings",
                    "gore" => "gores",
                    "slam" => "slams",
                    "squeeze" => "squeezes",
                    "drop" => "drops on",
                    "tentacle" => "lashes",
                    "slash" => "slashes",
                    "spear" => "skewers",
                    "tail" => "lashes",
                    "pincer" => "pinches",
                    "talon" => "rakes",
                    "beak" => "pecks",
                    "peck" => "pecks",
                    "strike" => "strikes",
                    "sword" => "slashes",
                    "whip" => "whips",
                    _ => a.Verb + "s",
                };
                Elem elem = a.Sp switch { AtkSp.Fire => Elem.Fire, AtkSp.Cold => Elem.Cold, AtkSp.Shock => Elem.Elec, AtkSp.Acid => Elem.Acid, _ => Elem.Phys };
                if (dmg > 0)
                {
                    Dm.Msg("{bad}" + name + " " + verb + (verb.EndsWith(" on") ? " you" : "") + (crit ? " — a critical hit!" : "") + "{/} {dice}(" + roll + Sgn(mod) + " vs " + need + ", " + dmg + " dmg){/}");
                    Damage(p, dmg, elem, m, m.A);
                }
                else Dm.Msg("{bad}" + name + " " + verb + " you.{/}");
                if (p.Dead) return;
                if (a.Sp != AtkSp.None && Rng.Chance(a.SpChance)) Special(m, a);
                if (p.Has(Status.FireShield) && !m.Resists(Elem.Fire) && dmg > 0)
                {
                    Dm.Msg("{orange}Your fire shield scorches " + m.The + "!{/}");
                    Damage(m, dmg, Elem.Fire, p, "fire shield");
                }
            }
        }

        static void Special(Monster m, Attack a)
        {
            var p = P;
            switch (a.Sp)
            {
                case AtkSp.Poison:
                    if (p.Wearing("a_poison") || p.Has(Status.Poisoned) && false) break;
                    if (!Dm.SavingThrow(p, Save.Death, 0, "poison"))
                    {
                        Dm.Msg("{bad}You feel very sick!{/}");
                        Damage(p, Rng.Roll(1, 6), Elem.Poison, m, m.A);
                        p.Add(Status.Poisoned, Rng.Range(8, 16));
                    }
                    break;
                case AtkSp.Poison2:
                    if (p.Wearing("a_poison")) break;
                    if (!Dm.SavingThrow(p, Save.Death, 0, "poison"))
                    {
                        Dm.Msg("{bad}Deadly venom burns through your veins!{/}", true);
                        Damage(p, Rng.Roll(3, 6), Elem.Poison, m, m.A);
                        p.Add(Status.Poisoned, Rng.Range(15, 30));
                        p.Add(Status.Weakened, 60, 1);
                    }
                    break;
                case AtkSp.Paralyze:
                    if (p.Wearing("r_free")) { Dm.Msg("You feel momentarily stiff."); break; }
                    if (m.Def.Id == "ghoul" && p.Race == Race.Elf) { Dm.Msg("{good}Your elven blood resists the ghoul's paralysis.{/}"); break; }
                    if (p.Has(Status.Paralyzed)) break;
                    if (!Dm.SavingThrow(p, Save.Death, 0, "paralysis"))
                    {
                        int t = Rng.Range(2, 5);
                        Dm.Msg("{bad}You are paralyzed!{/}", true);
                        p.Add(Status.Paralyzed, t);
                    }
                    break;
                case AtkSp.Drain:
                    if (!Dm.SavingThrow(p, Save.Death, -2, "level drain")) Dm.LoseLevel();
                    break;
                case AtkSp.Disease:
                    if (p.Class == Cls.Paladin || p.Wearing("a_health") || p.Has(Status.Diseased)) break;
                    if (!Dm.SavingThrow(p, Save.Death, 0, "disease"))
                    {
                        Dm.Msg("{bad}You feel feverish. You have contracted a disease!{/}", true);
                        p.Add(Status.Diseased, 400);
                    }
                    break;
                case AtkSp.Rust:
                    {
                        var targets = p.Eq.Values.Where(i => i.Def.Metal && i.Def.Kind != IK.Light && i.Def.Kind != IK.Ring && i.Def.Kind != IK.Amulet).ToList();
                        if (targets.Count == 0) { Dm.Msg("The rust monster's antennae find nothing to eat."); break; }
                        var t = Rng.Pick(targets);
                        if (t.Ench > 0 && Rng.Chance(30 + t.Ench * 15)) { Dm.Msg("Your " + t.Def.Name + " resists the rust!"); break; }
                        if (t.Erosion < 3) { t.Erosion++; Dm.Msg("{warn}Your " + t.Def.Name + " rusts!{/}", true); }
                        else
                        {
                            Dm.Msg("{bad}Your " + t.Def.Name + " crumbles into a pile of rust!{/}", true);
                            var slot = p.Eq.First(kv => kv.Value == t).Key;
                            p.Eq.Remove(slot);
                            p.Pack.Remove(t);
                        }
                        break;
                    }
                case AtkSp.Acid:
                    {
                        var arm = p.Worn(Slot.Body) ?? p.Worn(Slot.Shield);
                        if (arm != null && Rng.Chance(30) && arm.Erosion < 3 && !(arm.Ench > 0 && Rng.Chance(50)))
                        {
                            arm.Erosion++;
                            Dm.Msg("{warn}Your " + arm.Def.Name + " corrodes!{/}");
                        }
                        break;
                    }
                case AtkSp.Fire:
                    if (!p.Resists(Elem.Fire) && Rng.Chance(15)) BurnItem(IK.Scroll, "catches fire and burns");
                    break;
                case AtkSp.Cold:
                    if (!p.Resists(Elem.Cold) && Rng.Chance(15)) BurnItem(IK.Potion, "freezes and shatters");
                    break;
                case AtkSp.Petrify:
                    if (p.Has(Status.Stoning)) break;
                    if (!Dm.SavingThrow(p, Save.Petrify, 0, "petrification"))
                    {
                        Dm.Msg("{bad}You feel yourself slowing down... you are turning to stone!{/}", true);
                        p.Add(Status.Stoning, 6);
                    }
                    break;
                case AtkSp.Engulf:
                case AtkSp.Swallow:
                    if (Dm.Engulfer == null && !Dm.SavingThrow(p, Save.Breath, 0, "engulf"))
                    {
                        Dm.Engulfer = m;
                        p.Add(a.Sp == AtkSp.Swallow ? Status.Swallowed : Status.Engulfed, 9999);
                        Dm.Msg(a.Sp == AtkSp.Swallow ? "{bad}" + m.TheCap + " swallows you whole!{/}" : "{bad}You are engulfed by " + m.The + "!{/}", true);
                        m.Pos = p.Pos;
                    }
                    break;
                case AtkSp.BloodDrain:
                    if (Rng.Chance(40)) { Dm.Msg("{bad}" + m.TheCap + " drinks your blood!{/}"); Damage(p, Rng.D(4), Elem.Phys, m, m.A); }
                    break;
                case AtkSp.StrDrain:
                    {
                        Dm.Msg("{bad}You feel your strength ebb away.{/}", true);
                        p.Add(Status.Weakened, 80);
                        p.StPow[Status.Weakened] = Math.Min(10, p.Pow(Status.Weakened) + 1);
                        if (p.Stat(Stat.Str) <= 3) Dm.Die("being drained of all strength by " + m.A);
                        break;
                    }
                case AtkSp.StealGold:
                    if (p.Gold > 0)
                    {
                        int amt = Math.Min(p.Gold, Rng.Range(10, 30) + p.Gold / 4);
                        p.Gold -= amt;
                        m.Inv.Add(new Item(ItemDb.Get("gold"), amt));
                        Dm.Msg("{bad}" + m.TheCap + " snatches " + amt + " gold and runs!{/}", true);
                        m.Fleeing = true;
                        m.FleeTurns = 40;
                    }
                    break;
                case AtkSp.Confuse:
                    if (p.Wearing("r_mind")) break;
                    if (!Dm.SavingThrow(p, Save.Spell, 0, "confusion")) { Dm.Msg("{bad}You feel confused.{/}"); p.Add(Status.Confused, Rng.Range(3, 6)); }
                    break;
                case AtkSp.Sleep:
                    if (p.Race == Race.Elf && Rng.Chance(90) || p.Race == Race.HalfElf && Rng.Chance(30)) { Dm.Msg("{good}You resist the sleep poison.{/}"); break; }
                    if (!Dm.SavingThrow(p, Save.Death, 0, "sleep")) { Dm.Msg("{bad}You fall asleep!{/}", true); p.Add(Status.Asleep, Rng.Range(3, 8)); }
                    break;
                case AtkSp.Stun:
                    p.Add(Status.Stunned, Rng.Range(1, 2));
                    Dm.Msg("{bad}You are stunned!{/}");
                    break;
                case AtkSp.BrainEat:
                    if (p.Helpless || p.Has(Status.Stunned) || Rng.Chance(20))
                    {
                        Dm.Msg("{bad}" + m.TheCap + "'s tentacles burrow into your skull!{/}", true);
                        p.Drained[(int)Stat.Int]++;
                        if (p.Stat(Stat.Int) <= 3) Dm.Die("having their brain eaten by " + m.A);
                    }
                    break;
                case AtkSp.Slime:
                    if (!p.Has(Status.Sliming))
                    {
                        Dm.Msg("{bad}The green slime clings to you! Your flesh begins to tingle...{/}", true);
                        p.Add(Status.Sliming, 9);
                    }
                    break;
                case AtkSp.Hug:
                    Dm.Msg("{bad}" + m.TheCap + " grabs you in a crushing hug!{/}");
                    Damage(p, Rng.Roll(2, 8), Elem.Phys, m, m.A);
                    p.Holder = m;
                    p.Add(Status.Held, 2);
                    break;
                case AtkSp.Sticky:
                    if (p.Holder != m)
                    {
                        Dm.Msg("{bad}You are stuck to " + m.The + "!{/}");
                        p.Holder = m;
                        p.Add(Status.Held, 9999);
                    }
                    break;
                case AtkSp.Smudge:
                    Dm.Msg("{ink}" + m.TheCap + "'s touch smears your memory of the map like wet ink!{/}", true);
                    Dm.Map.Smudge(p.Pos, 25);
                    break;
                case AtkSp.Flux:
                    {
                        var perm = new[] { 0, 1, 2, 3, 4, 5 }.ToList();
                        Rng.Shuffle(perm);
                        p.FluxMap = perm.ToArray();
                        p.Add(Status.Flux, 100);
                        var moved = Enumerable.Range(0, 6).Where(i => perm[i] != i).Select(i => Rules.StatLong[i] + " reads as " + Rules.StatLong[perm[i]]).Take(2);
                        Dm.Msg("{red}Chaos floods your body! " + string.Join("; ", moved) + ".{/}", true);
                        break;
                    }
                case AtkSp.Bleed:
                    if (!p.Wearing("a_closure")) { p.Add(Status.Bleeding, Rng.Range(3, 6)); Dm.Msg("{bad}You are bleeding!{/}"); }
                    break;
            }
        }

        static void BurnItem(IK kind, string verb)
        {
            var cands = P.Pack.Where(i => i.Def.Kind == kind).ToList();
            if (cands.Count == 0) return;
            var it = Rng.Pick(cands);
            Dm.Msg("{warn}" + (it.Count > 1 ? "One of your " + it.Name(false, false) : "Your " + it.Name(false, false)) + " " + verb + "!{/}");
            if (it.Count > 1) it.Count--;
            else P.Pack.Remove(it);
        }

        public static void Missile(Actor shooter, Point target, Item missile, Item launcher, int range, int extraHit = 0)
        {
            var map = Dm.Map;
            var path = Path.Ray(map, shooter.Pos, target, range, true);
            if (path.Count == 0) return;
            char g = missile.Def.Kind == IK.Ammo || missile.Def.Kind == IK.Weapon ? MissileGlyph(shooter.Pos, target) : missile.Glyph;
            Point land = path[path.Count - 1];
            Actor hitActor = null;
            int pathEnd = path.Count;
            for (int i = 0; i < path.Count; i++)
            {
                var a = map.ActorAt(path[i]);
                if (a == null || a == shooter) continue;
                if (a is Monster am && am.Ally && shooter.IsPlayer && Rng.Chance(70)) continue;
                hitActor = a;
                land = path[i];
                pathEnd = i + 1;
                break;
            }
            Anim.Bolt(path.Take(pathEnd).ToList(), g, missile.Color, false, 0.018f);
            bool isPotion = missile.Def.Effect is "oil" or "holy_water";
            bool shattered = false;
            if (hitActor != null)
            {
                bool hit = ResolveMissileHit(shooter, hitActor, missile, launcher, Path.Cheb(shooter.Pos, hitActor.Pos), range, extraHit);
                if (isPotion) shattered = true;
                if (hit && missile.Def.Kind == IK.Ammo && Rng.Chance(45)) { missile = null; }
                if (missile != null && missile.Def.Kind == IK.Weapon && missile.Def.Effect == "thrower" && shooter.IsPlayer)
                {
                    Dm.Msg("{gold}The Dwarven Thrower spins back into your hand.{/}");
                    Actions.ReturnToHand(missile);
                    return;
                }
            }
            else if (isPotion) shattered = true;
            if (shattered && missile != null)
            {
                if (missile.Def.Effect == "oil")
                {
                    Synth.Play(Sfx.Fire);
                    Dm.Msg("{orange}The flask of oil bursts into flame!{/}");
                    Magic.Explosion(land, 1, new Dice(2, 6), Elem.Fire, shooter, "burning oil", true);
                }
                else
                {
                    Synth.Play(Sfx.Holy);
                    Dm.Msg("{holy}The holy water splashes!{/}");
                    foreach (var q in map.Neighbors(land).Append(land))
                    {
                        var a = map.ActorAt(q);
                        if (a is Monster mm && (mm.IsUndead || mm.Def.G == '&')) Damage(mm, Rng.Roll(2, 4), Elem.Holy, shooter, "holy water");
                    }
                }
                return;
            }
            if (missile != null)
            {
                if (!map.Walkable(land)) land = path[Math.Max(0, pathEnd - 2)];
                if (map.T[land.X, land.Y] is Tile.Chasm or Tile.Lava or Tile.DeepWater) return;
                map.Drop(missile, land);
            }
        }

        static char MissileGlyph(Point a, Point b)
        {
            int dx = Math.Sign(b.X - a.X), dy = Math.Sign(b.Y - a.Y);
            if (dx == 0) return '|';
            if (dy == 0) return '-';
            return dx == dy ? '\\' : '/';
        }

        static bool ResolveMissileHit(Actor shooter, Actor target, Item missile, Item launcher, int dist, int range, int extraHit)
        {
            if (shooter is Player p && target is Monster t)
            {
                if (target.Has(Status.ProtMissiles)) return false;
                int mod = Rules.DexMissile(p.Stat(Stat.Dex)) + missile.Ench + extraHit;
                if (launcher != null) mod += launcher.Ench;
                if (p.Race == Race.Halfling && (launcher == null || launcher.Def.Id == "sling")) mod += 3;
                if (p.Race == Race.Elf && launcher != null && launcher.Def.Id.Contains("bow")) mod += 1;
                if (p.Class == Cls.Ranger && launcher != null && launcher.Def.Id.Contains("bow")) mod += 1;
                if (launcher != null && launcher.Def.Effect == "oath") mod += 2;
                if (!p.Proficient(launcher ?? missile)) mod -= 4;
                if (dist > range / 2) mod -= 2;
                if (p.Has(Status.Blessed)) mod++;
                if (p.Has(Status.Prayer)) mod++;
                if (p.Has(Status.Heroism)) mod += 2;
                if (t.Sleeping || t.Helpless) mod += 4;
                int need = p.Thac0 - t.AC;
                int roll = Dm.PlayerD20(out int alt);
                bool crit = roll == 20;
                bool hit = roll != 1 && (roll == 20 || roll + mod >= need);
                Dm.AddDie(20, roll, mod, need, "shoot " + t.Name, hit, crit, roll == 1, alt);
                WakeUp(t);
                string what = missile.Def.Kind == IK.Ammo ? "The " + missile.Def.Name : "The " + missile.BaseName(false);
                if (missile.Def.Effect is "oil" or "holy_water") return true;
                if (!hit)
                {
                    Dm.Msg(what + " misses " + t.The + ". {dice}(" + roll + Sgn(mod) + " vs " + need + "){/}");
                    return false;
                }
                if (t.Def.Has(MF.NeedMagic) && missile.Ench <= 0 && (launcher == null || launcher.Ench <= 0)) { Dm.Msg(what + " passes harmlessly through " + t.The + "."); return true; }
                if (t.Def.Has(MF.NeedSilver) && !missile.Def.Has(WF.Silver) && missile.Ench <= 0 && (launcher == null || launcher.Ench <= 0)) { Dm.Msg(what + " does not harm " + t.The + "."); return true; }
                var dice = t.Size == Size.Large ? missile.Def.DmgL : missile.Def.Dmg;
                int dmg = dice.Roll() + (crit ? dice.Roll() : 0) + missile.Ench;
                if (launcher == null) dmg += p.StrBonus.dmg;
                if (launcher != null && launcher.Def.Id == "long_bow") dmg += 1;
                if (launcher != null && launcher.Def.Effect == "oath") dmg += Rng.D(6);
                if (launcher != null) dmg += launcher.Ench;
                if (missile.Def.Effect == "thrower") dmg = dmg * (t.Def.Has(MF.Giant) ? 3 : 2);
                if (p.Class == Cls.Ranger && t.Def.Has(MF.Giant)) dmg += p.Level;
                dmg = Math.Max(1, dmg);
                Synth.Play(crit ? Sfx.Crit : Sfx.Hit);
                Dm.Msg((crit ? "{crit}Critical! {/}" : "") + what + " hits " + t.The + " for " + dmg + ". {dice}(" + roll + Sgn(mod) + " vs " + need + "){/}");
                Damage(t, dmg, Elem.Phys, p, "you");
                return true;
            }
            if (target is Player pl)
            {
                if (pl.Has(Status.ProtMissiles)) { Dm.Msg("The missile bounces off your protective ward."); return false; }
                var sm = shooter as Monster;
                int need = (sm?.Def.Thac0 ?? 18) - pl.AC;
                int roll = Rng.D(20);
                if (pl.Has(Status.Invisible)) roll -= 4;
                if (roll < need && roll != 20)
                {
                    Dm.Msg("The " + missile.Def.Name + " misses you.");
                    return false;
                }
                int dmg = missile.Def.Dmg.Roll();
                Dm.Msg("{bad}You are hit by " + Item.Article(missile.Def.Name) + "!{/} {dice}(" + dmg + " dmg){/}");
                Damage(pl, dmg, Elem.Phys, shooter, sm?.A ?? "a missile");
                return true;
            }
            if (target is Monster tm2)
            {
                var sm = shooter as Monster;
                int need = (sm?.Def.Thac0 ?? 18) - tm2.AC;
                if (Rng.D(20) < need) return false;
                Damage(tm2, missile.Def.Dmg.Roll(), Elem.Phys, shooter, "a missile");
                return true;
            }
            return false;
        }

        public static void MonsterShoot(Monster m, Actor target)
        {
            var r = m.Def.Ranged;
            var fake = new ItemDef { Id = "_missile", Name = r.Name, Kind = IK.Ammo, G = r.Glyph, C = r.Color, Dmg = r.Dmg, DmgL = r.Dmg };
            for (int i = 0; i < r.Count; i++)
            {
                if (target.Dead || m.Dead) break;
                var path = Path.Ray(Dm.Map, m.Pos, target.Pos, r.Range, true);
                if (path.Count == 0) break;
                int end = path.Count;
                Actor hit = null;
                for (int k = 0; k < path.Count; k++)
                {
                    var a = Dm.Map.ActorAt(path[k]);
                    if (a != null && a != m) { hit = a; end = k + 1; break; }
                }
                if (Dm.CanSeeMonster(m) || Dm.Map.Vis[target.Pos.X, target.Pos.Y])
                    Anim.Bolt(path.Take(end).ToList(), r.Glyph == '-' || r.Glyph == ')' ? MissileGlyph(m.Pos, target.Pos) : r.Glyph, r.Color, false, 0.018f);
                if (i == 0 && Dm.CanSeeMonster(m)) Dm.Msg(m.TheCap + (r.Name.Contains("boulder") ? " hurls " : r.Count > 1 ? " flings " : " shoots ") + Item.Article(r.Name) + (r.Count > 1 ? "s!" : "!"));
                if (hit == null) continue;
                bool ok = ResolveMissileHit(m, hit, new Item(fake), null, Path.Cheb(m.Pos, hit.Pos), r.Range, 0);
                if (ok && hit.IsPlayer && r.Sp == AtkSp.Sleep)
                {
                    var p = (Player)hit;
                    if (p.Race == Race.Elf && Rng.Chance(90)) Dm.Msg("{good}You shrug off the drow sleep poison.{/}");
                    else if (!Dm.SavingThrow(p, Save.Death, 0, "sleep poison")) { Dm.Msg("{bad}The drow poison puts you to sleep!{/}", true); p.Add(Status.Asleep, Rng.Range(3, 7)); }
                }
            }
        }
    }
}
