using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class Ai
    {
        static Player P => Dm.Player;
        static Map M => Dm.Map;

        public static bool CanSeePlayer(Monster m)
        {
            var p = P;
            if (p.Dead) return false;
            int d = Path.Cheb(m.Pos, p.Pos);
            if (d > 14) return false;
            if (!Dm.Los[m.Pos.X, m.Pos.Y] && d > 1) return false;
            if (p.Has(Status.Invisible) && !m.Def.Has(MF.Boss) && m.Def.Id != "beholder" && d > 1) return false;
            if (p.Has(Status.Hidden) && d > 1) return false;
            if (d <= 2) return true;
            if (M.Lum(p.Pos.X, p.Pos.Y) > 0.15f) return true;
            if ((m.Def.Has(MF.Infra) || m.Def.Has(MF.Undead) || m.Def.Has(MF.Animal)) && d <= 6) return true;
            return false;
        }

        public static void Act(Monster m)
        {
            var p = P;
            if (m.Dead || p.Dead) return;
            if (m.Has(Status.Webbed))
            {
                if (m.Def.Has(MF.Webber) || Rng.Chance(25 + m.HdLevel * 5)) { m.Remove(Status.Webbed); if (Dm.CanSeeMonster(m)) Dm.Msg(m.TheCap + " tears free of the web."); }
                return;
            }
            if (m.TrueDef.Has(MF.Morph) && --m.MorphTimer <= 0)
            {
                m.MorphTimer = Rng.Range(6, 14);
                var nd = Spawn.PickMonster(Math.Max(1, M.Depth + 1), d => d.Freq > 0 && !d.Has(MF.Immobile) && !d.Has(MF.Mimic) && d != m.Def);
                if (nd != null)
                {
                    float frac = m.HpFrac;
                    bool vis = Dm.CanSeeMonster(m);
                    string old = m.The;
                    m.Def = nd;
                    m.MaxHp = Math.Max(m.MaxHp, nd.HD * 5);
                    m.Hp = Math.Max(1, (int)(m.MaxHp * frac));
                    if (vis) Dm.Msg("{magic}" + char.ToUpper(old[0]) + old.Substring(1) + " shimmers and becomes " + Item.Article(nd.Name) + "!{/}");
                }
            }
            if (Dm.Engulfer == m) { Digest(m); return; }
            if (m.Def.Has(MF.Immobile)) { Immobile(m); return; }
            if (m.Disguised)
            {
                if (Path.Cheb(m.Pos, p.Pos) == 1 && Rng.Chance(8)) { m.Disguised = false; Dm.Msg("{bad}The chest sprouts teeth and lunges!{/}", true); }
                else return;
            }
            if (m.Ally) { AllyAct(m); return; }
            if (m.Peaceful) { PeacefulAct(m); return; }

            bool sees = CanSeePlayer(m);
            if (m.Sleeping)
            {
                if (!Notice(m, sees)) return;
                m.Sleeping = false;
                m.Aware = 25;
                m.LastSeen = p.Pos;
                if (Rng.D(6) <= 2 && !m.Def.Has(MF.Boss))
                {
                    if (Dm.CanSeeMonster(m)) Dm.Msg("{dim}" + m.TheCap + " wakes up, startled. {dice}(surprised!){/}{/}");
                    return;
                }
                if (Dm.CanSeeMonster(m) && !m.Def.Has(MF.Boss)) Dm.Msg(m.TheCap + " notices you!");
                if (m.Def.Has(MF.Boss)) { Dm.Msg("{crit}The Referee rises. \"Initiative,\" it says, and rolls.{/}", true); Synth.Play(Sfx.Dice); }
                return;
            }
            if (sees)
            {
                if (m.Aware <= 0 && m.Def.Has(MF.Shrieker) == false && Rng.D(6) <= 1 && !m.Def.Has(MF.Boss))
                {
                    m.Aware = 20;
                    m.LastSeen = p.Pos;
                    if (Dm.CanSeeMonster(m)) Dm.Msg("{dim}" + m.TheCap + " is surprised to see you!{/}");
                    return;
                }
                m.Aware = 20;
                m.LastSeen = p.Pos;
            }
            else if (m.Aware > 0) m.Aware--;

            if (m.Def.Has(MF.Boss)) { RefereeTurn(m, sees); if (m.Dead) return; }

            var foe = AdjacentAlly(m);
            if (foe != null && (Path.Cheb(m.Pos, p.Pos) > 1 || Rng.Chance(40))) { Combat.MonsterMelee(m, foe); return; }

            bool fleeing = m.Fleeing || m.Has(Status.Feared) || m.Has(Status.Turned);
            if (m.Fleeing && --m.FleeTurns <= 0) { m.Fleeing = false; fleeing = m.Has(Status.Feared) || m.Has(Status.Turned); }
            if (m.Has(Status.Confused) && Rng.Chance(60)) { RandomStep(m); return; }
            if (m.Def.Has(MF.Erratic) && Rng.Chance(50)) { RandomStep(m); return; }

            if (fleeing)
            {
                if (Path.Cheb(m.Pos, p.Pos) == 1 && !StepAway(m)) Combat.MonsterMelee(m, p);
                else if (Path.Cheb(m.Pos, p.Pos) > 1) StepAway(m);
                return;
            }

            if (m.Aware <= 0) { Wander(m); return; }

            int dist = Path.Cheb(m.Pos, p.Pos);
            if (sees && Specials(m, dist)) return;

            if (dist == 1 && !p.Dead)
            {
                if (p.Has(Status.BladeBarrier)) { Dm.Msg(m.TheCap + " is shredded as it reaches through your blade barrier!"); Combat.Damage(m, Rng.Roll(3, 6), Elem.Phys, p, "blade barrier"); if (m.Dead) return; }
                Combat.MonsterMelee(m, p);
                return;
            }
            if (m.Def.Ranged != null && sees && dist <= m.Def.Ranged.Range && dist > 1 && m.RangedCd <= 0 && Rng.Chance(m.Def.Ranged.Chance + 15))
            {
                Combat.MonsterShoot(m, p);
                m.RangedCd = m.Def.Ranged.Count > 1 ? 3 : 0;
                return;
            }
            if (m.RangedCd > 0) m.RangedCd--;
            Chase(m, sees ? p.Pos : m.LastSeen ?? p.Pos);
        }

        static bool Notice(Monster m, bool sees)
        {
            var p = P;
            int d = Path.Cheb(m.Pos, p.Pos);
            int stealth = p.Stealth;
            int chance;
            if (sees) chance = Math.Max(3, 30 - stealth / 3 + (d <= 3 ? 25 : 0) - d);
            else if (d <= 6) chance = Math.Max(1, 8 - stealth / 12);
            else chance = 0;
            if (m.Def.Has(MF.Boss) && d <= 9 && sees) chance = 100;
            return Rng.Chance(chance);
        }

        static Monster AdjacentAlly(Monster m)
        {
            foreach (var o in M.Monsters)
                if (!o.Dead && o.Ally && Path.Cheb(o.Pos, m.Pos) == 1) return o;
            return null;
        }

        static void Immobile(Monster m)
        {
            var p = P;
            int d = Path.Cheb(m.Pos, p.Pos);
            if (m.Def.Has(MF.Shrieker))
            {
                if (M.Vis[m.Pos.X, m.Pos.Y] && d <= 8 && Rng.Chance(50))
                {
                    Dm.Msg("{magenta}The shrieker shrieks!{/}");
                    Synth.Play(Sfx.Shriek, 0.6f);
                    Dm.Noise(m.Pos, 25, 80);
                    if (Rng.OneIn(12)) Dm.WanderingMonster(true);
                }
                return;
            }
            if (m.Def.Id == "green_slime" && d == 1 && Rng.Chance(40) && !p.Has(Status.Sliming))
            {
                Dm.Msg("{bad}The green slime drops onto you from above!{/}", true);
                Combat.MonsterMelee(m, p);
                return;
            }
            if (d == 1 && m.Def.Attacks.Count > 0 && m.Def.Attacks[0].Dmg.Max > 0) Combat.MonsterMelee(m, p);
        }

        static void Digest(Monster m)
        {
            var p = P;
            m.Pos = p.Pos;
            if (p.Has(Status.Swallowed))
            {
                Dm.Msg("{bad}Digestive acids burn you!{/}");
                Combat.Damage(p, Rng.Roll(2, 8), Elem.Acid, m, m.A);
            }
            else
            {
                Dm.Msg("{bad}The gelatinous cube's acidic body burns you.{/}");
                Combat.Damage(p, Rng.Roll(1, 6), Elem.Acid, m, m.A);
                if (Rng.Chance(30) && !p.Wearing("r_free") && !Dm.SavingThrow(p, Save.Death, 0, "paralysis")) p.Add(Status.Paralyzed, 2);
            }
        }

        static void RefereeTurn(Monster m, bool sees)
        {
            if (!sees || m.Aware <= 0) return;
            m.CastCd--;
            if (m.Hp < m.MaxHp / 4 && Path.Cheb(m.Pos, P.Pos) == 1 && Rng.Chance(40))
            {
                Magic.Cast(m, SpellId.Blink, m.Pos, 18);
                return;
            }
            if (Rng.Chance(14))
            {
                int r = Rng.D(6);
                Dm.AddDie(6, r, 0, 0, "the Random Encounter Table", null);
                Synth.Play(Sfx.Dice);
                Dm.Msg("{crit}The Referee rolls on the Random Encounter Table: " + r + "!{/}", true);
                switch (r)
                {
                    case 1:
                        Magic.Cast(m, SpellId.SummonUndead, m.Pos, 18);
                        break;
                    case 2:
                        Dm.Msg("{purple}\"Darkness falls.\"{/}");
                        foreach (var q in Magic.BallCells(P.Pos, 4)) M.Dark[q.X, q.Y] = 20;
                        break;
                    case 3:
                        Dm.Msg("{crit}\"Gravity reverses, briefly.\"{/} You are slammed into the ceiling and back down!");
                        Combat.Damage(P, Rng.Roll(2, 6), Elem.Force, m, "reversed gravity");
                        P.Add(Status.Stunned, 1);
                        break;
                    case 4:
                        Dm.Msg("{crit}\"Wandering monsters.\"{/}");
                        Dm.WanderingMonster(true);
                        Dm.WanderingMonster(true);
                        break;
                    case 5:
                        int heal = m.MaxHp / 5;
                        m.Hp = Math.Min(m.MaxHp, m.Hp + heal);
                        Dm.Msg("{crit}\"I fudge.\"{/} The Referee's wounds close. (+" + heal + ")");
                        break;
                    case 6:
                        Dm.Msg("{crit}\"Critical success... for you.\"{/} The Referee scowls, and the Heart pulses warmly.");
                        P.Add(Status.Fortune, 15, 1);
                        break;
                }
            }
        }

        static bool Specials(Monster m, int dist)
        {
            var p = P;
            var d = m.Def;
            if (m.CastCd > 0) m.CastCd--;
            if (m.BreathCd > 0) m.BreathCd--;
            if (d.GazeSp != AtkSp.None && dist <= 6 && !p.Has(Status.Blind) && M.Vis[m.Pos.X, m.Pos.Y] && Rng.Chance(35))
            {
                Gaze(m);
                if (m.Dead || p.Dead) return true;
            }
            if (d.Breath != null && m.BreathCd <= 0 && dist <= d.Breath.Range && Rng.Chance(d.Breath.Chance))
            {
                Breathe(m);
                m.BreathCd = Rng.Range(4, 8);
                return true;
            }
            if (d.Spells.Count > 0 && m.CastCd <= 0 && !m.Has(Status.Silenced) && !m.Feebleminded && dist <= 9 && Rng.Chance(d.CastChance))
            {
                var options = d.Spells.Where(s =>
                {
                    var sd = SpellDb.Get(s);
                    if (s == SpellId.Blink) return m.Hp < m.MaxHp / 3;
                    if (s == SpellId.Haste) return !m.Has(Status.Hasted);
                    if (s == SpellId.Invisibility) return !m.Has(Status.Invisible);
                    if (s == SpellId.Darkness2) return M.Lum(m.Pos.X, m.Pos.Y) > 0.3f && Rng.Chance(30);
                    if (s == SpellId.SummonUndead) return M.Monsters.Count(x => x.IsUndead && Path.Cheb(x.Pos, m.Pos) < 8) < 4;
                    if (sd.Tgt is Target.Cone or Target.Touch) return dist <= sd.Range;
                    if (s == SpellId.FingerOfDeath) return Rng.Chance(25);
                    if (s == SpellId.Fireball || s == SpellId.IceStorm || s == SpellId.FlameStrike) return dist >= 3 || m.Def.Has(MF.ResFire);
                    return dist <= sd.Range;
                }).ToList();
                if (options.Count > 0)
                {
                    var s = Rng.Pick(options);
                    var sd = SpellDb.Get(s);
                    var target = sd.Tgt == Target.Self ? m.Pos : p.Pos;
                    Magic.Cast(m, s, target, Math.Max(1, m.HdLevel));
                    m.CastCd = d.Has(MF.Boss) ? 1 : 2;
                    return true;
                }
            }
            return false;
        }

        static void Gaze(Monster m)
        {
            var p = P;
            if (m.Def.GazeSp == AtkSp.Petrify)
            {
                if (p.Has(Status.Stoning)) return;
                Dm.Msg("{bad}You meet " + m.The + "'s gaze!{/}", true);
                if (!Dm.SavingThrow(p, Save.Petrify, 0, "petrifying gaze"))
                {
                    Dm.Msg("{bad}Your limbs grow heavy and grey. You are turning to stone!{/}", true);
                    p.Add(Status.Stoning, 5);
                }
            }
            else if (m.Def.GazeSp == AtkSp.Confuse)
            {
                if (p.Wearing("r_mind")) return;
                if (!Dm.SavingThrow(p, Save.Spell, 0, "confusing gaze")) { Dm.Msg("{bad}" + m.TheCap + "'s eyes swirl. You feel confused!{/}"); p.Add(Status.Confused, Rng.Range(3, 6)); }
            }
        }

        static void Breathe(Monster m)
        {
            var b = m.Def.Breath;
            var p = P;
            bool vis = Dm.CanSeeMonster(m);
            Dm.Msg("{bad}" + (vis ? m.TheCap : "Something") + " breathes " + b.Name + "!{/}", true);
            Synth.Play(b.Elem == Elem.Fire ? Sfx.Fire : b.Elem == Elem.Cold ? Sfx.Cold : b.Elem == Elem.Elec ? Sfx.Thunder : Sfx.Zap);
            if (b.Elem == Elem.Elec && !b.Cone)
            {
                Magic.LightningBolt(m, p.Pos, b.Range, b.Dmg, m.Def.Name + "'s lightning");
                return;
            }
            List<Point> cells;
            if (b.Cone) cells = Magic.ConeCells(m.Pos, p.Pos, b.Range);
            else cells = Path.Ray(M, m.Pos, p.Pos, b.Range, false);
            var col = Magic.ElemColor(b.Elem);
            if (b.Cone) Anim.Burst(m.Pos, cells, col, '*', 0.45f);
            else Anim.Bolt(cells, '*', col, true, 0.015f);
            foreach (var a in Magic.ActorsIn(cells).Where(a => a != m).ToList())
            {
                int dmg = b.Dmg.Roll();
                if (Dm.SavingThrow(a, Save.Breath, 0, "breath")) dmg /= 2;
                if (a.IsPlayer) Dm.Msg("{bad}The " + b.Name + " engulfs you!{/} {dice}(" + dmg + "){/}");
                Combat.Damage(a, dmg, b.Elem, m, m.A + "'s " + b.Name);
            }
            if (b.Elem == Elem.Poison) Magic.MakeCloud(p.Pos, 1, Elem.Poison, 3, col);
            if (b.Elem == Elem.Fire)
                foreach (var c in cells) if (M.T[c.X, c.Y] is Tile.Grass or Tile.Web) M.Fire[c.X, c.Y] = 4;
        }

        static void AllyAct(Monster m)
        {
            var p = P;
            Monster target = null;
            int best = 99;
            foreach (var o in M.Monsters)
            {
                if (o.Dead || !o.Hostile || o.Disguised) continue;
                int d = Path.Cheb(o.Pos, m.Pos);
                if (d > 8 || d >= best) continue;
                if (d > 1 && !Fov.Los(M, m.Pos, o.Pos)) continue;
                if (o.Sleeping && Path.Cheb(o.Pos, p.Pos) > 3) continue;
                best = d;
                target = o;
            }
            if (target != null)
            {
                if (best == 1) { Combat.MonsterMelee(m, target); return; }
                if (m.Def.Spells.Count > 0 && Rng.Chance(30) && m.Def.Spells.Any(s => SpellDb.Get(s).Offensive))
                {
                    var s = Rng.Pick(m.Def.Spells.Where(s2 => SpellDb.Get(s2).Offensive).ToList());
                    Magic.Cast(m, s, target.Pos, m.HdLevel);
                    return;
                }
                StepToward(m, target.Pos);
                return;
            }
            int pd = Path.Cheb(m.Pos, p.Pos);
            if (pd > 2) Chase(m, p.Pos, true);
            else if (Rng.Chance(20)) RandomStep(m);
        }

        static void PeacefulAct(Monster m)
        {
            if (m.Def.Has(MF.Shopkeeper))
            {
                if (m.Pos != m.Home) StepToward(m, m.Home);
                return;
            }
            if (Rng.Chance(30)) RandomStep(m);
        }

        static void Wander(Monster m)
        {
            if (m.Def.Has(MF.Ambush)) return;
            if (Rng.Chance(40)) return;
            if (m.Route == null || m.Route.Count == 0)
            {
                if (Rng.Chance(70)) return;
                var goal = M.RandomFloor(q => Path.Cheb(q, m.Pos) < 25);
                var dist = Path.DistMap(M, new[] { goal }, (x, y) => M.PassableFor(m, x, y), 60);
                if (dist[m.Pos.X, m.Pos.Y] == int.MaxValue) return;
                m.Route = new Queue<Point>();
                var cur = m.Pos;
                int guard = 0;
                while (cur != goal && guard++ < 80)
                {
                    Point bestP = cur;
                    foreach (var q in M.Neighbors(cur)) if (dist[q.X, q.Y] < dist[bestP.X, bestP.Y]) bestP = q;
                    if (bestP == cur) break;
                    m.Route.Enqueue(bestP);
                    cur = bestP;
                }
            }
            if (m.Route.Count > 0)
            {
                var next = m.Route.Peek();
                if (TryMove(m, next)) m.Route.Dequeue();
                else m.Route.Clear();
            }
        }

        static void Chase(Monster m, Point target, bool ally = false)
        {
            var p = P;
            var pd = M.PlayerDist;
            bool targetIsPlayer = target == p.Pos;
            if (targetIsPlayer && pd != null && pd[m.Pos.X, m.Pos.Y] != int.MaxValue && !m.Def.Has(MF.Burrow))
            {
                Point bestP = m.Pos;
                int bestD = pd[m.Pos.X, m.Pos.Y];
                var cands = M.Neighbors(m.Pos).ToList();
                Rng.Shuffle(cands);
                foreach (var q in cands)
                {
                    if (!M.PassableFor(m, q.X, q.Y)) continue;
                    var a = M.ActorAt(q);
                    if (a != null && !(a.IsPlayer && !ally)) continue;
                    int dq = pd[q.X, q.Y];
                    if (dq < bestD) { bestD = dq; bestP = q; }
                }
                if (bestP != m.Pos)
                {
                    if (bestP == p.Pos) { if (!ally) Combat.MonsterMelee(m, p); return; }
                    TryMove(m, bestP);
                    return;
                }
                foreach (var q in cands)
                {
                    if (!M.PassableFor(m, q.X, q.Y) || M.ActorAt(q) != null) continue;
                    if (pd[q.X, q.Y] <= pd[m.Pos.X, m.Pos.Y] && Rng.Chance(50)) { TryMove(m, q); return; }
                }
                return;
            }
            StepToward(m, target);
            if (m.Pos == m.LastSeen && !CanSeePlayer(m)) m.LastSeen = null;
        }

        static void StepToward(Monster m, Point target)
        {
            var d = Path.Step(m.Pos, target);
            var opts = new List<Point> { d, new Point(d.X, 0), new Point(0, d.Y), new Point(d.X, -d.Y == 0 ? 1 : -d.Y), new Point(d.X == 0 ? 1 : -d.X, d.Y) };
            foreach (var o in opts)
            {
                if (o == Point.Zero) continue;
                var q = new Point(m.Pos.X + o.X, m.Pos.Y + o.Y);
                if (q == P.Pos)
                {
                    if (m.Hostile) Combat.MonsterMelee(m, P);
                    return;
                }
                if (m.Def.Has(MF.Burrow) && Map.InB(q) && M.T[q.X, q.Y] is Tile.Wall or Tile.Rubble or Tile.DoorSecret)
                {
                    M.T[q.X, q.Y] = Tile.Rubble;
                    if (M.Vis[q.X, q.Y] || Path.Cheb(q, P.Pos) < 8) Dm.Msg("{brown}You hear the grinding of stone.{/}");
                    m.Pos = q;
                    return;
                }
                if (TryMove(m, q)) return;
            }
        }

        static bool StepAway(Monster m)
        {
            var pd = M.PlayerDist;
            var p = P;
            Point best = m.Pos;
            int bestScore = pd != null && pd[m.Pos.X, m.Pos.Y] != int.MaxValue ? pd[m.Pos.X, m.Pos.Y] : Path.Cheb(m.Pos, p.Pos);
            foreach (var q in M.Neighbors(m.Pos))
            {
                if (!M.PassableFor(m, q.X, q.Y) || M.ActorAt(q) != null) continue;
                int s = pd != null && pd[q.X, q.Y] != int.MaxValue ? pd[q.X, q.Y] : Path.Cheb(q, p.Pos);
                if (s > bestScore) { bestScore = s; best = q; }
            }
            if (best == m.Pos) return false;
            return TryMove(m, best);
        }

        static void RandomStep(Monster m)
        {
            var opts = M.Neighbors(m.Pos).Where(q => M.PassableFor(m, q.X, q.Y) && M.ActorAt(q) == null).ToList();
            if (opts.Count > 0) TryMove(m, Rng.Pick(opts));
        }

        public static bool TryMove(Monster m, Point q)
        {
            if (!Map.InB(q)) return false;
            if (M.ActorAt(q) != null) return false;
            var t = M.T[q.X, q.Y];
            if (!M.PassableFor(m, q.X, q.Y)) return false;
            if (t == Tile.DoorClosed && !m.Def.Has(MF.Phase))
            {
                M.T[q.X, q.Y] = Tile.DoorOpen;
                if (M.Vis[q.X, q.Y]) Dm.Msg("{dim}You see a door open.{/}");
                return true;
            }
            if (t == Tile.DoorLocked && !m.Def.Has(MF.Phase))
            {
                if (Rng.Chance(20))
                {
                    M.T[q.X, q.Y] = Tile.DoorBroken;
                    Dm.Msg("{warn}WHAMM! A door bursts open somewhere nearby.{/}");
                    Synth.Play(Sfx.Door, 0.6f);
                }
                return true;
            }
            if (m.Def.Has(MF.Burrow) && t is Tile.Wall or Tile.Rubble) M.T[q.X, q.Y] = Tile.Rubble;
            m.Pos = q;
            if (t == Tile.Web && !m.Def.Has(MF.Webber) && !m.Def.Has(MF.Fly) && Rng.Chance(40)) m.Add(Status.Webbed, Rng.Range(2, 4));
            if (m.Def.Id == "gel_cube")
            {
                foreach (var it in M.ItemsAt(q).Where(i => i.Def.Kind != IK.Chest).ToList())
                {
                    M.Items.Remove(it);
                    m.Inv.Add(it);
                }
            }
            var trap = M.TrapAt(q);
            if (trap != null && !m.Def.Has(MF.Fly) && m.Aware > 0 && Rng.Chance(8)) Traps.MonsterTrigger(m, trap);
            return true;
        }
    }
}
