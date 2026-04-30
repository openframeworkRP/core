using OpenFramework.Systems.Jobs;
using System.ComponentModel;
using System.Threading.Tasks;

namespace OpenFramework.Inventory;

/// <summary>
/// Définition d'une ressource d'item stockable dans un inventaire.
/// </summary>
[AssetType( Name = "Item Metadata", Extension = "item", Category = "Roleplay" )]
public class ItemMetadata : GameResource
{
	public static HashSet<ItemMetadata> All { get; set; } = new();

	#region Enums
	public enum ItemType
	{
		None, Weapon, Magazine, Ammo, Document, Furniture, Food, Minerals, Environmental, Misc, Clothing, Tools
	}

	public enum FurnitureType
	{
		None, Chair, Table, Sofa, Bed, Cabinet, Storage,
		Electronics, Appliance, Lighting, Decoration, Plant,
		Outdoor, Utility, Criminal, Medical
	}
	#endregion

	#region Definition
	[Category( "Definition" )] public string Name { get; set; }
	[Category( "Definition" )] public string Description { get; set; }
	[Category( "Definition" )] public bool IsEnabled { get; set; }

	[Category( "Definition" )]
	[Description( "Prix de base (achat/revente)." )]
	public int Price { get; set; } = 0;

	[Category( "Definition" ), Browsable( false )]
	public ItemType ItemCategoryType =>
		IsWeapon ? ItemType.Weapon :
		IsMagazine ? ItemType.Magazine :
		IsAmmo ? ItemType.Ammo :
		IsDocument ? ItemType.Document :
		IsFurniture ? ItemType.Furniture :
		IsFood ? ItemType.Food :
		IsMinerals ? ItemType.Minerals :
		IsClothing ? ItemType.Clothing :
		IsTools ? ItemType.Tools :
		ItemType.Misc;

	[Category( "Definition" ), HideIf( nameof( IsWeapon ), true )]
	public PrefabFile WorldObjectPrefab { get; set; }

	[Category( "Definition" ), HideIf( nameof( IsWeapon ), true )]
	public PrefabFile TrashObjectPrefab { get; set; }

	[Category( "Definition" )] public Model PreviewObject { get; set; }
	[Category( "Definition" )] public Texture Icon { get; set; }
	[Category( "Definition" )] public float Weight { get; set; }
	[Category( "Definition" )] public int MaxStack { get; set; } = 5;


	[Category( "Definition" )]
	public Dictionary<string, string> Attributes { get; set; } = new();

	[Property, Category( "Behavior" )]
	public bool CanBePickedUp { get; set; } = true;

	[Property, Category( "Behavior" )]
	public bool IsUnique { get; set; } = false;

	[Browsable( false )]
	public bool IsConsumable => OnUseAction != null;

	[Category( "Behavior" )] public float UseDuration { get; set; } = 1.0f;

	[Category( "Behavior" ), Hide]
	public bool CanBeSpawned => WorldObjectPrefab != null;

	[Category( "Behavior" ), SingleAction]
	public Func<PlayerPawn, InventoryItem, Task<bool>> OnUseAction { get; set; }
	#endregion

	#region Features Craft
	public struct CraftIngredient
	{
		[Property] public ItemMetadata ItemResource { get; set; }
		[Property] public int Quantity { get; set; }
	}

	[Property, Group( "Crafting" )] public bool IsCraftable { get; set; }
	[Property, Group( "Crafting" ), ShowIf( nameof( IsCraftable ), true )]
	public List<CraftIngredient> Recipe { get; set; } = new();

	[Property, Group( "Crafting" ), ShowIf( nameof( IsCraftable ), true )]
	public JobList CraftJobAccess { get; set; } = JobList.None;
	#endregion

	#region Features Logic
	private void DisableOthers( string keep )
	{
		if ( keep != nameof( IsWeapon ) && IsWeapon ) IsWeapon = false;
		if ( keep != nameof( IsMagazine ) && IsMagazine ) IsMagazine = false;
		if ( keep != nameof( IsAmmo ) && IsAmmo ) IsAmmo = false;
		if ( keep != nameof( IsPack ) && IsPack ) IsPack = false;
		if ( keep != nameof( IsDocument ) && IsDocument ) IsDocument = false;
		if ( keep != nameof( IsFurniture ) && IsFurniture ) IsFurniture = false;
		if ( keep != nameof( IsFood ) && IsFood ) IsFood = false;
		if ( keep != nameof( IsMinerals ) && IsMinerals ) IsMinerals = false;
		if ( keep != nameof( IsTools ) && IsTools ) IsTools = false;
		if ( keep != nameof( IsClothing ) && IsClothing ) IsClothing = false;
		if ( keep != nameof( CanHeal ) && CanHeal ) CanHeal = false;
	}

	private void EnsureAttribute( string key, string defaultValue )
	{
		Attributes ??= new();
		if ( !Attributes.ContainsKey( key ) ) Attributes.Add( key, defaultValue );
	}
	#endregion

