using OpenFramework;
using OpenFramework.Extension;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using OpenFramework.Systems.Pawn;
using OpenFramework.UI.World.Storage;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Tools;

/// <summary>
/// Placé sur le PlayerPawn — gère le placement et le déplacement de props/meubles.
/// Inspiré du système InteriorDesigner de Braxen (ghost, snap grille, rotation molette).
/// </summary>
public sealed class PropPlacer : Component
{
	[Property] public float MaxPlaceDistance { get; set; } = 250f;

	/// <summary>Sensibilite de rotation R+souris (degres par delta look). Calque le GrabSystem.</summary>
	[Property] public float RotationSensitivity { get; set; } = 1.5f;

	// Valeurs de snap disponibles (molette + Shift pour cycler)
	private static readonly float[] PositionSnaps = { 0f, 4f, 8f, 16f, 32f };
	private static readonly float[] AngleSnaps = { 5f, 15f, 45f, 90f };

	public bool IsPlacing => _placingItem != null && !_movingObject.IsValid();
	public bool IsMoving => _movingObject.IsValid();
	public bool IsActive => _placingItem != null;

	private ItemMetadata _placingItem;
	private GameObject _movingObject;
	private readonly List<ModelRenderer> _movingObjectRenderers = new();
	private GameObject _ghostObject;
	private BBox _ghostBounds;
	private readonly List<ModelRenderer> _ghostRenderers = new();
	private Rotation _currentRotation = Rotation.Identity;
	private float _snapPosition = 0f;
	private float _snapAngle = 45f;

	private PlayerPawn Player => Components.Get<PlayerPawn>( FindMode.EverythingInSelfAndAncestors );

	/// <summary>
	/// Démarre le mode placement pour un item de meuble depuis l'inventaire.
	/// À appeler côté client (owner uniquement).
	/// </summary>
	public void StartPlacing( ItemMetadata item )
	{
		if ( IsProxy ) return;
		if ( item?.WorldObjectPrefab == null ) return;

		CancelPlacing();
		_placingItem = item;
		_currentRotation = Rotation.FromYaw( 0 );
		SpawnGhost();
	}

	/// <summary>
	/// Démarre le mode déplacement pour un objet monde déjà posé.
	/// À appeler côté client (owner uniquement).
	/// </summary>
	public void StartMoving( GameObject worldObject )
	{
		if ( IsProxy ) return;
		if ( !worldObject.IsValid() ) return;

		var inventoryItem = worldObject.Components.Get<InventoryItem>( FindMode.EverythingInSelfAndDescendants );
		var meta = inventoryItem?.Metadata;
		Log.Info( $"[PropPlacer:StartMoving] worldObject={worldObject.Name} inventoryItem={( inventoryItem != null ? "trouvé" : "ABSENT" )} meta={meta?.ResourceName ?? "null"} worldPrefab={meta?.WorldObjectPrefab != null}" );
		if ( meta?.WorldObjectPrefab == null ) return;

		CancelPlacing();
		_movingObject = worldObject;
		_placingItem = meta;
		// On ne conserve que le yaw : si le meuble est tombe sur le cote
		// (pitch/roll non nuls), on repart d'une orientation droite. Le joueur
		// peut ensuite ajuster en 3D avec R+souris.
		var yaw = worldObject.WorldRotation.Angles().yaw;
		_currentRotation = Rotation.FromYaw( yaw );

		// Masquer l'objet réel localement le temps du déplacement
		_movingObjectRenderers.Clear();
		foreach ( var mr in worldObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( mr.Enabled )
			{
				_movingObjectRenderers.Add( mr );
				mr.Enabled = false;
			}
		}

		SpawnGhost();
	}

	/// <summary>
	/// Annule le mode actif (placement ou déplacement) et restaure l'état.
	/// </summary>
	public void CancelPlacing()
	{
		_placingItem = null;
		RestoreMovingObject();
		_movingObject = null;
		DestroyGhost();

		// Securite : si on annule pendant que R est maintenu, on relache
		// le verrou camera pour ne pas figer le viseur du joueur.
		var player = Player;
		if ( player.IsValid() && player.IsRotatingObject )
			player.IsRotatingObject = false;
	}

	private void RestoreMovingObject()
	{
		foreach ( var mr in _movingObjectRenderers )
			if ( mr.IsValid() ) mr.Enabled = true;
		_movingObjectRenderers.Clear();
	}

