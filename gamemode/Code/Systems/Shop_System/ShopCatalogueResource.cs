using Sandbox;
using OpenFramework.Inventory;

namespace OpenFramework.Systems.Shop_System;

[AssetType( Name = "Shop Catalogue", Extension = "shopcat", Category = "roleplay" )]
public class ShopCatalogueResource : GameResource
{
	[Property] public string DisplayName { get; set; }
	[Property] public List<ShopCategory> Categories { get; set; } = new();

	public class ShopCategory
	{
		[Property] public string Name { get; set; } = "Nouvelle Catégorie";
		[Property] public List<ShopItem> Items { get; set; } = new();
	}

	public class ShopItem
	{
		[Property] public ItemMetadata Item { get; set; }
		[Property] public int MinOrderQuantity { get; set; } = 1;
	}
}
