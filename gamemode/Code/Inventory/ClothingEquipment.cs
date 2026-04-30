using OpenFramework.Systems.Pawn;
using System.Linq;
using System.Threading.Tasks;
using static Sandbox.Clothing;

namespace OpenFramework.Inventory;

/// <summary>
/// Gère les 10 slots d'équipement de vêtements sur le joueur.
/// Chaque slot correspond à une catégorie de vêtement (chapeau, haut, gants, etc.).
/// Fonctionne avec un InventoryContainer dédié (séparé de l'inventaire principal).
/// </summary>
public sealed class ClothingEquipment : Component
{
	/// <summary>
	/// Enum des slots d'équipement visibles dans l'inventaire.
	/// L'ordre correspond aux slots UI (colonne gauche puis droite).
	/// </summary>
	public enum Slot
	{
		Head = 0,      // Chapeau / Masque
		Top = 1,       // Haut (T-shirt, chemise, etc.)
		Gloves = 2,    // Gants
		Bottom = 3,    // Bas (pantalon, jeans, etc.)
		Shoes = 4,     // Chaussures
		Wrist = 5,     // Montre / bracelet
		Glasses = 6,   // Lunettes
		Jacket = 7,    // Veste / Manteau / Hoodie
		Ears = 8,      // Casque / oreillette
		Back = 9       // Sac à dos
	}

	public const int SlotCount = 10;

	/// <summary>
	/// Le container dédié aux vêtements équipés (10 slots).
	/// Doit être assigné dans l'éditeur ou créé au spawn.
	/// </summary>
	[Property] public InventoryContainer Container { get; set; }

	/// <summary>
	/// Détermine dans quel slot d'équipement un ItemMetadata clothing doit aller,
	/// basé sur la ClothingCategory de sa ressource Clothing.
	/// </summary>
	public static Slot? GetSlotForClothing( ItemMetadata meta )
	{
		if ( meta == null || !meta.IsClothing || meta.ClothingResource == null )
			return null;

		var category = meta.ClothingResource.Category;

		return category switch
		{
			// Head
			ClothingCategory.Hat or ClothingCategory.HatCap or ClothingCategory.HatBeanie
			or ClothingCategory.HatFormal or ClothingCategory.HatCostume or ClothingCategory.HatUniform
			or ClothingCategory.HatSpecial or ClothingCategory.Headwear
			or ClothingCategory.HeadTech or ClothingCategory.HeadBand
			or ClothingCategory.HeadJewel or ClothingCategory.HeadSpecial
				=> Slot.Head,

			// Top (T-shirts, chemises, gilets/vests portés directement sur le torse, etc.)
			ClothingCategory.Tops or ClothingCategory.TShirt or ClothingCategory.Shirt
			or ClothingCategory.Sweatshirt or ClothingCategory.Knitwear
			or ClothingCategory.Fullbody or ClothingCategory.Suit or ClothingCategory.Uniform
			or ClothingCategory.Costume or ClothingCategory.Dress
			or ClothingCategory.Vest
				=> Slot.Top,

			// Jacket (vestes, manteaux, hoodies, gilets tactiques par-dessus)
			ClothingCategory.Jacket or ClothingCategory.Coat or ClothingCategory.Hoodie
			or ClothingCategory.Cardigan or ClothingCategory.Gilet
				=> Slot.Jacket,

			// Gloves
			ClothingCategory.Gloves => Slot.Gloves,

			// Bottom
			ClothingCategory.Bottoms or ClothingCategory.Jeans or ClothingCategory.Trousers
			or ClothingCategory.Shorts or ClothingCategory.Skirt
				=> Slot.Bottom,

			// Shoes
			ClothingCategory.Footwear or ClothingCategory.Shoes or ClothingCategory.Trainers
			or ClothingCategory.Boots or ClothingCategory.Heels or ClothingCategory.Sandals
			or ClothingCategory.Slippers or ClothingCategory.Socks
				=> Slot.Shoes,

			// Wrist
			ClothingCategory.Wristwear or ClothingCategory.WristWatch
			or ClothingCategory.WristBand or ClothingCategory.WristJewel
			or ClothingCategory.WristSpecial or ClothingCategory.Ring
				=> Slot.Wrist,

			// Glasses
			ClothingCategory.Eyewear or ClothingCategory.GlassesEye
			or ClothingCategory.GlassesSun or ClothingCategory.GlassesSpecial
				=> Slot.Glasses,

			// Ears
			ClothingCategory.EarringStud or ClothingCategory.EarringDangle
			or ClothingCategory.EarringSpecial or ClothingCategory.Piercing
			or ClothingCategory.PierceNose or ClothingCategory.PierceEyebrow
			or ClothingCategory.PierceSpecial
				=> Slot.Ears,

			// Necklace → Ears slot (accessoire divers)
			ClothingCategory.NecklaceChain or ClothingCategory.NecklacePendant
			or ClothingCategory.NecklaceSpecial
				=> Slot.Ears,

			// Tout le reste → pas d'équipement slot (cheveux, barbe, skin, maquillage, etc.)
			_ => null
		};
	}

