using Sandbox;
using OpenFramework.Inventory;

namespace OpenFramework.Systems.Armury_Systems;

[AssetType( Name = "Armury Catalogue", Extension = "armury", Category = "roleplay" )]
public class ArmuryCatalogueResource : GameResource
{
	[Property] public List<ArmuryLicense> Licenses { get; set; } = new();

	public class ArmuryLicense
	{
		[Property] public string Name { get; set; } = "Nouvelle License";
		[Property] public List<ArmuryCategory> Categories { get; set; } = new();
	}

	public class ArmuryCategory
	{
		[Property] public string Name { get; set; } = "Nouvelle Catégorie";
		[Property] public List<ArmuryItem> Items { get; set; } = new();
	}

	public class ArmuryItem
	{
		[Property] public ItemMetadata Item { get; set; }
	}
}
