using Sandbox;

namespace OpenFramework.Systems.Shop_System;

public sealed class ShopCatalogueManager : Component
{

	[Property] public List<ShopCatalogueResource> ShopCatalogueResources { get; set; }

}
