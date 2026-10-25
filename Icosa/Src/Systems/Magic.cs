using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class Magic
    {
        static Player P => Dm.Player;
        static Map M => Dm.Map;

        public static void AutoPlan(Player p)
        {
            var slots = p.Slots;
            TrimPlan(p);
            for (int lvl = 1; lvl <= slots.Length; lvl++)
            {
                int have = p.Plan.Count(s => SpellDb.Get(s).Level == lvl);
                var avail = p.Castable.Where(s => SpellDb.Get(s).Level == lvl).ToList();
                if (avail.Count == 0) continue;
                var pref = PreferredOrder(p, avail);
                int i = 0;
                while (have < slots[lvl - 1])
                {
                    p.Plan.Add(pref[i % pref.Count]);
                    have++;
                    i++;
                }
            }
        }

        static List<SpellId> PreferredOrder(Player p, List<SpellId> avail)
        {
            SpellId[] fav = { SpellId.MagicMissile, SpellId.Sleep, SpellId.CureLight, SpellId.Bless, SpellId.Web, SpellId.MirrorImage, SpellId.CHoldPerson, SpellId.SpiritualHammer,
                SpellId.Fireball, SpellId.LightningBolt, SpellId.Prayer, SpellId.CureDisease, SpellId.IceStorm, SpellId.DimensionDoor, SpellId.CureSerious, SpellId.ConeOfCold,
                SpellId.FlameStrike, SpellId.CureCritical, SpellId.Disintegrate, SpellId.Heal, SpellId.FingerOfDeath, SpellId.HolyWord, SpellId.Restoration };
            return avail.OrderBy(s => { int i = Array.IndexOf(fav, s); return i < 0 ? 99 : i; }).ToList();
        }

        public static void TrimPlan(Player p)
        {
            var slots = p.Slots;
            var castable = p.Castable.ToHashSet();
            p.Plan.RemoveAll(s => !castable.Contains(s));
            for (int lvl = 1; lvl <= 9; lvl++)
            {
                int max = lvl <= slots.Length ? slots[lvl - 1] : 0;
                while (p.Plan.Count(s => SpellDb.Get(s).Level == lvl) > max)
                {
                    int idx = p.Plan.FindLastIndex(s => SpellDb.Get(s).Level == lvl);
                    p.Plan.RemoveAt(idx);
                }
            }
        }

        public static void Memorize(Player p)
        {
            TrimPlan(p);
            p.Memorized.Clear();
            p.Memorized.AddRange(p.Plan);
        }

        public static Monster SummonAt(string id, Point near, bool ally, int turns)
        {
            var def = MonsterDb.Get(id);
            if (def == null) return null;
            var spot = M.NearestFree(near, 4);
            if (spot == null) return null;
            var m = new Monster(def) { Pos = spot.Value, Ally = ally, SummonTurns = turns, Aware = 50 };
            if (ally) m.Peaceful = false;
            M.Monsters.Add(m);
            return m;
        }

        public static List<Point> BallCells(Point c, int r)
        {
            var cells = new List<Point>();
            Fov.Compute(c, r, M.BlocksSight, (x, y) =>
            {
                if (Map.InB(x, y) && (M.Walkable(x, y) || M.T[x, y] is Tile.DoorClosed or Tile.DeepWater or Tile.Lava or Tile.Chasm or Tile.Statue))
                    if ((x - c.X) * (x - c.X) + (y - c.Y) * (y - c.Y) <= r * r + r) cells.Add(new Point(x, y));
            });
            if (!cells.Contains(c)) cells.Add(c);
            return cells;
        }

        public static List<Point> ConeCells(Point o, Point target, int len)
        {
            var dir = new Vector2(target.X - o.X, target.Y - o.Y);
            if (dir.LengthSquared() < 0.01f) dir = new Vector2(1, 0);
            dir.Normalize();
            var cells = new List<Point>();
            Fov.Compute(o, len, M.BlocksSight, (x, y) =>
            {
                if (!Map.InB(x, y) || (x == o.X && y == o.Y)) return;
                var v = new Vector2(x - o.X, y - o.Y);
                float l = v.Length();
                if (l > len + 0.5f) return;
                v /= l;
                if (Vector2.Dot(v, dir) >= 0.80f || l < 1.5f && Vector2.Dot(v, dir) > 0.6f)
                    if (!M.BlocksSight(x, y)) cells.Add(new Point(x, y));
            });
            return cells;
        }

        public static IEnumerable<Actor> ActorsIn(IEnumerable<Point> cells)
        {
            foreach (var c in cells)
            {
                var a = M.ActorAt(c);
                if (a != null) yield return a;
            }
        }

        public static void Explosion(Point c, int r, Dice dmg, Elem e, Actor src, string cause, bool fire = false, bool saveHalf = true)
        {
            var cells = BallCells(c, r);
            Anim.Burst(c, cells, ElemColor(e), e == Elem.Fire ? '*' : e == Elem.Cold ? '*' : '·', 0.4f);
            foreach (var a in ActorsIn(cells).ToList())
            {
                int d = dmg.Roll();
                if (saveHalf && Dm.SavingThrow(a, Save.Breath, 0, cause)) d /= 2;
                Hurt(a, d, e, src, cause);
            }
            if (fire)
                foreach (var p in cells)
                    if (M.T[p.X, p.Y] is Tile.Grass or Tile.Web or Tile.Floor or Tile.Corridor or Tile.Moss && Rng.Chance(M.T[p.X, p.Y] is Tile.Grass or Tile.Web ? 90 : 25))
                        M.Fire[p.X, p.Y] = Rng.Range(2, 6);
            BurnCorpses(cells, e);
        }

        static void BurnCorpses(List<Point> cells, Elem e)
        {
            if (e != Elem.Fire && e != Elem.Acid) return;
            foreach (var it in M.Items.Where(i => cells.Contains(i.Pos) && i.CorpseOf != null && i.CorpseOf.Has(MF.Revives)).ToList())
            {
                M.Items.Remove(it);
                if (M.Vis[it.Pos.X, it.Pos.Y]) Dm.Msg("{orange}The " + it.CorpseOf.Name + " corpse burns to ash. It will not rise again.{/}");
            }
            foreach (var p in cells)
                if (M.T[p.X, p.Y] == Tile.Web) M.T[p.X, p.Y] = Tile.Ash;
        }

        public static Color ElemColor(Elem e) => e switch
        {
            Elem.Fire => new Color(255, 110, 30),
            Elem.Cold => new Color(150, 210, 255),
            Elem.Elec => new Color(220, 230, 255),
            Elem.Acid => new Color(150, 230, 60),
            Elem.Poison => new Color(110, 200, 80),
            Elem.Holy => new Color(255, 240, 160),
            Elem.Necro => new Color(120, 60, 160),
            Elem.Force => new Color(200, 160, 255),
            _ => new Color(200, 160, 255),
        };

        static void Hurt(Actor a, int d, Elem e, Actor src, string cause)
        {
            if (a is Player p && p.Has(Status.Globe) && src is Monster) { Dm.Msg("The spell breaks against your globe of invulnerability."); return; }
            if (a.IsPlayer && src != null && !src.IsPlayer) Dm.Msg("{bad}You are caught in the " + cause + "!{/} {dice}(" + d + "){/}");
            else if (a is Monster m && Dm.CanSeeMonster(m)) Dm.Msg(m.TheCap + " is caught in the " + cause + ". {dice}(" + d + "){/}");
            else if (a.IsPlayer) Dm.Msg("{bad}You are caught in the " + cause + "!{/} {dice}(" + d + "){/}");
            Combat.Damage(a, d, e, src, cause);
            if (a is Monster mm && src != null && src.IsPlayer && !mm.Dead) Combat.WakeUp(mm);
        }

        static Actor FirstInPath(Actor caster, Point target, int range, out List<Point> path)
        {
            path = Path.Ray(M, caster.Pos, target, range, true);
            foreach (var p in path)
            {
                var a = M.ActorAt(p);
                if (a != null && a != caster) return a;
            }
            return null;
        }

        static bool AffectsHumanoid(Monster m) => m.IsHumanoid && !m.Def.Has(MF.Undead) && m.Def.Size != Size.Large;

        static string Name(Actor a) => a.IsPlayer ? "you" : a.The;
        static string NameCap(Actor a) => a.IsPlayer ? "You" : a.TheCap;

        public static bool Cast(Actor caster, SpellId id, Point target, int lvl, Item item = null)
        {
            var sd = SpellDb.Get(id);
            bool byPlayer = caster.IsPlayer;
            var p = P;
            if (!byPlayer && caster.Has(Status.Silenced)) return false;
            Synth.Play(sd.Cleric ? Sfx.Holy : Sfx.Spell, 0.7f);
            if (!byPlayer && Dm.CanSeeMonster((Monster)caster)) Dm.Msg("{magic}" + caster.TheCap + " casts " + sd.Name + "!{/}");
            if (!byPlayer && p.Has(Status.Globe) && sd.Level <= 4 && sd.Offensive && sd.Tgt != Target.Self)
            {
                Dm.Msg("{magic}The spell shatters against your globe of invulnerability.{/}");
                return true;
            }
            switch (id)
            {
                case SpellId.MagicMissile:
                    {
                        int n = Math.Min(5, 1 + (lvl - 1) / 2);
                        var t = M.ActorAt(target);
                        if (t == null || t == caster)
                        {
                            t = FirstInPath(caster, target, sd.Range, out _);
                            if (t == null) { Dm.Say(caster, "The missiles fizzle into the dark.", ""); return true; }
                        }
                        var line = Path.Line(caster.Pos, t.Pos).Skip(1).ToList();
                        for (int i = 0; i < n && !t.Dead; i++)
                        {
                            Anim.Bolt(line, '*', sd.C, true, 0.014f);
                            if (t.Has(Status.Shielded)) { Dm.Msg("The missile splashes harmlessly against " + (t.IsPlayer ? "your" : t.The + "'s") + " shield."); continue; }
                            int d = Rng.D(4) + 1;
                            if (t.IsPlayer) Dm.Msg("{bad}A magic missile strikes you!{/} {dice}(" + d + "){/}");
                            else Dm.Msg("The magic missile strikes " + Name(t) + ". {dice}(" + d + "){/}");
                            Combat.Damage(t, d, Elem.Force, caster, "a magic missile");
                        }
                        return true;
                    }
                case SpellId.Sleep:
                    {
                        var cells = BallCells(target, 2);
                        Anim.Burst(target, cells, sd.C, 'z', 0.4f);
                        int pool = Rng.Roll(2, 4) + lvl / 3;
                        var victims = ActorsIn(cells).Where(a => a != caster).OrderBy(a => a is Monster mm ? mm.HdLevel : p.Level).ToList();
                        foreach (var a in victims)
                        {
                            if (a is Monster m)
                            {
                                int hd = Math.Max(1, m.HdLevel);
                                if (hd > 4 || m.Def.Has(MF.ResSleep) || (m.Ally && byPlayer)) continue;
                                if (pool < hd) break;
                                pool -= hd;
                                m.Add(Status.Asleep, Rng.Range(10, 20) + lvl);
                                m.Sleeping = false;
                                if (Dm.CanSeeMonster(m)) Dm.Msg(m.TheCap + " falls asleep.");
                            }
                            else if (a is Player pl)
                            {
                                if (pl.Level > 4 || (pl.Race == Race.Elf && Rng.Chance(90)) || (pl.Race == Race.HalfElf && Rng.Chance(30))) { Dm.Msg("You feel drowsy for a moment."); continue; }
                                Dm.Msg("{bad}You fall into a magical sleep!{/}", true);
                                pl.Add(Status.Asleep, Rng.Range(3, 7));
                            }
                        }
                        return true;
                    }
                case SpellId.BurningHands:
                    {
                        var cells = ConeCells(caster.Pos, target, 3);
                        Anim.Burst(caster.Pos, cells, sd.C, '^', 0.35f);
                        foreach (var a in ActorsIn(cells).Where(a => a != caster).ToList()) Hurt(a, Rng.D(4) + lvl, Elem.Fire, caster, "flames");
                        BurnCorpses(cells, Elem.Fire);
                        return true;
                    }
                case SpellId.ShockingGrasp:
                    {
                        var t = M.ActorAt(target);
                        if (t == null || t == caster) { Dm.Say(caster, "Sparks crackle uselessly between your fingers.", ""); return true; }
                        Anim.Flash(t.Pos, sd.C, 0.3f);
                        Hurt(t, Rng.D(8) + lvl, Elem.Elec, caster, "shocking grasp");
                        return true;
                    }
                case SpellId.Shield:
                    caster.Add(Status.Shielded, 5 * lvl + 10);
                    Dm.Say(caster, "{magic}An invisible shield springs up before you.{/}", caster.TheCap + " is surrounded by a shimmering shield.");
                    return true;
                case SpellId.CharmPerson:
                case SpellId.CharmMonster:
                    {
                        var t = M.ActorAt(target);
                        if (t is Monster m && m != caster)
                        {
                            bool can = id == SpellId.CharmMonster ? !m.Def.Has(MF.Mindless) && !m.Def.Has(MF.Undead) : AffectsHumanoid(m) && m.HdLevel <= 4 + lvl / 4;
                            if (!can || m.Def.Has(MF.Boss) || m.Def.Has(MF.Shopkeeper)) { Dm.Msg(m.TheCap + " is unaffected."); return true; }
                            if (Dm.SavingThrow(m, Save.Spell, 0, "charm")) { Dm.Msg(m.TheCap + " resists your charm!"); Combat.WakeUp(m); return true; }
                            m.Ally = true;
                            m.Peaceful = false;
                            m.Sleeping = false;
                            m.Fleeing = false;
                            m.Add(Status.Charmed, 300 + lvl * 20);
                            Dm.Msg("{pink}" + m.TheCap + " looks at you with adoring eyes.{/}");
                        }
                        else if (t is Player pl && !byPlayer)
                        {
                            if (pl.Wearing("r_mind") || (pl.Race == Race.Elf && Rng.Chance(90))) { Dm.Msg("You shake off a strange warmth."); return true; }
                            if (!Dm.SavingThrow(pl, Save.Spell, 0, "charm")) { Dm.Msg("{bad}You feel strangely fond of " + caster.The + "... you can't bring yourself to act.{/}", true); pl.Add(Status.Paralyzed, Rng.Range(2, 4)); }
                        }
                        return true;
                    }
                case SpellId.Light:
                case SpellId.CLight:
                case SpellId.ContinualLight:
                    {
                        int dur = id == SpellId.ContinualLight ? 4000 : 300 + lvl * 30;
                        caster.Add(Status.Insight, dur, id == SpellId.ContinualLight ? 6 : 4);
                        foreach (var c in BallCells(caster.Pos, 4))
                        {
                            M.Lit[c.X, c.Y] = true;
                            foreach (var q in M.Neighbors(c)) if (!M.Walkable(q)) M.Lit[q.X, q.Y] = true;
                        }
                        Dm.Say(caster, "{yellow}A soft, steady light blooms around you.{/}", "Light blooms around " + caster.The + ".");
                        return true;
                    }
                case SpellId.Identify:
                    if (item != null) Actions.IdentifyItem(item);
                    return true;
                case SpellId.DetectMagic:
                    {
                        int n = 0;
                        foreach (var it in M.Items.Concat(p.Pack))
                            if (it.IsMagic && !it.Known)
                            {
                                it.MagicSensed = true;
                                n++;
                                if (M.Items.Contains(it)) { M.Seen[it.Pos.X, it.Pos.Y] = true; M.MemCh[it.Pos.X, it.Pos.Y] = it.Glyph; M.MemFg[it.Pos.X, it.Pos.Y] = it.Color; }
                            }
                        Dm.Msg("{magic}You sense " + (n == 0 ? "no unknown magic" : n + " magical auras") + " nearby.{/}");
                        return true;
                    }
                case SpellId.HoldPortal:
                    if (M.T[target.X, target.Y] is Tile.DoorClosed or Tile.DoorOpen)
                    {
                        if (M.ActorAt(target) != null) { Dm.Msg("Something is in the way."); return true; }
                        M.T[target.X, target.Y] = Tile.DoorLocked;
                        Dm.Msg("{magic}The door seals shut with a sound like a held breath.{/}");
                    }
                    else Dm.Msg("There is no door there.");
                    return true;
                case SpellId.ProtEvil:
                case SpellId.CProtEvil:
                case SpellId.ProtEvil10:
                    caster.Add(Status.ProtEvil, id == SpellId.ProtEvil10 ? 400 + lvl * 20 : 20 * lvl + 30);
                    Dm.Say(caster, "{holy}A ward against evil surrounds you.{/}", "");
                    return true;
                case SpellId.FindFamiliar:
                    {
                        if (M.Monsters.Any(m => m.Ally && m.Def.Id.StartsWith("familiar"))) { Dm.Msg("You already have a familiar."); return true; }
                        var f = SummonAt(Rng.Chance(50) ? "familiar_cat" : "familiar_raven", caster.Pos, true, 0);
                        if (f != null) { f.MaxHp += lvl; f.Hp = f.MaxHp; Dm.Msg("{magic}" + f.TheCap + " appears and regards you with intelligent eyes.{/}"); }
                        return true;
                    }
                case SpellId.Web:
                    {
                        var cells = BallCells(target, 1 + lvl / 6);
                        Anim.Burst(target, cells, sd.C, '"', 0.3f);
                        foreach (var c in cells)
                            if (M.T[c.X, c.Y] is Tile.Floor or Tile.Corridor or Tile.Grass or Tile.Rubble or Tile.Ash) M.T[c.X, c.Y] = Tile.Web;
                        foreach (var a in ActorsIn(cells).Where(a => a != caster).ToList())
                        {
                            if (a is Player pl && pl.Wearing("r_free")) continue;
                            if (!Dm.SavingThrow(a, Save.Petrify, 0, "web")) { a.Add(Status.Webbed, Rng.Range(4, 8) + lvl / 2); Dm.Say(a, "{bad}You are stuck in the web!{/}", a.TheCap + " is stuck in the webs."); }
                        }
                        return true;
                    }
                case SpellId.Invisibility:
                    caster.Add(Status.Invisible, byPlayer ? 600 : 100);
                    Dm.Say(caster, "{magic}You fade from sight.{/}", caster.TheCap + " vanishes!");
                    return true;
                case SpellId.MirrorImage:
                    {
                        int n = Rng.D(4) + lvl / 3;
                        caster.Set(Status.MirrorImage, 30 * lvl, n);
                        Dm.Say(caster, "{magic}" + n + " shimmering duplicates of you appear!{/}", "");
                        return true;
                    }
                case SpellId.Knock:
                    {
                        var t = M.T[target.X, target.Y];
                        if (t is Tile.DoorLocked or Tile.DoorClosed or Tile.DoorSecret)
                        {
                            M.T[target.X, target.Y] = Tile.DoorOpen;
                            Synth.Play(Sfx.Door);
                            Dm.Msg("{magic}With a loud knock, the door swings open.{/}");
                            return true;
                        }
                        var chest = M.ItemsAt(target).Concat(M.ItemsAt(caster.Pos)).FirstOrDefault(i => i.Def.Kind == IK.Chest && i.Locked);
                        if (chest != null) { chest.Locked = false; Dm.Msg("{magic}The chest's lock clicks open.{/}"); return true; }
                        Dm.Msg("Nothing there to open.");
                        return true;
                    }
                case SpellId.StinkingCloud:
                    MakeCloud(target, 1, Elem.Magic, 8 + lvl / 2, sd.C);
                    Dm.Msg("{green}A nauseating cloud billows outward.{/}");
                    return true;
                case SpellId.AcidArrow:
                    {
                        var t = FirstInPath(caster, target, sd.Range, out var path);
                        Anim.Bolt(path.TakeWhile(q => M.ActorAt(q) == null || M.ActorAt(q) == caster).Concat(t != null ? new[] { t.Pos } : new Point[0]).ToList(), '-', sd.C, true);
                        if (t == null) return true;
                        Hurt(t, Rng.Roll(2, 4) + Rng.Roll(Math.Max(1, lvl / 3), 4), Elem.Acid, caster, "acid arrow");
                        return true;
                    }
                case SpellId.Strength:
                    {
                        int s = p.IsWarrior ? 19 : 18;
                        caster.Add(Status.GiantStr, 300 + lvl * 20, s);
                        Dm.Say(caster, "{orange}Your muscles swell with magical strength.{/}", "");
                        return true;
                    }
                case SpellId.RayEnfeeble:
                    {
                        var t = FirstInPath(caster, target, sd.Range, out var path);
                        Anim.Bolt(path, '~', sd.C, true);
                        if (t is Monster m)
                        {
                            if (Dm.SavingThrow(m, Save.Spell, 0, "")) { Dm.Msg(m.TheCap + " resists."); return true; }
                            m.DmgPenalty = 25 + lvl * 2;
                            m.Add(Status.Weakened, 100);
                            Dm.Msg(m.TheCap + " looks feeble.");
                        }
                        else if (t is Player pl && !Dm.SavingThrow(pl, Save.Spell, 0, "enfeeblement")) { pl.Add(Status.Weakened, 60, 4); Dm.Msg("{bad}You feel feeble.{/}"); }
                        return true;
                    }
                case SpellId.Darkness:
                case SpellId.Darkness2:
                    {
                        var c = id == SpellId.Darkness2 ? caster.Pos : target;
                        foreach (var q in BallCells(c, 3)) M.Dark[q.X, q.Y] = 40 + lvl * 2;
                        Dm.Msg("{purple}Magical darkness swallows the light.{/}");
                        return true;
                    }
                case SpellId.Scare:
                    {
                        var t = M.ActorAt(target) as Monster;
                        if (t == null) return true;
                        if (t.HdLevel > 6 || t.Def.Has(MF.Mindless) || t.IsUndead || Dm.SavingThrow(t, Save.Spell, 0, "")) { Dm.Msg(t.TheCap + " is not frightened."); return true; }
                        t.Fleeing = true;
                        t.FleeTurns = Rng.Roll(3, 4) + lvl;
                        Dm.Msg(t.TheCap + " flees in terror!");
                        return true;
                    }
                case SpellId.Fireball:
                case SpellId.DelayedBlast:
                    {
                        var path = Path.Ray(M, caster.Pos, target, sd.Range, true);
                        var hit = path.Count > 0 ? path.Last() : target;
                        foreach (var q in path) if (M.ActorAt(q) != null && M.ActorAt(q) != caster) { hit = q; break; }
                        Anim.Bolt(Path.Line(caster.Pos, hit).Skip(1).ToList(), '*', sd.C, true, 0.02f);
                        Synth.Play(Sfx.Fire);
                        int dice = id == SpellId.DelayedBlast ? lvl : Math.Min(10, lvl);
                        Explosion(hit, 2, new Dice(Math.Max(1, dice), 6, id == SpellId.DelayedBlast ? lvl : 0), Elem.Fire, caster, "fireball", true);
                        return true;
                    }
                case SpellId.LightningBolt:
                    {
                        int dmgDice = Math.Min(10, Math.Max(1, lvl));
                        LightningBolt(caster, target, 12, new Dice(dmgDice, 6), "lightning bolt");
                        return true;
                    }
                case SpellId.Haste:
                    caster.Add(Status.Hasted, 10 + lvl * 3);
                    Dm.Say(caster, "{yellow}You feel yourself moving faster.{/}", caster.TheCap + " is moving faster.");
                    return true;
                case SpellId.HoldPerson:
                case SpellId.CHoldPerson:
                case SpellId.HoldMonster:
                    {
                        var t = M.ActorAt(target);
                        if (t == null || t == caster) { Dm.Say(caster, "Your spell finds no target.", ""); return true; }
                        if (t is Monster m)
                        {
                            bool ok = id == SpellId.HoldMonster ? !m.Def.Has(MF.Undead) || true : AffectsHumanoid(m);
                            if (!ok || m.Def.Has(MF.Mindless) && id != SpellId.HoldMonster) { Dm.Msg(m.TheCap + " is unaffected."); return true; }
                            if (Dm.SavingThrow(m, Save.Spell, m.Def.Has(MF.Boss) ? 4 : 0, "")) { Dm.Msg(m.TheCap + " resists the hold!"); Combat.WakeUp(m); return true; }
                            m.Add(Status.Paralyzed, Rng.Range(4, 8) + lvl / 2);
                            Dm.Msg("{yellow}" + m.TheCap + " is held fast!{/}");
                        }
                        else if (t is Player pl)
                        {
                            if (pl.Wearing("r_free")) { Dm.Msg("You feel a brief stiffness."); return true; }
                            if (!Dm.SavingThrow(pl, Save.Spell, 0, "hold")) { Dm.Msg("{bad}You are held fast by magic!{/}", true); pl.Add(Status.Paralyzed, Rng.Range(2, 5)); }
                        }
                        return true;
                    }
                case SpellId.DispelMagic:
                case SpellId.CDispel:
                    {
                        var cells = BallCells(target, 2);
                        Anim.Burst(target, cells, sd.C, '·', 0.3f);
                        foreach (var a in ActorsIn(cells).ToList())
                        {
                            foreach (var s in new[] { Status.Hasted, Status.Invisible, Status.Shielded, Status.MirrorImage, Status.Blessed, Status.Slowed, Status.Webbed, Status.Charmed, Status.Paralyzed, Status.Asleep, Status.FireShield, Status.Globe, Status.GiantStr, Status.Sanctuary, Status.Errata, Status.Fortune })
                                a.Remove(s);
                            if (a is Monster m)
                            {
                                if (m.SummonTurns > 0) { m.Dead = true; Dm.Msg(m.TheCap + " is dispelled!"); }
                                if (m.Ally && m.Turns(Status.Charmed) == 0 && m.Def.Freq > 0 && m.SummonTurns == 0) m.Ally = false;
                            }
                        }
                        Dm.Msg("{magic}Magic unravels in a wave of grey light.{/}");
                        return true;
                    }
                case SpellId.Slow:
                    {
                        var cells = BallCells(target, 2);
                        Anim.Burst(target, cells, sd.C, '·', 0.3f);
                        foreach (var a in ActorsIn(cells).Where(a => a != caster).ToList())
                        {
                            if (a is Monster m && m.Ally && byPlayer) continue;
                            if (!Dm.SavingThrow(a, Save.Spell, 0, "slow")) { a.Add(Status.Slowed, 10 + lvl); Dm.Say(a, "{bad}You slow down.{/}", a.TheCap + " slows down."); }
                        }
                        return true;
                    }
                case SpellId.Blink:
                    {
                        var spot = M.RandomFloor(q => Path.Cheb(q, caster.Pos) <= 7 && Path.Cheb(q, caster.Pos) >= 3 && M.Free(q));
                        Teleport(caster, spot);
                        return true;
                    }
                case SpellId.ProtMissiles:
                    caster.Add(Status.ProtMissiles, 10 * lvl + 30);
                    Dm.Say(caster, "{magic}The air around you hardens against missiles.{/}", "");
                    return true;
                case SpellId.MonsterSummon:
                    {
                        int n = Rng.Range(2, 3);
                        for (int i = 0; i < n; i++)
                        {
                            var def = Spawn.PickMonster(Math.Max(1, lvl / 2 + 1), d => d.Freq > 0 && !d.Has(MF.Immobile) && !d.Has(MF.Mimic));
                            if (def == null) continue;
                            var m = SummonAt(def.Id, caster.Pos, byPlayer, 30 + lvl * 2);
                            if (m != null && !byPlayer) { m.Ally = false; m.Aware = 50; }
                        }
                        Dm.Msg("{magic}Creatures answer the summons!{/}");
                        return true;
                    }
                case SpellId.IceStorm:
                    Synth.Play(Sfx.Cold);
                    Explosion(target, 2, new Dice(3, 10), Elem.Cold, caster, "ice storm", false, false);
                    return true;
                case SpellId.DimensionDoor:
                    if (!M.Walkable(target) || M.ActorAt(target) != null || !M.Seen[target.X, target.Y]) { Dm.Msg("You cannot step there."); return true; }
                    Teleport(caster, target);
                    return true;
                case SpellId.Teleport:
                    Teleport(caster, M.Walkable(target) && M.ActorAt(target) == null && M.Seen[target.X, target.Y] && byPlayer ? target : (Point?)null);
                    return true;
                case SpellId.Fear:
                    {
                        var cells = ConeCells(caster.Pos, target, 6);
                        Anim.Burst(caster.Pos, cells, sd.C, '!', 0.3f);
                        foreach (var a in ActorsIn(cells).Where(a => a != caster).ToList())
                        {
                            if (a is Monster m)
                            {
                                if (m.Def.Has(MF.Mindless) || m.Def.Has(MF.Boss) || (m.Ally && byPlayer)) continue;
                                if (!Dm.SavingThrow(m, Save.Spell, 0, "")) { m.Fleeing = true; m.FleeTurns = 10 + lvl; Dm.Msg(m.TheCap + " turns to flee!"); }
                            }
                            else if (a is Player pl && !Dm.SavingThrow(pl, Save.Spell, 0, "fear")) { pl.Add(Status.Feared, Rng.Range(4, 8)); Dm.Msg("{bad}You are overcome with fear!{/}", true); }
                        }
                        return true;
                    }
                case SpellId.WallOfFire:
                    {
                        var cells = BallCells(target, 1);
                        foreach (var c in cells) if (M.Walkable(c)) M.Fire[c.X, c.Y] = Rng.Range(6, 12) + lvl / 2;
                        Anim.Burst(target, cells, sd.C, '^', 0.4f);
                        Synth.Play(Sfx.Fire);
                        foreach (var a in ActorsIn(cells).Where(a => a != caster).ToList()) Hurt(a, Rng.Roll(2, 6) + lvl, Elem.Fire, caster, "wall of fire");
                        BurnCorpses(cells, Elem.Fire);
                        return true;
                    }
                case SpellId.PolymorphOther:
                    {
                        var t = FirstInPath(caster, target, sd.Range, out var path);
                        Anim.Bolt(path, '*', sd.C, true);
                        if (t is Monster m) Polymorph(m);
                        else if (t is Player) Dm.Msg("You feel momentarily different. Then the same.");
                        return true;
                    }
                case SpellId.FireShield:
                    caster.Add(Status.FireShield, 20 + lvl * 2);
                    Dm.Say(caster, "{orange}Flames wreathe your body.{/}", "");
                    return true;
                case SpellId.ConeOfCold:
                    {
                        var cells = ConeCells(caster.Pos, target, 6);
                        Synth.Play(Sfx.Cold);
                        Anim.Burst(caster.Pos, cells, sd.C, '*', 0.45f);
                        foreach (var a in ActorsIn(cells).Where(a => a != caster).ToList())
                        {
                            int d = Rng.Roll(Math.Max(1, lvl), 4) + lvl;
                            if (Dm.SavingThrow(a, Save.Spell, 0, "cold")) d /= 2;
                            Hurt(a, d, Elem.Cold, caster, "cone of cold");
                        }
                        return true;
                    }
                case SpellId.Cloudkill:
                    MakeCloud(target, 2, Elem.Poison, 8 + lvl / 2, sd.C);
                    Dm.Msg("{green}A yellow-green cloud of death rolls outward.{/}");
                    return true;
                case SpellId.AnimateDead:
                case SpellId.CAnimateDead:
                    {
                        int n = 0;
                        foreach (var c in M.Items.Where(i => i.Def.Kind == IK.Corpse && Path.Cheb(i.Pos, caster.Pos) <= 6).ToList())
                        {
                            if (n >= 2 + lvl / 3) break;
                            string kind = c.CorpseOf != null && c.CorpseOf.Size == Size.Large ? "zombie" : Rng.Chance(50) ? "skeleton" : "zombie";
                            var m = SummonAt(kind, c.Pos, true, 0);
                            if (m == null) continue;
                            M.Items.Remove(c);
                            n++;
                        }
                        Dm.Msg(n == 0 ? "There are no corpses nearby to animate." : "{purple}" + n + " corpse" + (n > 1 ? "s" : "") + " lurch to unholy life at your command!{/}");
                        return true;
                    }
                case SpellId.Passwall:
                    {
                        var d = new Point(target.X - caster.Pos.X, target.Y - caster.Pos.Y);
                        var c = caster.Pos;
                        int dug = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            c = new Point(c.X + d.X, c.Y + d.Y);
                            if (!Map.InB(c) || M.T[c.X, c.Y] is Tile.PermWall or Tile.Screen) break;
                            if (M.T[c.X, c.Y] is Tile.Wall or Tile.DoorSecret or Tile.Rubble or Tile.Crystal or Tile.Bookshelf or Tile.Pillar) { M.T[c.X, c.Y] = Tile.Corridor; dug++; }
                            else if (dug > 0) break;
                        }
                        Dm.Msg(dug > 0 ? "{magic}The stone melts away into a passage.{/}" : "The spell finds no stone to pass.");
                        return true;
                    }
                case SpellId.Feeblemind:
                    {
                        var t = M.ActorAt(target) as Monster;
                        if (t == null) return true;
                        if (Dm.SavingThrow(t, Save.Spell, -4, "")) { Dm.Msg(t.TheCap + " resists."); return true; }
                        t.Feebleminded = true;
                        Dm.Msg(t.TheCap + " drools vacantly.");
                        return true;
                    }
                case SpellId.Disintegrate:
                    {
                        var path = Path.Ray(M, caster.Pos, target, sd.Range, true, true);
                        var shown = new List<Point>();
                        foreach (var q in path)
                        {
                            shown.Add(q);
                            var a = M.ActorAt(q);
                            if (a != null && a != caster)
                            {
                                Anim.Bolt(shown, '=', sd.C, true);
                                if (a is Monster m && (m.Def.Has(MF.Boss) || Dm.SavingThrow(m, Save.Death, 0, "")))
                                {
                                    Dm.Msg(m.TheCap + " resists, but is burned by the green ray.");
                                    Combat.Damage(m, Rng.Roll(5, 6), Elem.Force, caster, "disintegration");
                                }
                                else if (a is Monster m2)
                                {
                                    Dm.Msg("{green}" + m2.TheCap + " is reduced to a fine grey dust!{/}");
                                    m2.Inv.Clear();
                                    Combat.Kill(m2, caster);
                                    M.Items.RemoveAll(i => i.Pos == m2.Pos && i.Def.Kind == IK.Corpse);
                                }
                                else if (a is Player pl)
                                {
                                    if (!Dm.SavingThrow(pl, Save.Death, 0, "disintegration")) Hurt(pl, Rng.Roll(8, 6), Elem.Force, caster, "disintegration ray");
                                    else Hurt(pl, Rng.Roll(2, 6), Elem.Force, caster, "disintegration ray");
                                }
                                return true;
                            }
                            if (M.T[q.X, q.Y] is Tile.Wall or Tile.DoorClosed or Tile.DoorLocked or Tile.DoorSecret or Tile.Statue or Tile.Pillar)
                            {
                                M.T[q.X, q.Y] = Tile.Corridor;
                                Anim.Bolt(shown, '=', sd.C, true);
                                Dm.Msg("{green}The stone vanishes in a puff of dust.{/}");
                                return true;
                            }
                            if (M.T[q.X, q.Y] == Tile.PermWall) break;
                        }
                        Anim.Bolt(shown, '=', sd.C, true);
                        return true;
                    }
                case SpellId.DeathSpell:
                    {
                        var cells = BallCells(target, 3);
                        Anim.Burst(target, cells, sd.C, '·', 0.4f);
                        int pool = Rng.Roll(4, 8) + lvl;
                        foreach (var m in ActorsIn(cells).OfType<Monster>().Where(m => !m.Ally && !m.IsUndead && m.HdLevel < 9 && !m.Def.Has(MF.Boss)).OrderBy(m => m.HdLevel).ToList())
                        {
                            int cost = Math.Max(1, m.HdLevel);
                            if (pool < cost) break;
                            pool -= cost;
                            Dm.Msg(m.TheCap + " drops dead.");
                            Combat.Kill(m, caster);
                        }
                        return true;
                    }
                case SpellId.Globe:
                    caster.Add(Status.Globe, 10 + lvl);
                    Dm.Say(caster, "{magic}A shimmering globe surrounds you.{/}", "");
                    return true;
                case SpellId.ChainLightning:
                    {
                        var first = M.ActorAt(target) ?? FirstInPath(caster, target, sd.Range, out _);
                        if (first == null || first == caster) return true;
                        var hitList = new List<Actor> { first };
                        var cur = first;
                        int dice = Math.Min(12, lvl);
                        var from = caster.Pos;
                        while (cur != null && dice > 0)
                        {
                            Anim.Bolt(Path.Line(from, cur.Pos).Skip(1).ToList(), '*', sd.C, true, 0.012f);
                            int d = Rng.Roll(dice, 6);
                            if (Dm.SavingThrow(cur, Save.Spell, 0, "lightning")) d /= 2;
                            Hurt(cur, d, Elem.Elec, caster, "chain lightning");
                            from = cur.Pos;
                            dice--;
                            var curPos = cur.Pos;
                            cur = M.Monsters.Where(m => !m.Dead && !hitList.Contains(m) && Path.Cheb(m.Pos, curPos) <= 4 && (!byPlayer || !m.Ally) && Fov.Los(M, curPos, m.Pos)).OrderBy(m => Path.Cheb(m.Pos, curPos)).FirstOrDefault();
                            if (cur != null) hitList.Add(cur);
                        }
                        Synth.Play(Sfx.Thunder, 0.6f);
                        return true;
                    }
                case SpellId.FingerOfDeath:
                    {
                        var t = M.ActorAt(target);
                        if (t == null || t == caster) return true;
                        Anim.Flash(t.Pos, sd.C, 0.5f);
                        if (t is Monster m)
                        {
                            if (m.IsUndead || m.Def.Has(MF.Boss) || Dm.SavingThrow(m, Save.Death, 0, "")) { Dm.Msg(m.TheCap + " shudders but survives."); Combat.Damage(m, Rng.Roll(3, 8) + lvl, Elem.Necro, caster, "finger of death"); }
                            else { Dm.Msg("{purple}" + m.TheCap + " clutches its heart and dies!{/}"); Combat.Kill(m, caster); }
                        }
                        else if (t is Player pl)
                        {
                            if (pl.Wearing("a_scarab")) { Dm.Msg("{gold}Your scarab absorbs the death magic!{/}"); return true; }
                            if (!Dm.SavingThrow(pl, Save.Death, 0, "death magic"))
                            {
                                Dm.Msg("{bad}The Referee points at you. Your heart stops.{/}", true);
                                Combat.Damage(pl, pl.Hp + 50, Elem.Necro, caster, "the Finger of Death");
                            }
                            else Hurt(pl, Rng.Roll(2, 8), Elem.Necro, caster, "finger of death");
                        }
                        return true;
                    }
                case SpellId.PowerWordStun:
                    {
                        var t = M.ActorAt(target);
                        if (t == null || t == caster) return true;
                        int t2 = t.Hp <= 30 ? Rng.Roll(4, 4) : t.Hp <= 60 ? Rng.Roll(2, 4) : t.Hp <= 90 ? Rng.D(4) : 0;
                        if (t2 == 0) { Dm.Msg(NameCap(t) + " is too mighty to be stunned."); return true; }
                        t.Add(Status.Stunned, t2);
                        Dm.Say(t, "{bad}You are stunned!{/}", t.TheCap + " reels, stunned!");
                        return true;
                    }
                case SpellId.IncendiaryCloud:
                    MakeCloud(target, 2, Elem.Fire, 6, sd.C);
                    Explosion(target, 2, new Dice(4, 6), Elem.Fire, caster, "incendiary cloud", true);
                    return true;
                case SpellId.MassCharm:
                    foreach (var m in ActorsIn(BallCells(target, 3)).OfType<Monster>().Where(m => !m.Ally && !m.Def.Has(MF.Mindless) && !m.Def.Has(MF.Boss) && !m.IsUndead).ToList())
                    {
                        if (Dm.SavingThrow(m, Save.Spell, -2, "")) continue;
                        m.Ally = true;
                        m.Peaceful = false;
                        m.Add(Status.Charmed, 500);
                        Dm.Msg("{pink}" + m.TheCap + " is charmed!{/}");
                    }
                    return true;
                case SpellId.MeteorSwarm:
                    {
                        Synth.Play(Sfx.Thunder);
                        var spots = new List<Point> { target };
                        for (int i = 0; i < 3; i++) spots.Add(new Point(target.X + Rng.Range(-3, 3), target.Y + Rng.Range(-2, 2)));
                        foreach (var s in spots)
                        {
                            if (!Map.InB(s)) continue;
                            Explosion(s, 1, new Dice(6, 6), Elem.Fire, caster, "meteor", true);
                        }
                        return true;
                    }
                case SpellId.PowerWordKill:
                    {
                        var t = M.ActorAt(target);
                        if (t is Monster m && m.Hp <= 60 && !m.Def.Has(MF.Boss)) { Dm.Msg("{purple}You speak the word. " + m.TheCap + " dies.{/}"); Combat.Kill(m, caster); }
                        else Dm.Msg("The word echoes, but nothing dies.");
                        return true;
                    }
                case SpellId.TimeStop:
                    p.Energy += Dm.ActCost * Rng.Range(3, 5);
                    Dm.Msg("{cyan}The world freezes. Dust hangs in the air. Only you move.{/}");
                    return true;
                case SpellId.CureLight:
                    HealSpell(caster, Rng.Roll(1, 8), "slightly");
                    return true;
                case SpellId.CureSerious:
                    HealSpell(caster, Rng.Roll(2, 8) + 1, "");
                    return true;
                case SpellId.CureCritical:
                    HealSpell(caster, Rng.Roll(3, 8) + 3, "much");
                    caster.Remove(Status.Stoning);
                    return true;
                case SpellId.Heal:
                    caster.Hp = Math.Max(caster.Hp, caster.MaxHp - Rng.D(4));
                    foreach (var s in new[] { Status.Blind, Status.Confused, Status.Diseased, Status.Poisoned, Status.Stoning, Status.Sliming, Status.Feared, Status.Weakened, Status.Bleeding, Status.Stunned }) caster.Remove(s);
                    Synth.Play(Sfx.Heal);
                    Dm.Say(caster, "{holy}A wave of divine warmth knits you whole.{/}", caster.TheCap + " is healed.");
                    return true;
                case SpellId.Bless:
                    caster.Add(Status.Blessed, 30 + lvl * 3);
                    foreach (var m in M.Monsters.Where(m => m.Ally && Path.Cheb(m.Pos, caster.Pos) <= 6)) m.Add(Status.Blessed, 30 + lvl * 3);
                    Dm.Say(caster, "{holy}You feel blessed.{/}", "");
                    return true;
                case SpellId.Command:
                    {
                        var t = M.ActorAt(target);
                        if (t is Monster m)
                        {
                            if (m.Def.Has(MF.Mindless) || m.HdLevel >= 6 && Dm.SavingThrow(m, Save.Spell, 0, "") || m.Def.Has(MF.Boss)) { Dm.Msg(m.TheCap + " ignores your command."); return true; }
                            m.Add(Status.Paralyzed, Rng.Range(1, 2));
                            Dm.Msg("You speak a single word: {holy}\"GROVEL!\"{/} " + m.TheCap + " grovels.");
                        }
                        else if (t is Player pl && !Dm.SavingThrow(pl, Save.Spell, 0, "command"))
                        {
                            Dm.Msg("{bad}A compelling song drags at you! You stumble toward " + caster.The + ".{/}", true);
                            var step = Path.Step(pl.Pos, caster.Pos);
                            var to = new Point(pl.Pos.X + step.X, pl.Pos.Y + step.Y);
                            if (M.Free(to)) pl.Pos = to;
                            pl.Energy -= 60;
                        }
                        return true;
                    }
                case SpellId.DetectEvil:
                    caster.Add(Status.Telepathy, 15);
                    Dm.Msg("{holy}You sense the presence of " + M.Monsters.Count(m => m.IsEvil) + " evil creatures on this level.{/}");
                    return true;
                case SpellId.Sanctuary:
                    caster.Add(Status.Sanctuary, 10 + lvl * 2);
                    Dm.Say(caster, "{holy}A sense of holy calm surrounds you.{/}", "");
                    return true;
                case SpellId.Silence:
                    foreach (var a in ActorsIn(BallCells(target, 2)).ToList()) a.Add(Status.Silenced, 20 + lvl);
                    Anim.Burst(target, BallCells(target, 2), sd.C, ' ', 0.3f);
                    Dm.Msg("{dim}A bubble of absolute silence falls over the area.{/}");
                    return true;
                case SpellId.SpiritualHammer:
                    {
                        var h = SummonAt("hammer", caster.Pos, true, 10 + lvl * 2);
                        if (h != null) Dm.Msg("{holy}A hammer of golden force appears, spinning at your side.{/}");
                        return true;
                    }
                case SpellId.SlowPoison:
                case SpellId.NeutralizePoison:
                    caster.Remove(Status.Poisoned);
                    if (id == SpellId.NeutralizePoison) caster.Remove(Status.Weakened);
                    Dm.Msg("{good}The poison is purged from your blood.{/}");
                    return true;
                case SpellId.FindTraps:
                    {
                        int n = 0;
                        foreach (var t in M.Traps.Values) if (!t.Found && Path.Cheb(t.Pos, caster.Pos) <= 12) { t.Found = true; M.Remember(t.Pos.X, t.Pos.Y); M.Seen[t.Pos.X, t.Pos.Y] = true; n++; }
                        Dm.Msg(n > 0 ? "{holy}Your god reveals " + n + " hidden trap" + (n > 1 ? "s" : "") + ".{/}" : "You sense no hidden traps nearby.");
                        return true;
                    }
                case SpellId.ResistFire:
                    caster.Add(Status.ResFire, 10 * lvl + 30);
                    Dm.Msg("{orange}You feel cool despite the heat.{/}");
                    return true;
                case SpellId.Augury:
                    Augury();
                    return true;
                case SpellId.CureDisease:
                    foreach (var s in new[] { Status.Diseased, Status.Sliming }) caster.Remove(s);
                    Dm.Msg("{good}You feel cleansed of disease.{/}");
                    return true;
                case SpellId.Prayer:
                    caster.Add(Status.Prayer, 10 + lvl);
                    foreach (var m in M.Monsters.Where(m => Path.Cheb(m.Pos, caster.Pos) <= 6))
                        m.Add(Status.Prayer, 10 + lvl);
                    Dm.Msg("{holy}You lead a fervent prayer. Allies take heart; enemies falter.{/}");
                    return true;
                case SpellId.RemoveCurse:
                    {
                        int n = 0;
                        foreach (var it in p.Eq.Values.Concat(p.Pack).Distinct().Where(i => i.Cursed)) { it.Cursed = false; it.Known = true; n++; }
                        Dm.Msg(n > 0 ? "{holy}You feel as if someone is helping you.{/}" : "You feel a gentle reassurance.");
                        return true;
                    }
                case SpellId.CreateFood:
                    {
                        int n = 1 + lvl / 5;
                        var f = Item.Make("std_ration", n);
                        Actions.GiveOrDrop(f);
                        Dm.Msg("{holy}Simple, wholesome food appears before you.{/}");
                        return true;
                    }
                case SpellId.SpeakWithDead:
                    {
                        var c = M.Items.FirstOrDefault(i => i.Def.Kind == IK.Corpse && Path.Cheb(i.Pos, caster.Pos) <= 2);
                        if (c == null) { Dm.Msg("There are no dead nearby to speak with."); return true; }
                        Dm.Msg("{purple}" + Lore.DeathTale() + "{/}");
                        if (M.Depth < Rules.MaxDepth)
                        {
                            M.Seen[M.Down.X, M.Down.Y] = true;
                            M.MemCh[M.Down.X, M.Down.Y] = '>';
                            M.MemFg[M.Down.X, M.Down.Y] = Pal.White;
                            Dm.Msg("{purple}It also tells you, with some bitterness, where the stairs down are.{/}");
                        }
                        return true;
                    }
                case SpellId.GlyphWarding:
                    if (M.T[caster.Pos.X, caster.Pos.Y] is Tile.Floor or Tile.Corridor) { M.T[caster.Pos.X, caster.Pos.Y] = Tile.Glyph; Dm.Msg("{holy}You inscribe a burning glyph on the floor.{/}"); }
                    else Dm.Msg("You cannot inscribe a glyph here.");
                    return true;
                case SpellId.SticksToSnakes:
                    {
                        int n = Rng.D(4) + lvl / 4;
                        for (int i = 0; i < n; i++) SummonAt("snake", caster.Pos, true, 20 + lvl * 2);
                        Dm.Msg("{green}Twigs and splinters writhe and become " + n + " hissing snakes!{/}");
                        return true;
                    }
                case SpellId.Divination:
                    M.RevealMap(false);
                    Dm.Msg("{holy}The layout of the level unfolds before your mind's eye.{/}");
                    return true;
                case SpellId.FlameStrike:
                    {
                        var cells = BallCells(target, 1);
                        Synth.Play(Sfx.Fire);
                        Anim.Burst(target, cells, new Color(255, 200, 80), '|', 0.45f);
                        foreach (var a in ActorsIn(cells).Where(a => a != caster || !byPlayer).ToList())
                        {
                            int d = Rng.Roll(6, 8);
                            if (Dm.SavingThrow(a, Save.Spell, 0, "flame strike")) d /= 2;
                            Hurt(a, d, Elem.Fire, caster, "flame strike");
                        }
                        BurnCorpses(cells, Elem.Fire);
                        return true;
                    }
                case SpellId.InsectPlague:
                    for (int i = 0; i < Rng.Range(2, 4); i++) SummonAt("insects", caster.Pos, true, 25 + lvl);
                    Dm.Msg("{yellow}A droning cloud of insects pours forth!{/}");
                    return true;
                case SpellId.DispelEvil:
                    foreach (var m in ActorsIn(BallCells(target, 3)).OfType<Monster>().Where(m => m.IsEvil && !m.Def.Has(MF.Boss)).ToList())
                    {
                        if ((m.SummonTurns > 0 || m.Def.G == '&' || m.IsUndead) && !Dm.SavingThrow(m, Save.Spell, 0, "")) { Dm.Msg("{holy}" + m.TheCap + " is banished!{/}"); m.Inv.Clear(); Combat.Kill(m, caster); }
                        else { m.Fleeing = true; m.FleeTurns = 10; }
                    }
                    Anim.Burst(target, BallCells(target, 3), sd.C, '+', 0.4f);
                    return true;
                case SpellId.Commune:
                    M.RevealMap(true);
                    caster.Add(Status.Telepathy, 40);
                    Dm.Msg("{holy}" + p.Deity + " shows you everything on this level.{/}");
                    return true;
                case SpellId.TrueSeeing:
                    caster.Add(Status.SeeInvis, 200);
                    foreach (var c in BallCells(caster.Pos, 10))
                    {
                        if (M.T[c.X, c.Y] == Tile.DoorSecret) M.T[c.X, c.Y] = Tile.DoorClosed;
                        if (M.Traps.TryGetValue(c, out var t)) t.Found = true;
                    }
                    foreach (var q in M.Neighbors(caster.Pos)) if (M.T[q.X, q.Y] == Tile.DoorSecret) M.T[q.X, q.Y] = Tile.DoorClosed;
                    for (int x = 0; x < Map.W; x++) for (int y = 0; y < Map.H; y++) if (Dm.Los[x, y] && M.T[x, y] == Tile.DoorSecret) M.T[x, y] = Tile.DoorClosed;
                    Dm.Msg("{holy}Your eyes see true. Illusions and secrets fall away.{/}");
                    return true;
                case SpellId.BladeBarrier:
                    caster.Add(Status.BladeBarrier, 10 + lvl);
                    Dm.Msg("{white}A whirling wall of blades springs up around you!{/}");
                    return true;
                case SpellId.WordOfRecall:
                    {
                        int dest = M.Depth > 1 ? 1 : p.DeepestDepth;
                        if (dest == M.Depth) { Dm.Msg("Your god sees no need to move you."); return true; }
                        Dm.Msg("{holy}You are snatched away in a flash of light!{/}");
                        Dm.EnterLevel(dest, dest == 1 ? Dm.Arrival.Down : Dm.Arrival.Teleport);
                        return true;
                    }
                case SpellId.FindPath:
                    {
                        if (M.Depth >= Rules.MaxDepth) { Dm.Msg("The path ends here. It always did."); return true; }
                        var dist = Path.DistMap(M, new[] { M.Down }, (x, y) => M.Walkable(x, y) || M.T[x, y] is Tile.DoorClosed or Tile.DoorLocked or Tile.DoorSecret);
                        var cur = caster.Pos;
                        int guard = 0;
                        while (cur != M.Down && guard++ < 400)
                        {
                            M.Seen[cur.X, cur.Y] = true;
                            M.Remember(cur.X, cur.Y);
                            Point best = cur;
                            foreach (var q in M.Neighbors(cur)) if (dist[q.X, q.Y] < dist[best.X, best.Y]) best = q;
                            if (best == cur) break;
                            cur = best;
                        }
                        M.Seen[M.Down.X, M.Down.Y] = true;
                        M.Remember(M.Down.X, M.Down.Y);
                        Dm.Msg("{holy}A path of light traces the way to the stairs below.{/}");
                        return true;
                    }
                case SpellId.Harm:
                    {
                        var t = M.ActorAt(target);
                        if (t is Monster m && m != caster)
                        {
                            if (m.Def.Has(MF.Boss)) { Combat.Damage(m, Rng.Roll(4, 8), Elem.Necro, caster, "harm"); return true; }
                            int left = Rng.D(4);
                            if (m.Hp > left) { Dm.Msg("{purple}" + m.TheCap + " withers at your touch.{/}"); m.Hp = left; Combat.WakeUp(m); }
                        }
                        return true;
                    }
                case SpellId.HolyWord:
                    {
                        var cells = BallCells(caster.Pos, 6);
                        Anim.Burst(caster.Pos, cells, sd.C, ' ', 0.5f);
                        Synth.Play(Sfx.Thunder);
                        foreach (var m in ActorsIn(cells).OfType<Monster>().Where(m => m.IsEvil && !m.Ally).ToList())
                        {
                            int hd = m.HdLevel;
                            if (m.Def.Has(MF.Boss)) { m.Add(Status.Stunned, 2); Dm.Msg(m.TheCap + " reels at the Holy Word!"); }
                            else if (hd < 4) { Dm.Msg("{holy}" + m.TheCap + " is struck dead by the Holy Word!{/}"); Combat.Kill(m, caster); }
                            else if (hd < 8) { m.Add(Status.Paralyzed, Rng.Roll(2, 6)); Dm.Msg(m.TheCap + " is paralyzed by the Holy Word."); }
                            else if (hd < 12) { m.Add(Status.Stunned, Rng.D(4) + 1); Dm.Msg(m.TheCap + " is stunned by the Holy Word."); }
                            else m.Add(Status.Slowed, 20);
                        }
                        return true;
                    }
                case SpellId.Earthquake:
                    {
                        Synth.Play(Sfx.Thunder);
                        Dm.Msg("{brown}The ground heaves and groans!{/}", true);
                        foreach (var q in BallCells(caster.Pos, 7))
                        {
                            var a = M.ActorAt(q);
                            if (a != null && a != caster) Hurt(a, Rng.Roll(3, 10), Elem.Force, caster, "earthquake");
                        }
                        for (int x = caster.Pos.X - 7; x <= caster.Pos.X + 7; x++)
                            for (int y = caster.Pos.Y - 7; y <= caster.Pos.Y + 7; y++)
                            {
                                if (!Map.InB(x, y) || x <= 0 || y <= 0 || x >= Map.W - 1 || y >= Map.H - 1) continue;
                                if (M.T[x, y] == Tile.Wall && Rng.Chance(20)) M.T[x, y] = Tile.Rubble;
                                else if (M.T[x, y] == Tile.Floor && Rng.Chance(3) && M.ActorAt(new Point(x, y)) == null && M.Depth < Rules.MaxDepth) M.T[x, y] = Tile.Chasm;
                            }
                        return true;
                    }
                case SpellId.Restoration:
                    {
                        bool any = false;
                        if (caster is Player pl)
                        {
                            while (pl.Level < pl.MaxLevelReached)
                            {
                                pl.Level++;
                                pl.Xp = Math.Max(pl.Xp, Rules.XpFor(pl.Class, pl.Level));
                                any = true;
                            }
                            for (int i = 0; i < 6; i++) if (pl.Drained[i] > 0) { pl.Drained[i] = 0; any = true; }
                            pl.Remove(Status.Weakened);
                        }
                        Dm.Msg(any ? "{holy}Your lost vitality floods back into you!{/}" : "{holy}You feel restored, though nothing was lost.{/}");
                        return true;
                    }
                case SpellId.Regenerate:
                    caster.Add(Status.Regenerating, 200 + lvl * 10);
                    Dm.Msg("{holy}Your flesh tingles with renewing power.{/}");
                    return true;
                case SpellId.CauseLight:
                case SpellId.CauseSerious:
                    {
                        var t = M.ActorAt(target);
                        if (t == null || t == caster) return true;
                        int d = id == SpellId.CauseLight ? Rng.Roll(1, 8) : Rng.Roll(2, 8) + 1;
                        Anim.Flash(t.Pos, sd.C, 0.3f);
                        Hurt(t, d, Elem.Necro, caster, "unholy wound");
                        return true;
                    }
                case SpellId.FaerieFire:
                    {
                        var t = M.ActorAt(target);
                        if (t == null) return true;
                        t.Remove(Status.Invisible);
                        t.Remove(Status.Hidden);
                        Anim.Flash(t.Pos, sd.C, 0.6f);
                        Dm.Say(t, "{purple}You are outlined in violet flame!{/}", t.TheCap + " is outlined in violet flame.");
                        return true;
                    }
                case SpellId.MindBlast:
                    {
                        var cells = ConeCells(caster.Pos, target, 6);
                        Anim.Burst(caster.Pos, cells, sd.C, '~', 0.4f);
                        foreach (var a in ActorsIn(cells).Where(a => a != caster).ToList())
                        {
                            if (a is Player pl)
                            {
                                if (pl.Wearing("r_mind")) { Dm.Msg("Your ring repels the psionic blast."); continue; }
                                if (!Dm.SavingThrow(pl, Save.Spell, 0, "mind blast")) { pl.Add(Status.Stunned, Rng.D(3) + 1); Dm.Msg("{bad}A wave of psionic force crashes through your mind! You are stunned.{/}", true); }
                            }
                            else if (a is Monster mm && !mm.Def.Has(MF.Mindless) && !Dm.SavingThrow(mm, Save.Spell, 0, "")) mm.Add(Status.Stunned, 2);
                        }
                        return true;
                    }
                case SpellId.SummonUndead:
                    {
                        string[] pool = M.Depth >= 12 ? new[] { "wight", "wraith", "spectre", "mummy" } : M.Depth >= 7 ? new[] { "ghoul", "wight", "zombie" } : new[] { "skeleton", "zombie", "ghoul" };
                        int n = Rng.Range(2, 3);
                        for (int i = 0; i < n; i++)
                        {
                            var m = SummonAt(Rng.Pick(pool), caster.Pos, false, 0);
                            if (m != null) { m.Aware = 50; m.LastSeen = p.Pos; }
                        }
                        if (Dm.CanSeeMonster((Monster)caster) || true) Dm.Msg("{purple}The dead claw their way up through the floor!{/}", true);
                        return true;
                    }
                case SpellId.EyeRays:
                    EyeRay((Monster)caster);
                    return true;
            }
            return true;
        }

        static void EyeRay(Monster b)
        {
            var p = P;
            var line = Path.Line(b.Pos, p.Pos).Skip(1).ToList();
            int r = Rng.D(10);
            string[] names = { "", "charm", "sleep", "telekinesis", "slow", "flesh-to-stone", "disintegration", "fear", "wounding", "death", "paralysis" };
            var col = new[] { Color.White, new Color(255, 150, 200), new Color(160, 160, 255), new Color(200, 200, 200), new Color(120, 150, 200), new Color(160, 160, 150), new Color(120, 255, 120), new Color(150, 80, 200), new Color(200, 40, 40), new Color(60, 0, 80), new Color(220, 220, 120) }[r];
            Anim.Bolt(line, '*', col, true, 0.014f);
            Dm.Msg("{magic}The beholder's eye stalk fires a " + names[r] + " ray!{/}");
            switch (r)
            {
                case 1:
                    if (!Dm.SavingThrow(p, Save.Spell, 0, "charm") && !p.Wearing("r_mind")) { Dm.Msg("{bad}You stand gaping in admiration.{/}"); p.Add(Status.Paralyzed, 2); }
                    break;
                case 2:
                    if (p.Race == Race.Elf && Rng.Chance(90)) break;
                    if (!Dm.SavingThrow(p, Save.Spell, 0, "sleep")) { Dm.Msg("{bad}You fall asleep!{/}", true); p.Add(Status.Asleep, Rng.Range(2, 5)); }
                    break;
                case 3:
                    {
                        var d = Path.Step(b.Pos, p.Pos);
                        for (int i = 0; i < 3; i++)
                        {
                            var n = new Point(p.Pos.X + d.X, p.Pos.Y + d.Y);
                            if (!M.Free(n)) { Combat.Damage(p, Rng.D(6), Elem.Force, b, "being slammed into a wall"); break; }
                            p.Pos = n;
                        }
                        Dm.Msg("{bad}You are hurled backward!{/}");
                        break;
                    }
                case 4:
                    if (!Dm.SavingThrow(p, Save.Spell, 0, "slow")) { p.Add(Status.Slowed, 10); Dm.Msg("{bad}You slow down.{/}"); }
                    break;
                case 5:
                    if (!Dm.SavingThrow(p, Save.Petrify, 0, "petrification")) { p.Add(Status.Stoning, 6); Dm.Msg("{bad}You begin turning to stone!{/}", true); }
                    break;
                case 6:
                    if (!Dm.SavingThrow(p, Save.Death, 0, "disintegration")) Combat.Damage(p, Rng.Roll(6, 6), Elem.Force, b, "a disintegration ray");
                    break;
                case 7:
                    if (!Dm.SavingThrow(p, Save.Spell, 0, "fear")) { p.Add(Status.Feared, 6); Dm.Msg("{bad}You are terrified!{/}"); }
                    break;
                case 8:
                    Combat.Damage(p, Rng.Roll(2, 8) + 3, Elem.Necro, b, "a wounding ray");
                    break;
                case 9:
                    if (p.Wearing("a_scarab")) { Dm.Msg("{gold}Your scarab absorbs the death ray!{/}"); break; }
                    if (!Dm.SavingThrow(p, Save.Death, 0, "death ray")) Combat.Damage(p, Rng.Roll(8, 6), Elem.Necro, b, "a death ray");
                    break;
                case 10:
                    if (p.Wearing("r_free")) break;
                    if (!Dm.SavingThrow(p, Save.Death, 0, "paralysis")) { p.Add(Status.Paralyzed, Rng.Range(2, 4)); Dm.Msg("{bad}You are paralyzed!{/}", true); }
                    break;
            }
        }

        static void Augury()
        {
            int threat = M.Monsters.Where(m => m.Hostile).Sum(m => m.Def.Xp);
            int lvl = P.Level;
            string weal = threat < lvl * 20 ? "weal" : threat < lvl * 60 ? "weal and woe" : "woe";
            Dm.Msg("{holy}Your god whispers: \"" + weal.ToUpper() + ".\"{/}");
            if (M.Shops.Count > 0) Dm.Msg("{holy}\"A merchant waits on this level.\"{/}");
            if (M.Rooms.Any(r => r.Theme == "vault")) Dm.Msg("{holy}\"Gold lies sealed in stone nearby.\"{/}");
            for (int x = 0; x < Map.W; x++) for (int y = 0; y < Map.H; y++) if (M.T[x, y] == Tile.OracleDie) { Dm.Msg("{holy}\"The great die waits to be rolled.\"{/}"); x = Map.W; break; }
            var best = M.Items.Where(i => i.Def.Artifact).FirstOrDefault();
            if (best != null) Dm.Msg("{holy}\"A blade of legend lies on this level.\"{/}");
        }

        static void HealSpell(Actor a, int amt, string how)
        {
            int before = a.Hp;
            a.Hp = Math.Min(a.MaxHp, a.Hp + amt);
            Synth.Play(Sfx.Heal);
            Dm.Say(a, "{good}You feel " + (how.Length > 0 ? how + " " : "") + "better. (+" + (a.Hp - before) + "){/}", a.TheCap + " looks healthier.");
        }

        public static void MakeCloud(Point c, int r, Elem e, int turns, Color col)
        {
            var cells = BallCells(c, r);
            foreach (var q in cells)
            {
                M.Cloud[q.X, q.Y] = turns;
                M.CloudE[q.X, q.Y] = e;
            }
            Anim.Burst(c, cells, col, '░', 0.4f);
        }

        public static void LightningBolt(Actor caster, Point target, int len, Dice dmg, string cause)
        {
            var hits = new HashSet<Actor>();
            var path = new List<Point>();
            var from = caster.Pos;
            var to = target;
            int remaining = len;
            for (int bounce = 0; bounce < 3 && remaining > 0; bounce++)
            {
                var seg = Path.Ray(M, from, to, remaining, false);
                if (seg.Count == 0) break;
                path.AddRange(seg);
                remaining -= seg.Count;
                var end = seg[seg.Count - 1];
                var dir = new Point(Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
                var next = new Point(end.X + dir.X, end.Y + dir.Y);
                if (remaining <= 0 || !M.BlocksSight(next.X, next.Y)) break;
                to = new Point(end.X - (to.X - from.X), end.Y - (to.Y - from.Y));
                from = end;
                if (bounce == 0) Dm.Msg("{cyan}The bolt rebounds!{/}");
            }
            Synth.Play(Sfx.Thunder, 0.5f);
            Anim.Bolt(path, '*', new Color(220, 230, 255), true, 0.01f);
            foreach (var q in path)
            {
                var a = M.ActorAt(q);
                if (a == null || hits.Contains(a)) continue;
                hits.Add(a);
                int d = dmg.Roll();
                if (Dm.SavingThrow(a, Save.Spell, 0, "lightning")) d /= 2;
                Hurt(a, d, Elem.Elec, caster, cause);
            }
        }

        public static void Polymorph(Monster m)
        {
            if (m.Def.Has(MF.Boss) || m.Def.Has(MF.Shopkeeper)) { Dm.Msg(m.TheCap + " resists the transformation."); return; }
            if (!m.Def.Has(MF.Morph) && Dm.SavingThrow(m, Save.Spell, 0, "")) { Dm.Msg(m.TheCap + " shimmers, but resists."); return; }
            var def = Spawn.PickMonster(Math.Max(1, Dm.Map.Depth + Rng.Range(-3, 3)), d => d.Freq > 0 && d != m.Def);
            if (def == null) return;
            string old = m.The;
            m.Def = def;
            m.MaxHp = Math.Max(1, def.HalfHD ? Rng.D(4) : Rng.Roll(Math.Max(1, def.HD), 8) + def.HpPlus);
            m.Hp = m.MaxHp;
            m.Disguised = false;
            if (Dm.Map.Vis[m.Pos.X, m.Pos.Y]) Dm.Msg("{magic}" + char.ToUpper(old[0]) + old.Substring(1) + " twists and becomes " + Item.Article(def.Name) + "!{/}");
        }

        public static void Teleport(Actor a, Point? dest)
        {
            TeleportActor(a, dest);
        }

        public static void TeleportActor(Actor a, Point? dest)
        {
            var from = a.Pos;
            Point to = dest ?? M.RandomFloor(q => M.Free(q) && Path.Cheb(q, a.Pos) > 8 && M.T[q.X, q.Y] != Tile.Chasm && M.ShopAt(q) == null);
            if (!M.Free(to) && a.Pos != to)
            {
                var n = M.NearestFree(to, 3);
                if (n == null) return;
                to = n.Value;
            }
            Anim.Flash(from, new Color(200, 160, 255), 0.3f);
            a.Pos = to;
            Anim.Flash(to, new Color(200, 160, 255), 0.3f);
            if (a is Player p)
            {
                if (Dm.Engulfer != null) { Dm.Engulfer = null; p.Remove(Status.Engulfed); p.Remove(Status.Swallowed); }
                p.Holder = null;
                p.Remove(Status.Held);
                Dm.Msg("{magic}You feel a wrenching sensation as space folds around you.{/}");
                Synth.Play(Sfx.Zap);
            }
            else if (a is Monster m && Dm.Map.Vis[from.X, from.Y]) Dm.Msg(m.TheCap + " vanishes!");
        }

        public static void BladeBarrierTick()
        {
            var p = P;
            if (!p.Has(Status.BladeBarrier)) return;
            foreach (var m in M.Monsters.Where(m => !m.Dead && m.Hostile && Path.Cheb(m.Pos, p.Pos) == 1).ToList())
            {
                Dm.Msg(m.TheCap + " is shredded by your blade barrier!");
                Combat.Damage(m, Rng.Roll(4, 6), Elem.Phys, p, "blade barrier");
            }
        }
    }
}
