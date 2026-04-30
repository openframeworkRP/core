namespace OpenFramework.Api.Models.Administration.Logs;

/// <summary>
/// Trace tous les mouvements d'items pour audit anti-duplication.
/// Chaque transfert a une Source et une Target — si l'une est null/vide,
/// c'est une création (Source vide, ex: spawn admin) ou destruction (Target vide,
/// ex: consommation). Une duplication apparaît typiquement comme deux Add du
/// même ItemGameId pour deux ActorSteamId différents à 1-2s d'écart.
/// </summary>
public class InventoryLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime At { get; set; } = DateTime.UtcNow;

    /// <summary>SteamId du joueur dont l'inventaire est affecté (ou "system" pour les actions auto).</summary>
    public string ActorSteamId { get; set; } = "";

    /// <summary>CharacterId actif au moment du transfert si dispo.</summary>
    public string? CharacterId { get; set; }

    /// <summary>
    /// Type d'opération : "add", "remove", "move", "drop", "pickup", "use",
    /// "save_full", "save_clear", "transfer_bullets", "equip", "unequip",
    /// "store_in", "store_out". Conservé en string pour ouverture future.
    /// </summary>
    public string Action { get; set; } = "";

    public string ItemGameId { get; set; } = "";

    public int Count { get; set; }

    /// <summary>
    /// Type de source : "player_inventory", "player_clothing", "world",
    /// "dropped_bag", "storage_chest", "api_save", "death_drop", "spawn".
    /// </summary>
    public string SourceType { get; set; } = "";

    /// <summary>Id de la source (containerId, droppedBagId, characterId, etc.) si applicable.</summary>
    public string? SourceId { get; set; }

    public string TargetType { get; set; } = "";

    public string? TargetId { get; set; }

    /// <summary>JSON libre : slot index, attributes, durability, transferBulletsCount, etc.</summary>
    public string? MetadataJson { get; set; }
}