	private void SpawnGhost()
	{
		DestroyGhost();
		if ( _placingItem?.WorldObjectPrefab == null ) return;

		var prefab = GameObject.GetPrefab( _placingItem.WorldObjectPrefab.ResourcePath );
		if ( prefab == null ) return;

		_ghostObject = prefab.Clone( WorldPosition, Rotation.Identity );
		_ghostObject.NetworkMode = NetworkMode.Never;
		_ghostObject.Tags.Add( "ghost" );

		// Calculer les bounds AVANT de désactiver la physique
		_ghostBounds = CalculateBounds( _ghostObject );

		// Désactiver tout composant non-visuel pour éviter interactions physiques
		foreach ( var rb in _ghostObject.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) )
			rb.Enabled = false;
		foreach ( var col in _ghostObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
			col.Enabled = false;
		foreach ( var fv in _ghostObject.Components.GetAll<FurnitureVisual>( FindMode.EverythingInSelfAndDescendants ) )
			fv.Enabled = false;
		foreach ( var ii in _ghostObject.Components.GetAll<InventoryItem>( FindMode.EverythingInSelfAndDescendants ) )
			ii.Enabled = false;

		_ghostRenderers.Clear();
		foreach ( var mr in _ghostObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
			_ghostRenderers.Add( mr );
	}

	private static BBox CalculateBounds( GameObject go )
	{
		var col = go.Components.Get<Collider>( FindMode.EnabledInSelfAndDescendants );
		if ( col.IsValid() && col.LocalBounds != default )
			return col.LocalBounds;

		var mr = go.Components.Get<ModelRenderer>( FindMode.EnabledInSelfAndDescendants );
		if ( mr.IsValid() && mr.LocalBounds != default )
			return mr.LocalBounds;

		// Fallback : boite 32×32×32 centrée à mi-hauteur
		return new BBox( new Vector3( -16, -16, 0 ), new Vector3( 16, 16, 32 ) );
	}

	private void DestroyGhost()
	{
		if ( _ghostObject.IsValid() )
			_ghostObject.Destroy();

		_ghostObject = null;
		_ghostRenderers.Clear();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !IsActive ) return;
		OnUpdatePlacing();
	}

