namespace OpenFramework.Inventory;

public interface IInventoryProvider
{
	// L'ID unique pour la base de données
	string ContainerId { get; }

	// La référence vers le composant qui gère les items
	InventoryContainer Inventory { get; }
}
