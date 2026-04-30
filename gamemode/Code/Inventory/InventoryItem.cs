using OpenFramework.Extension;

namespace OpenFramework.Inventory;

public sealed class InventoryItem : Component
{
	/// <summary>
	/// Nom de l'attribut contenant le nombre de balles dans un chargeur.
	/// Defini par EnsureAttribute dans ItemMetadata.IsMagazine.
	/// </summary>
	public const string AttrMagCurrentAmmo = "current_ammo";

	/// <summary>
	/// Nom de l'attribut contenant le nombre d'items dans une boite/pack.
	/// Defini par EnsureAttribute dans ItemMetadata.IsPack.
	/// </summary>
	public const string AttrPackCount = "pack_count";

	/// <summary>
	/// Nom de l'attribut contenant les GUIDs des portes autorisées par cette clé.
	/// Format : GUIDs séparés par des virgules, ex: "aabb-...,ccdd-..."
	/// </summary>
	public const string AttrDoorGuids = "door_guids";

	// On synchronise directement la ressource
	[Property, Sync] public ItemMetadata Metadata { get; set; }

	[Property, Sync] public int SlotIndex { get; set; } = -1;

	/// <summary>
	/// Quantité avec autorité Host stricte.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost ), Change]
	public int Quantity { get; set; } = 1;

	/// <summary>
	/// Attributs sensibles (durabilité, etc.) avec autorité Host stricte.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )]
	public NetDictionary<string, string> Attributes { get; set; } = new();

	/// <summary>
	/// SteamId du joueur qui a droppe cet item au sol (0 si l'item est dans un
	/// inventaire, vient d'un spawner de map, ou n'a jamais ete drop).
	/// Ecrit a chaque drop physique single (InventoryContainer.Drop) et
	/// reecrit au re-drop si un autre joueur l'a ramasse entre-temps.
	/// Utilise par PlacedPropsCleanup pour supprimer les items au sol d'un
	/// joueur deconnecte > CleanupDelay.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )] public ulong DroppedBySteamId { get; set; } = 0;

	/// <summary>
	/// Nombre de balles dans un chargeur (stocké dans l'attribut current_ammo).
	/// Retourne 0 si ce n'est pas un chargeur.
	/// </summary>
	public int MagAmmo
	{
		get => Metadata?.IsMagazine == true ? Attributes.GetInt( AttrMagCurrentAmmo, 0 ) : 0;
		set
		{
			if ( Metadata?.IsMagazine != true ) return;
			Attributes.SetInt( AttrMagCurrentAmmo, Math.Clamp( value, 0, Metadata.MagCapacity ) );
		}
	}

	/// <summary>
	/// Capacite max du chargeur (0 si ce n'est pas un chargeur).
	/// </summary>
	public int MagCapacity => Metadata?.IsMagazine == true ? Metadata.MagCapacity : 0;

	/// <summary>
	/// Nombre d'items dans une boite/pack (stocke dans l'attribut pack_count).
	/// Retourne 0 si ce n'est pas un pack.
	/// </summary>
	public int PackCount
	{
		get => Metadata?.IsPack == true ? Attributes.GetInt( AttrPackCount, 0 ) : 0;
		set
		{
			if ( Metadata?.IsPack != true ) return;
			Attributes.SetInt( AttrPackCount, Math.Clamp( value, 0, Metadata.PackCapacity ) );
		}
	}

	/// <summary>
	/// Capacite max d'une boite/pack (0 si ce n'est pas un pack).
	/// </summary>
	public int PackCapacity => Metadata?.IsPack == true ? Metadata.PackCapacity : 0;

	// Propriétés de commodité
	public string Name => Metadata?.Name ?? "Item";

	/// <summary>
	/// Vérifie si l'objet est périmé.
	/// </summary>
	public bool IsExpired
	{
		get
		{
			if ( Attributes.TryGetValue( "expiry_timestamp", out var ts ) && float.TryParse( ts, out var expireTime ) )
			{
				return Time.Now >= expireTime;
			}
			return false;
		}
	}

	/// <summary>
	/// Temps restant avant expiration en secondes.
	/// </summary>
	public float TimeRemaining => Attributes.TryGetValue( "expiry_timestamp", out var ts ) && float.TryParse( ts, out var et )
		? MathF.Max( 0, et - Time.Now ) // Échéance fixe - Temps qui défile
		: -1f;

	protected override void OnStart()
	{
		// Seul le Host a l'autorité pour initialiser les données de l'instance
		if ( !Networking.IsHost ) return;
		if ( Metadata == null ) return;

		// 1. On copie les attributs par défaut du Metadata vers l'instance,
		// uniquement si pas deja ecrits (ex: CreateMagazineItem positionne current_ammo
		// avant OnStart — il ne faut pas l'ecraser par la valeur 0 par defaut).
		foreach ( var attribute in Metadata.Attributes )
		{
			if ( Attributes.ContainsKey( attribute.Key ) ) continue;
			Attributes[attribute.Key] = attribute.Value;
		}

		if ( Attributes.TryGetValue( "expire_time", out var durationStr ) )
		{
			if ( float.TryParse( durationStr, out var duration ) )
			{
				Attributes["expiry_timestamp"] = (Time.Now + duration).ToString();
			}
		}

		// Pre-remplissage des boites/packs (DefaultFillQuantity) via l'attribut pack_count.
		// Uniquement au premier OnStart — si l'item vient de la DB, pack_count y est deja.
		if ( Metadata.IsPack && Metadata.DefaultFillQuantity > 0 && !Attributes.ContainsKey( "pack_count_initialized" ) )
		{
			Attributes.SetInt( AttrPackCount, Math.Clamp( Metadata.DefaultFillQuantity, 0, Metadata.PackCapacity ) );
			Attributes["pack_count_initialized"] = "1";
		}
	}

	/// <summary>
	/// Logique déclenchée automatiquement lors de la modification de Quantity
	/// </summary>
	private void OnQuantityChanged( int oldValue, int newValue )
	{
		// Maintient le compteur synchronise du container parent pour l'UI client.
		if ( Networking.IsHost )
		{
			var parent = GameObject?.Parent?.GetComponent<InventoryContainer>();
			parent?.RefreshSyncedTotal();
		}

		if ( newValue <= 0 )
		{
			Log.Info( $"[System] {GameObject.Name} est épuisé. Auto-deletion lancée." );
			GameObject.Destroy();
		}
	}
}
