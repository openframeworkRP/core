namespace OpenFramework.Systems.Weapons;

/// <summary>
/// Composant attache a une <see cref="DroppedEquipment"/> pour persister l'etat
/// du chargeur charge dans l'arme jusqu'a ce qu'elle soit ramassee.
/// </summary>
public class WeaponAmmoDroppedState : Component
{
	[Property, Sync( SyncFlags.FromHost )] public string LoadedMagType { get; set; } = "";
	[Property, Sync( SyncFlags.FromHost )] public int LoadedMagAmmo { get; set; } = 0;
	[Property, Sync( SyncFlags.FromHost )] public int LoadedMagCapacity { get; set; } = 0;
	[Property, Sync( SyncFlags.FromHost )] public int PrimaryAmmo { get; set; } = 0;
}
