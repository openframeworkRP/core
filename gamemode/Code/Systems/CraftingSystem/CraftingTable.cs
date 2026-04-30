using OpenFramework.Inventory;
using OpenFramework.Systems.Jobs;

namespace OpenFramework.Systems.Crafting;

/// <summary>
/// Component à placer sur une table de craft dans le monde.
/// </summary>
public class CraftingTable : Component, IUse
{
	[Property] public string DisplayName { get; set; } = "Table de craft";

	[Property] public float UseRadius { get; set; } = 150f;

	/// <summary>
	/// Si défini, seul ce job peut utiliser cette table.
	/// JobList.None = accessible à tout le monde.
	/// </summary>
	[Property] public JobList JobAccess { get; set; } = JobList.None;

	/// <summary>
	/// Point de spawn où l'item crafté apparaîtra sur la table.
	/// Pointe vers un GameObject enfant positionné sur la surface de la table.
	/// </summary>
	[Property] public GameObject ItemSpawnPoint { get; set; }

	/// <summary>
	/// Si vide, toutes les recettes IsCraftable sont disponibles.
	/// Si rempli, seules ces recettes sont accessibles à cette table.
	/// </summary>
	[Property] public List<ItemMetadata> AllowedRecipes { get; set; } = new();

	/// <summary>
	/// Joueur en train d'utiliser la table. Null si libre.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public Client CurrentUser { get; set; } = null;

	public bool IsOccupied => CurrentUser != null;

	/// <summary>
	/// Position de spawn de l'item — SpawnPoint si défini, sinon dessus de la table.
	/// </summary>
	public Vector3 SpawnPosition => ItemSpawnPoint != null
		? ItemSpawnPoint.WorldPosition
		: WorldPosition + Vector3.Up * 20f;

	/// <summary>
	/// Vérifie si le client a le bon job pour utiliser cette table.
	/// JobAccess.None = accessible à tous.
	/// </summary>
	public bool CanBeUsedBy( Client client )
	{
		if ( JobAccess == JobList.None ) return true;
		if ( client?.Data?.Job == null ) return false;
		return client.Data.Job.Equals( JobAccess.ToString(), StringComparison.OrdinalIgnoreCase );
	}

	// ─────────────────────────────────────────────
	//  FILTRAGE DES RECETTES
	// ─────────────────────────────────────────────

	/// <summary>
	/// Retourne les recettes disponibles pour ce joueur sur cette table.
	/// Filtre par AllowedRecipes ET par JobAccess de chaque recette.
	/// </summary>
	public IEnumerable<ItemMetadata> GetAvailableRecipes( Client client = null )
	{
		var all = AllowedRecipes.Count > 0
			? AllowedRecipes.Where( x => x != null && x.IsCraftable )
			: ItemMetadata.All.Where( x => x.IsCraftable );

		// Filtre par job si un client est fourni
		if ( client != null )
		{
			var job = client.Data?.Job?.ToLower();
			all = all.Where( x =>
				x.CraftJobAccess == JobList.None ||
				x.CraftJobAccess.ToString().ToLower() == job
			);
		}

		return all;
	}

	// ─────────────────────────────────────────────
	//  IUse
	// ─────────────────────────────────────────────

	public UseResult CanUse( PlayerPawn player )
	{
		// Vérification du job
		if ( JobAccess != JobList.None )
		{
			var job = player.Client?.Data?.Job?.ToLower();
			if ( job != JobAccess.ToString().ToLower() )
				return $"Réservé aux {JobAccess}.";
		}

		// Vérification de l'occupation
		if ( IsOccupied && CurrentUser != player.Client )
			return "Cette table est déjà utilisée.";

		return true;
	}

	public void OnUse( PlayerPawn player )
	{
		using ( Rpc.FilterInclude( player.Client.Connection ) )
			CraftingUI.Open( this );
	}

	/// <summary>
	/// Libère la table (appelé à la fermeture de l'UI ou en fin de craft).
	/// </summary>
	[Rpc.Host]
	public void Release()
	{
		if ( !Networking.IsHost ) return;
		CurrentUser = null;
	}

	/// <summary>
	/// Tente de verrouiller la table pour ce client.
	/// Retourne false si déjà occupée par quelqu'un d'autre.
	/// </summary>
	public bool TryLock( Client client )
	{
		if ( !Networking.IsHost ) return false;
		if ( IsOccupied && CurrentUser != client ) return false;
		CurrentUser = client;
		return true;
	}
}
