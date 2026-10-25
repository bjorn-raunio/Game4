using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Icosa
{
    public class BotPilot
    {
        readonly string logPath;
        readonly int maxGames;
        int games;
        long lastTurn = -1;
        int sameTurn;
        int decisions;
        public int Errors;

        public BotPilot(string logPath, int maxGames)
        {
            this.logPath = logPath;
            this.maxGames = maxGames;
            File.WriteAllText(logPath, "bot start " + DateTime.Now + "\n");
        }

        public void Log(string s)
        {
            try { File.AppendAllText(logPath, s + "\n"); } catch { }
        }

        void Key(RogueGame g, char c) => g.InjectKey(new KeyIn { Ch = c });
        void Key(RogueGame g, Keys k) => g.InjectKey(new KeyIn { Key = k });

        void NewGame()
        {
            Dm.Ident.Shuffle();
            Dm.Map = null;
            var races = Enum.GetValues(typeof(Race)).Cast<Race>().ToArray();
            var classes = Enum.GetValues(typeof(Cls)).Cast<Cls>().ToArray();
            Race r;
            Cls c;
            do
            {
                r = races[Rng.Int(races.Length)];
                c = classes[Rng.Int(classes.Length)];
            } while (!Rules.Races[r].Classes.Contains(c));
            var stats = new[] { 15, 13, 14, 15, 15, 17 };
            if (c is Cls.Fighter or Cls.Paladin or Cls.Ranger) stats[0] = 17;
            if (c == Cls.MagicUser) stats[1] = 17;
            if (c == Cls.Cleric) stats[2] = 17;
            if (c == Cls.Thief) stats[3] = 17;
            var p = Chargen.Make(Lore.RandomName(r), r, c, Rules.Classes[c].Aligns[0], stats, 0);
            Ui.Replace(new GameScreen());
            Dm.NewGame(p);
            lastTurn = -1;
            sameTurn = 0;
        }

        public void Update(RogueGame g)
        {
            if (Anim.Blocking) { Anim.SkipAll(); return; }
            var top = Ui.Top;
            if (top is TitleScreen || top is CreationScreen) { NewGame(); return; }
            if (top is DeathScreen || top is VictoryScreen)
            {
                var p = Dm.Player;
                games++;
                Log("GAME " + games + ": " + p.RaceInfo.Name + " " + p.ClassInfo.Name + " L" + p.Level + " depth " + Dm.Depth + " deepest " + p.DeepestDepth + " turns " + Dm.Turn + " kills " + p.Kills + " gold " + p.Gold + (Dm.Won ? " WON" : " killed by " + Dm.KilledBy) + (Dm.HeartTaken ? " [heart]" : ""));
                if (games >= maxGames) { g.QuitRequested = true; return; }
                NewGame();
                return;
            }
            if (top is YesNoScreen) { Key(g, Rng.Chance(75) ? 'y' : 'n'); return; }
            if (top is DirectionScreen) { Key(g, "hjklyubn"[Rng.Int(8)]); return; }
            if (top is TargetScreen) { Key(g, Keys.Enter); return; }
            if (top is MenuScreen) { if (Rng.Chance(80)) Key(g, Keys.Enter); else Key(g, Keys.Escape); return; }
            if (top is TextScreen || top is CharacterScreen || top is SpellPrepScreen || top is HofScreen) { Key(g, Keys.Escape); return; }
            if (top is GameScreen gs)
            {
                if (gs.Busy || Dm.GameOver) return;
                decisions++;
                if (Dm.Turn == lastTurn) sameTurn++;
                else { sameTurn = 0; lastTurn = Dm.Turn; }
                if (sameTurn > 400)
                {
                    Log("STUCK at depth " + Dm.Depth + " turn " + Dm.Turn + " pos " + Dm.Player.Pos + " top " + top.GetType().Name);
                    Dm.Die("bot stuck");
                    return;
                }
                if (sameTurn > 40) { Key(g, "hjklyubn"[Rng.Int(8)]); return; }
                Decide(g);
            }
        }

        void Decide(RogueGame g)
        {
            var p = Dm.Player;
            var m = Dm.Map;
            if (m.Depth != lastDepth) { lastDepth = m.Depth; levelTurn = Dm.Turn; }
            if (Dm.Turn - levelTurn > 3000)
            {
                var ex = Actions.ExploreMap();
                var ns = Actions.NextStep(ex);
                Log("LINGER depth " + m.Depth + " turn " + Dm.Turn + " pos " + p.Pos + " hp " + p.Hp + "/" + p.MaxHp + " nut " + p.Nutrition + " explore " + (ex == null ? "null" : ns?.ToString() ?? "nostep") + " stairsSeen " + m.Seen[m.Down.X, m.Down.Y] + " down " + m.Down + " here " + string.Join(",", m.ItemsAt(p.Pos).Select(i => i.Def.Id)));
                foreach (var l in Dm.Log.Skip(Math.Max(0, Dm.Log.Count - 8))) Log("   " + Markup.Strip(l.Text));
                levelTurn = Dm.Turn;
            }
            var hostiles = m.Monsters.Where(x => !x.Dead && x.Hostile && Dm.CanSeeMonster(x) && !x.Disguised).ToList();
            var heal = p.Pack.FirstOrDefault(i => i.Def.Kind == IK.Potion && i.TypeKnown && i.Def.Effect is "heal" or "extra_heal");
            if (p.Hp < p.MaxHp / 3 && heal != null) { Key(g, 'q'); Key(g, heal.Letter); return; }
            if (p.Hp < p.MaxHp / 5 && p.PrayerTimeout < 100) { Key(g, 'P'); return; }
            if (Rng.Chance(4))
            {
                var it = p.Pack.Count > 0 ? p.Pack[Rng.Int(p.Pack.Count)] : null;
                if (it != null)
                {
                    char cmd = it.Def.Kind switch
                    {
                        IK.Potion => 'q', IK.Scroll => 'r', IK.Book => 'r', IK.Wand => 'z', IK.Tool => 'a', IK.Food => 'e',
                        IK.Armor or IK.Shield or IK.Helm or IK.Cloak or IK.Boots or IK.Gloves or IK.Ring or IK.Amulet or IK.Ioun or IK.Bracers or IK.Girdle => 'W',
                        IK.Weapon => 'w', _ => '\0',
                    };
                    if (cmd != '\0' && !(cmd == 'r' && it.Def.Effect == "spell" && false)) { Key(g, cmd); Key(g, it.Letter); return; }
                }
            }
            if (hostiles.Count > 0)
            {
                if (p.Caster && p.Memorized.Count > 0 && Rng.Chance(40)) { Key(g, 'c'); Key(g, 'a'); return; }
                if (p.Class is Cls.Cleric or Cls.Paladin && hostiles.Any(h => h.IsUndead) && Rng.Chance(30)) { Key(g, 'A'); Key(g, 't'); return; }
                if (p.Weapon != null && p.Weapon.Def.Has(WF.Launcher) && hostiles.All(h => Path.Cheb(h.Pos, p.Pos) > 1) && Rng.Chance(70)) { Key(g, 'f'); return; }
                g.InjectKey(new KeyIn { Key = Keys.Tab });
                return;
            }
            var fresh = m.ItemsAt(p.Pos).FirstOrDefault(i => i.Def.Kind == IK.Corpse && i.Age < 40 && i.CorpseOf != null && !i.CorpseOf.Attacks.Any(a => a.Sp is AtkSp.Poison or AtkSp.Poison2) && i.CorpseOf.Id is not ("cockatrice" or "basilisk" or "bandit"));
            if (fresh != null && p.Nutrition < 1500) { Key(g, 'e'); Key(g, 'y'); return; }
            if (p.Nutrition < 300)
            {
                var food = p.Pack.FirstOrDefault(i => i.Def.Kind == IK.Food);
                if (food != null) { Key(g, 'e'); Key(g, food.Letter); return; }
            }
            if (p.Worn(Slot.Light) == null)
            {
                var t = p.Pack.FirstOrDefault(i => i.Def.Kind == IK.Light && i.Fuel > 0);
                if (t != null) { Key(g, 'W'); Key(g, t.Letter); return; }
            }
            if (m.ItemsAt(p.Pos).Any(i => i.Price == 0 && i.Def.Kind != IK.Chest && !(i.Def.Kind == IK.Corpse) && p.Load + i.Weight <= p.Capacity) && p.Burden == 0) { Key(g, 'g'); return; }
            var chest = m.ItemsAt(p.Pos).FirstOrDefault(i => i.Def.Kind == IK.Chest && !i.Opened);
            if (chest != null && Rng.Chance(70)) { Key(g, 'o'); return; }
            if (p.Burden > 0)
            {
                var heavy = p.Pack.Where(i => !Actions.IsEquipped(i) && i.Def.Kind != IK.Heart).OrderByDescending(i => i.Weight).FirstOrDefault();
                if (heavy != null) { Key(g, 'd'); Key(g, heavy.Letter); return; }
            }
            if ((p.Hp < p.MaxHp / 2 || (p.Caster && p.Memorized.Count == 0)) && p.Nutrition > 400) { Key(g, 'R'); return; }
            if (m.T[p.Pos.X, p.Pos.Y] is Tile.Throne or Tile.OracleDie && Rng.Chance(50)) { Key(g, 'E'); return; }
            if (m.T[p.Pos.X, p.Pos.Y] == Tile.StairsDown) { Key(g, '>'); return; }
            if (Dm.HeartTaken)
            {
                if (m.T[p.Pos.X, p.Pos.Y] == Tile.StairsUp) { Key(g, '<'); return; }
                Key(g, '<');
                return;
            }
            if (m.Depth == Rules.MaxDepth)
            {
                var heart = m.Items.FirstOrDefault(i => i.Def.Kind == IK.Heart);
                if (heart != null && heart.Pos == p.Pos) { Key(g, 'g'); return; }
                if (heart != null && m.Seen[heart.Pos.X, heart.Pos.Y] && StepToward(g, heart.Pos)) return;
            }
            var dist = Actions.ExploreMap();
            if (dist != null && Actions.NextStep(dist) != null && Rng.Chance(97)) { stuckExplore = 0; Key(g, 'X'); return; }
            if (m.Depth < Rules.MaxDepth && m.Seen[m.Down.X, m.Down.Y] && StepToward(g, m.Down)) return;
            stuckExplore++;
            if (stuckExplore > 25)
            {
                int locked = 0, secret = 0;
                for (int x = 0; x < Map.W; x++)
                    for (int y = 0; y < Map.H; y++)
                    {
                        if (m.T[x, y] == Tile.DoorLocked) { locked++; m.T[x, y] = Tile.DoorClosed; }
                        if (m.T[x, y] == Tile.DoorSecret) secret++;
                    }
                Log("EXPLORE-STUCK depth " + m.Depth + " turn " + Dm.Turn + " stairsSeen " + (m.Depth < Rules.MaxDepth && m.Seen[m.Down.X, m.Down.Y]) + " locked " + locked + " secret " + secret);
                m.RevealMap(false);
                stuckExplore = 0;
            }
            Key(g, 's');
        }

        int stuckExplore;
        int lastDepth = -1;
        long levelTurn;

        bool StepToward(RogueGame g, Point goal)
        {
            var p = Dm.Player;
            var m = Dm.Map;
            var dist = Path.DistMap(m, new[] { goal }, (x, y) => m.Walkable(x, y) || m.T[x, y] is Tile.DoorClosed or Tile.DoorLocked);
            var next = Actions.NextStep(dist);
            if (next == null) return false;
            var d = new Point(next.Value.X - p.Pos.X, next.Value.Y - p.Pos.Y);
            if (m.T[next.Value.X, next.Value.Y] == Tile.DoorLocked && p.Class != Cls.Thief) { Key(g, 'D'); }
            string keys = "yku" + "h l" + "bjn";
            char c = keys[(d.Y + 1) * 3 + (d.X + 1)];
            if (c == ' ') return false;
            Key(g, c);
            return true;
        }
    }
}
