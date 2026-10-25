using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public class LogMsg
    {
        public string Text;
        public int Count = 1;
        public long Turn;
    }

    public class DieRoll
    {
        public int Sides, Result, Mod, Target;
        public string Label;
        public bool? Success;
        public bool Crit, Fumble;
        public float Born;
        public int Alt;
    }

    public enum SeenKind { No, Visible, Infra, Esp, Faint }

    public static partial class Dm
    {
        public const int ActCost = 120;
        public static Player Player;
        public static Map Map;
        public static Dictionary<int, Map> Levels = new Dictionary<int, Map>();
        public static long Turn;
        public static int Tick;
        public static readonly IdentTable Ident = new IdentTable();
        public static readonly List<LogMsg> Log = new List<LogMsg>();
        public static int LogMark;
        public static readonly List<DieRoll> Dice = new List<DieRoll>();
        public static bool GameOver, Won, Retired;
        public static string KilledBy = "";
        public static bool Interrupted;
        public static Monster Engulfer;
        public static readonly HashSet<Monster> KnownHostiles = new HashSet<Monster>();
        public static int WanderCounter;
        public static bool HeartTaken;
        public static long StartTicks;
        public static List<string> Achievements = new List<string>();
        public static int Score;
        public static int OracleUsed;

        public static int Depth => Map?.Depth ?? 0;

        public static void NewGame(Player p)
        {
            Player = p;
            Levels.Clear();
            Log.Clear();
            Dice.Clear();
            KnownHostiles.Clear();
            Achievements.Clear();
            Spawn.ArtifactsMade.Clear();
            Actions.Visited.Clear();
            foreach (var it in p.Pack) if (it.Def.Unknown) Ident.Learn(it.Def.Id);
            Turn = 0;
            Tick = 0;
            GameOver = Won = Retired = false;
            KilledBy = "";
            HeartTaken = false;
            Engulfer = null;
            Anim.Clear();
            StartTicks = DateTime.Now.Ticks;
            EnterLevel(1, Arrival.Start);
            Msg("{hi}Welcome, " + p.PName + " the " + p.RaceInfo.Name + " " + p.ClassInfo.Name + ".{/}");
            Msg("Somewhere below waits the {pink}Icosahedral Heart{/}, and the Referee who guards it. Press {yellow}?{/} for help.");
        }

        public enum Arrival { Start, Down, Up, Fall, Teleport }

        public static void EnterLevel(int depth, Arrival how)
        {
            if (Map != null)
            {
                Map.Monsters.RemoveAll(m => m.Ally && Path.Cheb(m.Pos, Player.Pos) > 1);
            }
            var followers = Map == null ? new List<Monster>() : Map.Monsters.Where(m => !m.Dead && (m.Ally || (m.Hostile && m.Aware > 0 && !m.Sleeping && m.CanAct && !m.Def.Has(MF.Immobile))) && Path.Cheb(m.Pos, Player.Pos) <= 1 && !m.Def.Has(MF.Shopkeeper)).ToList();
            if (Map != null) foreach (var f in followers) Map.Monsters.Remove(f);
            bool fresh = !Levels.TryGetValue(depth, out var m);
            if (fresh)
            {
                m = Gen.Generate(depth);
                Levels[depth] = m;
            }
            Map = m;
            Engulfer = null;
            Player.Remove(Status.Engulfed);
            Player.Remove(Status.Swallowed);
            Player.Remove(Status.Held);
            Point pos = how switch
            {
                Arrival.Down => m.Up,
                Arrival.Start => m.Up,
                Arrival.Up => m.Depth >= Rules.MaxDepth ? m.Up : m.Down,
                _ => m.RandomFloor(q => m.MonsterAt(q) == null && m.T[q.X, q.Y] != Tile.Chasm),
            };
            var free = m.NearestFree(pos, 8);
            Player.Pos = free ?? pos;
            foreach (var f in followers)
            {
                var fp = m.NearestFree(Player.Pos, 3);
                if (fp == null) continue;
                f.Pos = fp.Value;
                f.Energy = 0;
                m.Monsters.Add(f);
            }
            if (depth > Player.DeepestDepth) Player.DeepestDepth = depth;
            Synth.Ambience(depth);
            Anim.Clear();
            KnownHostiles.Clear();
            UpdateVision();
            m.ComputePlayerDist(Player);
            if (fresh || !m.Visited)
            {
                Msg("{gold}— Level " + depth + ": " + Rules.DepthNames[Math.Min(depth, Rules.DepthNames.Length - 1)] + " —{/}");
                if (m.Feeling != null) Msg("{dim}" + m.Feeling + "{/}");
            }
            m.Visited = true;
            if (HeartTaken && fresh == false && how == Arrival.Up && depth % 3 == 0)
            {
                Msg("{bad}The dungeon shudders. It knows what you carry.{/}");
                for (int i = 0; i < 2; i++) WanderingMonster(true);
            }
        }

        public static void Msg(string text, bool important = false)
        {
            if (important) Interrupted = true;
            if (Log.Count > 0 && Log[Log.Count - 1].Text == text && Log[Log.Count - 1].Turn >= Turn - 1)
            {
                Log[Log.Count - 1].Count++;
                Log[Log.Count - 1].Turn = Turn;
                return;
            }
            Log.Add(new LogMsg { Text = text, Turn = Turn });
            if (Log.Count > 600) { Log.RemoveRange(0, 100); LogMark = Math.Max(0, LogMark - 100); }
        }

        public static void Say(Actor a, string youText, string theyText)
        {
            if (a.IsPlayer) Msg(youText);
            else if (a is Monster m && CanSeeMonster(m)) Msg(theyText);
        }

        public static DieRoll AddDie(int sides, int result, int mod, int target, string label, bool? success, bool crit = false, bool fumble = false, int alt = 0)
        {
            var d = new DieRoll { Sides = sides, Result = result, Mod = mod, Target = target, Label = label, Success = success, Crit = crit, Fumble = fumble, Born = Ui.Time, Alt = alt };
            Dice.Add(d);
            if (Dice.Count > 9) Dice.RemoveAt(0);
            return d;
        }

        public static int PlayerD20(out int alt)
        {
            int a = Rng.D(20);
            alt = 0;
            if (FortuneState() > 0)
            {
                int b = Rng.D(20);
                alt = Math.Min(a, b);
                return Math.Max(a, b);
            }
            if (FortuneState() < 0)
            {
                int b = Rng.D(20);
                alt = Math.Max(a, b);
                return Math.Min(a, b);
            }
            return a;
        }

        public static int FortuneState()
        {
            if (!Player.Has(Status.Fortune)) return 0;
            return Math.Sign(Player.Pow(Status.Fortune));
        }

        public static bool SavingThrow(Actor a, Save s, int mod, string what)
        {
            int target = a.SaveTarget(s);
            int roll;
            int alt = 0;
            if (a is Player p)
            {
                roll = PlayerD20(out alt);
                if (s == Save.Spell) mod += Rules.WisSave(p.Stat(Stat.Wis));
                if (p.Has(Status.ProtEvil)) mod += 1;
            }
            else roll = Rng.D(20);
            bool ok = roll != 1 && (roll == 20 || roll + mod >= target);
            if (a.IsPlayer)
            {
                AddDie(20, roll, mod, target, "save vs " + SaveName(s), ok, roll == 20, roll == 1, alt);
                Msg("{dice}[save vs " + SaveName(s) + ": d20=" + roll + (mod != 0 ? (mod > 0 ? "+" : "") + mod : "") + " vs " + target + "]{/} " + (ok ? "{good}Saved!{/}" : "{bad}Failed!{/}"));
                if (roll == 20 || roll == 1) Synth.Play(Sfx.Dice);
            }
            return ok;
        }

        public static string SaveName(Save s) => s switch
        {
            Save.Death => "death/poison",
            Save.Petrify => "petrification",
            Save.Wand => "wands",
            Save.Breath => "breath",
            _ => "spells",
        };

        public static SeenKind Seen(Monster m)
        {
            if (m.Dead || Map == null) return SeenKind.No;
            var p = m.Pos;
            if (!Map.InB(p)) return SeenKind.No;
            bool invisible = (m.Def.Has(MF.Invis) || m.Has(Status.Invisible)) && !Player.Wearing("r_see_invis") && !Player.Has(Status.SeeInvis);
            if (m.Ally && Map.Vis[p.X, p.Y]) return SeenKind.Visible;
            if (Map.Vis[p.X, p.Y] && !invisible)
            {
                if (m.Def.Has(MF.Shadowy) && Map.Lum(p.X, p.Y) < 0.35f && Path.Cheb(p, Player.Pos) > 1) return SeenKind.No;
                if (m.Def.Has(MF.Transparent) && Path.Cheb(p, Player.Pos) > 3) return SeenKind.Faint;
                return SeenKind.Visible;
            }
            if (Player.Infravision && m.Def.Has(MF.Warm) && Los[p.X, p.Y] && Path.Cheb(p, Player.Pos) <= Player.RaceInfo.Infravision && !invisible) return SeenKind.Infra;
            bool esp = Player.Has(Status.ESP) || Player.Wearing("telepathy") || Player.Wearing("a_esp");
            if (esp && !m.Def.Has(MF.Mindless) && Path.Cheb(p, Player.Pos) <= 12) return SeenKind.Esp;
            if (Player.Has(Status.Telepathy)) return SeenKind.Esp;
            return SeenKind.No;
        }

        public static bool CanSeeMonster(Monster m)
        {
            var s = Seen(m);
            return s == SeenKind.Visible || s == SeenKind.Infra || s == SeenKind.Faint;
        }

        public static bool[,] Los = new bool[Map.W, Map.H];

        public static void UpdateVision()
        {
            Map.ComputeVision(Player);
            Array.Clear(Los, 0, Los.Length);
            var pp = Player.Pos;
            Fov.Compute(pp, 20, Map.BlocksSight, (x, y) => { if (Map.InB(x, y)) Los[x, y] = true; });
            foreach (var m in Map.Monsters)
            {
                if (m.Dead) continue;
                if (CanSeeMonster(m))
                {
                    if (m.Hostile && !m.Disguised && !KnownHostiles.Contains(m))
                    {
                        KnownHostiles.Add(m);
                        Interrupted = true;
                        if (!m.Seen)
                        {
                            m.Seen = true;
                            if (m.Def.Has(MF.Boss)) Msg("{crit}The Referee looks up from behind the Screen. \"Ah. A player.\"{/}", true);
                        }
                    }
                }
                else KnownHostiles.Remove(m);
            }
        }

        public static void Noise(Point p, int radius, int chance = 70)
        {
            foreach (var m in Map.Monsters)
            {
                if (m.Dead || !m.Sleeping) continue;
                if (Path.Cheb(m.Pos, p) <= radius && Rng.Chance(chance))
                {
                    m.Sleeping = false;
                    m.Aware = Math.Max(m.Aware, 20);
                    m.LastSeen = p;
                }
            }
        }

        public static void PlayerActed(int cost = ActCost)
        {
            Player.Energy -= cost;
            Run();
        }

        static void Run()
        {
            Map.ComputePlayerDist(Player);
            int guard = 0;
            while (!GameOver && guard++ < 200000)
            {
                if (Player.Energy >= ActCost && Player.CanAct) break;
                Tick++;
                Player.Energy = Math.Min(Player.Energy + Player.Speed, ActCost + 12);
                var list = Map.Monsters.ToList();
                foreach (var m in list)
                {
                    if (m.Dead || GameOver) continue;
                    m.Energy += m.Speed;
                    int acts = 0;
                    while (m.Energy >= ActCost && !m.Dead && !GameOver && acts++ < 4)
                    {
                        m.Energy -= ActCost;
                        if (m.CanAct) Ai.Act(m);
                    }
                }
                Map.Monsters.RemoveAll(x => x.Dead);
                if (Tick % 10 == 0) WorldTurn();
            }
            if (!GameOver) UpdateVision();
        }

        static void WorldTurn()
        {
            Turn++;
            var p = Player;
            TickStatuses(p);
            foreach (var m in Map.Monsters.ToList())
            {
                if (m.Dead) continue;
                TickStatuses(m);
                if (m.Def.Has(MF.Regen) && m.Hp < m.MaxHp && Turn % 2 == 0 && !m.Dead)
                    m.Hp = Math.Min(m.MaxHp, m.Hp + m.Def.RegenRate);
                else if (m.Hp < m.MaxHp && Turn % 12 == 0) m.Hp++;
                if (m.SummonTurns > 0 && --m.SummonTurns == 0)
                {
                    if (CanSeeMonster(m)) Msg(m.TheCap + " fades away.");
                    m.Dead = true;
                }
                if (m.Def.Has(MF.FearAura) && !p.Has(Status.Feared) && Path.Cheb(m.Pos, p.Pos) <= 2 && m.Hostile && !m.Sleeping && Turn % 5 == 0)
                {
                    if (!SavingThrow(p, Save.Spell, 0, "fear"))
                    {
                        Msg("{bad}Terror grips you at the sight of " + m.The + "!{/}", true);
                        p.Add(Status.Feared, Rng.Range(3, 6));
                    }
                }
                var mt = Map.T[m.Pos.X, m.Pos.Y];
                if (Map.Fire[m.Pos.X, m.Pos.Y] > 0 && !m.Resists(Elem.Fire)) Combat.Damage(m, Rng.Roll(1, 6), Elem.Fire, null, "flames");
                if (Map.Cloud[m.Pos.X, m.Pos.Y] > 0) CloudEffect(m);
                if (mt == Tile.Glyph && m.Hostile)
                {
                    Map.T[m.Pos.X, m.Pos.Y] = Tile.Floor;
                    if (CanSeeMonster(m)) Msg("{holy}The glyph of warding flares beneath " + m.The + "!{/}");
                    Combat.Damage(m, Rng.Roll(Math.Max(2, p.Level), 4), Elem.Holy, p, "glyph");
                }
            }
            Map.Monsters.RemoveAll(x => x.Dead);
            Map.TickTerrain();
            Magic.BladeBarrierTick();
            if (GameOver) return;

            int burnRate = Turn % 3 == 0 ? 0 : 1;
            if (p.Wearing("r_regen")) burnRate++;
            if (p.Wearing("r_sustenance") && Turn % 5 != 0) burnRate = 0;
            if (p.Has(Status.Hasted) && Turn % 2 == 0) burnRate++;
            int oldN = p.Nutrition;
            p.Nutrition -= burnRate;
            if (oldN > 300 && p.Nutrition <= 300) Msg("{warn}You are getting hungry.{/}", true);
            if (oldN > 100 && p.Nutrition <= 100) Msg("{bad}You feel weak from hunger.{/}", true);
            if (oldN > 0 && p.Nutrition <= 0) Msg("{bad}You are fainting from lack of food!{/}", true);
            if (p.Nutrition <= 0 && Rng.OneIn(10) && !p.Has(Status.Paralyzed))
            {
                Msg("{bad}You faint from hunger.{/}", true);
                p.Add(Status.Paralyzed, Rng.Range(2, 6));
            }
            if (p.Nutrition < -400) { Die("starvation"); return; }

            var light = p.Worn(Slot.Light);
            if (light != null && light.Lit && light.Def.Fuel < 99999)
            {
                light.Fuel--;
                if (light.Fuel == 100) Msg("{warn}Your " + light.Def.Name + " is burning low.{/}", true);
                if (light.Fuel == 25) Msg("{warn}Your " + light.Def.Name + " flickers and sputters.{/}", true);
                if (light.Fuel <= 0)
                {
                    light.Lit = false;
                    Msg("{bad}Your " + light.Def.Name + " goes out.{/}", true);
                    if (light.Def.Id == "torch" || light.Def.Id == "gland")
                    {
                        p.Eq.Remove(Slot.Light);
                        p.Pack.Remove(light);
                        var next = p.Pack.FirstOrDefault(i => i.Def.Kind == IK.Light && i.Fuel > 0 && i.Def.Id != "lantern");
                        if (next == null) next = p.Pack.FirstOrDefault(i => i.Def.Kind == IK.Light && i.Fuel > 0);
                        if (next != null)
                        {
                            if (next.Count > 1)
                            {
                                var one = next.Split(1);
                                one.Letter = Actions.FreeLetter();
                                p.Pack.Add(one);
                                next = one;
                            }
                            next.Lit = true;
                            p.Eq[Slot.Light] = next;
                            Msg("You light " + next.Name() + ".");
                        }
                        else Msg("{bad}You have no more light. The darkness presses close.{/}", true);
                    }
                }
            }

            int con = p.Stat(Stat.Con);
            int every = Math.Max(2, 18 - p.Level - Rules.ConHp(con, p.IsWarrior) * 2);
            if (p.Hp < p.MaxHp && !p.Has(Status.Diseased) && !p.Has(Status.Poisoned) && p.Nutrition > 0)
            {
                bool resting = p.RestTurns > 0;
                if (Turn % every == 0 || (resting && Turn % Math.Max(1, every / 2) == 0)) p.Hp++;
                if (p.Wearing("r_regen") || p.Has(Status.Regenerating)) p.Hp = Math.Min(p.MaxHp, p.Hp + 1);
                if (p.HasIoun("regen") && Turn % 3 == 0) p.Hp++;
                if (p.Wearing("a_closure") && Turn % 4 == 0) p.Hp++;
                p.Hp = Math.Min(p.Hp, p.MaxHp);
            }
            if (p.PrayerTimeout > 0) p.PrayerTimeout--;

            if (p.Has(Status.Poisoned) && Turn % 3 == 0)
            {
                if (p.Wearing("a_poison")) p.Remove(Status.Poisoned);
                else
                {
                    Combat.Damage(p, 1, Elem.Poison, null, "poison");
                    if (Rng.OneIn(8)) Msg("{bad}You feel the poison in your veins.{/}");
                }
            }
            if (p.Has(Status.Bleeding) && !p.Wearing("a_closure")) Combat.Damage(p, 1, Elem.Phys, null, "blood loss");
            if (p.Has(Status.Stoning))
            {
                int t = p.Turns(Status.Stoning);
                if (t == 4) Msg("{bad}You are slowing down.{/}", true);
                if (t == 3) Msg("{bad}Your limbs are stiffening.{/}", true);
                if (t == 2) Msg("{bad}Your limbs have turned to stone.{/}", true);
                if (t <= 1) { Msg("{bad}You are a statue.{/}", true); Die("petrification"); return; }
            }
            if (p.Has(Status.Sliming))
            {
                int t = p.Turns(Status.Sliming);
                if (t == 6) Msg("{bad}Your skin begins to peel away.{/}", true);
                if (t == 4) Msg("{bad}You are turning a little green.{/}", true);
                if (t == 2) Msg("{bad}Your limbs are getting oozy.{/}", true);
                if (t <= 1) { Msg("{bad}You have become a green slime.{/}", true); Die("turning into green slime"); return; }
            }
            if (p.Wearing("a_strangle"))
            {
                p.StrangleTurns++;
                Msg("{bad}It constricts your throat! (" + (6 - p.StrangleTurns) + ")" + "{/}", true);
                if (p.StrangleTurns >= 6) { Die("strangulation"); return; }
            }
            else p.StrangleTurns = 0;
            if (p.Wearing("r_teleport") && Rng.OneIn(85))
            {
                Msg("{magic}You feel a wrenching sensation.{/}", true);
                Magic.TeleportActor(p, null);
            }
            if (Map.Fire[p.Pos.X, p.Pos.Y] > 0) Combat.Damage(p, p.Resists(Elem.Fire) ? Rng.D(3) : Rng.Roll(1, 6), Elem.Fire, null, "burning");
            if (Map.Cloud[p.Pos.X, p.Pos.Y] > 0) CloudEffect(p);

            if (Map.T[p.Pos.X, p.Pos.Y] == Tile.Lava && !p.Has(Status.Levitate) && !p.Wearing("levitate")) { Die("lava"); return; }

            bool dark = p.LightRadius == 0 && Map.Lum(p.Pos.X, p.Pos.Y) < 0.1f;
            p.TurnsInDark = dark ? p.TurnsInDark + 1 : 0;
            if (p.TurnsInDark > 60 && Depth >= 2 && Rng.OneIn(40))
            {
                var spot = Map.NearestFree(p.Pos, 1);
                if (spot.HasValue && spot.Value != p.Pos)
                {
                    var dm = new Monster(MonsterDb.Get("darkmantle")) { Pos = spot.Value, Aware = 30 };
                    Map.Monsters.Add(dm);
                    Msg("{bad}Something drops from the ceiling in the darkness!{/}", true);
                    p.TurnsInDark = 0;
                }
            }

            foreach (var it in Map.Items.ToList())
            {
                if (it.Def.Kind != IK.Corpse) continue;
                it.Age++;
                if (it.CorpseOf != null && it.CorpseOf.Has(MF.Revives) && it.Age >= 12 && Rng.OneIn(4))
                {
                    if (Map.ActorAt(it.Pos) != null) continue;
                    Map.Items.Remove(it);
                    var t = new Monster(it.CorpseOf) { Pos = it.Pos, Aware = 40 };
                    t.Hp = t.MaxHp / 2;
                    Map.Monsters.Add(t);
                    if (Map.Vis[it.Pos.X, it.Pos.Y]) Msg("{bad}The " + it.CorpseOf.Name + " corpse knits itself back together and rises!{/}", true);
                    continue;
                }
                if (it.Age > 300) Map.Items.Remove(it);
            }
            foreach (var it in p.Pack.ToList())
                if (it.Def.Kind == IK.Corpse) it.Age++;

            WanderCounter++;
            int interval = HeartTaken ? 25 : 60;
            if (WanderCounter >= interval)
            {
                WanderCounter = 0;
                int roll = Rng.D(6);
                if (roll == 1)
                {
                    AddDie(6, 1, 0, 1, "wandering monster check", false);
                    WanderingMonster(false);
                }
            }
            foreach (var it in p.Iouns) it.OrbitPhase++;
            if (p.Has(Status.Errata) && p.Turns(Status.Errata) == 1) { p.ErrataMap = null; Msg("{good}The rules of your body revert to the proper edition.{/}"); }
        }

        static void CloudEffect(Actor a)
        {
            var e = Map.CloudE[a.Pos.X, a.Pos.Y];
            if (e == Elem.Poison)
            {
                if (a.Resists(Elem.Poison) || (a is Monster mm && mm.Def.Has(MF.Undead))) return;
                if (a is Monster m2 && m2.HdLevel < 5 && Rng.Chance(30)) { Combat.Damage(a, a.Hp + 1, Elem.Poison, null, "a poisonous cloud"); return; }
                Combat.Damage(a, Rng.Roll(1, 10), Elem.Poison, null, "a poisonous cloud");
            }
            else if (e == Elem.Magic)
            {
                if (a.Has(Status.Asleep) || a.Resists(Elem.Poison)) return;
                if (!SavingThrow(a, Save.Death, 0, "stench"))
                {
                    a.Add(Status.Stunned, 2);
                    Say(a, "{bad}You retch helplessly in the stinking cloud!{/}", a.TheCap + " retches helplessly.");
                }
            }
            else if (e == Elem.Fire)
            {
                if (!a.Resists(Elem.Fire)) Combat.Damage(a, Rng.Roll(2, 6), Elem.Fire, null, "an incendiary cloud");
            }
            else if (e == Elem.Cold)
            {
                if (a is Monster sm && (sm.Def.Has(MF.ResSleep) || sm.Def.Has(MF.Undead))) return;
                if (a.IsPlayer && (Player.Race == Race.Elf && Rng.Chance(90))) return;
                if (!SavingThrow(a, Save.Breath, 0, "gas"))
                {
                    a.Add(Status.Asleep, Rng.Range(4, 10));
                    Say(a, "{bad}You fall asleep.{/}", a.TheCap + " falls asleep.");
                }
            }
        }

        static void TickStatuses(Actor a)
        {
            foreach (var s in a.St.Keys.ToList())
            {
                int t = a.St[s];
                if (t >= 9999) continue;
                t--;
                if (t <= 0)
                {
                    a.St.Remove(s);
                    a.StPow.Remove(s);
                    if (a.IsPlayer) StatusEnded(s);
                    else if (a is Monster m) MonsterStatusEnded(m, s);
                }
                else a.St[s] = t;
            }
        }

        static void StatusEnded(Status s)
        {
            string msg = s switch
            {
                Status.Asleep => "You wake up.",
                Status.Paralyzed => "You can move again.",
                Status.Confused => "You feel less confused.",
                Status.Blind => "You can see again.",
                Status.Hasted => "You feel yourself slow down.",
                Status.Slowed => "You feel yourself speed up.",
                Status.Invisible => "You are visible again.",
                Status.Poisoned => "The poison has run its course.",
                Status.Blessed => "The blessing fades.",
                Status.Shielded => "Your magical shield winks out.",
                Status.Feared => "You regain your courage.",
                Status.Webbed => "You tear free of the webs.",
                Status.GiantStr => "Your strength returns to normal.",
                Status.Heroism => "You feel less heroic.",
                Status.ResFire => "You feel warmer. Uncomfortably so.",
                Status.ESP => "The voices in your head fall silent.",
                Status.MirrorImage => "Your mirror images fade.",
                Status.Held => "You can move again.",
                Status.ProtEvil => "You feel less protected.",
                Status.Stunned => "Your head clears.",
                Status.Sanctuary => "Your sanctuary ends.",
                Status.Prayer => "The prayer ends.",
                Status.Levitate => "You float gently to the ground.",
                Status.FireShield => "Your fire shield dies away.",
                Status.Globe => "The globe of invulnerability fades.",
                Status.Spiritual => "Your luck returns to normal.",
                Status.ProtMissiles => "You feel vulnerable to missiles again.",
                Status.Insight => "Your magical light fades.",
                Status.Charmed => "You shake off the enchantment.",
                Status.Weakened => "You feel your strength return.",
                Status.Regenerating => "Your regeneration slows.",
                Status.SeeInvis => "Your vision returns to normal.",
                Status.Diseased => "You feel healthier.",
                Status.Telepathy => "Your clairvoyant vision fades.",
                _ => null,
            };
            if (msg != null) Msg("{dim}" + msg + "{/}");
            if (s == Status.Levitate) Actions.LandOn(Player.Pos);
        }

        static void MonsterStatusEnded(Monster m, Status s)
        {
            if (s == Status.Charmed && m.Ally)
            {
                m.Ally = false;
                if (CanSeeMonster(m)) Msg(m.TheCap + " shakes off your charm!");
            }
            if (s == Status.Turned) m.Fleeing = false;
        }

        public static void WanderingMonster(bool aware)
        {
            int d = Depth;
            var def = Spawn.PickMonster(d, x => x.Freq > 0 && !x.Has(MF.Immobile) && !x.Has(MF.Mimic) && !x.Has(MF.Ambush));
            if (def == null) return;
            Point spot = Point.Zero;
            for (int t = 0; t < 200; t++)
            {
                var q = Map.RandomFloor();
                if (Map.Vis[q.X, q.Y] || Path.Cheb(q, Player.Pos) < 10) continue;
                spot = q;
                break;
            }
            if (spot == Point.Zero) return;
            int n = Math.Max(1, Rng.Range(def.GroupMin, Math.Min(def.GroupMax, 3)));
            for (int i = 0; i < n; i++)
            {
                var m = Spawn.PlaceMonster(Map, def, spot, false);
                if (m == null) continue;
                m.Aware = aware || Rng.Chance(50) ? 60 : 0;
                m.LastSeen = Player.Pos;
            }
            if (!aware) Msg("{dim}Somewhere in the dark, dice clatter behind a screen.{/}");
        }

        public static void Die(string cause)
        {
            var p = Player;
            if (GameOver) return;
            if (p.Carrying("heart"))
            {
                int r = Rng.D(20);
                bool ok = r >= 11;
                AddDie(20, r, 0, 11, "the Heart vs death", ok, r == 20, r == 1);
                Synth.Play(Sfx.Heart);
                if (ok)
                {
                    Msg("{pink}The Icosahedral Heart spins in your pack... and lands on " + r + ". Death is denied!{/}", true);
                    p.Hp = Math.Max(1, p.MaxHp / 3);
                    p.Remove(Status.Stoning);
                    p.Remove(Status.Sliming);
                    p.Remove(Status.Poisoned);
                    p.HeartSaves++;
                    if (p.Nutrition < 0) p.Nutrition = 300;
                    if (p.Wearing("a_strangle")) { var n = p.Eq.FirstOrDefault(kv => kv.Value.Def.Effect == "a_strangle"); p.Eq.Remove(n.Key); p.Pack.Remove(n.Value); Msg("The strangling necklace crumbles."); }
                    return;
                }
                Msg("{bad}The Icosahedral Heart spins... and lands on " + r + ". Not enough.{/}", true);
            }
            if (p.Wearing("a_second_roll") && !p.SecondRollUsed)
            {
                p.SecondRollUsed = true;
                var am = p.Eq.FirstOrDefault(kv => kv.Value.Def.Effect == "a_second_roll");
                p.Eq.Remove(am.Key);
                p.Pack.Remove(am.Value);
                Ident.Learn("a_second_roll");
                Msg("{gold}But wait... the dice are rolled again! Your amulet shatters, and you live!{/}", true);
                Synth.Play(Sfx.Holy);
                p.Hp = p.MaxHp;
                p.Remove(Status.Stoning);
                p.Remove(Status.Sliming);
                p.Remove(Status.Poisoned);
                if (p.Nutrition < 0) p.Nutrition = 900;
                return;
            }
            p.Dead = true;
            GameOver = true;
            KilledBy = cause;
            SaveGame.Delete();
            Synth.Play(Sfx.Death);
            Msg("{bad}You die...{/}", true);
            ComputeScore();
            Hof.Add();
        }

        public static void ComputeScore()
        {
            var p = Player;
            int s = p.Xp + p.Gold / 2 + p.DeepestDepth * 100 + p.Level * 50;
            foreach (var it in p.Pack) if (it.Def.Kind == IK.Gem) s += it.Value * it.Count / 10;
            if (HeartTaken) s += 2000;
            if (Won) s += 10000;
            Score = s;
        }

        public static void Win()
        {
            Won = true;
            GameOver = true;
            SaveGame.Delete();
            ComputeScore();
            Hof.Add();
            Synth.Play(Sfx.LevelUp);
        }

        public static void Retire()
        {
            Retired = true;
            GameOver = true;
            SaveGame.Delete();
            KilledBy = "retired to a quiet life";
            ComputeScore();
            Hof.Add();
        }

        public static void GainXp(int amt, bool silent = false)
        {
            var p = Player;
            if (amt <= 0) return;
            var prime = p.Class switch { Cls.Cleric => Stat.Wis, Cls.MagicUser => Stat.Int, Cls.Thief => Stat.Dex, _ => Stat.Str };
            if (p.Stat(prime) >= 16) amt += Math.Max(1, amt / 10);
            p.Xp += amt;
            while (p.Level < 20 && p.Xp >= Rules.XpFor(p.Class, p.Level + 1)) LevelUp();
        }

        public static void LevelUp()
        {
            var p = Player;
            p.Level++;
            int gain;
            if (p.Level <= 9)
            {
                int die = p.ClassInfo.HitDie;
                int r = Rng.D(die);
                gain = Math.Max(1, r + Rules.ConHp(p.Stat(Stat.Con), p.IsWarrior));
                AddDie(die, r, Rules.ConHp(p.Stat(Stat.Con), p.IsWarrior), 0, "hit points", true);
            }
            else gain = p.ClassInfo.HpAfter9;
            if (p.Level > p.MaxLevelReached)
            {
                p.MaxHp += gain;
                p.Hp += gain;
                p.MaxLevelReached = p.Level;
                p.LastHpGain = gain;
            }
            else
            {
                p.MaxHp += Math.Max(1, gain / 2);
                p.Hp += Math.Max(1, gain / 2);
            }
            Synth.Play(Sfx.LevelUp);
            Msg("{gold}Welcome to experience level " + p.Level + "! You are now a " + p.Title + ". (+" + gain + " HP){/}", true);
            if (p.Caster)
            {
                var slots = p.Slots;
                Msg("{magic}You may now memorize: " + string.Join(" / ", slots.Select((n, i) => "L" + (i + 1) + "×" + n)) + ". Use {yellow}p{/} to prepare.{/}");
                Magic.AutoPlan(p);
            }
            if (p.Class == Cls.Fighter && (p.Level == 7 || p.Level == 13)) Msg("{gold}You can now make more attacks each round!{/}");
            if (p.Class == Cls.Thief && (p.Level == 5 || p.Level == 9 || p.Level == 13)) Msg("{gold}Your backstab is now ×" + Rules.BackstabMul(p.Level) + "!{/}");
        }

        public static void LoseLevel()
        {
            var p = Player;
            if (p.Wearing("a_scarab"))
            {
                var sc = p.Eq.Values.First(i => i.Def.Effect == "a_scarab");
                if (sc.Charges <= 0) sc.Charges = 12;
                sc.Charges--;
                Ident.Learn("a_scarab");
                Msg("{gold}Your scarab glows and absorbs the draining touch!{/}");
                if (sc.Charges <= 0) { p.Eq.Remove(p.Eq.First(kv => kv.Value == sc).Key); p.Pack.Remove(sc); Msg("The scarab crumbles to dust."); }
                return;
            }
            if (p.Level <= 1)
            {
                Die("having their life force drained");
                return;
            }
            p.Level--;
            int loss = Math.Max(1, p.MaxHp / Math.Max(1, p.Level + 1));
            p.MaxHp = Math.Max(1, p.MaxHp - loss);
            p.Hp = Math.Min(p.Hp, p.MaxHp);
            p.Xp = Math.Max(Rules.XpFor(p.Class, p.Level), Rules.XpFor(p.Class, p.Level + 1) - 1);
            p.Xp = Rules.XpFor(p.Class, p.Level);
            Msg("{bad}Goodbye level " + (p.Level + 1) + ". You feel your life force drain away!{/}", true);
            Synth.Play(Sfx.Death, 0.5f);
            if (p.Caster)
            {
                var slots = p.Slots;
                p.Memorized.RemoveAll(s => SpellDb.Get(s).Level > slots.Length);
                Magic.TrimPlan(p);
            }
        }

        public static string DeathPhrase()
        {
            string k = KilledBy ?? "";
            if (Retired) return "retired from adventuring";
            if (k == "quit") return "quit the game";
            if (k.StartsWith("a ") || k.StartsWith("an ") || k.StartsWith("the ") || k.StartsWith("Death") || k.StartsWith("you") || char.IsUpper(k.FirstOrDefault())) return "killed by " + k;
            if (k.StartsWith("being") || k.StartsWith("having") || k.StartsWith("turning") || k.StartsWith("walking") || k.StartsWith("falling") || k.StartsWith("kicking")) return "died " + k;
            return "died of " + k;
        }

        public static string PlayTime()
        {
            var t = TimeSpan.FromTicks(DateTime.Now.Ticks - StartTicks);
            return t.Hours > 0 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }
    }
}
