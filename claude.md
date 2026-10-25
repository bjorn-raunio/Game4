# CLAUDE.md
- You are an unparalleled genius human designer who has schizophrenia. Your brain connects concepts that have only the most tangential relation, finding unique and special new creative ideas.
- Do not write any comments in code.

## The Game
**Tomb of the Twenty-Sided Heart** (`Icosa/`) is a C# MonoGame (DesktopGL, .NET 9) roguelike in the spirit of AD&D and NetHack. The player starts in a town, travels an overland map (square = a mile) to towns and dungeon entrances, with AD&D random encounters on separate local maps (levels are keyed by branch: `Map.Key = Branch*100+Depth`; branch 0 depth 0 is the overland, branches 10+ are towns, 99 is the current encounter), descends the 15-level Tomb, takes the Icosahedral Heart from the Hollow King (a lich enthroned behind a Bone Veil) and climbs back out. It uses AD&D rules (THAC0, descending AC, saving throws, Vancian spells, races/classes/alignments), shows every die roll in a dice tray, and draws remembered map areas as ink on graph paper. Run with `dotnet run --project Icosa`.
- The game is under development; changes that break existing save files are fine.

## Code Structure (`Icosa/Src`)
- `RogueGame.cs` – MonoGame host: renderer (terminal grid, box glyphs drawn as primitives), keyboard/mouse input, screen stack (`Ui`), `AutoPilot` script runner, bot hookup.
- `Bot.cs` – automated player for stress/balance testing.
- `Core/` – `Rng`/`Dice` and the `Pal` color palette.
- `Render/` – `Terminal` (133×45 cell buffer, `{color}markup{/}` text), `Anim` (bolts, bursts, flashes), `Synth` (procedural sound effects and ambience).
- `Data/` – static content: `MonsterDb`, `ItemDb`, `SpellDb`, `Rules` (races, classes, THAC0, saves, slots, stat tables), `Lore` (names, graffiti, epitaphs), `WorldDb` (dungeon branches, towns, gossip).
- `Entities/` – `Defs` (enums, `MonsterDef`/`ItemDef`/`SpellDef`), `Item` (naming, identification table), `Actor`/`Monster`/`Player`.
- `World/` – `Map` (tiles, lighting, vision, memory, places), `Fov` (shadowcasting) and `Path` (Dijkstra maps, lines), `Gen` (room-accretion dungeon generator, themes, final level, side-dungeon sanctums), `Wild` (overland map at travel scale with roads, sites and bells; separate local town maps; random-encounter maps; travel time), `Spawn` (monsters, loot, traps, shops).
- `Systems/` – `Dm` (global game state, energy-based turn loop, messages, saves, levels, XP), `Combat`, `Magic` (all spells), `Ai`, `Actions` (player verbs), `Use` (potions/scrolls/wands/tools/features), `Traps`, `SaveGame` (binary serializer).
- `UI/` – `GameScreen` (map, sidebar, log, command dispatch), `Menus` (menus, prompts, targeting/look), `Screens` (spell prep, character sheet, help, death/victory, hall of fame), `TitleScreen` (title, character creation, starting kits).

## Best Practices
- Content goes in the `Data/` tables (builder helpers like `M(...)`, `I(...)`, `S(...)`); behavior goes in `Systems/`, keyed by `Effect` strings, `SpellId` or `AtkSp`.
- Game logic acts through `Dm`: report with `Dm.Msg` (markup colors), end a player action with exactly one `Dm.PlayerActed(cost)`, deal damage only via `Combat.Damage`, roll player d20s with `Dm.PlayerD20` and log meaningful rolls with `Dm.AddDie`.
- UI prompts use `Prompts` (menus, yes/no, direction, target) with callbacks; never block in logic code.
- Only use glyphs that exist in `Content/*.spritefont` character regions; add a region if a new Unicode glyph is needed. Box/block characters (U+2500–U+259F) are drawn as primitives.
- When adding fields to entities or maps, update `SaveGame` (and bump `Version`).
- Keep code comment-free and match the surrounding compact style.
- Verify changes: kill any running `Icosa.dll` before `dotnet build` (DLL lock), take screenshots with `--auto script.txt` (`shot:file.png`, `^d` ctrl keys, `--wizard` debug keys), and run `--bot N log.txt` to catch exceptions and check balance.