	private void OnUpdatePlacing()
	{
		if ( !_ghostObject.IsValid() )
		{
			CancelPlacing();
			return;
		}

		var player = Player;
		if ( !player.IsValid() ) { CancelPlacing(); return; }

		HandleRotationInput();

		var trace = Scene.Trace.Ray( player.AimRay, MaxPlaceDistance )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.IgnoreGameObjectHierarchy( _ghostObject )
			.WithoutTags( "ghost", "player" );

		// Ignorer l'objet en cours de déplacement pour que le raycast ne le touche pas
		if ( _movingObject.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( _movingObject );

		var tr = trace.Run();

		if ( tr.Hit )
		{
			var position = ComputePlacementPosition( tr.EndPosition, tr.Normal );
			_ghostObject.WorldPosition = position;
			_ghostObject.WorldRotation = _currentRotation;

			SetGhostTint( Color.Green.WithAlpha( 0.6f ) );
			DrawSnapInfo( position );

			if ( Input.Pressed( "attack1" ) )
			{
				if ( _movingObject.IsValid() )
				{
					// Mode déplacement : téléporter l'objet réel via RPC
					RpcMoveProp( _movingObject, position, _currentRotation );
				}
				else
				{
					// Mode placement : spawner depuis l'inventaire.
					RpcPlaceProp( position, _currentRotation, _placingItem.ResourceName );
				}

				// CancelPlacing centralise le cleanup ghost/refs ET relache le
				// verrou IsRotatingObject sur le pawn. Sans ce reset, valider un
				// move avec R encore enfonce sortait du mode placement avant que
				// HandleRotationInput n'ait pu remettre le flag a false : la
				// camera restait figee jusqu'a la prochaine action qui clear le flag.
				CancelPlacing();
				return;
			}
		}
		else
		{
			// Pas de surface : ghost flotte devant le joueur
			_ghostObject.WorldPosition = player.AimRay.Position + player.AimRay.Forward * MaxPlaceDistance;
			_ghostObject.WorldRotation = _currentRotation;
			SetGhostTint( Color.Red.WithAlpha( 0.4f ) );
		}

		if ( Input.Pressed( "attack2" ) )
			CancelPlacing();
	}

	private void HandleRotationInput()
	{
		// R + souris : rotation libre 3D (calque le GrabSystem). Permet de
		// redresser un meuble tombe sur le cote, ou d'orienter finement sans
		// passer par les snaps. On bascule IsRotatingObject sur le pawn pour
		// que PlayerPawn.OnUpdateMovement n'applique pas Input.AnalogLook a la
		// camera (ToolsGunComponent fait pareil). On clear AnalogLook en plus
		// par defense, au cas ou un autre composant le lirait avant le pawn.
		var player = Player;
		if ( Input.Keyboard.Down( "R" ) )
		{
			if ( player.IsValid() ) player.IsRotatingObject = true;

			var look = Input.AnalogLook;
			_currentRotation = Rotation.FromAxis( Vector3.Up, -look.yaw * RotationSensitivity )
				* Rotation.FromAxis( Vector3.Right, look.pitch * RotationSensitivity )
				* _currentRotation;
			Input.AnalogLook = Angles.Zero;
			return;
		}

		// R relache : on rend le controle camera.
		if ( player.IsValid() && player.IsRotatingObject )
			player.IsRotatingObject = false;

		if ( Input.MouseWheel.y == 0 ) return;

		int dir = Input.MouseWheel.y > 0 ? -1 : 1;

		if ( Input.Down( "Run" ) )
		{
			// Shift + molette → cycle du snap de position
			var idx = Array.IndexOf( PositionSnaps, _snapPosition );
			idx = ( idx + dir + PositionSnaps.Length ) % PositionSnaps.Length;
			_snapPosition = PositionSnaps[idx];
		}
		else
		{
			// Molette seule → rotation yaw snappee
			var angles = _currentRotation.Angles();
			angles.yaw += Input.MouseWheel.y * _snapAngle;
			if ( _snapAngle > 0f )
				angles.yaw = MathF.Round( angles.yaw / _snapAngle ) * _snapAngle;
			_currentRotation = angles.ToRotation();
		}
	}

	private Vector3 ComputePlacementPosition( Vector3 hitPos, Vector3 normal )
	{
		// Soulève le ghost pour qu'il repose sur la surface (technique Braxen)
		var rotatedBounds = _ghostBounds.Rotate( _currentRotation );
		var minProj = rotatedBounds.Corners.Min( c => Vector3.Dot( c, normal ) );
		var position = hitPos - normal * minProj;

		if ( _snapPosition > 0f )
			position = position.SnapToGrid( _snapPosition, true, true, true );

		return position;
	}

	private void SetGhostTint( Color color )
	{
		foreach ( var mr in _ghostRenderers )
			mr.Tint = color;
	}

	private void DrawSnapInfo( Vector3 position )
	{
		var ang = _currentRotation.Angles();
		var text = $"Rotation: yaw {ang.yaw:0}° pitch {ang.pitch:0}° roll {ang.roll:0}° | Snap pos: {( _snapPosition == 0 ? "libre" : $"{_snapPosition}u" )} | Snap angle: {_snapAngle}° | [Molette] yaw | [Shift+Molette] snap pos | [R+Souris] rotation libre 3D";
		DebugOverlay.ScreenText( new Vector2( 50, 80 ), text, 12f, TextFlag.Left, Color.White, 0f );
	}

	// ─────────────────────────────────────────────────────────────────
	//  RPC HOST — Validation et spawn/déplacement
	// ─────────────────────────────────────────────────────────────────

	[Rpc.Host]
	private void RpcPlaceProp( Vector3 position, Rotation rotation, string itemResourceName )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		var pawn = caller.PlayerPawn as PlayerPawn;
		if ( pawn == null ) return;

		if ( position.IsNaN || position.IsInfinity )
		{
			caller.Notify( NotificationType.Error, "Position invalide." );
			return;
		}

		if ( Vector3.DistanceBetween( pawn.WorldPosition, position ) > MaxPlaceDistance + 50f )
		{
			caller.Notify( NotificationType.Error, "Trop loin pour poser l'objet." );
			return;
		}

		var container = pawn.InventoryContainer;
		if ( container == null )
		{
			caller.Notify( NotificationType.Error, "Inventaire introuvable." );
			return;
		}

		if ( !InventoryContainer.Has( container, itemResourceName, 1 ) )
		{
			caller.Notify( NotificationType.Error, "Objet introuvable dans l'inventaire." );
			return;
		}

		var meta = ItemMetadata.All.FirstOrDefault( m => m.ResourceName == itemResourceName );
		if ( meta?.WorldObjectPrefab == null )
		{
			caller.Notify( NotificationType.Error, "Cet objet ne peut pas être posé." );
			return;
		}

		// Anti-duplication : retirer AVANT de spawner
		InventoryContainer.Remove( container, itemResourceName, 1 );

		var spawned = Spawnable.CreateWithReturnFromHost(
			meta.WorldObjectPrefab.ResourcePath,
			new Transform( position, rotation )
		);

		if ( spawned == null )
		{
			// Spawn échoué : on remet l'item
			InventoryContainer.Add( container, itemResourceName, 1 );
			caller.Notify( NotificationType.Error, "Échec du placement." );
			return;
		}

		if ( caller.Data != null )
			caller.Data.CurrentProps++;

		// Ajouter FurnitureVisual si le prefab n'en a pas (nécessaire pour lock/unlock)
		bool fvWasCreated = false;
		var fv = spawned.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndDescendants );
		if ( fv == null )
		{
			fv = spawned.Components.Create<FurnitureVisual>();
			fv.Rb = spawned.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
			fv.Renderer = spawned.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
			fvWasCreated = true;
		}

