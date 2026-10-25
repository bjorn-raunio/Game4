using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icosa
{
    public static class SpellDb
    {
        public static readonly Dictionary<SpellId, SpellDef> All = new Dictionary<SpellId, SpellDef>();

        static void S(SpellId id, string name, int lvl, bool cleric, Target t, int range, Color c, bool off, string desc, bool monsterOnly = false)
        {
            All[id] = new SpellDef { Id = id, Name = name, Level = lvl, Cleric = cleric, Tgt = t, Range = range, C = c, Offensive = off, Desc = desc, MonsterOnly = monsterOnly };
        }

        public static SpellDef Get(SpellId id) => All[id];

        public static IEnumerable<SpellDef> Mage(int level) => All.Values.Where(s => !s.Cleric && !s.MonsterOnly && s.Level == level);
        public static IEnumerable<SpellDef> Cleric(int level) => All.Values.Where(s => s.Cleric && !s.MonsterOnly && s.Level == level);

        static SpellDb()
        {
            var fire = new Color(255, 120, 40);
            var cold = new Color(140, 210, 255);
            var elec = new Color(200, 220, 255);
            var arcane = new Color(190, 140, 255);
            var holy = new Color(255, 240, 160);
            var dark = new Color(120, 70, 160);
            var acid = new Color(150, 230, 60);
            var green = new Color(110, 200, 90);

            S(SpellId.MagicMissile, "Magic Missile", 1, false, Target.Single, 10, arcane, true, "Bolts of force that never miss: 1d4+1 each, one more bolt per two levels after the first (max 5).");
            S(SpellId.Sleep, "Sleep", 1, false, Target.Ball, 8, new Color(160, 160, 240), true, "Puts 2d4 Hit Dice of creatures (4 HD or less) to sleep in a radius of 2. No save.");
            S(SpellId.BurningHands, "Burning Hands", 1, false, Target.Cone, 3, fire, true, "A fan of flame: 1 point per level plus 1d4 to all in a short cone.");
            S(SpellId.ShockingGrasp, "Shocking Grasp", 1, false, Target.Touch, 1, elec, true, "Touch for 1d8 + 1 per level electrical damage. Never misses.");
            S(SpellId.Shield, "Shield", 1, false, Target.Self, 0, arcane, false, "An invisible disc grants AC 2 against missiles and AC 4 against melee for 5 turns per level.");
            S(SpellId.CharmPerson, "Charm Person", 1, false, Target.Single, 6, new Color(255, 150, 200), true, "A humanoid of 4 HD or less must save versus spell or become your devoted friend.");
            S(SpellId.Light, "Light", 1, false, Target.Self, 0, new Color(255, 250, 200), false, "Conjures a hovering mote of light that lights your surroundings for a long while.");
            S(SpellId.Identify, "Identify", 1, false, Target.Item, 0, arcane, false, "Reveals the nature of one item. At higher levels, more items.");
            S(SpellId.DetectMagic, "Detect Magic", 1, false, Target.Self, 0, arcane, false, "Reveals which items on this level and in your pack are magical.");
            S(SpellId.HoldPortal, "Hold Portal", 1, false, Target.Dir, 1, arcane, false, "Magically seals an adjacent door shut.");
            S(SpellId.ProtEvil, "Protection from Evil", 1, false, Target.Self, 0, holy, false, "Evil creatures suffer -2 to hit you and you gain +2 to saves against them.");
            S(SpellId.FindFamiliar, "Find Familiar", 1, false, Target.Self, 0, arcane, false, "Summons a small animal companion bound to your will.");

            S(SpellId.Web, "Web", 2, false, Target.Ball, 8, new Color(220, 220, 220), true, "Fills an area with sticky webbing that entangles creatures.");
            S(SpellId.Invisibility, "Invisibility", 2, false, Target.Self, 0, new Color(200, 200, 255), false, "You become invisible until you attack.");
            S(SpellId.MirrorImage, "Mirror Image", 2, false, Target.Self, 0, arcane, false, "Creates 1d4 illusory duplicates of you. Each absorbs one attack.");
            S(SpellId.Knock, "Knock", 2, false, Target.Dir, 1, arcane, false, "Opens locked or stuck doors and chests.");
            S(SpellId.StinkingCloud, "Stinking Cloud", 2, false, Target.Ball, 8, green, true, "A nauseating cloud that leaves creatures helpless with retching unless they save.");
            S(SpellId.AcidArrow, "Acid Arrow", 2, false, Target.Bolt, 10, acid, true, "A magical arrow of acid that deals 2d4 and keeps burning for several turns.");
            S(SpellId.Strength, "Strength", 2, false, Target.Self, 0, new Color(240, 160, 80), false, "Grants great strength for a while.");
            S(SpellId.RayEnfeeble, "Ray of Enfeeblement", 2, false, Target.Bolt, 8, dark, true, "A ray that weakens a creature, reducing the damage it deals.");
            S(SpellId.Darkness, "Darkness 15' Radius", 2, false, Target.Ball, 8, dark, false, "Creates a globe of magical darkness.");
            S(SpellId.Scare, "Scare", 2, false, Target.Single, 6, dark, true, "Frightens a creature of 6 HD or less into fleeing.");

            S(SpellId.Fireball, "Fireball", 3, false, Target.Ball, 10, fire, true, "A bead of flame bursts in a roaring sphere: 1d6 per level (max 10d6). Save for half.");
            S(SpellId.LightningBolt, "Lightning Bolt", 3, false, Target.Bolt, 12, elec, true, "A stroke of lightning, 1d6 per level (max 10d6), that passes through creatures and rebounds off walls.");
            S(SpellId.Haste, "Haste", 3, false, Target.Self, 0, new Color(255, 230, 120), false, "Doubles your speed for a time.");
            S(SpellId.HoldPerson, "Hold Person", 3, false, Target.Single, 8, new Color(200, 200, 120), true, "Paralyzes a humanoid that fails a save versus spell.");
            S(SpellId.DispelMagic, "Dispel Magic", 3, false, Target.Ball, 8, arcane, false, "Ends magical effects on everyone in an area, friend and foe.");
            S(SpellId.Slow, "Slow", 3, false, Target.Ball, 8, new Color(120, 150, 200), true, "Creatures in an area move at half speed unless they save.");
            S(SpellId.Blink, "Blink", 3, false, Target.Self, 0, arcane, false, "You blink to a random nearby location.");
            S(SpellId.ProtMissiles, "Protection from Normal Missiles", 3, false, Target.Self, 0, arcane, false, "Arrows, bolts, and thrown rocks cannot harm you.");
            S(SpellId.MonsterSummon, "Monster Summoning", 3, false, Target.Self, 0, arcane, false, "Summons 2d4 monsters from the dungeon's lists to fight for you.");

            S(SpellId.IceStorm, "Ice Storm", 4, false, Target.Ball, 10, cold, true, "Hailstones pound an area for 3d10 damage.");
            S(SpellId.DimensionDoor, "Dimension Door", 4, false, Target.Location, 12, arcane, false, "Step through space to a visible location.");
            S(SpellId.Fear, "Fear", 4, false, Target.Cone, 6, dark, true, "A cone of terror. Creatures that fail a save flee in panic.");
            S(SpellId.WallOfFire, "Wall of Fire", 4, false, Target.Location, 8, fire, true, "A curtain of roaring flame springs up, burning all who pass.");
            S(SpellId.PolymorphOther, "Polymorph Other", 4, false, Target.Bolt, 8, new Color(220, 120, 220), true, "Transforms a creature into a random other creature. Who knows?");
            S(SpellId.CharmMonster, "Charm Monster", 4, false, Target.Single, 8, new Color(255, 150, 200), true, "As Charm Person, but works on any living creature.");
            S(SpellId.FireShield, "Fire Shield", 4, false, Target.Self, 0, fire, false, "Flames sheathe you. Melee attackers take double the damage they deal.");

            S(SpellId.ConeOfCold, "Cone of Cold", 5, false, Target.Cone, 6, cold, true, "A cone of numbing cold: 1d4+1 per level. Save for half.");
            S(SpellId.Cloudkill, "Cloudkill", 5, false, Target.Ball, 8, green, true, "A poisonous cloud that kills weak creatures outright and harms the strong.");
            S(SpellId.HoldMonster, "Hold Monster", 5, false, Target.Single, 8, new Color(200, 200, 120), true, "Paralyzes any creature that fails a save versus spell.");
            S(SpellId.Teleport, "Teleport", 5, false, Target.Self, 0, arcane, false, "Teleports you elsewhere on this level, or to a level of your choosing.");
            S(SpellId.AnimateDead, "Animate Dead", 5, false, Target.Self, 0, dark, false, "Raises nearby corpses as skeletal servants.");
            S(SpellId.Passwall, "Passwall", 5, false, Target.Dir, 1, arcane, false, "Opens a tunnel through solid stone.");
            S(SpellId.Feeblemind, "Feeblemind", 5, false, Target.Single, 8, dark, true, "Reduces a spellcaster to a drooling idiot.");

            S(SpellId.Disintegrate, "Disintegrate", 6, false, Target.Bolt, 8, new Color(120, 255, 120), true, "A thin green ray. Anything it touches that fails to save is reduced to dust.");
            S(SpellId.DeathSpell, "Death Spell", 6, false, Target.Ball, 8, dark, true, "Snuffs out the lives of many weak creatures. No save.");
            S(SpellId.Globe, "Globe of Invulnerability", 6, false, Target.Self, 0, arcane, false, "A shimmering globe that stops spells of 4th level and lower.");
            S(SpellId.ChainLightning, "Chain Lightning", 6, false, Target.Single, 10, elec, true, "Lightning strikes a target and arcs to others nearby.");

            S(SpellId.FingerOfDeath, "Finger of Death", 7, false, Target.Single, 8, dark, true, "Points at a creature, which dies unless it saves versus death.");
            S(SpellId.PowerWordStun, "Power Word Stun", 7, false, Target.Single, 8, new Color(255, 255, 255), true, "A single word stuns a creature senseless.");
            S(SpellId.DelayedBlast, "Delayed Blast Fireball", 7, false, Target.Ball, 10, fire, true, "A fireball of terrible power: 1d6+1 per level.");
            S(SpellId.IncendiaryCloud, "Incendiary Cloud", 8, false, Target.Ball, 8, fire, true, "A great rolling cloud of flame.");
            S(SpellId.MassCharm, "Mass Charm", 8, false, Target.Ball, 8, new Color(255, 150, 200), true, "Charms every creature in an area that fails to save.");
            S(SpellId.MeteorSwarm, "Meteor Swarm", 9, false, Target.Ball, 12, fire, true, "Four blazing meteors. The dungeon shakes.");
            S(SpellId.PowerWordKill, "Power Word Kill", 9, false, Target.Single, 8, dark, true, "One word. One death. Works on creatures of 60 hit points or fewer.");
            S(SpellId.TimeStop, "Time Stop", 9, false, Target.Self, 0, new Color(200, 200, 255), false, "The world freezes. You do not.");

            S(SpellId.CureLight, "Cure Light Wounds", 1, true, Target.Self, 0, holy, false, "Heals 1d8 hit points.");
            S(SpellId.Bless, "Bless", 1, true, Target.Self, 0, holy, false, "You and your allies gain +1 to hit and to morale.");
            S(SpellId.CProtEvil, "Protection from Evil", 1, true, Target.Self, 0, holy, false, "Evil creatures suffer -2 to hit you and you gain +2 to saves against them.");
            S(SpellId.CLight, "Light", 1, true, Target.Self, 0, new Color(255, 250, 200), false, "Conjures a hovering mote of light.");
            S(SpellId.Command, "Command", 1, true, Target.Single, 6, holy, true, "Speak one word: the creature obeys and grovels for a turn.");
            S(SpellId.DetectEvil, "Detect Evil", 1, true, Target.Self, 0, holy, false, "Senses evil creatures on this level.");
            S(SpellId.Sanctuary, "Sanctuary", 1, true, Target.Self, 0, holy, false, "Monsters must save to attack you, so long as you do not attack.");

            S(SpellId.CHoldPerson, "Hold Person", 2, true, Target.Single, 8, new Color(200, 200, 120), true, "Paralyzes a humanoid that fails a save versus spell.");
            S(SpellId.Silence, "Silence 15' Radius", 2, true, Target.Ball, 8, new Color(150, 150, 170), true, "Creatures in the area cannot cast spells.");
            S(SpellId.SpiritualHammer, "Spiritual Hammer", 2, true, Target.Self, 0, holy, false, "A hammer of force appears and fights at your side.");
            S(SpellId.SlowPoison, "Slow Poison", 2, true, Target.Self, 0, green, false, "Cures poison in your veins.");
            S(SpellId.FindTraps, "Find Traps", 2, true, Target.Self, 0, holy, false, "Reveals all traps within sight.");
            S(SpellId.ResistFire, "Resist Fire", 2, true, Target.Self, 0, fire, false, "Halves fire damage for a time.");
            S(SpellId.Augury, "Augury", 2, true, Target.Self, 0, holy, false, "Ask your god what awaits on this level. The answer is honest, if cryptic.");

            S(SpellId.CureDisease, "Cure Disease", 3, true, Target.Self, 0, holy, false, "Cures disease, rot, and sliming.");
            S(SpellId.Prayer, "Prayer", 3, true, Target.Self, 0, holy, false, "You and allies gain +1 to hit, damage and saves; enemies nearby suffer -1.");
            S(SpellId.RemoveCurse, "Remove Curse", 3, true, Target.Self, 0, holy, false, "Removes curses from your equipped items.");
            S(SpellId.CDispel, "Dispel Magic", 3, true, Target.Ball, 8, holy, false, "Ends magical effects in an area.");
            S(SpellId.CreateFood, "Create Food and Water", 3, true, Target.Self, 0, holy, false, "Creates a bounty of simple food.");
            S(SpellId.CAnimateDead, "Animate Dead", 3, true, Target.Self, 0, dark, false, "Raises nearby corpses as skeletal servants.");
            S(SpellId.SpeakWithDead, "Speak with Dead", 3, true, Target.Self, 0, dark, false, "Ask a nearby corpse what it knew. The dead gossip.");
            S(SpellId.ContinualLight, "Continual Light", 3, true, Target.Self, 0, new Color(255, 250, 200), false, "A light that does not fade for a very long time.");
            S(SpellId.GlyphWarding, "Glyph of Warding", 3, true, Target.Self, 0, holy, false, "Inscribes a burning glyph beneath you that harms the next enemy to cross it.");

            S(SpellId.CureSerious, "Cure Serious Wounds", 4, true, Target.Self, 0, holy, false, "Heals 2d8+1 hit points.");
            S(SpellId.NeutralizePoison, "Neutralize Poison", 4, true, Target.Self, 0, green, false, "Cures all poison and grants a short immunity.");
            S(SpellId.SticksToSnakes, "Sticks to Snakes", 4, true, Target.Self, 0, green, false, "Turns sticks into 1d4+level/4 snakes that fight for you.");
            S(SpellId.Divination, "Divination", 4, true, Target.Self, 0, holy, false, "Reveals the layout of the level and the location of its stairs.");
            S(SpellId.ProtEvil10, "Protection from Evil 10' Radius", 4, true, Target.Self, 0, holy, false, "A long-lasting ward against evil.");

            S(SpellId.CureCritical, "Cure Critical Wounds", 5, true, Target.Self, 0, holy, false, "Heals 3d8+3 hit points.");
            S(SpellId.FlameStrike, "Flame Strike", 5, true, Target.Ball, 8, fire, true, "A column of holy fire: 6d8, save for half. Radius 1.");
            S(SpellId.InsectPlague, "Insect Plague", 5, true, Target.Self, 0, new Color(150, 140, 60), false, "Summons swarms of biting insects to plague your foes.");
            S(SpellId.DispelEvil, "Dispel Evil", 5, true, Target.Ball, 6, holy, true, "Evil creatures in the area must save or be banished.");
            S(SpellId.Commune, "Commune", 5, true, Target.Self, 0, holy, false, "Your god reveals the location of every creature and object on the level.");
            S(SpellId.TrueSeeing, "True Seeing", 5, true, Target.Self, 0, holy, false, "See the invisible, the secret, and the hidden.");

            S(SpellId.Heal, "Heal", 6, true, Target.Self, 0, holy, false, "Heals all but 1d4 hit points, cures disease, blindness and confusion.");
            S(SpellId.BladeBarrier, "Blade Barrier", 6, true, Target.Self, 0, new Color(220, 220, 240), false, "A whirling wall of blades surrounds you, shredding all that come near.");
            S(SpellId.WordOfRecall, "Word of Recall", 6, true, Target.Self, 0, holy, false, "Returns you instantly to the up stairs of the first level, or back to the deepest level you have reached.");
            S(SpellId.FindPath, "Find the Path", 6, true, Target.Self, 0, holy, false, "Reveals the way to the down staircase.");
            S(SpellId.Harm, "Harm", 6, true, Target.Touch, 1, dark, true, "A touch that reduces a creature to 1d4 hit points.");

            S(SpellId.HolyWord, "Holy Word", 7, true, Target.Ball, 6, holy, true, "A word of power: evil creatures are stunned, paralyzed, or slain.");
            S(SpellId.Earthquake, "Earthquake", 7, true, Target.Self, 0, new Color(160, 130, 90), true, "The earth heaves. Everything nearby takes damage and the walls crack.");
            S(SpellId.Restoration, "Restoration", 7, true, Target.Self, 0, holy, false, "Restores drained levels and ability scores.");
            S(SpellId.Regenerate, "Regenerate", 7, true, Target.Self, 0, holy, false, "Your wounds close continuously for a long time.");

            S(SpellId.CauseLight, "Cause Light Wounds", 1, true, Target.Single, 6, dark, true, "Inflicts 1d8 damage.", true);
            S(SpellId.CauseSerious, "Cause Serious Wounds", 4, true, Target.Single, 6, dark, true, "Inflicts 2d8+1 damage.", true);
            S(SpellId.Darkness2, "Darkness", 2, true, Target.Self, 0, dark, false, "Shrouds the area in darkness.", true);
            S(SpellId.FaerieFire, "Faerie Fire", 1, true, Target.Single, 8, new Color(220, 120, 255), true, "Outlines the target in violet flame.", true);
            S(SpellId.MindBlast, "Mind Blast", 5, false, Target.Cone, 6, new Color(220, 140, 255), true, "A cone of psionic force that stuns.", true);
            S(SpellId.SummonUndead, "Summon Undead", 5, false, Target.Self, 0, dark, false, "Calls the dead to serve.", true);
            S(SpellId.EyeRays, "Eye Rays", 5, false, Target.Single, 8, new Color(255, 200, 220), true, "One of ten terrible rays.", true);
        }
    }
}
