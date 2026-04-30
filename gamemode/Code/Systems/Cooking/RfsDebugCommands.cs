using OpenFramework.Systems.Pawn;

namespace OpenFramework.Systems.Cooking;

/// <summary>
/// Commandes console pour tester rapidement le système RFS-style burger V1.
/// Console s&box (touche `) :
///   rfs_kit             → spawn grill + cutting + fryer + plank + 1× chaque ingrédient
///   rfs_stations        → spawn grill + plank
///   rfs_cutting_board   → spawn la planche à découper
///   rfs_fryer           → spawn la friteuse
///   rfs_beef/bun_bottom/bun_top/cheese/lettuce/tomato → spawn 1 ingrédient
///   rfs_whole_tomato/lettuce/cheese/potato → spawn 1 ingrédient entier
///   rfs_raw_fries       → spawn une portion de frites crues
///   rfs_basket          → spawn un panier à friteuse (prop manipulable)
///   rfs_soda_fountain   → spawn une fontaine à soda
///   rfs_empty_cup       → spawn un gobelet vide
///   rfs_preview         → spawn un burger avec TOUTES les couches de la VisualLibrary
///                          empilées (utile pour régler StackHeight de chaque entrée)
/// </summary>
public class RfsDebugCommands : Component
{
	private const string DIR = "items/rfs/";

