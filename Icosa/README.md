# Tomb of the Twenty-Sided Heart

A roguelike in the spirit of Advanced Dungeons & Dragons, built with C# and MonoGame (DesktopGL).

Descend fifteen levels, take the Icosahedral Heart from the Hollow King — a lich who has guarded it behind a veil of bone for nine hundred years — and climb back out into daylight.

## Running

```
dotnet run --project Icosa
```

Requires the .NET 9 SDK (or newer, via roll-forward). Fonts are built from Windows' Consolas by the content pipeline.

## Features

- AD&D-style rules: 4d6-drop-lowest ability scores with 18/xx exceptional strength, 7 races, 6 classes, 9 alignments with their own deities, THAC0 vs. descending AC, the five saving throws, Vancian spell memorization, turn undead, backstab, thief skills, lay on hands, infravision, damage vs. Large creatures, fighter sweeps against weaklings, morale, surprise and wandering monster checks.
- ~80 monsters, from kobolds to beholders, plus some that only live here: paper golems, ink wraiths that smear your map, the Errata, the Wandering Monster itself, dice golems.
- ~100 spells, NetHack-style unidentified potions/scrolls/wands/rings/amulets, artifacts, ioun stones that orbit your head, the Deck of Many Things, shops, thrones, fountains, altars and the Oracle Die.
- Every die roll is shown in the Dice Tray. Explored-but-unseen areas are drawn in blue ink on graph paper.
- Colored dynamic lighting, procedural sound and ambience, save/restore, Hall of Fame.

Press `?` in game for the full key list. Mouse: hover to inspect, left-click to travel/attack, right-click to look.

## Developer switches

- `--seed N` fixed RNG seed
- `--wizard` debug keys (Ctrl+D descend, Ctrl+M map, Ctrl+H heal, Ctrl+L level, Ctrl+I item)
- `--auto script.txt` scripted input with `shot:file.png` screenshots
- `--bot N log.txt` an automated player that plays N games and logs results and exceptions
- `--genstats N out.txt [--seed S]` generates N levels of every dungeon depth headlessly and writes a table of layout metrics (route length, dead ends, loop %, unreachable cells, monsters near the stairs, item detour), set-piece counts and ASCII dumps of any disconnected level
