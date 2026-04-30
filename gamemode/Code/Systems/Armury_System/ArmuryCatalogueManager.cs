using Sandbox;

namespace OpenFramework.Systems.Armury_Systems;

public sealed class ArmuryCatalogueManager : Component
{

	[Property] public List<ArmuryCatalogueResource> ArmuryCatalogueResources { get; set; }

}
