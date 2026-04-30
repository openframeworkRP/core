using Facepunch;
using Sandbox.Events;
using OpenFramework.Extension;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.Systems.AtmSystem;
using OpenFramework.Systems.Grab_System;
using OpenFramework.UI.QuickMenuSystem;
using OpenFramework.World;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Pawn;

/// <summary>
/// Called on the player using something, when using something
/// </summary>
public record PlayerUseEvent( IUse Object ) : IGameEvent;

/// <summary>
/// Called on the player actioning something
/// </summary>
public record PlayerActionEvent( IActionnable Object ) : IGameEvent;

/// <summary>
/// Grab actions that the player can perform.
/// </summary>
public enum GrabAction
{
	None = 0,
	SweepDown,
	SweepRight,
	SweepLeft,
	PushButton
}

partial class PlayerPawn
{
	[ConVar( "pickup_debug" )]
	public static bool PickupDebug { get; set; } = false;

	/// <summary>
	/// Is the player holding use?
	/// </summary>
	[Sync] public bool IsUsing { get; set; }

	/// <summary>
	/// How far can we use stuff?
	/// </summary>
	[Property, Group( "Interaction" )] public float UseDistance { get; set; } = 72f;

	/// <summary>
	/// Duree minimale d'un hold E pour basculer en "long press" (radial / pickup
	/// direct selon la cible). En-dessous : tap court → action immediate (porte
	/// toggle, dialogue PNJ).
	/// </summary>
	[Property, Group( "Interaction" )] public float LongPressDuration { get; set; } = 0.25f;

	/// <summary>
	/// (Obsolete) Anciennement utilise pour le ramassage progressif via E. Le pickup
	/// passe maintenant par le radial menu (long press E). Conserve pour ne pas casser
	/// les scenes qui referencent encore la propriete.
	/// </summary>
	[Property, Group( "Interaction" )] public float PickupHoldDuration { get; set; } = 1f;

	/// <summary>
	/// (Obsolete) Anciennement utilise pour le maintien E sur joueur. Conserve pour
	/// compatibilite avec les scenes qui referencent encore la propriete.
	/// </summary>
	[Property, Group( "Interaction" )] public float PlayerInteractHoldDuration { get; set; } = 0.5f;

	/// <summary>
	/// (Obsolete) Anciennement utilise pour le maintien E sur ATM. L'ATM s'ouvre maintenant
	/// par long press E. Conserve pour compatibilite avec les scenes existantes.
	/// </summary>
	[Property, Group( "Interaction" )] public float AtmInteractHoldDuration { get; set; } = 0.4f;

	/// <summary>
	/// Which object did the player last press use on?
	/// </summary>
	public GameObject LastUsedObject { get; private set; }

	public IUse Hovered { get; private set; }

	/// <summary>
	/// Progression du ramassage (0 à 1). Conserve pour compatibilite avec le HUD.
	/// </summary>
	public float PickupProgress { get; set; }

	private float _eHeldTime;
	private bool _eLongHandled;

	private IEnumerable<SceneTraceResult> TraceUsables()
	{
		return Scene.Trace.Ray( AimRay, UseDistance )
			.Size( 5f )
			.IgnoreGameObjectHierarchy( GameObject )
			.HitTriggers()
			.RunAll() ?? Enumerable.Empty<SceneTraceResult>();
	}

	private IEnumerable<IUse> GetUsables()
	{
		return TraceUsables()
			.Select( x => x.GameObject.GetComponentInParent<IUse>() );
	}

	private IEnumerable<IActionnable> GetActionnables()
	{
		return TraceUsables()
			.Select( x => x.GameObject.GetComponentInParent<IActionnable>() );
	}

	/// <summary>Vrai si n'importe quelle UI ATM est ouverte pour ce client local (plus de lock serveur).</summary>
	private bool AnyAtmUiOpenForLocalClient()
	{
		foreach ( var atmUi in Scene.GetAllComponents<OpenFramework.AtmUI>() )
			if ( atmUi.IsOpenForLocalClient ) return true;
		return false;
	}

