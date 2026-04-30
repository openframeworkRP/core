using OpenFramework.Inventory.UI;
using OpenFramework.Systems.Jobs;
using OpenFramework.UI;

namespace OpenFramework.Inventory;

public partial class DroppedInventory : Component, IUse, IMarkerObject
{
	[Property, Group( "Settings" )] public string DisplayName { get; set; } = "Colis";

	[Property, Group( "Logic" ), RequireComponent]
	public InventoryContainer Container { get; set; }

	// ── Job Inventory ─────────────────────────────
	/// <summary>
	/// Si activé, seuls les joueurs du job spécifié peuvent ouvrir ce container.
	/// </summary>
	[Property, Group( "Job Inventory" )]
	public bool JobRestricted { get; set; } = false;

	/// <summary>
	/// Job autorisé à accéder à ce container.
	/// Ignoré si JobRestricted est false.
	/// </summary>
	[Property, Group( "Job Inventory" ), ShowIf( nameof( JobRestricted ), true )]
	public JobList RequiredJob { get; set; } = JobList.None;

	/// <summary>
	/// Est-ce que l'object doit s'auto supprimer quand il n'y a plus d'items dedans ?
	/// Si false, le colis restera même vide, et pourra être réutilisé pour y déposer des items.
	/// </summary>
	[Property, Group( "Logic" ), Sync( SyncFlags.FromHost )]
	public bool ShouldRemoveWhenEmpty { get; set; } = true;

	/// <summary>
	/// Sac créé à la mort d'un joueur — se détruit automatiquement après DropLifetime secondes.
	/// </summary>
	[Property, Group( "Logic" ), Sync( SyncFlags.FromHost )]
	public bool IsDeath { get; set; } = false;

	/// <summary>
	/// SteamId du joueur qui a cree cette box (0 = sac de mort, container de
	/// map, ou cree par un systeme). Utilise par PlacedPropsCleanup pour
	/// supprimer les boxes d'un joueur deconnecte > CleanupDelay. Les sacs
	/// de mort gardent 0 et sont nettoyes par leur propre Timer.HostAfter.
	/// </summary>
	[Property, Group( "Logic" ), Sync( SyncFlags.FromHost )]
	public ulong DroppedBySteamId { get; set; } = 0;

	/// <summary>
	/// Joueur qui a ce colis ouvert en ce moment.
	/// Null = personne ne l'utilise, tout le monde peut l'ouvrir.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public Client CurrentUser { get; private set; }

	public bool IsBusy => CurrentUser != null;

	// --- IMARKEROBJECT ---
	public Vector3 MarkerPosition => WorldPosition + Vector3.Up * 20f;
	public string DisplayText => IsBusy ? $"{DisplayName} (occupé)" : DisplayName;
	public string MarkerIcon => Container.Capacity > 30 ? "backpack" : "inventory";
	public float MarkerMaxDistance => 1000f;
	public int IconSize => 24;
	public bool ShowChevron => true;
	public bool LookOpacity => true;
	//public bool ShouldShow() => Container.IsValid() && Container.Items.Count() > 0;
	public bool ShouldShow() => false;
	public string InputHint
	{
		get
		{
			if ( IsBusy ) return "Déjà utilisé";
			if ( JobRestricted && RequiredJob != JobList.None ) return $"Réservé : {RequiredJob}";
			return "Interagir";
		}
	}

	protected override void OnStart()
	{
		Container.Name = DisplayName;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		if ( Container.Items.Count() == 0 && ShouldRemoveWhenEmpty )
			GameObject.Destroy();

		// Si le joueur qui avait le colis ouvert est parti ou déconnecté, on libère le verrou
		if ( CurrentUser != null && (!CurrentUser.IsValid || CurrentUser.PlayerPawn == null) )
		{
			CurrentUser = null;
		}
	}

	public UseResult CanUse( PlayerPawn player )
	{
		if ( Container == null ) return false;

		// Vérification du job
		if ( JobRestricted && RequiredJob != JobList.None )
		{
			var playerJob = player.Client?.Data?.Job;
			bool jobMatch = string.Equals( playerJob, RequiredJob.ToString(), StringComparison.OrdinalIgnoreCase );

			if ( !jobMatch )
				return $"Accès réservé aux {RequiredJob}.";
		}

		// Occupé par quelqu'un d'autre
		if ( IsBusy && CurrentUser != player.Client )
			return "Ce container est déjà utilisé par quelqu'un.";

		return true;
	}

	public void OnUse( PlayerPawn player )
	{
		// OnUse est appelé sur le Host via IUse → on pose/retire le verrou ici
		if ( IsBusy && CurrentUser == player.Client )
		{
			// Le même joueur ré-interagit → il ferme le colis
			Release( player.Client );
			return;
		}

		// Nouveau joueur → on lock et on ouvre l'UI côté client
		CurrentUser = player.Client;

		using ( Rpc.FilterInclude( player.Client.Connection ) )
			FullInventory.OpenNearby( Container );
	}

	/// <summary>
	/// Libère le verrou. Appelé quand le joueur ferme l'inventaire
	/// ou quand FullInventory.CloseNearby() est invoqué.
	/// </summary>
	[Rpc.Host]
	public static void Release( Client client )
	{
		// On cherche le DroppedInventory que ce client avait ouvert
		var dropped = Game.ActiveScene.GetAllComponents<DroppedInventory>()
			.FirstOrDefault( x => x.CurrentUser == client );

		if ( dropped != null )
			dropped.CurrentUser = null;
	}
}
