using Sandbox.Events;
using OpenFramework;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;

namespace OpenFramework.GameLoop.Rules;

/// <summary>
/// Supprime tous les objets monde rattaches a un joueur qui reste deconnecte
/// plus de <see cref="CleanupDelay"/>. Si le joueur reconnecte pendant le
/// grace period, le cleanup est annule.
///
/// Sources de tracking inspectees :
///   - <see cref="FurnitureVisual.PlacedBySteamId"/> (meubles poses, figes ou non)
///   - <see cref="InventoryItem.DroppedBySteamId"/> sur les items root au sol
///   - <see cref="DroppedInventory.DroppedBySteamId"/> sur les boxes/colis
/// Les sacs de mort (DroppedInventory.IsDeath=true) gardent DroppedBySteamId=0
/// et ne sont PAS touches : ils ont leur propre Timer.HostAfter(DropLifetime).
///
/// Composant host-only. Auto-cree au besoin par ServerManager s'il n'est
/// pas deja present dans la scene.
/// </summary>
public sealed class PlacedPropsCleanup : Component,
	IGameEventHandler<PlayerDisconnectedEvent>,
	IGameEventHandler<PlayerConnectedEvent>
{
	/// <summary>
	/// Temps (secondes) entre la deconnexion et la suppression des props.
	/// </summary>
	public const float CleanupDelay = 300f;

	// steamId -> RealTime d'expiration
	private readonly Dictionary<ulong, float> _pending = new();

	public void OnGameEvent( PlayerDisconnectedEvent eventArgs )
	{
		if ( !Networking.IsHost ) return;
		var client = eventArgs.Player;
		if ( client == null || client.SteamId == 0 ) return;

		_pending[client.SteamId] = RealTime.Now + CleanupDelay;
		Log.Info( $"[PlacedPropsCleanup] {client.DisplayName} ({client.SteamId}) deconnecte, cleanup prop(s) dans {CleanupDelay}s" );
	}

	public void OnGameEvent( PlayerConnectedEvent eventArgs )
	{
		if ( !Networking.IsHost ) return;
		var client = eventArgs.Client;
		if ( client == null ) return;

		if ( _pending.Remove( client.SteamId ) )
			Log.Info( $"[PlacedPropsCleanup] {client.DisplayName} reconnecte, cleanup annule" );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( _pending.Count == 0 ) return;

		var now = RealTime.Now;
		List<ulong> expired = null;
		foreach ( var kv in _pending )
		{
			if ( kv.Value <= now )
			{
				expired ??= new();
				expired.Add( kv.Key );
			}
		}
		if ( expired == null ) return;

		foreach ( var sid in expired )
		{
			DeletePropsOfPlayer( sid );
			_pending.Remove( sid );
		}
	}

	private void DeletePropsOfPlayer( ulong steamId )
	{
		// On collecte les GameObjects racines a detruire via les 3 sources de
		// tracking : meubles poses (FurnitureVisual.PlacedBySteamId), items
		// physiques droppes (InventoryItem.DroppedBySteamId sur un root) et
		// boxes/colis (DroppedInventory.DroppedBySteamId). Un HashSet evite
		// les doublons quand un meme GO porte plusieurs composants.
		var toDestroy = new HashSet<GameObject>();

		// LOG TEMPORAIRE (debug bug 27.04 : props placés ne despawn pas) —
		// dump tous les FV de la scene avec leur PlacedBySteamId pour confirmer
		// l'ecriture cote host. A retirer une fois le bug confirme corrige.
		int fvScanned = 0;
		int fvMatching = 0;
		foreach ( var fv in Scene.GetAllComponents<FurnitureVisual>() )
		{
			if ( !fv.IsValid() ) continue;
			fvScanned++;
			Log.Info( $"[PlacedPropsCleanup][debug] FV sur '{fv.GameObject?.Name}' PlacedBySteamId={fv.PlacedBySteamId} (cible={steamId})" );
			if ( fv.PlacedBySteamId != steamId ) continue;
			fvMatching++;
			var root = fv.GameObject?.Root;
			if ( root.IsValid() ) toDestroy.Add( root );
		}
		Log.Info( $"[PlacedPropsCleanup][debug] {fvScanned} FV scannes, {fvMatching} matchent steamId={steamId}" );

		foreach ( var item in Scene.GetAllComponents<InventoryItem>() )
		{
			if ( !item.IsValid() ) continue;
			if ( item.DroppedBySteamId != steamId ) continue;
			// On ne touche qu'aux items au sol (root). Les items presents
			// dans un inventaire sont parentes au container et ne sont pas
			// root — ils ne doivent pas etre supprimes ici.
			var go = item.GameObject;
			if ( go == null || !go.IsRoot ) continue;
			toDestroy.Add( go );
		}

		foreach ( var box in Scene.GetAllComponents<DroppedInventory>() )
		{
			if ( !box.IsValid() ) continue;
			if ( box.DroppedBySteamId != steamId ) continue;
			var root = box.GameObject?.Root;
			if ( root.IsValid() ) toDestroy.Add( root );
		}

		int count = 0;
		foreach ( var go in toDestroy )
		{
			if ( !go.IsValid() ) continue;
			go.Destroy();
			count++;
		}
		Log.Info( $"[PlacedPropsCleanup] {count} objet(s) supprime(s) du joueur {steamId}" );
	}

	/// <summary>
	/// Garantit qu'une instance du cleanup existe dans la scene. Appele depuis
	/// ServerManager au demarrage serveur.
	/// </summary>
	public static void EnsureExists( Scene scene )
	{
		if ( !Networking.IsHost ) return;
		if ( scene == null ) return;
		if ( scene.GetAllComponents<PlacedPropsCleanup>().FirstOrDefault() != null ) return;

		var go = scene.CreateObject();
		go.Name = "PlacedPropsCleanup";
		go.Components.Create<PlacedPropsCleanup>();
		Log.Info( "[PlacedPropsCleanup] Composant auto-cree dans la scene." );
	}
}
