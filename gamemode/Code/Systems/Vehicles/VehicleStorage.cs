using OpenFramework.Inventory;
using OpenFramework.Inventory.UI;
using OpenFramework.Systems.Pawn;
using OpenFramework.UI;

namespace OpenFramework.Systems.Vehicles;

public enum VehicleStorageType
{
	GloveBox,
	Trunk
}

/// <summary>
/// Point d'interaction sur un véhicule pour ouvrir un inventaire (boîte à gants, coffre, etc.).
/// Placé comme enfant du prefab véhicule avec un InventoryContainer frère.
/// Ouverture via touche I (Inventory) — détection automatique par proximité, dedans ou dehors.
/// </summary>
public partial class VehicleStorage : Component, IMarkerObject
{
	[Property, Group( "Settings" )] public VehicleStorageType StorageType { get; set; } = VehicleStorageType.Trunk;

	[Property, Group( "Settings" )] public string DisplayName { get; set; } = "Coffre";

	[Property, Group( "Logic" ), RequireComponent]
	public InventoryContainer Container { get; set; }

	[Property, Group( "Logic" )]
	public Vehicle Vehicle { get; set; }

	/// <summary>Rayon de détection pour le menu radial.</summary>
	[Property, Group( "Settings" )] public float DetectionRadius { get; set; } = 50f;

	/// <summary>
	/// Joueur qui a ce storage ouvert en ce moment.
	/// Null = personne ne l'utilise.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public Client CurrentUser { get; private set; }

	public bool IsBusy => CurrentUser != null;

	// ── IMarkerObject ────────────────────────────────────────────────────────
	public Vector3 MarkerPosition => WorldPosition + Vector3.Up * 10f;
	public string DisplayText => IsBusy ? $"{DisplayName} (occupé)" : DisplayName;
	public string MarkerIcon => StorageType == VehicleStorageType.GloveBox ? "dashboard" : "inventory_2";
	public float MarkerMaxDistance => 200f;
	public int IconSize => 20;
	public bool ShowChevron => false;
	public bool LookOpacity => true;
	public bool ShouldShow() => Container.IsValid();
	public string InputHint => IsBusy ? "Déjà utilisé" : "[I] Ouvrir";

	protected override void OnStart()
	{
		Container.Name = DisplayName;

		// Synchronise capacité/poids depuis les réglages du composant Vehicle
		if ( Networking.IsHost && Vehicle.IsValid() && Container.IsValid() )
		{
			if ( StorageType == VehicleStorageType.GloveBox )
			{
				Container.Capacity = Vehicle.GloveBoxCapacity;
				Container.MaxWeight = Vehicle.GloveBoxMaxWeight;
			}
			else
			{
				Container.Capacity = Vehicle.TrunkCapacity;
				Container.MaxWeight = Vehicle.TrunkMaxWeight;
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		// Si le joueur qui avait le storage ouvert est parti ou mort, on libère le verrou
		if ( CurrentUser != null && (!CurrentUser.IsValid || CurrentUser.PlayerPawn == null) )
		{
			CurrentUser = null;
		}
	}

	private bool DebugLog => Vehicle.IsValid() && Vehicle.ShowInventoryLogs;

	/// <summary>
	/// Pose le verrou serveur. Appelé depuis FullInventory.TryAttachVehicleStorage().
	/// </summary>
	[Rpc.Host]
	public void Open( PlayerPawn player )
	{
		if ( DebugLog ) Log.Info( $"[VehicleStorage] Open() — type={StorageType} player={player?.GameObject.Name} busy={IsBusy}" );

		if ( Container == null || !player.IsValid() ) return;

		// Déjà ouvert par ce joueur — ne rien faire (le toggle est géré côté UI)
		if ( IsBusy && CurrentUser == player.Client ) return;

		// Occupé par quelqu'un d'autre
		if ( IsBusy ) return;

		CurrentUser = player.Client;
		if ( DebugLog ) Log.Info( $"[VehicleStorage] LOCKED {DisplayName} for {player.GameObject.Name}" );
	}

	// ── Gizmo ─────────────────────────────────────────────────────────────────

	protected override void DrawGizmos()
	{
		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.LineThickness = 1.5f;

		Gizmo.Draw.Color = StorageType == VehicleStorageType.GloveBox
			? new Color( 0.9f, 0.7f, 0.2f, 0.6f )
			: new Color( 0.6f, 0.3f, 0.9f, 0.6f );

		Gizmo.Draw.LineSphere( new Sphere( Vector3.Zero, DetectionRadius ), 16 );

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Text( StorageType == VehicleStorageType.GloveBox ? "GLOVEBOX" : "TRUNK", Transform.World, size: 14 );
	}

	/// <summary>
	/// Libère le verrou. Appelé quand le joueur ferme l'inventaire ou quitte le véhicule.
	/// </summary>
	[Rpc.Host]
	public static void Release( Client client )
	{
		var storage = Game.ActiveScene.GetAllComponents<VehicleStorage>()
			.FirstOrDefault( x => x.CurrentUser == client );

		if ( storage != null )
			storage.CurrentUser = null;
	}
}
