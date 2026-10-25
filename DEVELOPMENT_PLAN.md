# Development Plan — Tomb of the Twenty-Sided Heart

Roadmap for expanding the game. Phases are ordered by priority; items within a phase can be done in any order. Each item lists where the work lands and how to verify it. Conventions are in `claude.md`.

---

## Phase 0 — Known Issues & Balance (do first)

| Item | Detail | Where | Done when |
|---|---|---|---|
| Early-game lethality | Bot runs show most deaths on levels 1–3, mainly from packs of wolves and worgs, skeletons, and wererats that only silver or magic can hurt. Tune group sizes, monster depths and level-1 HP; consider granting one silver weapon in some kits. | `MonsterDb`, `Spawn.PlaceGroup`, `Chargen.Kit` | Bot median deepest level ≥ 4 over 40 games |
| Food clock | Starvation is still the top bot death. Audit food per level against turns per level, and corpse drop rate and nutrition. | `Spawn.KindWeights`, `ItemDb` food, `Dm.WorldTurn` | Starvation < 10% of bot deaths |
| Sealed vaults | A vault can generate with no entrance at all. Always attach a secret door, or put a teleport trap on the level. | `Gen.AddVault` | No vault without an entrance across 200 generated levels |
| Locked doors blocking stairs | The connectivity check counts deep water as passable and ignores locked doors. Make sure the stairs stay reachable using only kickable doors. | `Gen.SecretAndLocked` | Bot `EXPLORE-STUCK` count near zero |
| Bot competence | Teach the bot to retreat, use stairs to escape, kick locked doors, wield silver against were-creatures, and eat corpses before rations. Better bot = better balance data. | `Bot.cs` | Bot regularly reaches level 8+ |
| Message spam | "Dice clatter behind a screen" fires too often; merge repeated combat lines. | `Dm.WanderingMonster`, `Dm.Msg` | Log stays readable in a 10-monster fight |

---

## Phase 1 — Core Feature Gaps

1. **Options screen**: volume, separate music/SFX mute, animation speed, key-binding display, colorblind palette toggle, and auto-pickup rules (gold/ammo/none). Persist to `%LocalAppData%/Icosa/options.txt`.
2. **Item naming ("call")**: let the player label unidentified item types (e.g. "smoky potion = maybe healing"). Store it in `IdentTable.Called` (already present but unused), show it in item names, and save it.
3. **Price identification**: shops show base prices in a way that hints at the item type, as in NetHack.
4. **Monsters using items**: intelligent monsters with `Hands` drink healing potions, zap wands and read scrolls from their `Inv`. Add an AI branch in `Ai.Specials`.
5. **Ranger and paladin spellcasting**: rangers get druid/mage spells at level 8+ and paladins clerical spells at level 9+. `Rules.Slots` partly supports paladins; add druid spells to `SpellDb`.
6. **Two-weapon fighting and a weapon-swap key**: for rangers and fighters, swap between a melee weapon and a launcher with one key.
7. **Containers**: a real Bag of Holding inventory (put in / take out) instead of passive weight reduction, plus sacks and boxes.
8. **Monster memory codex**: a "Monster Manual" screen of every creature seen, with kill counts and lore, unlocked by encounters.

---

## Phase 2 — Content Expansion

### Monsters (target +30)
- **Low (1–4):** giant frog, troglodyte (stench), xvart, giant weasel, kobold shaman, goblin wolf-rider.
- **Mid (5–9):** gargoyle (statue disguise), ogre mage (invisibility, cold cone), lamia (Wisdom drain), will-o'-wisp swarm, gnoll pack leader, earth elemental, ghast (stench + paralysis).
- **Deep (10–15):** iron golem (fire heals it), mind flayer elder brain encounter, death knight, nalfeshnee, drider, ancient dragons, demilich (soul-trapping gems).

### Items
- More artifacts (Luck Blade, Staff of the Magi, Robe of Eyes, Cubic Gate).
- More potions: polymorph self, gaseous form, climbing, dragon control.
- More rings: spell storing, telekinesis, shooting stars.
- Wands of negation and enemy detection.
- A "d4 of Despair", a "Loaded d20", and other dice-themed curios that change roll mechanics.

