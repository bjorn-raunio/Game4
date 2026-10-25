using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public class Item
    {
        public ItemDef Def;
        public int Count = 1;
        public int Ench;
        public bool Cursed;
        public bool Known;
        public int Charges;
        public bool ChargesKnown;
        public int Fuel;
        public bool Lit;
        public char Letter;
        public Point Pos;
        public List<Item> Contents;
        public bool Locked, Trapped, TrapKnown, Opened;
        public int Price;
        public MonsterDef CorpseOf;
        public int Age;
        public int Erosion;
        public List<SpellId> BookSpells;
        public string Title;
        public bool Tried;
        public bool MagicSensed;
        public int OrbitPhase;
        public string Engraving;

        public Item(ItemDef d, int count = 1)
        {
            Def = d;
            Count = Math.Max(1, count);
            Fuel = d.Fuel;
            if (!d.Charges.IsZero) Charges = d.Charges.Roll();
            if (d.Artifact) { Ench = d.Power; Known = false; }
        }

        public static Item Make(string id, int count = 1) => new Item(ItemDb.Get(id), count);

        public IK Kind => Def.Kind;
        public bool IsGold => Def.Kind == IK.Gold;
        public bool TypeKnown => !Def.Unknown || Dm.Ident.IsKnown(Def.Id);
        public bool IsMagic => Def.Magic || Ench != 0 || (Def.Kind is IK.Weapon or IK.Armor or IK.Shield or IK.Helm or IK.Cloak or IK.Ammo && Ench != 0);

        public int Weight
        {
            get
            {
                if (IsGold) return Count / 100;
                int w = Def.Wt * Count;
                if (Contents != null) w += Contents.Sum(c => c.Weight);
                if (CorpseOf != null) w = CorpseOf.Size == Size.Large ? 1000 : CorpseOf.Size == Size.Small ? 150 : 400;
                return w;
            }
        }

        public bool Stackable => Def.Stack || IsGold;

        public bool CanStack(Item o)
        {
            if (o == null || o == this || !Stackable || o.Def != Def) return false;
            if (Ench != o.Ench || Cursed != o.Cursed || Known != o.Known) return false;
            if (Def.Kind == IK.Light && (Fuel != o.Fuel || Lit || o.Lit)) return false;
            if (Price != o.Price) return false;
            return true;
        }

        public Item Split(int n)
        {
            n = Math.Clamp(n, 1, Count);
            var copy = (Item)MemberwiseClone();
            copy.Count = n;
            copy.Letter = '\0';
            Count -= n;
            return copy;
        }

        public string Appearance => Dm.Ident.Appearance(Def.Id);

        static string Pluralize(string s)
        {
            int of = s.IndexOf(" of ");
            if (of > 0) return Pluralize(s.Substring(0, of)) + s.Substring(of);
            if (s.EndsWith("staff")) return s.Substring(0, s.Length - 5) + "staves";
            if (s.EndsWith("y") && !s.EndsWith("ey") && !s.EndsWith("ay")) return s.Substring(0, s.Length - 1) + "ies";
            if (s.EndsWith("s") || s.EndsWith("x") || s.EndsWith("ch") || s.EndsWith("sh")) return s + "es";
            if (s.EndsWith("man")) return s.Substring(0, s.Length - 3) + "men";
            return s + "s";
        }

        public static string Article(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            char c = char.ToLower(s[0]);
            if (char.IsDigit(c) || c == '+' || c == '-') return "a " + s;
            return ("aeiou".IndexOf(c) >= 0 ? "an " : "a ") + s;
        }

        public string BaseName(bool plural)
        {
            var d = Def;
            switch (d.Kind)
            {
                case IK.Gold:
                    return plural ? "gold pieces" : "gold piece";
                case IK.Corpse:
                    {
                        string n = (CorpseOf != null ? CorpseOf.Name : "unknown") + " corpse";
                        return plural ? n + "s" : n;
                    }
                case IK.Potion:
                    if (TypeKnown) return (plural ? "potions" : "potion") + " of " + d.Name;
                    return Appearance + (plural ? " potions" : " potion");
                case IK.Scroll:
                    if (d.Effect == "spell" || TypeKnown) return d.Effect == "spell" ? (plural ? "scrolls" + d.Name.Substring(6) : d.Name) : (plural ? "scrolls" : "scroll") + " of " + d.Name;
                    return (plural ? "scrolls" : "scroll") + " labeled " + Appearance;
                case IK.Wand:
                    if (TypeKnown) return (plural ? "wands" : "wand") + " of " + d.Name;
                    return Appearance + (plural ? " wands" : " wand");
                case IK.Ring:
                    if (TypeKnown) return (plural ? "rings" : "ring") + " of " + d.Name;
                    return Appearance + (plural ? " rings" : " ring");
                case IK.Amulet:
                    if (TypeKnown) return plural ? Pluralize(d.Name) : d.Name;
                    return Appearance + (plural ? " amulets" : " amulet");
                case IK.Book:
                    return (plural ? "spellbooks" : "spellbook") + (Title != null ? " \"" + Title + "\"" : "");
                default:
                    return plural ? (d.Plural ?? Pluralize(d.Name)) : d.Name;
            }
        }

        public string Name(bool article = true, bool full = true)
        {
            bool plural = Count > 1;
            string core = BaseName(plural);
            var pre = new List<string>();
            var post = new List<string>();
            bool showEnch = Known && Def.Kind is IK.Weapon or IK.Armor or IK.Shield or IK.Helm or IK.Cloak or IK.Boots or IK.Gloves or IK.Ammo or IK.Girdle or IK.Bracers;
            if (Known && Cursed) pre.Add("cursed");
            if (Erosion == 1) pre.Add(Def.Metal ? "rusty" : "burnt");
            if (Erosion == 2) pre.Add(Def.Metal ? "very rusty" : "very burnt");
            if (Erosion >= 3) pre.Add(Def.Metal ? "thoroughly rusty" : "thoroughly burnt");
            if (showEnch && !Def.Artifact) pre.Add((Ench >= 0 ? "+" : "") + Ench);
            if (Def.Kind == IK.Ring && Known && TypeKnown && (Def.Effect == "r_protection")) post.Add((Ench >= 0 ? "+" : "") + Ench);
            if (Def.Artifact && Known && Def.Kind == IK.Weapon) post.Add("(" + (Ench >= 0 ? "+" : "") + Ench + ")");
            if (full)
            {
                if ((Def.Kind == IK.Wand || !Def.Charges.IsZero) && (ChargesKnown || Known)) post.Add("(" + Charges + ")");
                if (Def.Kind == IK.Light && Def.Fuel < 99999) post.Add("(" + (Lit ? "lit, " : "") + Fuel + ")");
                else if (Def.Kind == IK.Light && Lit) post.Add("(lit)");
                if (Def.Kind == IK.Book && Known && BookSpells != null) post.Add("[" + string.Join(", ", BookSpells.Select(s => SpellDb.Get(s).Name)) + "]");
                if (Def.Kind == IK.Chest)
                {
                    if (Opened) post.Add("(open)");
                    else if (Locked) post.Add("(locked)");
                    if (TrapKnown && Trapped) post.Add("(trapped)");
                }
                if (MagicSensed && !Known && IsMagic) post.Add("{magic}");
                if (Tried && !TypeKnown) post.Add("(tried)");
                if (Price > 0) post.Add("(for sale, " + Price * Count + " gp)");
            }
            string s = string.Join(" ", pre.Concat(new[] { core }).Concat(post));
            if (!article) return s;
            if (Def.Artifact && (Known || Def.Kind == IK.Heart)) return (Def.Kind == IK.Heart ? "the " : "") + s;
            if (Def.Kind is IK.Boots or IK.Gloves or IK.Bracers) return (plural ? Count + " pairs of " : "a pair of ") + s;
            if (plural) return Count + " " + s;
            return Article(s);
        }

        public Color Color
        {
            get
            {
                if (Def.Kind == IK.Potion) return Dm.Ident.LookColor(Def.Id, Def.C);
                if (Def.Kind == IK.Wand || Def.Kind == IK.Ring || Def.Kind == IK.Amulet) return Dm.Ident.LookColor(Def.Id, Def.C);
                if (Def.Kind == IK.Corpse && CorpseOf != null) return Pal.Mul(CorpseOf.C, 0.75f);
                return Def.C;
            }
        }

        public char Glyph => Def.Kind == IK.Corpse ? '%' : Def.G;

        public int Value
        {
            get
            {
                int v = Def.Value;
                if (Def.Kind is IK.Weapon or IK.Armor or IK.Shield or IK.Helm && Ench > 0) v += Ench * Ench * 300;
                if (Def.Kind == IK.Ring && Def.Effect == "r_protection") v = 400 * Math.Max(1, Ench);
                if (Cursed) v /= 4;
                return Math.Max(0, v);
            }
        }

        public string Describe()
        {
            var lines = new List<string>();
            var d = Def;
            if (d.Kind == IK.Weapon || d.Kind == IK.Ammo)
            {
                string dm = "Damage " + d.Dmg + " (vs. large " + d.DmgL + ")";
                if (d.Has(WF.Launcher)) dm = "Launcher for " + d.Ammo + "s; range " + d.Range;
                lines.Add(dm + (d.Has(WF.TwoHand) ? ", two-handed" : "") + (d.Has(WF.Blunt) ? ", blunt" : "") + (d.Has(WF.Thrown) ? ", throwable" : "") + (d.Has(WF.Silver) ? ", silver" : "") + ".");
            }
            if (d.Kind == IK.Armor) lines.Add("Base armor class " + d.Ac + ".");
            if (d.Kind == IK.Shield) lines.Add("Improves armor class by " + d.Ac + ".");
            if (d.Kind == IK.Bracers) lines.Add("Armor class " + d.Ac + " when no body armor is worn.");
            if (d.Kind == IK.Light) lines.Add("Light radius " + d.LightRadius + (d.Fuel < 99999 ? ", " + Fuel + " turns of fuel." : ", never burns out."));
            if (d.Kind == IK.Food || d.Kind == IK.Corpse) lines.Add("Nutrition " + d.Nutrition + ".");
            if (TypeKnown || !d.Unknown) lines.Add(d.Desc);
            else lines.Add("You have not identified this " + d.Kind.ToString().ToLower() + ". Its nature is a mystery.");
            if (d.Kind == IK.Book && BookSpells != null)
            {
                if (Known) lines.Add("Contains: " + string.Join(", ", BookSpells.Select(s => SpellDb.Get(s).Name + " (L" + SpellDb.Get(s).Level + ")")) + ".");
                else lines.Add("The pages are full of arcane script. A magic-user could read it.");
            }
            if (Known && Cursed) lines.Add("{bad}It is cursed.{/}");
            lines.Add("{dim}Weight " + Weight + ". " + (Value > 0 ? "Worth about " + Value + " gp." : "") + "{/}");
            return string.Join("\n", lines);
        }
    }

    public class IdentTable
    {
        readonly Dictionary<string, string> looks = new Dictionary<string, string>();
        readonly Dictionary<string, Color> lookColors = new Dictionary<string, Color>();
        public readonly HashSet<string> Known = new HashSet<string>();
        public readonly Dictionary<string, string> Called = new Dictionary<string, string>();
        public Dictionary<string, string> Looks => looks;
        public Dictionary<string, Color> LookColors => lookColors;

        public void Shuffle()
        {
            looks.Clear();
            lookColors.Clear();
            Known.Clear();
            Assign(IK.Potion, ItemDb.PotionLooks);
            Assign(IK.Scroll, ItemDb.ScrollLooks);
            Assign(IK.Wand, ItemDb.WandLooks);
            Assign(IK.Ring, ItemDb.RingLooks);
            Assign(IK.Amulet, ItemDb.AmuletLooks);
        }

        void Assign(IK kind, string[] pool)
        {
            var ids = ItemDb.All.Values.Where(d => d.Kind == kind && d.Unknown).Select(d => d.Id).ToList();
            var p = pool.ToList();
            Rng.Shuffle(p);
            for (int i = 0; i < ids.Count; i++)
            {
                string look = i < p.Count ? p[i] : "strange #" + i;
                looks[ids[i]] = look;
                lookColors[ids[i]] = ColorFor(kind, look);
            }
        }

        static Color ColorFor(IK kind, string look)
        {
            string l = look.ToLower();
            (string key, Color c)[] map =
            {
                ("crimson", new Color(220, 40, 50)), ("ruby", new Color(220, 30, 60)), ("green", new Color(80, 200, 90)), ("jade", new Color(80, 200, 120)),
                ("emerald", new Color(40, 200, 90)), ("smoky", new Color(150, 150, 150)), ("violet", new Color(180, 100, 240)), ("milky", new Color(240, 240, 230)),
                ("amber", new Color(240, 170, 50)), ("black", new Color(80, 80, 90)), ("ink", new Color(70, 70, 110)), ("pink", new Color(250, 150, 190)),
                ("blue", new Color(90, 140, 240)), ("sapphire", new Color(60, 90, 240)), ("oily", new Color(150, 130, 80)), ("gold", new Color(255, 210, 60)),
                ("silver", new Color(210, 215, 225)), ("brackish", new Color(120, 140, 100)), ("luminous", new Color(230, 255, 200)), ("orange", new Color(250, 140, 40)),
                ("steaming", new Color(220, 210, 200)), ("icy", new Color(180, 230, 255)), ("clotted", new Color(150, 40, 40)), ("rainbow", new Color(255, 180, 255)),
                ("tea", new Color(170, 120, 60)), ("yellow", new Color(240, 230, 80)), ("white", new Color(245, 245, 245)), ("chartreuse", new Color(190, 240, 60)),
                ("sea", new Color(80, 200, 170)), ("oak", new Color(160, 120, 70)), ("ebony", new Color(70, 60, 60)), ("bone", new Color(230, 220, 200)),
                ("glass", new Color(190, 230, 240)), ("copper", new Color(210, 120, 70)), ("iron", new Color(150, 150, 160)), ("crystal", new Color(200, 240, 255)),
                ("marble", new Color(230, 230, 230)), ("pine", new Color(190, 160, 100)), ("redwood", new Color(170, 70, 50)), ("brass", new Color(220, 190, 90)),
                ("ivory", new Color(250, 245, 220)), ("tin", new Color(190, 190, 190)), ("platinum", new Color(230, 230, 240)), ("uranium", new Color(140, 255, 120)),
                ("moonstone", new Color(210, 220, 255)), ("tiger", new Color(200, 140, 40)), ("onyx", new Color(60, 60, 70)), ("opal", new Color(230, 240, 255)),
                ("topaz", new Color(250, 200, 70)), ("wood", new Color(160, 120, 70)), ("coral", new Color(250, 120, 110)), ("agate", new Color(180, 120, 100)),
                ("pearl", new Color(245, 240, 235)), ("granite", new Color(150, 140, 140)), ("obsidian", new Color(50, 40, 60)), ("wire", new Color(180, 180, 190)),
                ("engagement", new Color(255, 220, 120)), ("dark", new Color(140, 40, 60)),
            };
            foreach (var (key, c) in map) if (l.Contains(key)) return c;
            return kind switch
            {
                IK.Potion => new Color(Rng.Range(80, 255), Rng.Range(80, 255), Rng.Range(80, 255)),
                IK.Amulet => new Color(240, 200, 100),
                IK.Wand => new Color(180, 160, 140),
                _ => new Color(200, 200, 200),
            };
        }

        public string Appearance(string id) => looks.TryGetValue(id, out var l) ? l : id;
        public Color LookColor(string id, Color fallback) => lookColors.TryGetValue(id, out var c) ? c : fallback;
        public bool IsKnown(string id) => Known.Contains(id);

        public bool Learn(string id)
        {
            return Known.Add(id);
        }
    }
}