	/// <summary>
	/// Vérifie si un item peut être équipé dans un slot donné.
	/// L'item doit être un vêtement, avoir une ClothingResource, et sa catégorie
	/// doit correspondre exactement au slot ciblé.
	/// </summary>
	public static bool CanEquipInSlot( ItemMetadata meta, int slotIndex )
	{
		var expected = GetSlotForClothing( meta );
		if ( expected == null ) return false;
		return (int)expected.Value == slotIndex;
	}

	/// <summary>
	/// Retourne l'inventaire principal du joueur (pas le clothing container).
	/// </summary>
	private static InventoryContainer GetMainInventory( ClothingEquipment equipment )
	{
		return equipment.GameObject.Components
			.GetAll<InventoryContainer>( FindMode.EnabledInSelfAndChildren )
			.FirstOrDefault( c => c != equipment.Container );
	}

	/// <summary>
	/// Retourne l'item actuellement équipé dans un slot donné, ou null.
	/// </summary>
	public InventoryItem GetEquipped( Slot slot )
	{
		return Container?.Items.FirstOrDefault( x => x.SlotIndex == (int)slot );
	}

	/// <summary>
	/// Équipe un item clothing depuis l'inventaire principal vers le slot approprié.
	/// Applique visuellement le vêtement sur le joueur.
	/// </summary>
	[Rpc.Host]
	public static void Equip( ClothingEquipment equipment, InventoryItem item )
	{
		if ( !Networking.IsHost || equipment == null || item == null ) return;

		var meta = item.Metadata;
		if ( meta == null || !meta.IsClothing || meta.ClothingResource == null )
		{
			Log.Warning( $"[ClothingDebug] Equip REJECTED — item {item.Name ?? "<null>"} is not a valid clothing item (IsClothing={meta?.IsClothing}, resource={meta?.ClothingResource?.ResourceName ?? "<null>"})" );
			return;
		}

		var slot = GetSlotForClothing( meta );
		if ( slot == null )
		{
			Log.Warning( $"[ClothingDebug] Equip REJECTED — no slot mapping for {meta.Name} (category={meta.ClothingResource.Category})" );
			return;
		}

		int targetSlot = (int)slot.Value;
		var sourceContainer = item.Components.GetInAncestors<InventoryContainer>();
		Log.Info( $"[ClothingDebug] Equip START — item={meta.Name} from container={sourceContainer?.Name ?? "<null>"} slot#{item.SlotIndex} → CLOTHING slot {slot.Value} (#{targetSlot})" );

		// Si un item est déjà dans ce slot, le remettre dans l'inventaire principal
		var existing = equipment.GetEquipped( slot.Value );
		if ( existing != null )
		{
			var mainContainer = GetMainInventory( equipment );
			Log.Info( $"[ClothingDebug] Equip DISPLACE — existing={existing.Name} in CLOTHING slot#{existing.SlotIndex} → mainInventory={mainContainer?.Name ?? "<null>"}" );
			if ( mainContainer != null )
				InventoryContainer.MoveItem( existing, mainContainer, -1 );

			// Retire aussi le visuel du vêtement déplacé (sinon il reste sur le dresser
			// si sa catégorie diffère de celle du nouveau vêtement).
			if ( existing.Metadata?.ClothingResource != null )
				RemoveClothingVisual( equipment, existing.Metadata );
		}

		// Déplacer l'item vers le container d'équipement
		InventoryContainer.MoveItem( item, equipment.Container, targetSlot );
		Log.Info( $"[ClothingDebug] Equip DONE — item={meta.Name} now in container={item.Components.GetInAncestors<InventoryContainer>()?.Name ?? "<null>"} slot#{item.SlotIndex}" );

		// Appliquer visuellement
		ApplyClothingVisual( equipment, meta );
	}