### Spells
- Fill the gaps at levels 6–9 (Contingency, Prismatic Spray, Gate, Wish).
- Druid list: Entangle, Call Lightning, Summon Animal.

### Dungeon features & themes
- **Branches:** the Gnomish Mines (levels 3–6, a town with temple and shops) and the Spider Queen's sub-level.
- **New room themes:** alchemy lab (mix potions), wizard's study (spellbooks plus guardian), prison (rescue an NPC ally), gambling hall (bet gold on d20 rolls against an NPC).
- **Hazards:** collapsing ceilings, rising water, darkness zones that swallow light radius.
- **Hand-crafted vault templates** loaded from text files in `Content/` and merged into generation.

### Interaction
- **Lycanthropy:** catching it from were-creatures leads to transformations; cured by prayer or wolfsbane.
- **Polymorph self:** become a monster temporarily with its attacks and flags.
- **NPC hirelings:** recruit at the town for gold, loyalty driven by Charisma (classic AD&D).
- **Quests:** each level has a 25% chance of a small objective from a ghost or NPC, rewarded with XP and items.

---

## Phase 3 — Endgame & Replayability

1. **Referee fight phases**: at 50% HP the Screen collapses and the arena changes; at 25% the lich rolls on a deadlier table. Add the demilich phylactery as a true kill condition.
2. **Ascension pressure**: while the Heart is carried, the level layouts shift (rerolls of levels already visited) and a pursuer chases the player (the Referee's "Rules Lawyer" or a revived lich shade).
3. **Bones files**: a dead character's ghost and gear can appear in future games (saved under `%LocalAppData%/Icosa/bones/`).
4. **Daily seed and challenge runs**: a fixed daily seed, conduct tracking (no prayer, no armor, illiterate, pacifist) shown on the tombstone and in the Hall of Fame.
5. **Achievements screen**: built from `Dm.Achievements` and persisted across games.
6. **Morgue file**: write a full text dump of the character, inventory, last messages and the dungeon overview on death.

---

## Phase 4 — Presentation & Polish

- Optional tiles mode: a 16×16 sprite atlas with a toggle between ASCII and tiles in the renderer (`RogueGame.DrawTerminal`).
- More juice: screen shake on crits and explosions, floating damage numbers (`TextFx` exists but is unused), a death slow-motion fade.
- Music: procedural melody layers per depth band on top of the existing ambience (`Synth.Ambience`).
- An in-game tutorial level (level 0) with scripted messages covering THAC0, saves and spell memorization.
- Larger maps with a scrolling viewport (currently fixed 100×36) as an option.
- Accessibility: font size scaling, high-contrast palette, a screen-reader-friendly message log export.

---

## Technical Work (ongoing)

- **Save versioning**: keep `SaveGame.Version` in sync, and add migration or graceful rejection for old saves.
- **Data-driven content**: move the monster, item and spell tables to JSON in `Content/` so they can be modded without recompiling. Keep the C# builders as the loader.
- **Performance**: cache static-light contributions per level and only recompute dynamic lights each turn (`Map.ComputeLight`).
- **Tests**: a small xUnit project for `Rules`, `Dice.Parse`, `Markup.Wrap`, save/load round-trips and generator connectivity (a seeded sweep of 500 levels).
- **CI-style check**: a script that runs `--bot 20` and fails on any `EXCEPTION`, `STUCK` or `EXPLORE-STUCK` line.

---

## Verification Checklist per Feature
1. `dotnet build` with 0 errors (kill any running `Icosa.dll` first).
2. A screenshot pass via `--auto` (plus `--wizard` to reach deep content) shows the feature rendering correctly.
3. `--bot 40 log.txt` shows no new exceptions, and balance metrics don't regress.
4. Any new state is saved and restored (save, quit, continue).
5. Help text (`HelpScreen`) and `README.md` are updated if controls changed.