	private void UpdateUse()
	{
		IsUsing = Input.Down( "Use" );

		// Pendant qu'on porte un objet via GrabSystem, on lui laisse l'exclusivite
		// sur E : le drop est gere par GrabSystem.OnUpdate (re-press E → drop). On
		// ne route ni tap court ni long press ailleurs pour eviter qu'un drop ne
		// declenche aussi un dialogue PNJ ou un radial sur ce qu'on regarde.
		var rootTags = GameObject.Root?.Tags;
		bool isGrabbing = rootTags != null && rootTags.Has( "is_grabbing" );
		if ( isGrabbing )
		{
			_eHeldTime = 0f;
			_eLongHandled = false;
			return;
		}

		var hits = TraceUsables().ToList();

		Hovered = hits
			.Select( x => x.GameObject.GetComponentInParent<IUse>() )
			.FirstOrDefault();

		// Detection tap court vs long press sur E.
		// - Tap court (release < LongPressDuration) : action immediate sur la cible
		//   (porte toggle, dialogue PNJ). Si la cible n'a pas de quick-action
		//   (item au sol, decor grabbable, meuble...), rien ne se passe.
		// - Long press (down >= LongPressDuration) : pickup direct (item au sol,
		//   arme jetee, carton) ou radial menu (porte, meuble, joueur, decor
		//   grabbable). Le PNJ est explicitement skip cote long press.
		if ( Input.Pressed( "Use" ) )
		{
			_eHeldTime = 0f;
			_eLongHandled = false;
		}

		if ( Input.Down( "Use" ) )
		{
			if ( !_eLongHandled )
			{
				_eHeldTime += Time.Delta;
				if ( _eHeldTime >= LongPressDuration )
				{
					_eLongHandled = true;
					Log.Info( $"[Use][long-trigger] held={_eHeldTime:F2}s ≥ {LongPressDuration:F2}s → HandleLongPress (hits={hits.Count})" );
					HandleLongPress( hits );
				}
			}
		}
		else
		{
			if ( _eHeldTime > 0f && !_eLongHandled )
			{
				Log.Info( $"[Use][short-release] held={_eHeldTime:F2}s → HandleShortPress" );
				HandleShortPress( hits );
			}
			else if ( _eLongHandled )
			{
				Log.Info( $"[Use][release] held={_eHeldTime:F2}s longHandled=true" );
			}
			_eHeldTime = 0f;
			_eLongHandled = false;
		}
	}

	/// <summary>
	/// Tap court E (quick action) : action immediate sur la cible.
	/// - Porte (Door / RollingDoor) → toggle ouvert/ferme
	/// - PNJ (Npc / NpcLogical) → declenche dialogue
	/// Pour tout autre IUse on ne fait rien : le pickup, le grab et les options
	/// type "verrouiller" passent obligatoirement par le long press (radial ou
	/// pickup direct). Cela evite tout ramassage accidentel au simple tap.
	///
	/// On cible le GameObject explicite (RequestUseTargetHost) plutot que de
	/// passer un AimRay au host : sinon, si un joueur ou un autre IUE se trouve
	/// entre le pawn et la vraie cible, le re-trace cote host renvoie le mauvais
	/// objet et l'interaction echoue silencieusement (bug "impossible de parler
	/// au PNJ vente bouf si un joueur est colle a moi").
	/// </summary>
	private void HandleShortPress( List<SceneTraceResult> hits )
	{
		foreach ( var hit in hits )
		{
			// Porte : toggle direct via OnUse cote host.
			var door = hit.GameObject.Components.Get<Door>( FindMode.EverythingInSelfAndAncestors )
				?? hit.GameObject.Components.Get<Door>( FindMode.EverythingInSelfAndDescendants );
			if ( door != null )
			{
				Log.Info( $"[E][short] Door detectee → RequestUseTargetHost (toggle)" );
				RequestUseTargetHost( door.GameObject );
				return;
			}

			var rollingDoor = hit.GameObject.Components.Get<RollingDoor>( FindMode.EverythingInSelfAndAncestors )
				?? hit.GameObject.Components.Get<RollingDoor>( FindMode.EverythingInSelfAndDescendants );
			if ( rollingDoor != null )
			{
				Log.Info( $"[E][short] RollingDoor detectee → RequestUseTargetHost (toggle)" );
				RequestUseTargetHost( rollingDoor.GameObject );
				return;
			}

			// PNJ : ouvre le dialogue. On passe le GameObject du PNJ explicitement
			// pour que le host n'ait pas a refaire un trace (qui pourrait taper un
			// joueur entre nous et le PNJ → dialogue impossible).
			var npc = hit.GameObject.Components.Get<OpenFramework.Systems.Npc.Npc>( FindMode.EverythingInSelfAndAncestors )
				?? hit.GameObject.Components.Get<OpenFramework.Systems.Npc.Npc>( FindMode.EverythingInSelfAndDescendants );
			var npcLogical = hit.GameObject.Components.Get<OpenFramework.NpcLogical>( FindMode.EverythingInSelfAndAncestors )
				?? hit.GameObject.Components.Get<OpenFramework.NpcLogical>( FindMode.EverythingInSelfAndDescendants );

			if ( npc != null || npcLogical != null )
			{
				var npcGo = npcLogical?.GameObject ?? npc?.GameObject;
				Log.Info( $"[E][short] PNJ detecte ({(npc != null ? "Npc" : "NpcLogical")}) → RequestUseTargetHost (dialogue) sur {npcGo?.Name}" );
				RequestUseTargetHost( npcGo );
				return;
			}
		}
	}

