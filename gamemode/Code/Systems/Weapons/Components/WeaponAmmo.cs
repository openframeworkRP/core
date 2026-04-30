using Facepunch;
using Sandbox.Events;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems.Weapons.Interfaces;

namespace OpenFramework.Systems.Weapons;

[Title( "Ammo" ), Group( "Weapon Components" )]
public partial class WeaponAmmo : Component, IGameEventHandler<EquipmentDeployedEvent>, IDroppedWeaponState
{
	private const string AttrMagType = "loaded_mag_type";
	private const string AttrMagAmmo = "loaded_mag_ammo";
	private const string AttrMagCapacity = "loaded_mag_capacity";

	// Sync FromHost : client en serveur dedie doit voir LinkedItem pour resoudre
	// CompatibleEquippedAmmo dans le menu contextuel chargeur (bouton EQUIPER).
	[Property, Sync( SyncFlags.FromHost )] public InventoryItem LinkedItem { get; set; }

	/// <summary>
	/// Nombre de balles dans le chargeur actuel (synchronisé pour l'UI).
	/// </summary>
	[Property, Sync( SyncFlags.FromHost ), Change] public int Ammo { get; set; } = 0;

	/// <summary>
	/// Capacité max du chargeur actuel.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )] public int MaxAmmo { get; set; } = 0;