	[ConCmd( "rfs_stations" )]
	public static void CmdStations()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnStations( pawn );
	}

	[Rpc.Host]
	private static void ServerSpawnStations( PlayerPawn pawn )
	{
		if ( !Networking.IsHost || pawn == null ) return;

		var origin = pawn.WorldPosition + pawn.WorldRotation.Forward * 80f;
		var rot = Rotation.LookAt( -pawn.WorldRotation.Forward );

		SpawnAt( DIR + "rfs_grill.prefab", origin + pawn.WorldRotation.Right * -60f, rot );
		SpawnAt( DIR + "rfs_assembly_plank.prefab", origin + pawn.WorldRotation.Right * 60f, rot );

		Log.Info( "[rfs_stations] Grill + Planche spawnés devant le joueur" );
	}

	[ConCmd( "rfs_beef" )]
	public static void CmdBeef()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_raw_beef.prefab" );
	}

	[ConCmd( "rfs_bun_bottom" )]
	public static void CmdBunBottom()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_bun_bottom.prefab" );
	}

	[ConCmd( "rfs_bun_top" )]
	public static void CmdBunTop()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_bun_top.prefab" );
	}

	[ConCmd( "rfs_cheese" )]
	public static void CmdCheese()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_cheese.prefab" );
	}

	[ConCmd( "rfs_lettuce" )]
	public static void CmdLettuce()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_lettuce.prefab" );
	}

	[ConCmd( "rfs_tomato" )]
	public static void CmdTomato()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_tomato.prefab" );
	}

	[ConCmd( "rfs_whole_tomato" )]
	public static void CmdWholeTomato()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_whole_tomato.prefab" );
	}

	[ConCmd( "rfs_whole_lettuce" )]
	public static void CmdWholeLettuce()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_whole_lettuce.prefab" );
	}

	[ConCmd( "rfs_whole_cheese" )]
	public static void CmdWholeCheese()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_whole_cheese.prefab" );
	}

	[ConCmd( "rfs_whole_potato" )]
	public static void CmdWholePotato()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_whole_potato.prefab" );
	}

	[ConCmd( "rfs_raw_fries" )]
	public static void CmdRawFries()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_raw_fries.prefab" );
	}

	[ConCmd( "rfs_basket" )]
	public static void CmdBasket()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_basket.prefab" );
	}

	[ConCmd( "rfs_fries_pouch" )]
	public static void CmdFriesPouch()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_fries_pouch.prefab" );
	}

	[ConCmd( "rfs_fryer" )]
	public static void CmdFryer()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnFryer( pawn );
	}

	[Rpc.Host]
	private static void ServerSpawnFryer( PlayerPawn pawn )
	{
		if ( !Networking.IsHost || pawn == null ) return;
		var pos = pawn.WorldPosition + pawn.WorldRotation.Forward * 80f;
		var rot = Rotation.LookAt( -pawn.WorldRotation.Forward );
		SpawnAt( DIR + "rfs_fryer.prefab", pos, rot );
		Log.Info( "[rfs_fryer] Friteuse spawnée" );
	}

	[ConCmd( "rfs_soda_fountain" )]
	public static void CmdSodaFountain()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnSodaFountain( pawn );
	}

	[Rpc.Host]
	private static void ServerSpawnSodaFountain( PlayerPawn pawn )
	{
		if ( !Networking.IsHost || pawn == null ) return;
		var pos = pawn.WorldPosition + pawn.WorldRotation.Forward * 80f;
		var rot = Rotation.LookAt( -pawn.WorldRotation.Forward );
		SpawnAt( DIR + "rfs_soda_fountain.prefab", pos, rot );
		Log.Info( "[rfs_soda_fountain] Fontaine à soda spawnée" );
	}

	[ConCmd( "rfs_empty_cup" )]
	public static void CmdEmptyCup()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnIngredient( pawn, "rfs_empty_cup.prefab" );
	}

	[ConCmd( "rfs_cutting_board" )]
	public static void CmdCuttingBoard()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnCuttingBoard( pawn );
	}

	[Rpc.Host]
	private static void ServerSpawnCuttingBoard( PlayerPawn pawn )
	{
		if ( !Networking.IsHost || pawn == null ) return;
		var pos = pawn.WorldPosition + pawn.WorldRotation.Forward * 80f;
		var rot = Rotation.LookAt( -pawn.WorldRotation.Forward );
		SpawnAt( DIR + "rfs_cutting_board.prefab", pos, rot );
		Log.Info( "[rfs_cutting_board] Planche à découper spawnée" );
	}

	[Rpc.Host]
	private static void ServerSpawnIngredient( PlayerPawn pawn, string prefabName )
	{
		if ( !Networking.IsHost || pawn == null ) return;

		// Spawn 30u devant le joueur, à hauteur de tête, avec un peu de rotation
		var pos = pawn.WorldPosition + pawn.WorldRotation.Forward * 30f + Vector3.Up * 50f;
		SpawnAt( DIR + prefabName, pos, Rotation.Identity );

		Log.Info( $"[rfs] {prefabName} spawné devant {pawn.DisplayName}" );
	}

	[ConCmd( "rfs_kit" )]
	public static void CmdKit()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnKit( pawn );
	}

	[Rpc.Host]
	private static void ServerSpawnKit( PlayerPawn pawn )
	{
		if ( !Networking.IsHost || pawn == null ) return;

		var origin = pawn.WorldPosition + pawn.WorldRotation.Forward * 80f;
		var rot = Rotation.LookAt( -pawn.WorldRotation.Forward );

		// 4 stations alignées
		SpawnAt( DIR + "rfs_grill.prefab",          origin + pawn.WorldRotation.Right * -150f, rot );
		SpawnAt( DIR + "rfs_cutting_board.prefab",  origin + pawn.WorldRotation.Right *  -50f, rot );
		SpawnAt( DIR + "rfs_fryer.prefab",          origin + pawn.WorldRotation.Right *   50f, rot );
		SpawnAt( DIR + "rfs_assembly_plank.prefab", origin + pawn.WorldRotation.Right *  150f, rot );

		// Ingrédients en pile devant le joueur (espacés)
		var ingPos = pawn.WorldPosition + pawn.WorldRotation.Forward * 30f + Vector3.Up * 50f;
		var step = pawn.WorldRotation.Right * 10f;

		SpawnAt( DIR + "rfs_bun_bottom.prefab",    ingPos + step * -4f, Rotation.Identity );
		SpawnAt( DIR + "rfs_raw_beef.prefab",      ingPos + step * -3f, Rotation.Identity );
		SpawnAt( DIR + "rfs_whole_cheese.prefab",  ingPos + step * -2f, Rotation.Identity );
		SpawnAt( DIR + "rfs_whole_tomato.prefab",  ingPos + step * -1f, Rotation.Identity );
		SpawnAt( DIR + "rfs_whole_lettuce.prefab", ingPos + step *  0f, Rotation.Identity );
		SpawnAt( DIR + "rfs_whole_potato.prefab",  ingPos + step *  1f, Rotation.Identity );
		SpawnAt( DIR + "rfs_bun_top.prefab",       ingPos + step *  2f, Rotation.Identity );

		// Panier à friteuse + pochette + gobelet vide
		SpawnAt( DIR + "rfs_basket.prefab",      ingPos + step * 3f + Vector3.Up * 10f, Rotation.Identity );
		SpawnAt( DIR + "rfs_fries_pouch.prefab", ingPos + step * 4f + Vector3.Up * 10f, Rotation.Identity );
		SpawnAt( DIR + "rfs_empty_cup.prefab",   ingPos + step * 5f + Vector3.Up * 10f, Rotation.Identity );

		// Fontaine à soda (5e station alignée plus loin)
		SpawnAt( DIR + "rfs_soda_fountain.prefab", origin + pawn.WorldRotation.Right * 250f, rot );

		Log.Info( "[rfs_kit] 5 stations + ingrédients + panier + pochette + gobelet spawnés" );
	}

	private static GameObject SpawnAt( string prefabPath, Vector3 pos, Rotation rot )
	{
		var go = Spawnable.CreateWithReturnFromHost( prefabPath, new Transform( pos, rot ) );
		if ( go == null )
		{
			Log.Warning( $"[rfs] Échec spawn {prefabPath}" );
			return null;
		}
		go.NetworkSpawn();
		return go;
	}

	[ConCmd( "rfs_preview" )]
	public static void CmdPreviewBurger()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;
		ServerSpawnPreviewBurger( pawn );
	}

	[Rpc.Host]
	private static void ServerSpawnPreviewBurger( PlayerPawn pawn )
	{
		if ( !Networking.IsHost || pawn == null ) return;

		// Spawn 80u devant le joueur, à hauteur de tête
		var pos = pawn.WorldPosition + pawn.WorldRotation.Forward * 80f + Vector3.Up * 50f;
		var go = Spawnable.CreateWithReturnFromHost( DIR + "rfs_burger.prefab", new Transform( pos, Rotation.Identity ) );
		if ( go == null )
		{
			Log.Warning( "[rfs_preview] Échec spawn burger prefab" );
			return;
		}

		// Désactive la physique pour le maintenir flottant à la position du spawn
		var rb = go.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
		if ( rb.IsValid() ) rb.MotionEnabled = false;

		var burger = go.Components.Get<Burger>( FindMode.EverythingInSelfAndDescendants );
		if ( burger == null )
		{
			Log.Warning( "[rfs_preview] Burger component introuvable" );
			go.Destroy();
			return;
		}

		// Construit la pile avec TOUTES les entrées de la VisualLibrary
		// dans l'ordre où elles sont déclarées dans le prefab.
		var types = burger.VisualLibrary?
			.Where( v => v != null && v.Model != null )
			.Select( v => v.Type.ToString() )
			.ToList() ?? new List<string>();

		if ( types.Count == 0 )
		{
			Log.Warning( "[rfs_preview] VisualLibrary est vide ou aucun model assigné — preview impossible" );
			go.Destroy();
			return;
		}

		burger.CondimentsCsv = string.Join( ",", types );
		// Teintes blanches (1,1,1,1) pour voir les couches sans coloration
		burger.TintsCsv = string.Join( ";", types.Select( _ => "1.000,1.000,1.000,1.000" ) );
		burger.IsFinalized = false; // pas finalisé pour pouvoir refaire le preview

		go.NetworkSpawn();

		Log.Info( $"[rfs_preview] Preview burger spawné avec {types.Count} couches : {string.Join( ", ", types )}" );
	}
}