	/// <summary>
	/// Déséquipe un item du slot clothing et le remet dans l'inventaire principal.
	/// </summary>
	[Rpc.Host]
	public static void Unequip( ClothingEquipment equipment, InventoryItem item, InventoryContainer targetContainer = null, int targetSlot = -1 )
	{
		if ( !Networking.IsHost || equipment == null || item == null ) return;

		var meta = item.Metadata;
		if ( meta == null || !meta.IsClothing )
		{
			Log.Warning( $"[ClothingDebug] Unequip REJECTED — item {item.Name ?? "<null>"} is not clothing" );
			return;
		}

		// Utiliser le container cible si fourni, sinon l'inventaire principal
		var destination = targetContainer ?? GetMainInventory( equipment );
		if ( destination == null )
		{
			Log.Warning( $"[ClothingDebug] Unequip REJECTED — no destination container for {meta.Name}" );
			return;
		}

		// Sécurité : si la destination est le container clothing lui-même, on refuse
		// (sinon on risque de swap un item vers son propre slot).
		if ( destination == equipment.Container )
		{
			Log.Warning( $"[ClothingDebug] Unequip REJECTED — destination == clothing container for {meta.Name}" );
			return;
		}

		Log.Info( $"[ClothingDebug] Unequip START — item={meta.Name} from CLOTHING slot#{item.SlotIndex} → container={destination.Name} slot#{targetSlot}" );

		// Pré-repère l'occupant éventuel du slot cible : si la MoveItem déclenche un swap,
		// il finira dans le container clothing et devra être appliqué visuellement.
		InventoryItem occupantBefore = null;
		if ( targetSlot != -1 )
			occupantBefore = destination.Items.FirstOrDefault( x => x.SlotIndex == targetSlot );

		// Remettre dans le slot ciblé (ou premier slot libre si -1)
		InventoryContainer.MoveItem( item, destination, targetSlot );

		Log.Info( $"[ClothingDebug] Unequip DONE — item={meta.Name} now in container={item.Components.GetInAncestors<InventoryContainer>()?.Name ?? "<null>"} slot#{item.SlotIndex}" );

		// Retirer visuellement le vêtement désequipé
		RemoveClothingVisual( equipment, meta );

		// Si un swap a envoyé l'occupant dans le container clothing, on applique son visuel.
		if ( occupantBefore != null && occupantBefore != item
			&& occupantBefore.Metadata?.ClothingResource != null
			&& occupantBefore.Components.GetInAncestors<InventoryContainer>() == equipment.Container )
		{
			Log.Info( $"[ClothingDebug] Unequip SWAP-EQUIP — {occupantBefore.Name} a été déplacé dans CLOTHING slot#{occupantBefore.SlotIndex}, application du visuel" );
			ApplyClothingVisual( equipment, occupantBefore.Metadata );
		}
	}

	/// <summary>
	/// Applique le vêtement visuellement. Le pattern est :
	/// 1. Le host met a jour Client.SavedClothingJson (etat synced, source de verite)
	/// 2. La sync engine replique aux clients courants ET futurs
	/// 3. Le [Change] de SavedClothingJson sur chaque client reconstruit le dresser et appelle Apply
	/// On garde aussi un Rpc.Broadcast pour la reactivite immediate des clients deja connectes.
	/// </summary>
	private static void ApplyClothingVisual( ClothingEquipment equipment, ItemMetadata meta )
	{
		var pawn = equipment.Components.GetInAncestorsOrSelf<PlayerPawn>();
		if ( pawn == null || meta.ClothingResource == null ) return;

		// 1) Source de verite synced — gere les late-joiners
		RebuildClothingJsonFromState( equipment );

		// 2) Effet immediat pour les clients deja connectes
		Client.BroadcastEquip( pawn.GameObject, meta.ClothingResource.ResourcePath, Color.White );
	}

	/// <summary>
	/// Retire le vêtement visuellement du Dresser du joueur.
	/// </summary>
	private static void RemoveClothingVisual( ClothingEquipment equipment, ItemMetadata meta )
	{
		var pawn = equipment.Components.GetInAncestorsOrSelf<PlayerPawn>();
		if ( pawn == null || meta.ClothingResource == null ) return;

		var dresser = pawn.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		if ( dresser == null ) return;

		// 1) Source de verite synced — gere les late-joiners
		RebuildClothingJsonFromState( equipment );

		// 2) Retirer le vetement du dresser cote clients deja connectes (effet immediat)
		BroadcastRemoveClothing( pawn.GameObject, meta.ClothingResource.ResourcePath );
	}

	/// <summary>
	/// Reconstruit Client.SavedClothingJson a partir de :
	///   - les categories preservees (cheveux, barbe, skin) deja presentes dans le JSON existant
	///   - les vetements actuellement dans equipment.Container (slots equipement)
	/// Host-only : c'est l'autorite qui pousse le state synced aux clients.
	/// </summary>
	public static void RebuildClothingJsonFromState( ClothingEquipment equipment )
	{
		if ( !Networking.IsHost || equipment == null ) return;
		var pawn = equipment.Components.GetInAncestorsOrSelf<PlayerPawn>();
		var client = pawn?.Client;
		if ( client == null ) return;

		var preservedCategories = new HashSet<Sandbox.Clothing.ClothingCategory>
		{
			Sandbox.Clothing.ClothingCategory.Hair,
			Sandbox.Clothing.ClothingCategory.Facial,
			Sandbox.Clothing.ClothingCategory.Skin,
		};

		var preserved = new List<string>();
		if ( !string.IsNullOrEmpty( client.SavedClothingJson ) && client.SavedClothingJson != "[]" )
		{
			try
			{
				var existing = System.Text.Json.JsonSerializer.Deserialize<List<string>>( client.SavedClothingJson );
				if ( existing != null )
				{
					foreach ( var path in existing )
					{
						var c = ResourceLibrary.Get<Sandbox.Clothing>( path );
						if ( c != null && preservedCategories.Contains( c.Category ) )
							preserved.Add( path );
					}
				}
			}
			catch { }
		}

		var equipped = equipment.Container?.Items
			.Where( i => i?.Metadata?.ClothingResource != null )
			.Select( i => i.Metadata.ClothingResource.ResourcePath )
			.ToList() ?? new List<string>();

		var combined = preserved.Concat( equipped ).Distinct().ToList();
		client.SavedClothingJson = System.Text.Json.JsonSerializer.Serialize( combined );
	}

