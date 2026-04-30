using Facepunch;

/// <summary>
/// What slot is this equipment for?
/// </summary>
public enum EquipmentSlot
{
	Undefined = 0,

	/// <summary>
	/// Non-pistol guns.
	/// </summary>
	Primary = 1,

	/// <summary>
	/// Pistols.
	/// </summary>
	Secondary = 2,

	/// <summary>
	/// Knives etc.
	/// </summary>
	Melee = 3,

	/// <summary>
	/// Grenades, flashbangs etc.
	/// </summary>
	Throwable = 4,

	/// <summary>
	/// Phone, radio, tablet, lockpick, handcuffs, taser etc.
	/// </summary>
	Handheld = 5,

	/// <summary>
	/// Fists.
	/// </summary>
	Punch = 6,
}

/// <summary>
/// A resource definition for a piece of equipment. This could be a weapon, or a deployable, or a gadget, or a grenade.. Anything really.
/// </summary>
[AssetType( Name = "Equipment Item", Extension = "equip", Category = "Roleplay" )]
public partial class EquipmentResource : GameResource
{
	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "track_changes", width, height, "", "#5877E0" );
	}


	public static HashSet<EquipmentResource> All { get; set; } = new();

	[Category( "Base" )]
	public string Name { get; set; } = "My Equipment";
	
	[Category( "Base" )]
	public string Description { get; set; } = "";

	[Category( "Base" )]
	public EquipmentSlot Slot { get; set; }

	/// <summary>
	/// If true, owner will drop this equipment if they disconnect.
	/// </summary>
	[Category( "Base" )]
	public bool DropOnDisconnect { get; set; } = false;

	/// <summary>
	/// The equipment's icon
	/// </summary>
	[Group( "Base" ), ImageAssetPath] public string Icon { get; set; }

	/// <summary>
	/// The prefab to create and attach to the player when spawning it in.
	/// </summary>
	[Category( "Prefabs" )]
	public GameObject MainPrefab { get; set; }

	/// <summary>
	/// A world model that we'll put on the player's arms in third person.
	/// </summary>
	[Category( "Prefabs" )]
	public GameObject WorldModelPrefab { get; set; }

	/// <summary>
	/// The prefab to create when making a viewmodel for this equipment.
	/// </summary>
	[Category( "Prefabs" )]
	public GameObject ViewModelPrefab { get; set; }

	/// <summary>
	/// The equipment's model
	/// </summary>
	[Category( "Information" )]
	public Model WorldModel { get; set; }

	[Category( "Damage" )]
	public float? ArmorReduction { get; set; }

	[Category( "Damage" )]
	public float? HelmetReduction { get; set; }

	[Category( "Information" )]
	[Model.BodyGroupMask( ModelParameter = "WorldModel" )]
	public ulong WorldModelBodyGroups { get; set; }

	[Property, Group( "Movement" )]
	public float CollisionOffset { get; set; } = 6f;
	protected override void PostLoad()
	{
		if ( All.Contains( this ) )
		{
			Log.Warning( "Tried to add two of the same equipment (?)" );
			return;
		}

		All.Add( this );
	}

	public static EquipmentResource Get( string name )
	{
		return All.FirstOrDefault( x => x.Name == name );
	}
}