	/// <summary>
	/// Long press E : ouvre le radial menu approprie selon ce qu'on vise, OU
	/// declenche un pickup direct pour les items au sol (WorldItem, arme jetee,
	/// carton). Le hold E sert d'anti-duplication : un simple tap ne ramasse
	/// jamais. Les PNJ sont explicitement ignores (pas de radial sur PNJ).
	/// </summary>
	private void HandleLongPress( List<SceneTraceResult> hits )
	{
		// PNJ : aucun radial. Le tap court a deja gere le dialogue via HandleShortPress.
		foreach ( var hit in hits )
		{
			var npc = hit.GameObject.Components.Get<OpenFramework.Systems.Npc.Npc>( FindMode.EverythingInSelfAndAncestors )
				?? hit.GameObject.Components.Get<OpenFramework.Systems.Npc.Npc>( FindMode.EverythingInSelfAndDescendants );
			var npcLogical = hit.GameObject.Components.Get<OpenFramework.NpcLogical>( FindMode.EverythingInSelfAndAncestors )
				?? hit.GameObject.Components.Get<OpenFramework.NpcLogical>( FindMode.EverythingInSelfAndDescendants );
			if ( npc != null || npcLogical != null )
			{
				Log.Info( "[E][long] PNJ vise → ignore (pas de radial PNJ)" );
				return;
			}
		}

		// Toggle ATM : si une UI ATM est deja ouverte → ferme, peu importe la cible.
		if ( Connection.Local != null )
		{
			foreach ( var atmUi in Scene.GetAllComponents<OpenFramework.AtmUI>() )
			{
				if ( atmUi.IsOpenForLocalClient )
				{
					Log.Info( "[E][long] Fermeture ATM (toggle)" );
					atmUi.CloseFromExternal();
					return;
				}
			}
		}

		// Joueur en face → radial menu joueur. Detection via tag "player" :
		// le collider du pawn peut etre une capsule "movement" ou une hitbox
		// dans une branche fille, ou le PlayerPawn lui-meme peut etre sur le
		// Root — bref Components.Get<PlayerPawn> est peu fiable. Le tag est
		// pose sur tous les colliders du pawn, donc on s'appuie dessus puis
		// on remonte au Root pour retrouver le PlayerPawn.
		PlayerPawn hoveredPlayer = null;
		foreach ( var hit in hits )
		{
			var go = hit.GameObject;
			if ( go == null ) continue;

			var rootTags = go.Root?.Tags;
			bool isPlayerHit = (go.Tags?.Has( "player" ) ?? false) || (rootTags?.Has( "player" ) ?? false);
			if ( !isPlayerHit ) continue;
			if ( go.Root == null ) continue;

			var p = Scene.GetAllComponents<PlayerPawn>()
				.FirstOrDefault( x => x.GameObject?.Root == go.Root );

			if ( p != null && p != this )
			{
				hoveredPlayer = p;
				Log.Info( $"[E][player-search] joueur detecte via tag player : {p.GameObject.Name} proxy={p.IsProxy} displayName={p.DisplayName}" );
				break;
			}
		}

		if ( hoveredPlayer != null )
		{
			Log.Info( $"[E][long] PlayerRadialMenu pour {hoveredPlayer.DisplayName}" );
			PlayerRadialMenu.Open( hoveredPlayer );
			return;
		}

		// ATM en face → ouverture directe (pas de radial intermediaire).
		AtmButtonInteract hoveredAtm = null;
		foreach ( var hit in hits )
		{
			var atm = hit.GameObject.Components.Get<AtmButtonInteract>( FindMode.EverythingInSelfAndAncestors );
			if ( atm == null ) continue;
			hoveredAtm = atm;
			break;
		}

		if ( hoveredAtm != null && hoveredAtm.Atm != null )
		{
			Log.Info( "[E][long] Ouverture ATM" );
			hoveredAtm.Atm.Open();
			return;
		}

		// WeedPot → radial spécifique avec "Récolter" et "Ramasser le pot".
		// Doit être détecté AVANT WorldItem car WeedPot possède un composant WorldItem.
		WeedPot hoveredWeedPot = null;
		foreach ( var hit in hits )
		{
			var wp = hit.GameObject.Components.Get<WeedPot>( FindMode.EverythingInSelfAndAncestors );
			if ( wp == null ) continue;
			hoveredWeedPot = wp;
			break;
		}

		if ( hoveredWeedPot != null )
		{
			Log.Info( $"[E][long] OpenForWeedPot {hoveredWeedPot.GameObject.Name}" );
			PlayerRadialMenu.OpenForWeedPot( hoveredWeedPot );
			return;
		}

		// WorldItem au sol (hors d'un container) → PICKUP DIRECT au hold E.
		// Le hold de LongPressDuration sert d'anti-duplication (un simple tap ne
		// ramasse jamais). Pour les items dans un container (colis/poubelle), le
		// pickup passe par l'UI du container, pas par ce path.
		WorldItem hoveredWorldItem = null;
		foreach ( var hit in hits )
		{
			var wi = hit.GameObject.Components.Get<WorldItem>( FindMode.EverythingInSelfAndAncestors );
			if ( wi == null ) continue;
			if ( wi.Components.Get<InventoryContainer>( FindMode.EverythingInSelfAndAncestors ) != null ) continue;
			hoveredWorldItem = wi;
			break;
		}

		if ( hoveredWorldItem != null )
		{
			Log.Info( $"[E][long] Pickup direct WorldItem {hoveredWorldItem.GameObject.Name}" );
			RequestPickupItem( hoveredWorldItem );
			return;
		}

		// DroppedEquipment (arme jetee) → PICKUP DIRECT au hold E.
		DroppedEquipment hoveredDroppedWeapon = null;
		foreach ( var hit in hits )
		{
			var de = hit.GameObject.Components.Get<DroppedEquipment>( FindMode.EverythingInSelfAndAncestors );
			if ( de == null ) continue;
			hoveredDroppedWeapon = de;
			break;
		}

		if ( hoveredDroppedWeapon != null )
		{
			Log.Info( $"[E][long] Pickup direct DroppedWeapon {hoveredDroppedWeapon.GameObject.Name}" );
			RequestPickupTarget( hoveredDroppedWeapon.GameObject );
			return;
		}

		// DroppedInventory (carton/colis) → PICKUP DIRECT au hold E (ouvre
		// l'inventaire du carton via IUse.OnUse cote host).
		DroppedInventory hoveredDropped = null;
		foreach ( var hit in hits )
		{
			var di = hit.GameObject.Components.Get<DroppedInventory>( FindMode.EverythingInSelfAndAncestors );
			if ( di == null ) continue;
			hoveredDropped = di;
			break;
		}

		if ( hoveredDropped != null )
		{
			Log.Info( $"[E][long] Pickup direct DroppedInventory {hoveredDropped.GameObject.Name}" );
			RequestPickupTarget( hoveredDropped.GameObject );
			return;
		}

		// TrashBin (poubelle publique) → radial avec "Ouvrir la poubelle".
		// Meme logique que DroppedInventory : tout passe par le radial pour
		// eviter une ouverture parasite de l'inventaire au hold E.
		OpenFramework.Systems.Jobs.TrashBin hoveredTrash = null;
		foreach ( var hit in hits )
		{
			var t = hit.GameObject.Components.Get<OpenFramework.Systems.Jobs.TrashBin>( FindMode.EverythingInSelfAndAncestors );
			if ( t == null ) continue;
			hoveredTrash = t;
			break;
		}

		if ( hoveredTrash != null )
		{
			Log.Info( $"[E][long] OpenForTrashBin {hoveredTrash.GameObject.Name}" );
			PlayerRadialMenu.OpenForTrashBin( hoveredTrash );
			return;
		}

		// Lookup direct des ShopSign dans la scene : plus fiable qu'une trace ray
		// pour les enseignes accrochees en hauteur (collider tres fin du modele).
		// On retient le sign le plus proche dans le cone de visee qui est dans la
		// range. Cela couvre tous les modeles d'enseigne (sign_metal_name,
		// sm_sign_06b, etc.) sans dependre de la geometrie du collider.
		OpenFramework.Systems.Tools.ShopSign closestSign = null;
		float closestSqr = float.MaxValue;
		float maxRangeSqr = OpenFramework.Systems.Tools.ShopSign.InteractionRange * OpenFramework.Systems.Tools.ShopSign.InteractionRange;
		var aimForward = AimRay.Forward;
		foreach ( var sign in Scene.GetAllComponents<OpenFramework.Systems.Tools.ShopSign>() )
		{
			if ( !sign.IsValid() ) continue;
			var toSign = sign.WorldPosition - WorldPosition;
			var sqr = toSign.LengthSquared;
			if ( sqr > maxRangeSqr ) continue;
			var dir = toSign.Normal;
			var dot = Vector3.Dot( dir, aimForward );
			// dot >= 0.85 -> sign dans un cone d'environ +/- 32 degres autour de la visee.
			if ( dot < 0.85f ) continue;
			if ( sqr < closestSqr )
			{
				closestSign = sign;
				closestSqr = sqr;
			}
		}

		if ( closestSign != null )
		{
			var signFv = closestSign.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndAncestors )
				?? closestSign.GameObject.Root?.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndDescendants );
			if ( signFv != null )
			{
				if ( OpenFramework.Systems.Tools.ShopSign.DebugLogs ) Log.Info( $"[E][long] ShopSign le plus proche dans le cone : '{signFv.GameObject.Name}' (dist={MathF.Sqrt( closestSqr ):0})" );
				PlayerRadialMenu.OpenForFurniture( signFv.GameObject );
				return;
			}
			else if ( OpenFramework.Systems.Tools.ShopSign.DebugLogs )
			{
				Log.Info( $"[E][long] ShopSign trouve sur '{closestSign.GameObject.Name}' mais aucun FurnitureVisual associe" );
			}
		}

		// Trace dediee aux objets a tag "actionmenu"/"furniture" (porte, meuble, coffre, chaise...).
		var trace = Scene.Trace.Ray( AimRay, Constants.Instance.InteractionDistance )
			.Size( 5f )
			.IgnoreGameObjectHierarchy( GameObject )
			.HitTriggers()
			.WithAnyTags( "actionmenu", "furniture" )
			.Run();

		if ( trace.Hit )
		{
			var components = trace.GameObject.Components;

			var door = components.Get<Door>( FindMode.EverythingInSelfAndChildren );
			if ( door != null )
			{
				DoorRadialMenu.Open( door );
				return;
			}

			// Le collider du panneau est sur l'enfant 'Panel' du prefab → on cherche aussi
			// chez les ancetres pour retrouver le RollingDoor pose sur la racine.
			var rollingDoor = components.Get<RollingDoor>( FindMode.EverythingInSelfAndAncestors )
				?? components.Get<RollingDoor>( FindMode.EverythingInSelfAndChildren );
			if ( rollingDoor != null )
			{
				RollingDoorRadialMenu.Open( rollingDoor );
				return;
			}

			var policeLocker = components.Get<PoliceLocker>( FindMode.EverythingInSelfAndAncestors )
				?? components.Get<PoliceLocker>( FindMode.EverythingInSelfAndChildren );
			if ( policeLocker != null )
			{
				PlayerRadialMenu.OpenForPoliceLocker( policeLocker );
				return;
			}

			var storage = components.GetInParentOrSelf<StorageComponent>();
			var furnitureVisual = components.GetInParentOrSelf<FurnitureVisual>()
				?? trace.GameObject.Root?.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndDescendants );

			if ( OpenFramework.Systems.Tools.ShopSign.DebugLogs ) Log.Info( $"[E][long] Trace hit '{trace.GameObject?.Name}' -> furnitureVisual={furnitureVisual != null}, storage={storage != null}" );

			if ( furnitureVisual != null )
			{
				PlayerRadialMenu.OpenForFurniture( furnitureVisual.GameObject );
				return;
			}

			if ( storage != null )
			{
				using ( Rpc.FilterInclude( Client.Local.Connection ) )
					QuickMenu.OpenLocal( new StorageActionMenu( storage ) );
				return;
			}

			var chair = components.GetInParentOrSelf<ChairComponent>();
			if ( chair != null && !chair.IsOccupied )
			{
				// Path host-authoritative : on demande au host de nous asseoir.
				// Le host valide la disponibilite (IsOccupied) en strict authority.
				chair.RequestSit();
				Input.Clear( "Jump" );
				return;
			}
		}

		// Decor grabbable (tag "grab" + Rigidbody motion enabled) → radial avec
		// option "Grab". Couvre les props physiques poses sur la map qui ne sont
		// ni furniture (FurnitureVisual), ni WorldItem, ni porte. La verif
		// MotionEnabled ecarte les Rigidbody statiques (Motion Disabled).
		GameObject grabbableTarget = null;
		foreach ( var hit in hits )
		{
			var go = hit.GameObject;
			if ( go == null ) continue;
			var rootTags = go.Root?.Tags;
			bool hasGrabTag = (go.Tags?.Has( "grab" ) ?? false) || (rootTags?.Has( "grab" ) ?? false);
			if ( !hasGrabTag ) continue;

			var body = go.Components.GetInParentOrSelf<Rigidbody>();
			if ( body == null || !body.MotionEnabled ) continue;

			grabbableTarget = body.GameObject;
			break;
		}

		if ( grabbableTarget != null )
		{
			Log.Info( $"[E][long] OpenForGrabbable {grabbableTarget.Name}" );
			PlayerRadialMenu.OpenForGrabbable( grabbableTarget );
			return;
		}

		// Aucun radial specifique → on tente uniquement IActionnable (PNJ, etc.).
		// IUse n'est volontairement plus declenche en fallback : il ouvrait
		// automatiquement l'inventaire des colis/containers (DroppedInventory),
		// alors que le user veut que le pickup d'item passe par le radial.
		// Pour les vrais usages IUse au-dela des containers (boutons, switches),
		// il faudra leur ajouter une route dediee dans HandleLongPress si besoin.
		Log.Info( "[E][long] aucune cible specifique → fallback RequestActionHost" );
		RequestActionHost( AimRay );
	}

	/// <summary>
	/// Wrapper appele depuis le radial menu OpenForWorldItem → declenche le RPC host.
	/// Le check IsValid host-side empeche toute duplication sur double-click.
	/// </summary>
	public void RequestPickupItem( WorldItem item )
	{
		if ( item == null || !item.IsValid() ) return;
		RequestPickupHost( item );
	}

	/// <summary>
	/// Wrapper appele depuis le radial menu OpenForDroppedWeapon (ou tout autre IUse
	/// dont on a la reference directe). Cote host on resout l'IUse via le GameObject.
	/// </summary>
	public void RequestPickupTarget( GameObject targetGo )
	{
		if ( targetGo == null || !targetGo.IsValid() ) return;
		RequestUseTargetHost( targetGo );
	}

	[Rpc.Host]
	private void RequestUseHost( Ray ray )
	{
		var pawn = Rpc.Caller.GetClient().PlayerPawn as PlayerPawn;
		if ( pawn == null ) return;

		var hits = pawn.GetUsables();
		var usable = hits.FirstOrDefault( x => x is not null );

		if ( usable != null && usable.CanUse( pawn ) is { } useResult )
		{
			if ( useResult.CanUse )
			{
				pawn.UpdateLastUsedObjectOwner( usable as Component );
				Game.ActiveScene.Dispatch( new PlayerUseEvent( usable ) );

				usable.OnUse( pawn );
			}
			else if ( !string.IsNullOrEmpty( useResult.Reason ) )
			{
				pawn.ShowUseDeniedOwner( useResult.Reason );
			}
		}
	}

	/// <summary>
	/// RPC host pour utiliser un IUse identifie par reference directe (pas par AimRay).
	/// Necessaire quand l'action vient d'un click radial : la souris ne pointe plus
	/// la cible originale au moment du click, donc on passe le GameObject explicite.
	/// </summary>
	[Rpc.Host]
	private void RequestUseTargetHost( GameObject targetGo )
	{
		Log.Info( $"[UseTarget][host] called targetGo={targetGo?.Name ?? "<null>"} valid={targetGo.IsValid()}" );
		if ( !targetGo.IsValid() ) { Log.Warning( "[UseTarget][host] abort : targetGo invalid" ); return; }
		var pawn = Rpc.Caller.GetClient().PlayerPawn as PlayerPawn;
		if ( pawn == null ) { Log.Warning( "[UseTarget][host] abort : pawn null" ); return; }

		// On essaie self+ancestors+descendants pour ne pas rater l'IUse selon ou il
		// est attache (root, enfant, etc.) sur le DroppedEquipment ou l'item vise.
		var usable = targetGo.Components.Get<IUse>( FindMode.EverythingInSelfAndAncestors )
			?? targetGo.Components.Get<IUse>( FindMode.EverythingInSelfAndDescendants );
		if ( usable == null ) { Log.Warning( $"[UseTarget][host] abort : aucun IUse trouve sur {targetGo.Name}" ); return; }

		Log.Info( $"[UseTarget][host] IUse trouve : {usable.GetType().Name}" );

		if ( usable.CanUse( pawn ) is { } useResult )
		{
			if ( useResult.CanUse )
			{
				pawn.UpdateLastUsedObjectOwner( usable as Component );
				Game.ActiveScene.Dispatch( new PlayerUseEvent( usable ) );
				Log.Info( $"[UseTarget][host] OnUse → {usable.GetType().Name}" );
				usable.OnUse( pawn );
			}
			else if ( !string.IsNullOrEmpty( useResult.Reason ) )
			{
				Log.Info( $"[UseTarget][host] denied : {useResult.Reason}" );
				pawn.ShowUseDeniedOwner( useResult.Reason );
			}
		}
	}

	[Rpc.Host]
	private void RequestActionHost( Ray ray )
	{
		var pawn = Rpc.Caller.GetClient().PlayerPawn as PlayerPawn;
		if ( pawn == null ) return;

		var actionnable = pawn.GetActionnables().FirstOrDefault( x => x is not null );
		if ( actionnable == null ) return;

		if ( actionnable.CanAction( pawn ) is { } result )
		{
			if ( result.CanUse )
			{
				Game.ActiveScene.Dispatch( new PlayerActionEvent( actionnable ) );
				actionnable.OnAction( pawn );
			}
			else if ( !string.IsNullOrEmpty( result.Reason ) )
			{
				pawn.ShowUseDeniedOwner( result.Reason );
			}
		}
	}

	[Rpc.Host]
	private void RequestPickupHost( WorldItem worldItem )
	{
		if ( worldItem == null || !worldItem.IsValid() )
		{
			if ( PickupDebug ) Log.Warning( $"[Pickup] WorldItem est null ou invalide" );
			return;
		}

		var client = Rpc.Caller.GetClient();
		var pawn = client?.PlayerPawn as PlayerPawn;
		if ( pawn == null )
		{
			if ( PickupDebug ) Log.Warning( $"[Pickup] Pawn est null" );
			return;
		}

		var item = worldItem.Item;
		if ( item == null )
		{
			if ( PickupDebug ) Log.Warning( $"[Pickup] Item est null sur le WorldItem" );
			return;
		}

		var inventory = pawn.InventoryContainer;
		if ( inventory == null )
		{
			if ( PickupDebug ) Log.Warning( $"[Pickup] InventoryContainer introuvable sur le pawn" );
			return;
		}

		var meta = item.Metadata;
		var quantity = item.Quantity;

		if ( PickupDebug ) Log.Info( $"[Pickup] Ajout de {quantity}x {meta.Name} dans l'inventaire" );

		InventoryContainer.Add( inventory, meta.ResourceName, quantity );
		worldItem.GameObject.Destroy();

		var notifMessage = quantity > 1 ? $"+{quantity}x {meta.Name}" : $"+{meta.Name}";
		client?.Notify( NotificationType.Success, notifMessage );
	}

	[Rpc.Owner]
	private void UpdateLastUsedObjectOwner( Component component )
	{
		if ( component.IsValid() )
			LastUsedObject = component.GameObject;
	}

	[Rpc.Owner]
	private void ShowUseDeniedOwner( string reason )
	{
		Client.Local.Notify( NotificationType.Error, reason );
	}

}
