using System.Globalization;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (System.Array.IndexOf(args, "--worldgen") >= 0) { Icosa.GenStats.World(args); return; }
if (System.Array.IndexOf(args, "--genstats") >= 0) { Icosa.GenStats.Run(args); return; }
using var game = new Icosa.RogueGame(args);
game.Run();