	#region Feature: Tools
	private bool _isTools;
	[FeatureEnabled( "Tools" )]
	public bool IsTools
	{
		get => _isTools;
		set
		{
			if ( _isTools == value ) return;
			_isTools = value;
			if ( value ) { DisableOthers( nameof( IsTools ) ); EnsureAttribute( "durability", "100" ); EnsureAttribute( "fuel", "100" ); }
			else { Attributes?.Remove( "durability" ); Attributes?.Remove( "fuel" ); }
		}
	}
	[Feature( "Tools" )] public EquipmentResource ToolsResource { get; set; }
	#endregion

	#region Feature: Weapon
	private bool _isWeapon;
	[FeatureEnabled( "Weapon" )]
	public bool IsWeapon
	{
		get => _isWeapon;
		set
		{
			if ( _isWeapon == value ) return;
			_isWeapon = value;
			if ( value )
			{
				DisableOthers( nameof( IsWeapon ) );
				EnsureAttribute( "primary_ammo", "0" );
				EnsureAttribute( "loaded_mag_id", "" );
				EnsureAttribute( "durability", "100" );
			}
			else
			{
				Attributes?.Remove( "primary_ammo" );
				Attributes?.Remove( "loaded_mag_id" );
				Attributes?.Remove( "durability" );
			}
		}
	}
	[Feature( "Weapon" )] public EquipmentResource WeaponResource { get; set; }

	/// <summary>
	/// Le type de munition/calibre que cette arme utilise (référence directe).
	/// </summary>
	[Feature( "Weapon" ), Property] public ItemMetadata AmmoType { get; set; }
	#endregion

	#region Feature: Magazine
	private bool _isMagazine;
	[FeatureEnabled( "Magazine" )]
	public bool IsMagazine
	{
		get => _isMagazine;
		set
		{
			if ( _isMagazine == value ) return;
			_isMagazine = value;
			if ( value )
			{
				DisableOthers( nameof( IsMagazine ) );
				EnsureAttribute( "current_ammo", "0" );
			}
			else
			{
				Attributes?.Remove( "current_ammo" );
			}
		}
	}

	/// <summary>
	/// Le calibre que ce chargeur peut contenir.
	/// </summary>
	[Feature( "Magazine" ), Property] public ItemMetadata MagAmmoType { get; set; }

	[Feature( "Magazine" ), Property] public int MagCapacity { get; set; } = 12;
	#endregion

	#region Feature: Ammo
	private bool _isAmmo;
	[FeatureEnabled( "Ammo" )]
	public bool IsAmmo
	{
		get => _isAmmo;
		set
		{
			if ( _isAmmo == value ) return;
			_isAmmo = value;
			if ( value )
			{
				DisableOthers( nameof( IsAmmo ) );

				// Usure infligée à l'arme à chaque tir (reste en attribut, runtime)
				EnsureAttribute( "weapon_wear", "0.01" );
			}
			else
			{
				Attributes?.Remove( "weapon_wear" );
			}
		}
	}

	// --- Ballistique : dégâts ---

	[Feature( "Ammo" ), Group( "Damage" )] public float BaseDamage { get; set; } = 25f;
	[Feature( "Ammo" ), Group( "Damage" )]
	public Curve DamageFalloff { get; set; } = new( new List<Curve.Frame>() { new( 0, 1 ), new( 1, 0 ) } );
	[Feature( "Ammo" ), Group( "Damage" )] public float MaxRange { get; set; } = 1024000f;

	/// <summary>
	/// Nombre de projectiles par tir (buckshot = 9, balle classique = 1).
	/// </summary>
	[Feature( "Ammo" ), Group( "Damage" )] public int BulletCount { get; set; } = 1;

	// --- Ballistique : physique ---

	/// <summary>
	/// Énergie à la bouche (Joules). 9mm ~500, 5.56 ~1800, 7.62 ~3500.
	/// </summary>
	[Feature( "Ammo" ), Group( "Ballistics" )] public float MuzzleEnergy { get; set; } = 500f;

	/// <summary>
	/// Masse de la balle (kg). 9mm ~0.008, 5.56 ~0.004, 7.62 ~0.01.
	/// </summary>
	[Feature( "Ammo" ), Group( "Ballistics" )] public float BulletMass { get; set; } = 0.008f;

	/// <summary>
	/// Calibre (mm).
	/// </summary>
	[Feature( "Ammo" ), Group( "Ballistics" )] public float BulletCaliber { get; set; } = 9f;

	// --- Ballistique : pénétration ---

	[Feature( "Ammo" ), Group( "Penetration" )] public int MaxPenetrations { get; set; } = 3;
	[Feature( "Ammo" ), Group( "Penetration" )] public float MaxWallThickness { get; set; } = 128f;

	// --- Ballistique : ricochet ---

	[Feature( "Ammo" ), Group( "Ricochet" )] public int RicochetMaxHits { get; set; } = 2;
	[Feature( "Ammo" ), Group( "Ricochet" )] public float MaxRicochetAngle { get; set; } = 60f;
	[Feature( "Ammo" ), Group( "Ricochet" )] public float RicochetEnergyRetention { get; set; } = 0.55f;
	#endregion

