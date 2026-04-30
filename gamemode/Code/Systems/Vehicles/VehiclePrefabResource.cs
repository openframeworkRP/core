namespace OpenFramework.Systems.Vehicles;

[AssetType( Name = "Vehicle", Extension = "vehicle", Category = "Roleplay" )]
public partial class VehiclePrefabResource : GameResource
{
	[Category( "General" )]
	public string DisplayName { get; set; }

	[Category( "General" )]
	public VehicleBrand Brand { get; set; } = VehicleBrand.None;

	[Category( "General" )]
	public string Model { get; set; }

	[Category( "General" )]
	public VehicleType Type { get; set; } = VehicleType.Unknown;

	[Category( "Engine" )]
	public int EngineDisplacementCC { get; set; }   // 1598, 1995…

	[Category( "Engine" )]
	public int Horsepower { get; set; }

	[Category( "Engine" )]
	public int TorqueNm { get; set; }

	[Category( "Engine" )]
	public VehicleFuelType FuelType { get; set; } = VehicleFuelType.Petrol;

	[Category( "Specs" )]
	public VehicleDriveType DriveType { get; set; } = VehicleDriveType.FWD;

	[Category( "Specs" )]
	public VehicleTransmission Transmission { get; set; } = VehicleTransmission.Manual;

	[Category( "Economy" )]
	public float Price { get; set; }     // Prix RP

	[Category( "Prefab" )]
	[ResourceType( "prefab" )]
	public PrefabFile Prefab { get; set; }

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "directions_car", width, height, "#fdea60", "black" );
	}

	public void SpawnVehicle( Scene scene, Vector3 position, Rotation rotation )
	{
		if ( Prefab == null )
		{
			Log.Warning( $"Vehicle '{DisplayName}' has no Prefab assigned." );
			return;
		}

		Spawnable.Server(Prefab.ResourcePath, position, rotation );
	}

}
