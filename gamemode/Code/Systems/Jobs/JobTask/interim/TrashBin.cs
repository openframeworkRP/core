using OpenFramework.Inventory;
using OpenFramework.Inventory.UI;
using OpenFramework.UI;

namespace OpenFramework.Systems.Jobs;

/// <summary>
/// Poubelle publique avec un inventaire.
/// Tout joueur peut y déposer des items.
/// Le GarbageManager y injecte aussi des items "Déchet" pour le job éboueur.
/// </summary>
public sealed class TrashBin : Component, IUse, IMarkerObject
{
	[Property, Group( "Settings" )]
	public string DisplayName { get; set; } = "Poubelle";

	/// <summary>
	/// Nombre max de déchets que cette poubelle peut contenir.
	/// Le GarbageManager n'ajoutera plus de déchets si ce seuil est atteint.
	/// </summary>
	[Property, Group( "Settings" )]
	public int MaxFillLevel { get; set; } = 5;

	[Property, Group( "Logic" ), RequireComponent]
	public InventoryContainer Container { get; set; }

	/// <summary>
	/// Nombre d'items actuellement dans la poubelle.
	/// </summary>
	public int FillLevel => Container?.Items.Count() ?? 0;

	/// <summary>
	/// Est-ce que la poubelle est pleine ?
	/// </summary>
	public bool IsFull => FillLevel >= MaxFillLevel;

	/// <summary>
	/// Joueur qui a cette poubelle ouverte en ce moment.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public Client CurrentUser { get; private set; }

	public bool IsBusy => CurrentUser != null;

	// ── IMarkerObject ────────────────────────────────
	public Vector3 MarkerPosition => WorldPosition + Vector3.Up * 20f;
	public string DisplayText => IsBusy ? $"{DisplayName} (occupé)" : DisplayName;
	public string MarkerIcon => "delete";
	public float MarkerMaxDistance => 500f;
	public int IconSize => 20;
	public bool ShowChevron => false;
	public bool LookOpacity => true;
	//public bool ShouldShow() => Container.IsValid();
	public bool ShouldShow() => false;
	public string InputHint => IsBusy ? "Déjà utilisé" : string.Empty;

	protected override void OnStart()
	{
		Container.Name = DisplayName;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		if ( CurrentUser != null && (!CurrentUser.IsValid || CurrentUser.PlayerPawn == null) )
		{
			CurrentUser = null;
		}
	}

	// ── IUse ─────────────────────────────────────────
	public UseResult CanUse( PlayerPawn player )
	{
		if ( Container == null ) return false;

		if ( IsBusy && CurrentUser != player.Client )
			return "Cette poubelle est déjà utilisée par quelqu'un.";

		return true;
	}

	public void OnUse( PlayerPawn player )
	{
		if ( IsBusy && CurrentUser == player.Client )
		{
			Release( player.Client );
			return;
		}

		CurrentUser = player.Client;

		using ( Rpc.FilterInclude( player.Client.Connection ) )
			FullInventory.OpenNearby( Container );
	}

	[Rpc.Host]
	public static void Release( Client client )
	{
		var trashCan = Game.ActiveScene.GetAllComponents<TrashBin>()
			.FirstOrDefault( x => x.CurrentUser == client );

		if ( trashCan != null )
			trashCan.CurrentUser = null;
	}
}
