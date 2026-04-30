using OpenFramework.Inventory;

namespace OpenFramework;

[AssetType(  Name = "Clothing Catalogue", Extension = "clocat" , Category = "roleplay" )]
public partial class ClothingCatalogResource : GameResource
{
	[Property] public string Title { get; set; } = "Nouveau Catalogue";

	// Premier niveau : "Cheveux", "Hauts", "Bas"
	[Property] public List<ClothingMainCategory> MainCategories { get; set; } = new();

	public class ClothingMainCategory
	{
		[Property] public string Name { get; set; }
		[Property, ImageAssetPath] public string Icon { get; set; }

		// Deuxième niveau : "Courts", "Longs", "Dégradés"
		[Property] public List<ClothingSubCategory> SubCategories { get; set; } = new();
	}

	public class ClothingSubCategory
	{
		[Property] public string Name { get; set; }
		[Property] public List<ItemMetadata> Items { get; set; } = new();
	}
}