	[Rpc.Broadcast]
	public static void BroadcastRemoveClothing( GameObject playerObj, string clothingPath )
	{
		if ( !playerObj.IsValid() ) return;

		var dresser = playerObj.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		if ( dresser == null ) return;

		var clothing = ResourceLibrary.Get<Clothing>( clothingPath );
		if ( clothing == null ) return;

		dresser.Clothing.RemoveAll( x => x.Clothing == clothing );

		var playerBody = playerObj.Components.Get<PlayerBody>( FindMode.EverythingInSelfAndChildren );
		_ = ApplyDresserSafe( dresser, playerBody );
	}

	private static async System.Threading.Tasks.Task ApplyDresserSafe( Dresser dresser, PlayerBody playerBody )
	{
		// Pré-configure le skin AVANT Apply pour que les meshes créés (underwear, etc.) héritent du bon MaterialGroup
		playerBody?.RestoreAppearance();

		await dresser.Apply();

		// Attend un frame pour que le moteur finalise les body groups et meshes
		await GameTask.DelayRealtime( 1 );

		// Re-applique le skin APRÈS Apply (Dresser.Apply peut réinitialiser le MaterialGroup)
		playerBody?.RestoreAppearance();

		// Force aussi le MaterialGroup sur le BodyTarget du Dresser et ses enfants skin
		RestoreSkinOnDresser( dresser, playerBody );
	}

	/// <summary>
	/// Force le bon MaterialGroup sur le BodyTarget du Dresser et tous les renderers enfants
	/// qui représentent du skin humain (underwear, etc.).
	/// </summary>
	public static void RestoreSkinOnDresser( Dresser dresser, PlayerBody playerBody )
	{
		if ( dresser == null || playerBody?.Renderer == null ) return;

		var skinGroup = playerBody.Renderer.MaterialGroup;

		// Force sur le BodyTarget si c'est un objet différent du Renderer du PlayerBody
		if ( dresser.BodyTarget != null && dresser.BodyTarget != playerBody.Renderer )
		{
			dresser.BodyTarget.MaterialGroup = skinGroup;
		}

		// Applique aussi sur tous les enfants SkinnedModelRenderer du dresser (underwear, skin overlays, etc.)
		foreach ( var child in dresser.GameObject.Children )
		{
			var smr = child.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndChildren );
			if ( smr != null && smr != playerBody.Renderer && smr != dresser.BodyTarget )
			{
				smr.MaterialGroup = skinGroup;
			}
		}
	}

	/// <summary>
	/// Équipe directement un vêtement par ResourceName (utilisé pendant la création de personnage).
	/// Ne déplace pas d'item — crée un nouvel item dans le container d'équipement.
	/// </summary>
	[Rpc.Host]
	public void EquipFromResourceName( string itemResourceName )
	{
		if ( !Networking.IsHost || Container == null ) return;

		var meta = ItemMetadata.All.FirstOrDefault( x => x.ResourceName == itemResourceName );
		if ( meta == null || !meta.IsClothing ) return;

		var slot = GetSlotForClothing( meta );
		if ( slot == null )
		{
			// Pas de slot pour ce type (cheveux, barbe, etc.) → on ignore
			return;
		}

		int targetSlot = (int)slot.Value;

		// Vérifier qu'il n'y a pas déjà un item dans ce slot
		var existing = GetEquipped( slot.Value );
		if ( existing != null )
		{
			existing.GameObject.Destroy();
		}

		// Créer l'item dans le container d'équipement
		var go = new GameObject( true );
		go.Parent = Container.GameObject;
		go.Name = $"Equipped_{meta.Name}";

		var invItem = go.Components.Create<InventoryItem>();
		invItem.Metadata = meta;
		invItem.SlotIndex = targetSlot;
		invItem.Quantity = 1;

		go.NetworkSpawn();

		Container.MarkDirty();
	}
}