	#region Feature: Pack (boîte, pack, ensemble)
	private bool _isPack;
	[FeatureEnabled( "Pack" )]
	public bool IsPack
	{
		get => _isPack;
		set
		{
			if ( _isPack == value ) return;
			_isPack = value;
			if ( value )
			{
				DisableOthers( nameof( IsPack ) );
				EnsureAttribute( "pack_count", "0" );
			}
			else
			{
				Attributes?.Remove( "pack_count" );
			}
		}
	}

	/// <summary>
	/// Capacité du pack (nombre d'items max à l'intérieur).
	/// </summary>
	[Feature( "Pack" ), Property] public int PackCapacity { get; set; } = 50;

	/// <summary>
	/// Type d'item contenu dans le pack (ex: balle 9mm pour une boîte de 9mm).
	/// </summary>
	[Feature( "Pack" ), Property] public ItemMetadata PackContentType { get; set; }

	/// <summary>
	/// Quantité d'items à mettre dans le pack à la création (pré-remplissage).
	/// </summary>
	[Feature( "Pack" ), Property] public int DefaultFillQuantity { get; set; } = 0;
	#endregion

	#region Feature: Documents
	private bool _isDocument;
	[FeatureEnabled( "Documents" )]
	public bool IsDocument
	{
		get => _isDocument;
		set { if ( _isDocument == value ) return; _isDocument = value; if ( value ) DisableOthers( nameof( IsDocument ) ); }
	}
	[Feature( "Documents" )] public EquipmentResource DocumentViewModel { get; set; }
	#endregion

	#region Feature: Furniture
	private bool _isFurniture;
	[FeatureEnabled( "Furniture" )]
	public bool IsFurniture
	{
		get => _isFurniture;
		set
		{
			if ( _isFurniture == value ) return;
			_isFurniture = value;
			if ( value ) { DisableOthers( nameof( IsFurniture ) ); EnsureAttribute( "owner_sid", "0" ); }
			else Attributes?.Remove( "owner_sid" );
		}
	}
	[Feature( "Furniture" )] public FurnitureType FurnitureCategoryType { get; set; }
	#endregion

	#region Feature: Food
	private bool _isFood;
	[FeatureEnabled( "Food" )]
	public bool IsFood
	{
		get => _isFood;
		set
		{
			if ( _isFood == value ) return;
			_isFood = value;
			if ( value )
			{
				DisableOthers( nameof( IsFood ) );
				EnsureAttribute( "expire_time", "3600" );
				EnsureAttribute( "nutritional_value", "10" );
			}
			else { Attributes?.Remove( "expire_time" ); Attributes?.Remove( "nutritional_value" ); }
		}
	}
	[Feature( "Food" ), MinMax( 1, 100 )] public float RestoreMinValue { get; set; } = 1;
	[Feature( "Food" )] public float RestoreMaxValue { get; set; } = 10;
	[Feature( "Food" )] public SoundEvent ConsumptionSound { get; set; }
	#endregion

	#region Feature: Minerals
	private bool _isMinerals;
	[FeatureEnabled( "Minerals" )]
	public bool IsMinerals
	{
		get => _isMinerals;
		set { if ( _isMinerals == value ) return; _isMinerals = value; if ( value ) DisableOthers( nameof( IsMinerals ) ); }
	}
	#endregion

	#region Feature: Clothing
	private bool _isClothing;
	[FeatureEnabled( "Clothing" )]
	public bool IsClothing
	{
		get => _isClothing;
		set { if ( _isClothing == value ) return; _isClothing = value; if ( value ) DisableOthers( nameof( IsClothing ) ); }
	}
	[Feature( "Clothing" )] public Clothing ClothingResource { get; set; }
	#endregion

	#region Feature: SubContainer (legacy)
	/// <summary>
	/// Plus aucun item n'utilise de vrai sous-conteneur : chargeurs stockent
	/// current_ammo, packs stockent pack_count — la synchronisation d'un
	/// container enfant sur serveur dedie causait des pertes de contenu.
	/// Proprietes conservees a false pour compatibilite de code, a supprimer
	/// une fois que plus rien n'y fait reference.
	/// </summary>
	[Browsable( false )]
	public bool HasSubContainer => false;
	#endregion

	#region Feature: Heal
	private bool _canHeal;
	[FeatureEnabled( "Heal" )]
	public bool CanHeal
	{
		get => _canHeal;
		set
		{
			if ( _canHeal == value ) return;
			_canHeal = value;
			if ( value ) { DisableOthers( nameof( CanHeal ) ); EnsureAttribute( "uses_left", "1" ); }
			else Attributes?.Remove( "uses_left" );
		}
	}
	[Feature( "Heal" ), MinMax( 1, 100 )] public float HealAmount { get; set; } = 1;
	#endregion

	#region Engine Overrides
	protected override void PostLoad()
	{
		if ( All.Contains( this ) ) return;
		All.Add( this );
	}

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "backpack", width, height, "#fdea60", "black" );
	}
	#endregion
}
