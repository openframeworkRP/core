using Facepunch;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems.Jobs;
using System.Threading.Tasks;

namespace OpenFramework.Systems.Crafting;

/// <summary>
/// Système de craft côté Host.
/// - Une seule personne à la fois par table (TryLock)
/// - Spawn l'item crafté sur la table (SpawnPoint) plutôt que dans l'inventaire
/// - Filtre les recettes par JobAccess
/// </summary>
public class CraftingSystem : Component
{
	public static CraftingSystem Instance { get; private set; }

	protected override void OnAwake() => Instance = this;

	// ─────────────────────────────────────────────
	//  DEMANDE DE CRAFT  (client → host)
	// ─────────────────────────────────────────────

	[Rpc.Host]
	public static void RequestCraft( CraftingTable table, string recipeResourceName )
	{
		if ( !Networking.IsHost ) return;

		var caller = Rpc.Caller.GetClient();
		var pawn   = caller?.PlayerPawn;
		if ( pawn == null || table == null ) return;

		// 1. Vérification que la table est libre
		if ( !table.TryLock( caller ) )
		{
			Log.Warning( $"[Craft] {caller.DisplayName} : table déjà occupée par {table.CurrentUser?.DisplayName}" );
			using ( Rpc.FilterInclude( caller.Connection ) )
				CraftingUI.NotifyOccupied();
			return;
		}

		// 2. Vérification de proximité
		float dist = Vector3.DistanceBetween( pawn.WorldPosition, table.WorldPosition );
		if ( dist > table.UseRadius )
		{
			table.Release();
			Log.Warning( $"[Craft] {caller.DisplayName} trop loin de la table" );
			return;
		}

		// 3. Vérification de la recette (avec filtre job)
		var recipe = table.GetAvailableRecipes( caller )
			.FirstOrDefault( x => x.ResourceName == recipeResourceName );

		if ( recipe == null )
		{
			table.Release();
			Log.Warning( $"[Craft] Recette '{recipeResourceName}' non disponible pour {caller.DisplayName}" );
			return;
		}

		// 4. Vérification des ingrédients
		var inventory = pawn.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		if ( inventory == null ) { table.Release(); return; }

		if ( !HasIngredients( inventory, recipe ) )
		{
			table.Release();
			using ( Rpc.FilterInclude( caller.Connection ) )
				CraftingUI.NotifyMissingIngredients();
			return;
		}

		// 5. Lance la progression côté client
		using ( Rpc.FilterInclude( caller.Connection ) )
			CraftingUI.StartProgress( recipe.UseDuration, $"Fabrication de {recipe.Name}..." );

		// 6. Attend la durée puis valide le craft
		_ = ExecuteCraftAsync( caller, pawn, inventory, table, recipe );
	}

	private static async Task ExecuteCraftAsync(
		Client caller, PlayerPawn pawn,
		InventoryContainer inventory,
		CraftingTable table, ItemMetadata recipe )
	{
		await GameTask.DelaySeconds( recipe.UseDuration );

		// Re-vérifie que le joueur est toujours valide et proche
		if ( !pawn.IsValid() )
		{
			table.Release();
			return;
		}

		float dist = Vector3.DistanceBetween( pawn.WorldPosition, table.WorldPosition );
		if ( dist > table.UseRadius )
		{
			table.Release();
			//Log.Warning( $"[Craft] {caller.DisplayName} s'est éloigné, craft annulé" );
			/*
			using ( Rpc.FilterInclude( caller.Connection ) )
				CraftingUI.NotifyCancelled();
			*/
			return;
		}

		// Re-vérifie les ingrédients
		if ( !HasIngredients( inventory, recipe ) )
		{
			table.Release();
			/*
			using ( Rpc.FilterInclude( caller.Connection ) )
				CraftingUI.NotifyMissingIngredients();
			*/
			return;
		}

		// 7. Consomme les ingrédients
		foreach ( var ingredient in recipe.Recipe )
		{
			if ( ingredient.ItemResource == null ) continue;
			InventoryContainer.Remove( inventory, ingredient.ItemResource.ResourceName, ingredient.Quantity );
		}

		// 8. Spawn l'item sur la table (via WorldItem prefab si disponible)
		SpawnItemOnTable( recipe, table );

		// 9. Libère la table
		table.Release();

		//Log.Info( $"[Craft] {caller.DisplayName} a crafté {recipe.Name} sur {table.DisplayName}" );
		/*
		using ( Rpc.FilterInclude( caller.Connection ) )
			CraftingUI.NotifySuccess( recipe.Name );*/
	}

	private static void SpawnItemOnTable( ItemMetadata recipe, CraftingTable table )
	{
		var spawnPos = table.SpawnPosition;

		if ( recipe.IsWeapon )
		{
			if ( recipe.WeaponResource == null )
			{
				Log.Warning( $"[Craft] {recipe.Name} est une arme mais n'a pas de WeaponResource." );
				return;
			}

			DroppedEquipment.Create( recipe.WeaponResource, spawnPos );
			return;
		}

		if ( recipe.WorldObjectPrefab == null )
		{
			Log.Warning( $"[Craft] {recipe.Name} n'a pas de WorldObjectPrefab, impossible de spawner sur la table." );
			return;
		}

		var spawnTransform = new Transform( spawnPos, Rotation.Identity );
		var go = Spawnable.CreateWithReturnFromHost( recipe.WorldObjectPrefab.ResourcePath, spawnTransform );
		if ( go == null ) return;

		var item = go.GetComponentInChildren<InventoryItem>();
		if ( item != null )
		{
			item.Quantity = 1;
			item.SlotIndex = -1;
		}

		go.NetworkSpawn();
	}

	// ─────────────────────────────────────────────
	//  ANNULATION  (client ferme l'UI pendant le craft)
	// ─────────────────────────────────────────────

	[Rpc.Host]
	public static void CancelCraft( CraftingTable table )
	{
		if ( !Networking.IsHost || table == null ) return;
		var caller = Rpc.Caller.GetClient();
		if ( table.CurrentUser == caller )
			table.Release();
	}

	// ─────────────────────────────────────────────
	//  HELPER
	// ─────────────────────────────────────────────

	public static bool HasIngredients( InventoryContainer inventory, ItemMetadata recipe )
	{
		foreach ( var ingredient in recipe.Recipe )
		{
			if ( ingredient.ItemResource == null ) continue;
			if ( !InventoryContainer.Has( inventory, ingredient.ItemResource.ResourceName, ingredient.Quantity ) )
				return false;
		}
		return true;
	}
}
