using Sandbox;

[AssetType( Name = "Npc Dresser Data", Extension = "dresser", Category = "Npc Dresser" )]
public class DresserDataNpc : GameResource
{
	[Property, Title("Hair")]
	public ClothingContainer.ClothingEntry Hair { get; set; }
	
	[Property, Title("Beard")]
	public ClothingContainer.ClothingEntry Beard { get; set; }

	[Property, Title( "Hat" )]
	public ClothingContainer.ClothingEntry Hat { get; set; }

	[Property, Title( "Glasses" )]
	public ClothingContainer.ClothingEntry Glasses { get; set; }

	[Property, Title( "Top" )]
	public ClothingContainer.ClothingEntry Top { get; set; }

	[Property, Title( "Gloves" )]
	public ClothingContainer.ClothingEntry Gloves { get; set; }

	[Property, Title( "Watch" )]
	public ClothingContainer.ClothingEntry Watch { get; set; }

	[Property, Title( "Underwear" )]
	public ClothingContainer.ClothingEntry Underwear { get; set; }
	
	[Property, Title("Bottom")]
	public ClothingContainer.ClothingEntry Bottom { get; set; }
	
	
	[Property, Title("Shoes")]
	public ClothingContainer.ClothingEntry Shoes { get; set; }

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "backpack", width, height, "#fdea60", "black" );
	}
}