		// Ajouter InventoryItem si absent — nécessaire pour Ramasser et Déplacer
		var worldItem = spawned.Components.Get<InventoryItem>( FindMode.EverythingInSelfAndDescendants );
		if ( worldItem == null )
		{
			worldItem = spawned.Components.Create<InventoryItem>();
			worldItem.Metadata = meta;
			Log.Info( $"[PropPlacer] InventoryItem créé dynamiquement pour '{meta.ResourceName}'" );
		}

		// IsLocked = true seulement sur les instances fraîchement créées (pas issues du prefab)
		// pour éviter de contaminer le cache in-memory du prefab (ce qui gèlerait les drops suivants)
		if ( fvWasCreated )
			fv.IsLocked = true;

		// Verrou proprietaire + traque du placeur posés AVANT NetworkSpawn pour
		// que la valeur soit incluse dans le snapshot initial réseau et persiste
		// host-side (sinon Scene.GetAllComponents<FurnitureVisual>() lit 0 dans
		// PlacedPropsCleanup et le prop n'est pas nettoye apres deconnexion).
		fv.OwnerLocked = true;
		fv.PlacedBySteamId = caller.SteamId;

		spawned.NetworkSpawn();

		// UpdateFreeze après NetworkSpawn gère le Rigidbody verrouillé sur tous les clients
		fv.UpdateFreeze( true );

		// Si c'est un coffre code (RequiresCode=true), on attribue l'owner
		// au placeur. Le coffre demarre IsLocked=true sans code : le
		// placeur devra definir son code lui-meme via le QuickMenu du
		// coffre quand il le souhaite (pas d'ouverture automatique du
		// panel a la pose, qui captait des frappes avant le focus de la
		// TextEntry et faisait sauter des chiffres). Les storages
		// classiques (cabinet, tiroir...) ne demandent pas de code et
		// restent accessibles librement.
		var storage = spawned.Components.Get<StorageComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( storage != null && storage.RequiresCode )
		{
			storage.AssignOwnerFromHost( caller );
		}

		Log.Info( $"[PropPlacer] {pawn.DisplayName} a posé {meta.Name} à {position} (verrouillé)" );
	}

	[Rpc.Host]
	private void RpcMoveProp( GameObject worldObject, Vector3 position, Rotation rotation )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		var pawn = caller.PlayerPawn as PlayerPawn;
		if ( pawn == null ) return;

		if ( !worldObject.IsValid() )
		{
			caller.Notify( NotificationType.Error, "Objet introuvable." );
			return;
		}

		if ( position.IsNaN || position.IsInfinity )
		{
			caller.Notify( NotificationType.Error, "Position invalide." );
			return;
		}

		if ( Vector3.DistanceBetween( pawn.WorldPosition, position ) > MaxPlaceDistance + 50f )
		{
			caller.Notify( NotificationType.Error, "Trop loin pour déplacer l'objet." );
			return;
		}

		// Verrou proprietaire : un autre joueur ne peut pas deplacer un meuble
		// tant que son proprietaire ne l'a pas deverrouille.
		var ownerFv = worldObject.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndDescendants );
		if ( ownerFv != null && !ownerFv.CanBeManipulatedBy( caller ) )
		{
			caller.Notify( NotificationType.Error, "Cet objet appartient a un autre joueur." );
			return;
		}

		// Un coffre a code verrouille ne peut pas etre deplace. Il faut
		// d'abord entrer son code pour le deverrouiller. Les storages
		// classiques (sans code) ne sont pas concernes.
		var lockedStorage = worldObject.Components.Get<StorageComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( lockedStorage != null && lockedStorage.RequiresCode && lockedStorage.IsLocked )
		{
			caller.Notify( NotificationType.Error, "Coffre verrouille : deverrouillez-le d'abord." );
			return;
		}

		worldObject.WorldPosition = position;
		worldObject.WorldRotation = rotation;

		// Réinitialise la vélocité si l'objet a de la physique
		var rb = worldObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
		if ( rb.IsValid() )
		{
			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
		}

		// Replacer un objet = transfert de propriete au nouveau joueur.
		// PlacedPropsCleanup l'utilise pour savoir qui nettoyer a la deco.
		var movedFv = worldObject.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndDescendants );
		if ( movedFv != null )
			movedFv.PlacedBySteamId = caller.SteamId;

		Log.Info( $"[PropPlacer] {pawn.DisplayName} a déplacé un meuble vers {position}" );
	}

	protected override void OnDestroy()
	{
		DestroyGhost();
	}
}