	/// <summary>
	/// Indique si un chargeur est actuellement insere (synchronise pour les visuels).
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change] public bool MagPresent { get; set; } = false;

	[Property, Group( "Visual" )] public string MagazineBodyGroupName { get; set; } = "magazine";
	[Property, Group( "Visual" )] public int ViewModelHasMagValue { get; set; } = 0;
	[Property, Group( "Visual" )] public int ViewModelNoMagValue { get; set; } = 2;
	[Property, Group( "Visual" )] public int WorldModelHasMagValue { get; set; } = 0;
	[Property, Group( "Visual" )] public int WorldModelNoMagValue { get; set; } = 1;

	public void OnAmmoChanged( int oldValue, int newValue )
	{
		if ( !Networking.IsHost || LinkedItem == null ) return;
		LinkedItem.Attributes.SetInt( "primary_ammo", newValue );
	}

	public void OnMagPresentChanged( bool oldValue, bool newValue )
	{
		UpdateMagazineVisual( newValue );
	}

	void IGameEventHandler<EquipmentDeployedEvent>.OnGameEvent( EquipmentDeployedEvent eventArgs )
	{
		UpdateMagazineVisual( MagPresent );
	}

	private void UpdateMagazineVisual( bool hasMag )
	{
		var equipment = Components.GetInAncestorsOrSelf<Equipment>();
		if ( equipment == null || string.IsNullOrEmpty( MagazineBodyGroupName ) )
			return;

		var vmRenderer = equipment.ViewModel?.ModelRenderer;
		int vmValue = hasMag ? ViewModelHasMagValue : ViewModelNoMagValue;
		if ( vmRenderer.IsValid() )
			vmRenderer.SetBodyGroup( MagazineBodyGroupName, vmValue );

		var wmRenderer = equipment.WorldModel?.ModelRenderer;
		int wmValue = hasMag ? WorldModelHasMagValue : WorldModelNoMagValue;
		if ( wmRenderer.IsValid() )
			wmRenderer.SetBodyGroup( MagazineBodyGroupName, wmValue );
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		RefreshAmmoFromAttributes();
		MagPresent = HasMagazine;
	}

	/// <summary>
	/// Lit l'état du chargeur depuis les attributs du LinkedItem et met à jour Ammo/MaxAmmo.
	/// </summary>
	public void RefreshAmmoFromAttributes()
	{
		if ( LinkedItem == null )
		{
			Ammo = 0;
			MaxAmmo = 0;
			return;
		}

		Ammo = LinkedItem.Attributes.GetInt( AttrMagAmmo, 0 );
		MaxAmmo = LinkedItem.Attributes.GetInt( AttrMagCapacity, 0 );
	}

	/// <summary>
	/// Insère un chargeur dans l'arme : capture son état puis détruit son GameObject.
	/// Le chargeur n'existe plus comme InventoryItem physique, son état vit dans les attributs de l'arme.
	/// </summary>
	public void LoadMagazine( InventoryItem magazine )
	{
		if ( !Networking.IsHost ) return;
		if ( magazine == null || magazine.Metadata == null || !magazine.Metadata.IsMagazine ) return;

		var meta = magazine.Metadata;
		int bullets = magazine.MagAmmo;

		magazine.GameObject.Destroy();

		SetLoadedMag( meta, bullets );
	}

	/// <summary>
	/// Equipe un chargeur depuis l'inventaire dans l'arme. Si l'arme a deja un
	/// chargeur, il est ejecte dans le slot libere par le nouveau. Appelable
	/// depuis le client via RPC.
	/// </summary>
	[Rpc.Host]
	public static void RPC_EquipMagazine( WeaponAmmo ammo, InventoryItem mag )
	{
		if ( !Networking.IsHost ) return;
		if ( ammo == null || !ammo.IsValid() ) return;
		if ( mag == null || !mag.IsValid || mag.Metadata == null || !mag.Metadata.IsMagazine ) return;
		if ( ammo.LinkedItem?.Metadata?.AmmoType == null ) return;
		if ( mag.Metadata.MagAmmoType != ammo.LinkedItem.Metadata.AmmoType )
			return;

		var newMeta = mag.Metadata;
		int newBullets = mag.MagAmmo;
		int newSlot = mag.SlotIndex;

		mag.GameObject.Destroy();

		if ( ammo.HasMagazine )
			ammo.UnloadMagazine( newSlot );

		ammo.SetLoadedMag( newMeta, newBullets );
	}

	/// <summary>
	/// Écrit directement l'état d'un chargeur dans les attributs de l'arme (sans GameObject source).
	/// Utile quand le chargeur a déjà été consommé ailleurs (ex: swap de chargeur).
	/// </summary>
	public void SetLoadedMag( ItemMetadata magMeta, int bullets )
	{
		if ( !Networking.IsHost || LinkedItem == null || magMeta == null ) return;

		LinkedItem.Attributes[AttrMagType] = magMeta.ResourceName ?? "";
		LinkedItem.Attributes.SetInt( AttrMagAmmo, bullets );
		LinkedItem.Attributes.SetInt( AttrMagCapacity, magMeta.MagCapacity );

		RefreshAmmoFromAttributes();
		MagPresent = true;
	}

	/// <summary>
	/// Retire le chargeur actuel et recrée un InventoryItem de chargeur dans l'inventaire du joueur.
	/// <paramref name="preferredSlot"/> permet de forcer un slot spécifique (utile lors d'un swap
	/// pour placer l'ancien mag dans le slot libéré par le nouveau).
	/// </summary>
	public InventoryItem UnloadMagazine( int preferredSlot = -1 )
	{
		if ( !Networking.IsHost ) return null;
		if ( LinkedItem == null || !HasMagazine ) return null;

		var magTypeName = LinkedItem.Attributes.GetValueOrDefault( AttrMagType, "" );
		if ( string.IsNullOrEmpty( magTypeName ) ) return null;

		var magMeta = ItemMetadata.All.FirstOrDefault( x => x.ResourceName == magTypeName );
		if ( magMeta == null ) return null;

		int bullets = LinkedItem.Attributes.GetInt( AttrMagAmmo, 0 );

		var playerContainer = LinkedItem.Components.GetInAncestors<InventoryContainer>();
		if ( playerContainer == null ) return null;

		int slot = preferredSlot >= 0 ? preferredSlot : playerContainer.GetFirstFreeSlot();
		if ( slot < 0 )
		{
			DropMagazineToGround( magMeta, bullets );
			ClearLoadedMag();
			return null;
		}

		var newMag = CreateMagazineItem( playerContainer, magMeta, slot, bullets );

		ClearLoadedMag();
		return newMag;
	}

	/// <summary>
	/// Drop physique du chargeur ejecte quand l'inventaire du joueur est plein.
	/// Spawn le WorldObjectPrefab devant le joueur et ecrit le nombre de balles dans l'attribut current_ammo.
	/// </summary>
	private void DropMagazineToGround( ItemMetadata magMeta, int bullets )
	{
		if ( magMeta.WorldObjectPrefab == null )
			return;

		var owner = Components.GetInAncestorsOrSelf<Equipment>()?.Owner;
		if ( owner == null )
			return;

		var dropPos = owner.AimRay.Position + owner.AimRay.Forward * 40f;
		var transform = new Transform( dropPos, Rotation.Identity );

		var worldGo = Spawnable.CreateWithReturnFromHost( magMeta.WorldObjectPrefab.ResourcePath, transform, Connection.Host );
		if ( worldGo == null )
			return;

		var droppedItem = worldGo.GetComponentInChildren<InventoryItem>();
		if ( droppedItem != null )
		{
			droppedItem.Quantity = 1;
			droppedItem.SlotIndex = -1;
			droppedItem.Attributes.SetInt( InventoryItem.AttrMagCurrentAmmo, bullets );
		}

		worldGo.NetworkSpawn();
	}

	/// <summary>
	/// Crée un InventoryItem de chargeur avec `bullets` balles stockees dans l'attribut current_ammo.
	/// </summary>
	private static InventoryItem CreateMagazineItem( InventoryContainer container, ItemMetadata magMeta, int slot, int bullets )
	{
		var magGo = new GameObject( true );
		magGo.Parent = container.GameObject;
		magGo.Name = $"Item_{magMeta.Name}";

		var newMag = magGo.Components.Create<InventoryItem>();
		newMag.Metadata = magMeta;
		newMag.SlotIndex = slot;

		magGo.NetworkSpawn();

		// InventoryItem.OnStart copie les attributs par defaut du Metadata → "current_ammo"=0.
		// On l'ecrase apres NetworkSpawn pour que la valeur soit sync aux clients.
		newMag.Attributes.SetInt( InventoryItem.AttrMagCurrentAmmo, bullets );

		container.RefreshSyncedTotal();
		container.MarkDirty();
		return newMag;
	}

	/// <summary>
	/// Vide les attributs de chargeur inséré.
	/// </summary>
	public void ClearLoadedMag()
	{
		if ( LinkedItem == null ) return;
		LinkedItem.Attributes[AttrMagType] = "";
		LinkedItem.Attributes.SetInt( AttrMagAmmo, 0 );
		LinkedItem.Attributes.SetInt( AttrMagCapacity, 0 );
		Ammo = 0;
		MaxAmmo = 0;
		if ( Networking.IsHost ) MagPresent = false;
	}

	/// <summary>
	/// Consomme 1 balle du chargeur chargé.
	/// </summary>
	public void ConsumeBullet()
	{
		if ( Networking.IsHost )
		{
			ConsumeBulletOnHost();
			return;
		}
		RPC_ConsumeBullet();
	}

	[Rpc.Host]
	private void RPC_ConsumeBullet() => ConsumeBulletOnHost();

	private void ConsumeBulletOnHost()
	{
		if ( !Networking.IsHost || LinkedItem == null ) return;

		int current = LinkedItem.Attributes.GetInt( AttrMagAmmo, 0 );
		if ( current <= 0 ) return;

		int next = current - 1;
		LinkedItem.Attributes.SetInt( AttrMagAmmo, next );
		Ammo = next;
	}

	public bool HasAmmo => Ammo > 0;
	public bool IsFull => MaxAmmo > 0 && Ammo >= MaxAmmo;
	public bool HasMagazine => LinkedItem != null
		&& !string.IsNullOrEmpty( LinkedItem.Attributes.GetValueOrDefault( AttrMagType, "" ) );
	public bool CanReload => !IsFull;

	/// <summary>
	/// Résout le type de munition actuellement chargé (via le chargeur inséré).
	/// Retourne null si pas de chargeur ou chargeur inconnu.
	/// </summary>
	public ItemMetadata GetLoadedAmmoMeta()
	{
		if ( LinkedItem == null ) return null;

		var magTypeName = LinkedItem.Attributes.GetValueOrDefault( AttrMagType, "" );
		if ( string.IsNullOrEmpty( magTypeName ) ) return null;

		if ( _cachedMagTypeName == magTypeName && _cachedAmmoMeta != null )
			return _cachedAmmoMeta;

		var magMeta = ItemMetadata.All.FirstOrDefault( x => x.ResourceName == magTypeName );
		_cachedMagTypeName = magTypeName;
		_cachedAmmoMeta = magMeta?.MagAmmoType;
		return _cachedAmmoMeta;
	}

	private string _cachedMagTypeName;
	private ItemMetadata _cachedAmmoMeta;

	// --- IDroppedWeaponState : preserve l'etat du chargeur quand l'arme est jetee ---

	void IDroppedWeaponState.CopyToDroppedWeapon( DroppedEquipment dropped )
	{
		if ( !Networking.IsHost || dropped == null ) return;

		var state = dropped.GetOrAddComponent<WeaponAmmoDroppedState>();
		if ( LinkedItem != null )
		{
			state.LoadedMagType = LinkedItem.Attributes.GetValueOrDefault( AttrMagType, "" );
			state.LoadedMagAmmo = LinkedItem.Attributes.GetInt( AttrMagAmmo, 0 );
			state.LoadedMagCapacity = LinkedItem.Attributes.GetInt( AttrMagCapacity, 0 );
			state.PrimaryAmmo = LinkedItem.Attributes.GetInt( "primary_ammo", Ammo );
		}
		else
		{
			state.LoadedMagAmmo = Ammo;
			state.LoadedMagCapacity = MaxAmmo;
			state.PrimaryAmmo = Ammo;
		}
	}

	void IDroppedWeaponState.CopyFromDroppedWeapon( DroppedEquipment dropped )
	{
		if ( !Networking.IsHost || dropped == null ) return;

		var state = dropped.Components.Get<WeaponAmmoDroppedState>();
		if ( state == null )
			return;

		if ( LinkedItem == null )
			return;

		LinkedItem.Attributes[AttrMagType] = state.LoadedMagType ?? "";
		LinkedItem.Attributes.SetInt( AttrMagAmmo, state.LoadedMagAmmo );
		LinkedItem.Attributes.SetInt( AttrMagCapacity, state.LoadedMagCapacity );
		LinkedItem.Attributes.SetInt( "primary_ammo", state.PrimaryAmmo );

		RefreshAmmoFromAttributes();
		MagPresent = HasMagazine;
	}
}
