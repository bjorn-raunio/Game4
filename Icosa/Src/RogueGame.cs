using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Icosa
{
    public struct KeyIn
    {
        public Keys Key;
        public char Ch;
        public bool Shift, Ctrl, Alt;

        public bool Is(char c) => Ch == c && !Ctrl;
        public bool Is(Keys k) => Key == k && Ch == '\0';
        public bool IsEsc => Key == Keys.Escape && Ch == '\0';
        public bool IsEnter => Key == Keys.Enter && Ch == '\0';
        public bool IsConfirm => IsEnter || Ch == ' ';

        public bool Dir(out Point d, out bool run)
        {
            d = Point.Zero;
            run = false;
            if (Ctrl) return false;
            switch (Key)
            {
                case Keys.Up: d = new Point(0, -1); run = Shift; return Ch == '\0';
                case Keys.Down: d = new Point(0, 1); run = Shift; return Ch == '\0';
                case Keys.Left: d = new Point(-1, 0); run = Shift; return Ch == '\0';
                case Keys.Right: d = new Point(1, 0); run = Shift; return Ch == '\0';
                case Keys.Home: d = new Point(-1, -1); run = Shift; return Ch == '\0';
                case Keys.PageUp: d = new Point(1, -1); run = Shift; return Ch == '\0';
                case Keys.End: d = new Point(-1, 1); run = Shift; return Ch == '\0';
                case Keys.PageDown: d = new Point(1, 1); run = Shift; return Ch == '\0';
                case Keys.NumPad8: d = new Point(0, -1); run = Shift; return Ch == '\0';
                case Keys.NumPad2: d = new Point(0, 1); run = Shift; return Ch == '\0';
                case Keys.NumPad4: d = new Point(-1, 0); run = Shift; return Ch == '\0';
                case Keys.NumPad6: d = new Point(1, 0); run = Shift; return Ch == '\0';
                case Keys.NumPad7: d = new Point(-1, -1); run = Shift; return Ch == '\0';
                case Keys.NumPad9: d = new Point(1, -1); run = Shift; return Ch == '\0';
                case Keys.NumPad1: d = new Point(-1, 1); run = Shift; return Ch == '\0';
                case Keys.NumPad3: d = new Point(1, 1); run = Shift; return Ch == '\0';
            }
            switch (Ch)
            {
                case 'k': d = new Point(0, -1); return true;
                case 'j': d = new Point(0, 1); return true;
                case 'h': d = new Point(-1, 0); return true;
                case 'l': d = new Point(1, 0); return true;
                case 'y': d = new Point(-1, -1); return true;
                case 'u': d = new Point(1, -1); return true;
                case 'b': d = new Point(-1, 1); return true;
                case 'n': d = new Point(1, 1); return true;
                case 'K': d = new Point(0, -1); run = true; return true;
                case 'J': d = new Point(0, 1); run = true; return true;
                case 'H': d = new Point(-1, 0); run = true; return true;
                case 'L': d = new Point(1, 0); run = true; return true;
                case 'Y': d = new Point(-1, -1); run = true; return true;
                case 'U': d = new Point(1, -1); run = true; return true;
                case 'B': d = new Point(-1, 1); run = true; return true;
                case 'N': d = new Point(1, 1); run = true; return true;
            }
            return false;
        }

        public bool IsWait => (Key == Keys.NumPad5 && Ch == '\0') || (Ch == '.' && !Ctrl);

        public int MenuMove()
        {
            if (Ch != '\0') return 0;
            if (Key == Keys.Up || Key == Keys.NumPad8) return -1;
            if (Key == Keys.Down || Key == Keys.NumPad2) return 1;
            if (Key == Keys.PageUp) return -10;
            if (Key == Keys.PageDown) return 10;
            return 0;
        }
    }

    public abstract class Screen
    {
        public virtual bool Overlay => false;
        public virtual void Key(KeyIn k) { }
        public virtual void Update(float dt) { }
        public abstract void Draw(Terminal t);
        public virtual void DrawPixels(Gfx g) { }
        public virtual void OnShow() { }
    }

    public static class Ui
    {
        public static readonly List<Screen> Stack = new List<Screen>();
        public static Screen Top => Stack.Count > 0 ? Stack[Stack.Count - 1] : null;
        public static void Push(Screen s) { Stack.Add(s); s.OnShow(); }
        public static void Pop() { if (Stack.Count > 0) Stack.RemoveAt(Stack.Count - 1); Top?.OnShow(); }
        public static void Pop(Screen s) { Stack.Remove(s); }
        public static void Replace(Screen s) { Stack.Clear(); Push(s); }
        public static float Time;
        public static Point? MouseCell;
        public static bool Click, RightClick;
        public static bool MouseMoved;
    }

    public class Gfx
    {
        public SpriteBatch Sb;
        public Texture2D Px;
        public SpriteFont Font, Big;

        public void Rect(float x, float y, float w, float h, Color c) => Sb.Draw(Px, new Rectangle((int)x, (int)y, (int)Math.Ceiling(w), (int)Math.Ceiling(h)), c);

        public void Line(Vector2 a, Vector2 b, Color c, float thick = 1f)
        {
            var d = b - a;
            float len = d.Length();
            if (len < 0.01f) return;
            float ang = (float)Math.Atan2(d.Y, d.X);
            Sb.Draw(Px, a, null, c, ang, new Vector2(0, 0.5f), new Vector2(len, thick), SpriteEffects.None, 0);
        }

        public void Text(SpriteFont f, string s, Vector2 pos, Color c, float scale = 1f) =>
            Sb.DrawString(f, s, pos, c, 0, Vector2.Zero, scale, SpriteEffects.None, 0);

        public void TextCentered(SpriteFont f, string s, Vector2 center, Color c, float scale = 1f)
        {
            var m = f.MeasureString(s) * scale;
            Sb.DrawString(f, s, center - m / 2, c, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
    }

    public class RogueGame : Game
    {
        public static RogueGame Instance;
        public static bool Wizard;
        readonly GraphicsDeviceManager gdm;
        SpriteBatch sb;
        Texture2D px;
        SpriteFont font, big;
        RenderTarget2D rt;
        readonly Terminal term = new Terminal();
        readonly Gfx gfx = new Gfx();
        readonly Queue<KeyIn> keyQueue = new Queue<KeyIn>();
        KeyboardState prevKb;
        Keys repeatKey = Keys.None;
        float repeatTimer;
        readonly Dictionary<char, Vector2> glyphSize = new Dictionary<char, Vector2>();
        float glyphScale = 1f;
        float glyphYOff;
        readonly string[] args;
        AutoPilot auto;
        BotPilot bot;
        int botFrame;
        public bool QuitRequested;

        static readonly Keys[] PolledKeys =
        {
            Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown,
            Keys.NumPad0, Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4, Keys.NumPad5, Keys.NumPad6, Keys.NumPad7, Keys.NumPad8, Keys.NumPad9,
            Keys.Escape, Keys.Enter, Keys.Tab, Keys.Back, Keys.Delete, Keys.Insert,
            Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12,
        };

        public RogueGame(string[] args)
        {
            Instance = this;
            this.args = args ?? Array.Empty<string>();
            gdm = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = Terminal.W * Terminal.CellW,
                PreferredBackBufferHeight = Terminal.H * Terminal.CellH,
                SynchronizeWithVerticalRetrace = true,
            };
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            Window.Title = "Tomb of the Twenty-Sided Heart";
            Content.RootDirectory = "Content";
            IsFixedTimeStep = false;
        }

        protected override void LoadContent()
        {
            sb = new SpriteBatch(GraphicsDevice);
            px = new Texture2D(GraphicsDevice, 1, 1);
            px.SetData(new[] { Color.White });
            font = Content.Load<SpriteFont>("Main");
            big = Content.Load<SpriteFont>("Big");
            rt = new RenderTarget2D(GraphicsDevice, Terminal.W * Terminal.CellW, Terminal.H * Terminal.CellH);
            gfx.Sb = sb;
            gfx.Px = px;
            gfx.Font = font;
            gfx.Big = big;
            glyphScale = Math.Min(1f, (Terminal.CellH + 3f) / font.LineSpacing);
            glyphYOff = (Terminal.CellH - font.LineSpacing * glyphScale) / 2f;
            Window.TextInput += OnTextInput;
            Synth.Init();
            Hof.Load();
            int ai = Array.IndexOf(args, "--auto");
            if (ai >= 0 && ai + 1 < args.Length) auto = new AutoPilot(File.ReadAllText(args[ai + 1]));
            Wizard = Array.IndexOf(args, "--wizard") >= 0;
            int bi = Array.IndexOf(args, "--bot");
            if (bi >= 0 && bi + 2 < args.Length)
            {
                bot = new BotPilot(args[bi + 2], int.Parse(args[bi + 1]));
                SaveGame.Disabled = true;
                gdm.SynchronizeWithVerticalRetrace = false;
                gdm.ApplyChanges();
                Anim.SpeedMul = 100f;
                Synth.Muted = true;
            }
            int si = Array.IndexOf(args, "--seed");
            if (si >= 0 && si + 1 < args.Length) Rng.Init(int.Parse(args[si + 1]));
            else Rng.Init(Environment.TickCount);
            Ui.Replace(new TitleScreen());
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            if (bot == null && auto == null && Ui.Stack.Any(s => s is GameScreen) && Dm.Player != null && !Dm.GameOver) SaveGame.Save();
            base.OnExiting(sender, args);
        }

        void OnTextInput(object sender, TextInputEventArgs e)
        {
            char c = e.Character;
            if (c < 32 || c == 127) return;
            var kb = Keyboard.GetState();
            if (kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl)) return;
            for (var k = Keys.NumPad0; k <= Keys.Divide; k++)
                if (kb.IsKeyDown(k)) return;
            keyQueue.Enqueue(new KeyIn { Key = e.Key, Ch = c, Shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift) });
        }

        public void InjectKey(KeyIn k) => keyQueue.Enqueue(k);

        void PollKeys(float dt)
        {
            var kb = Keyboard.GetState();
            bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
            bool ctrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
            bool alt = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
            foreach (var k in PolledKeys)
            {
                if (kb.IsKeyDown(k) && !prevKb.IsKeyDown(k))
                {
                    if (k == Keys.Enter && alt) { ToggleFullscreen(); continue; }
                    if (k == Keys.F11) { ToggleFullscreen(); continue; }
                    keyQueue.Enqueue(new KeyIn { Key = k, Shift = shift, Ctrl = ctrl, Alt = alt });
                    repeatKey = k;
                    repeatTimer = 0.28f;
                }
            }
            if (ctrl)
            {
                for (var k = Keys.A; k <= Keys.Z; k++)
                    if (kb.IsKeyDown(k) && !prevKb.IsKeyDown(k))
                        keyQueue.Enqueue(new KeyIn { Key = k, Ch = (char)('a' + (k - Keys.A)), Ctrl = true, Shift = shift });
            }
            if (repeatKey != Keys.None)
            {
                if (!kb.IsKeyDown(repeatKey)) repeatKey = Keys.None;
                else
                {
                    repeatTimer -= dt;
                    if (repeatTimer <= 0)
                    {
                        repeatTimer = 0.055f;
                        if (keyQueue.Count < 2) keyQueue.Enqueue(new KeyIn { Key = repeatKey, Shift = shift, Ctrl = ctrl, Alt = alt });
                    }
                }
            }
            prevKb = kb;
        }

        MouseState prevMouse;
        Rectangle lastDest = new Rectangle(0, 0, Terminal.W * Terminal.CellW, Terminal.H * Terminal.CellH);

        void PollMouse()
        {
            var ms = Mouse.GetState();
            Ui.Click = ms.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            Ui.RightClick = ms.RightButton == ButtonState.Pressed && prevMouse.RightButton == ButtonState.Released;
            Ui.MouseMoved = ms.X != prevMouse.X || ms.Y != prevMouse.Y;
            if (lastDest.Width > 0 && lastDest.Contains(ms.X, ms.Y))
            {
                float sx = (ms.X - lastDest.X) / (float)lastDest.Width * Terminal.W;
                float sy = (ms.Y - lastDest.Y) / (float)lastDest.Height * Terminal.H;
                Ui.MouseCell = new Point((int)sx, (int)sy);
            }
            else Ui.MouseCell = null;
            prevMouse = ms;
        }

        public void ToggleFullscreen()
        {
            gdm.HardwareModeSwitch = false;
            if (!gdm.IsFullScreen)
            {
                gdm.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                gdm.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
                gdm.IsFullScreen = true;
            }
            else
            {
                gdm.IsFullScreen = false;
                gdm.PreferredBackBufferWidth = Terminal.W * Terminal.CellW;
                gdm.PreferredBackBufferHeight = Terminal.H * Terminal.CellH;
            }
            gdm.ApplyChanges();
        }

        protected override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt > 0.1f) dt = 0.1f;
            Ui.Time += dt;
            if (IsActive && bot == null) PollKeys(dt);
            if (IsActive && bot == null && auto == null) PollMouse();
            else { Ui.Click = Ui.RightClick = false; }
            try
            {
                int iterations = bot != null ? 30 : 1;
                for (int iter = 0; iter < iterations && !QuitRequested; iter++)
                {
                    if (bot != null) { dt = 0.05f; Ui.Time += dt; }
                    auto?.Update(this, dt);
                    if (bot != null && keyQueue.Count == 0) bot.Update(this);
                    Anim.Update(dt);
                    var top = Ui.Top;
                    top?.Update(dt);
                    int guard = 0;
                    while (keyQueue.Count > 0 && guard++ < 8)
                    {
                        if (Anim.Blocking)
                        {
                            var k = keyQueue.Peek();
                            if (k.IsEsc || k.IsConfirm) { keyQueue.Dequeue(); Anim.SkipAll(); }
                            break;
                        }
                        var key = keyQueue.Dequeue();
                        Ui.Top?.Key(key);
                    }
                }
                if (bot != null && (botFrame++ % 30) != 0) SuppressDraw();
            }
            catch (Exception ex) when (bot != null)
            {
                bot.Errors++;
                bot.Log("EXCEPTION depth " + Dm.Depth + " turn " + Dm.Turn + ": " + ex);
                keyQueue.Clear();
                Anim.Clear();
                if (bot.Errors > 30) QuitRequested = true;
                Ui.Replace(new TitleScreen());
            }
            if (QuitRequested) Exit();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            term.Clear();
            int start = Ui.Stack.Count - 1;
            while (start > 0 && Ui.Stack[start].Overlay) start--;
            for (int i = Math.Max(0, start); i < Ui.Stack.Count; i++) Ui.Stack[i].Draw(term);

            GraphicsDevice.SetRenderTarget(rt);
            GraphicsDevice.Clear(Color.Black);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            DrawTerminal();
            for (int i = Math.Max(0, start); i < Ui.Stack.Count; i++) Ui.Stack[i].DrawPixels(gfx);
            sb.End();
            GraphicsDevice.SetRenderTarget(null);

            auto?.AfterDraw(this, rt);

            GraphicsDevice.Clear(Color.Black);
            var pp = GraphicsDevice.PresentationParameters;
            float scale = Math.Min(pp.BackBufferWidth / (float)rt.Width, pp.BackBufferHeight / (float)rt.Height);
            int w = (int)(rt.Width * scale), h = (int)(rt.Height * scale);
            var dest = new Rectangle((pp.BackBufferWidth - w) / 2, (pp.BackBufferHeight - h) / 2, w, h);
            lastDest = dest;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque, Math.Abs(scale - 1f) < 0.01f ? SamplerState.PointClamp : SamplerState.LinearClamp);
            sb.Draw(rt, dest, Color.White);
            sb.End();
            base.Draw(gameTime);
        }

        void DrawTerminal()
        {
            const int cw = Terminal.CellW, ch = Terminal.CellH;
            for (int y = 0; y < Terminal.H; y++)
                for (int x = 0; x < Terminal.W; x++)
                {
                    var bg = term.Bg[x, y];
                    if (bg.R != 0 || bg.G != 0 || bg.B != 0) sb.Draw(px, new Rectangle(x * cw, y * ch, cw, ch), bg);
                    if (term.Paper[x, y])
                    {
                        sb.Draw(px, new Rectangle(x * cw, y * ch, cw, 1), Pal.PaperLine);
                        sb.Draw(px, new Rectangle(x * cw, y * ch, 1, ch), Pal.PaperLine);
                    }
                }
            for (int y = 0; y < Terminal.H; y++)
                for (int x = 0; x < Terminal.W; x++)
                {
                    char c = term.Ch[x, y];
                    if (c == ' ') continue;
                    var fg = term.Fg[x, y];
                    if (c >= '─' && c <= '▟') { DrawBoxGlyph(c, x * cw, y * ch, fg); continue; }
                    if (!glyphSize.TryGetValue(c, out var sz))
                    {
                        sz = font.Characters.Contains(c) ? font.MeasureString(c.ToString()) : font.MeasureString("?");
                        glyphSize[c] = sz;
                    }
                    if (!font.Characters.Contains(c)) c = '?';
                    var pos = new Vector2(x * cw + (cw - sz.X * glyphScale) / 2f, y * ch + glyphYOff);
                    sb.DrawString(font, c.ToString(), new Vector2((float)Math.Round(pos.X), (float)Math.Round(pos.Y)), fg, 0, Vector2.Zero, glyphScale, SpriteEffects.None, 0);
                }
        }

        void DrawBoxGlyph(char c, int x, int y, Color fg)
        {
            const int cw = Terminal.CellW, ch = Terminal.CellH;
            int cx = x + cw / 2, cy = y + ch / 2;
            void H(int x0, int x1, int yy) => sb.Draw(px, new Rectangle(x0, yy, x1 - x0, 1), fg);
            void V(int y0, int y1, int xx) => sb.Draw(px, new Rectangle(xx, y0, 1, y1 - y0), fg);
            switch (c)
            {
                case '─': H(x, x + cw, cy); break;
                case '│': V(y, y + ch, cx); break;
                case '┌': H(cx, x + cw, cy); V(cy, y + ch, cx); break;
                case '┐': H(x, cx + 1, cy); V(cy, y + ch, cx); break;
                case '└': H(cx, x + cw, cy); V(y, cy + 1, cx); break;
                case '┘': H(x, cx + 1, cy); V(y, cy + 1, cx); break;
                case '├': V(y, y + ch, cx); H(cx, x + cw, cy); break;
                case '┤': V(y, y + ch, cx); H(x, cx, cy); break;
                case '┬': H(x, x + cw, cy); V(cy, y + ch, cx); break;
                case '┴': H(x, x + cw, cy); V(y, cy, cx); break;
                case '┼': H(x, x + cw, cy); V(y, y + ch, cx); break;
                case '═': H(x, x + cw, cy - 2); H(x, x + cw, cy + 2); break;
                case '║': V(y, y + ch, cx - 2); V(y, y + ch, cx + 2); break;
                case '╔': H(cx - 2, x + cw, cy - 2); H(cx + 2, x + cw, cy + 2); V(cy - 2, y + ch, cx - 2); V(cy + 2, y + ch, cx + 2); break;
                case '╗': H(x, cx + 3, cy - 2); H(x, cx - 1, cy + 2); V(cy - 2, y + ch, cx + 2); V(cy + 2, y + ch, cx - 2); break;
                case '╚': H(cx - 2, x + cw, cy + 2); H(cx + 2, x + cw, cy - 2); V(y, cy + 3, cx - 2); V(y, cy - 1, cx + 2); break;
                case '╝': H(x, cx + 3, cy + 2); H(x, cx - 1, cy - 2); V(y, cy + 3, cx + 2); V(y, cy - 1, cx - 2); break;
                case '█': sb.Draw(px, new Rectangle(x, y, cw, ch), fg); break;
                case '▀': sb.Draw(px, new Rectangle(x, y, cw, ch / 2), fg); break;
                case '▄': sb.Draw(px, new Rectangle(x, y + ch / 2, cw, ch / 2), fg); break;
                case '▌': sb.Draw(px, new Rectangle(x, y, cw / 2, ch), fg); break;
                case '▐': sb.Draw(px, new Rectangle(x + cw / 2, y, cw / 2, ch), fg); break;
                case '░': sb.Draw(px, new Rectangle(x, y, cw, ch), fg * 0.25f); break;
                case '▒': sb.Draw(px, new Rectangle(x, y, cw, ch), fg * 0.5f); break;
                case '▓': sb.Draw(px, new Rectangle(x, y, cw, ch), fg * 0.75f); break;
                default:
                    if (font.Characters.Contains(c))
                        sb.DrawString(font, c.ToString(), new Vector2(x, y + glyphYOff), fg, 0, Vector2.Zero, glyphScale, SpriteEffects.None, 0);
                    break;
            }
        }
    }

    public class AutoPilot
    {
        readonly List<string> tokens = new List<string>();
        int idx;
        float wait;
        string pendingShot;

        public AutoPilot(string script)
        {
            foreach (var line in script.Split('\n'))
            {
                var l = line.Trim();
                if (l.Length == 0 || l.StartsWith("//")) continue;
                tokens.AddRange(l.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        public void Update(RogueGame g, float dt)
        {
            if (pendingShot != null) return;
            if (wait > 0) { wait -= dt; return; }
            if (Anim.Blocking) return;
            if (idx >= tokens.Count) return;
            string t = tokens[idx++];
            if (t.StartsWith("wait:")) { wait = float.Parse(t.Substring(5), System.Globalization.CultureInfo.InvariantCulture); return; }
            if (t.StartsWith("shot:")) { pendingShot = t.Substring(5); return; }
            if (t == "quit") { g.QuitRequested = true; return; }
            if (t.StartsWith("<") && t.EndsWith(">") && t.Length > 2)
            {
                string name = t.Substring(1, t.Length - 2);
                bool shift = false;
                if (name.StartsWith("S-")) { shift = true; name = name.Substring(2); }
                if (Enum.TryParse<Keys>(name, out var key)) g.InjectKey(new KeyIn { Key = key, Shift = shift });
                return;
            }
            if (t.StartsWith("^") && t.Length == 2)
            {
                g.InjectKey(new KeyIn { Ch = t[1], Ctrl = true, Key = Keys.A + (char.ToLower(t[1]) - 'a') });
                wait = 0.05f;
                return;
            }
            if (t.StartsWith("text:"))
            {
                foreach (char c in t.Substring(5)) g.InjectKey(new KeyIn { Ch = c == '_' ? ' ' : c });
                return;
            }
            foreach (char c in t) g.InjectKey(new KeyIn { Ch = c });
            wait = 0.03f;
        }

        public void AfterDraw(RogueGame g, RenderTarget2D rt)
        {
            if (pendingShot == null) return;
            using (var fs = File.Create(pendingShot)) rt.SaveAsPng(fs, rt.Width, rt.Height);
            pendingShot = null;
        }
    }
}
