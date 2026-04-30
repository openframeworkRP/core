using Sandbox.Events;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using OpenFramework.Utility;
using static Facepunch.NotificationSystem;

namespace OpenFramework.World;

public sealed class Door : Component, IUse, IGameEventHandler<PlayerDisconnectedEvent>, IGameEventHandler<PlayerConnectedEvent>, IDoorAnimated, IDoorGraceable
{
	[Property] public Curve AnimationCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 1f, 1.0f ) );

	[Property, Group( "Sound" )] public SoundEvent OpenSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent OpenFinishedSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent CloseSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent CloseFinishedSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent LockSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent UnlockSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent KnockSound { get; set; }

	[Property] public GameObject Pivot { get; set; }
	[Property, Range( 0.0f, 90.0f )] public float TargetAngle { get; set; } = 90.0f;
	[Property] public float OpenTime { get; set; } = 0.5f;
	[Property] public bool OpenAwayFromPlayer { get; set; } = true;

	[Sync( SyncFlags.FromHost )] public Client Owner { get; set; } = null;
	[Sync( SyncFlags.FromHost )] public List<Client> CoOwners { get; private set; } = new();

	/// <summary>
	/// SteamId du owner déconnecté — conservé pendant le grace period.
	/// </summary>
	public ulong PendingOwnerSteamId { get; private set; } = 0;
	public float PendingOwnerExpireAt { get; private set; } = 0f;

	/// <summary>Durée du grace period en secondes (5 minutes).</summary>
	public const float OwnerGracePeriod = 300f;

	/// <summary>
	/// Purchase category
	/// </summary>
	[Property, FeatureEnabled( "Purchase" )] public bool CanBePurchased { get; set; } = true;
	[Property, Sync( SyncFlags.FromHost ), Feature( "Purchase" )] public int Price { get; set; } = 500;

	/// <summary>
	/// Job Access by JobList
	/// </summary>
	[Property, FeatureEnabled( "Job" )] public bool CanBeAllowedJob { get; set; } = false;
	[Property, Sync( SyncFlags.FromHost ), Feature( "Job" )] public string JobName { get; set; }

	/// <summary>
	/// Portes enfants qui héritent de cette porte maître
	/// </summary>
	[Property, Sync( SyncFlags.FromHost ), Feature( "Door List" )] public List<GameObject> DoorsList { get; set; }

	public enum DoorState { Open, Opening, Closing, Closed }

	Transform StartTransform { get; set; }
	Vector3 PivotPosition { get; set; }
	bool ReverseDirection { get; set; }
	[Sync( SyncFlags.FromHost )] public TimeSince LastUse { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsLocked { get; set; } = false;
	[Sync( SyncFlags.FromHost ), Change( nameof( OnStateChanged ) )] public DoorState State { get; set; } = DoorState.Closed;
	private DoorState DefaultState { get; set; } = DoorState.Closed;

	GrabAction IUse.GetGrabAction() => State == DoorState.Open ? GrabAction.SweepRight : GrabAction.SweepLeft;

	protected override void OnStart()
	{
		StartTransform = Transform.Local;
		PivotPosition = Pivot is not null ? Pivot.WorldPosition : StartTransform.Position;
		DefaultState = State;
		SyncChildrenOwnership();

		// Auto-attach DistanceNetworkVisibility si pas deja present.
		// Ne fait rien si AlwaysTransmit n'est pas decoche dans le prefab Network.
		if ( !Components.Get<DistanceNetworkVisibility>().IsValid() )
			Components.Create<DistanceNetworkVisibility>();
	}

	protected override void OnEnabled()
	{
		// Couvre les cas ou la porte est spawn alors qu'elle est deja en animation
		// (snapshot recu sur un client en cours d'animation), ou avec une grace en cours
		// (host qui restore une scene).
		if ( State == DoorState.Opening || State == DoorState.Closing )
			DoorAnimationSystem.RegisterAnimator( this );
		if ( PendingOwnerSteamId != 0 )
			DoorAnimationSystem.RegisterGraceable( this );
	}

	protected override void OnDisabled()
	{
		DoorAnimationSystem.UnregisterAnimator( this );
		DoorAnimationSystem.UnregisterGraceable( this );
	}

	// Sync change callback : appele localement ET cote client quand le State sync arrive.
	// On (de)inscrit au DoorAnimationSystem en consequence — ainsi l'animation tourne
	// sur toutes les machines qui voient la porte, mais seulement pendant l'animation.
	private void OnStateChanged( DoorState oldVal, DoorState newVal )
	{
		if ( newVal == DoorState.Opening || newVal == DoorState.Closing )
			DoorAnimationSystem.RegisterAnimator( this );
		else
			DoorAnimationSystem.UnregisterAnimator( this );
	}

	// ─────────────────────────────────────────────
	//  ANIMATION TICK — appele par DoorAnimationSystem a 50Hz uniquement
	//  pendant que la porte est dans l'etat Opening ou Closing.
	// ─────────────────────────────────────────────

	void IDoorAnimated.TickDoorAnimation()
	{
		if ( State != DoorState.Opening && State != DoorState.Closing )
		{
			// Safety net : si on arrive ici sans etat d'animation, le change callback
			// nous a manques (cas exotique). On se desinscrit nous-memes.
			DoorAnimationSystem.UnregisterAnimator( this );
			return;
		}

		var time = LastUse.Relative.Remap( 0.0f, OpenTime, 0.0f, 1.0f );
		var curve = AnimationCurve.Evaluate( time );
		if ( State == DoorState.Closing ) curve = 1.0f - curve;

		var targetAngle = TargetAngle;
		if ( ReverseDirection ) targetAngle *= -1.0f;

		Transform.Local = StartTransform.RotateAround( PivotPosition, Rotation.FromYaw( curve * targetAngle ) );

		if ( time < 1f ) return;

		// Fin d'animation : transition Opening->Open / Closing->Closed.
		// Le setter Change() declenchera OnStateChanged qui nous desinscrira.
		State = State == DoorState.Opening ? DoorState.Open : DoorState.Closed;

		if ( Networking.IsHost )
		{
			if ( State == DoorState.Open && OpenFinishedSound is not null ) PlaySound( OpenFinishedSound );
			if ( State == DoorState.Closed && CloseFinishedSound is not null ) PlaySound( CloseFinishedSound );
		}
	}

	// ─────────────────────────────────────────────
	//  GRACE PERIOD TICK — appele par DoorAnimationSystem a 1Hz uniquement
	//  pendant qu'un PendingOwner est en attente.
	// ─────────────────────────────────────────────

	void IDoorGraceable.TickGraceCheck()
	{
		// Host-only : seul le host pilote l'expiration (l'etat est sync ensuite)
		if ( !Networking.IsHost ) return;
		if ( PendingOwnerSteamId == 0 )
		{
			DoorAnimationSystem.UnregisterGraceable( this );
			return;
		}
		if ( Time.Now < PendingOwnerExpireAt ) return;

		Log.Info( $"[Door] Grace period expiré pour {PendingOwnerSteamId} sur '{GameObject.Name}'" );
		ClearOwnership();
	}

	// ─────────────────────────────────────────────
	//  DÉCONNEXION
	// ─────────────────────────────────────────────

	public void OnGameEvent( PlayerDisconnectedEvent eventArgs )
	{
		if ( !Networking.IsHost ) return;
		if ( eventArgs.Player != Owner ) return;

		// Démarre le grace period — la porte reste au joueur 5 minutes
		PendingOwnerSteamId = Owner.SteamId;
		PendingOwnerExpireAt = Time.Now + OwnerGracePeriod;

		// Inscrit au DoorAnimationSystem pour que le check tourne a 1Hz pendant la grace
		DoorAnimationSystem.RegisterGraceable( this );

		// Notifie les co-owners
		foreach ( var coOwner in CoOwners )
			coOwner.Notify( NotificationType.Warning, $"Le propriétaire s'est déconnecté. La porte expire dans {OwnerGracePeriod / 60:0} minutes." );

		// On met Owner à null temporairement (plus en ligne) mais PendingOwnerSteamId garde la trace
		Owner = null;

		Log.Info( $"[Door] Owner {PendingOwnerSteamId} déconnecté, grace period de {OwnerGracePeriod}s démarré sur '{GameObject.Name}'" );
	}

	// ─────────────────────────────────────────────
	//  RECONNEXION
	// ─────────────────────────────────────────────

	public void OnGameEvent( PlayerConnectedEvent eventArgs )
	{
		if ( !Networking.IsHost ) return;
		if ( PendingOwnerSteamId == 0 ) return;

		var client = eventArgs.Client;
		if ( client?.SteamId != PendingOwnerSteamId ) return;

		// Le owner reconnecte dans le grace period → on lui redonne la porte
		Owner = client;
		PendingOwnerSteamId = 0;
		PendingOwnerExpireAt = 0f;

		// Plus de grace en attente : on se desinscrit du DoorAnimationSystem
		DoorAnimationSystem.UnregisterGraceable( this );

		client.Notify( NotificationType.Success, "Vous avez récupéré vos portes." );
		SyncChildrenOwnership();

		Log.Info( $"[Door] {client.DisplayName} a récupéré sa porte '{GameObject.Name}' après reconnexion." );
	}

	// ─────────────────────────────────────────────
	//  CLEAR OWNERSHIP (grace period expiré)
	// ─────────────────────────────────────────────

	private void ClearOwnership()
	{
		Owner = null;
		PendingOwnerSteamId = 0;
		PendingOwnerExpireAt = 0f;
		CoOwners = new List<Client>();
		IsLocked = false;
		State = DefaultState;
		LastUse = 0.0f;

		// Plus de grace en attente
		DoorAnimationSystem.UnregisterGraceable( this );

		if ( CloseSound is not null ) PlaySound( CloseSound );
		SyncChildrenOwnership();
	}

	// ─────────────────────────────────────────────
	//  RESTE DU CODE (inchangé)
	// ─────────────────────────────────────────────

	public UseResult CanUse( PlayerPawn player ) => State is DoorState.Open or DoorState.Closed;

	private void PlaySound( SoundEvent resource ) => PlaySoundRpc( resource.ResourcePath );

	[Rpc.Broadcast]
	private void PlaySoundRpc( string resourcePath )
	{
		var resource = ResourceLibrary.Get<SoundEvent>( resourcePath );
		if ( resource == null ) return;
		Sound.Play( resource, WorldPosition );
	}

	private void SyncChildrenOwnership()
	{
		foreach ( var go in DoorsList ?? new() )
		{
			if ( go == null ) continue;
			var childDoor = go.Components.Get<Door>();
			if ( childDoor == null || childDoor == this ) continue;

			childDoor.Owner = this.Owner;
			childDoor.CoOwners = new List<Client>( this.CoOwners );
			childDoor.IsLocked = this.IsLocked;
			childDoor.CanBePurchased = false;
		}
	}

	[Rpc.Host]
	public static void TryToBuy( Door door )
	{
		var caller = Rpc.Caller.GetClient();

		if ( door.CanBeAllowedJob )
		{ caller.Notify( NotificationType.Error, "Cette porte appartient à un bâtiment public." ); return; }

		if ( !door.CanBePurchased )
		{ caller.Notify( NotificationType.Error, "Cette porte ne peut pas être achetée." ); return; }

		if ( door.Owner == caller )
		{ caller.Notify( NotificationType.Error, "Cette porte vous appartient déjà." ); return; }

		if ( door.Owner != null || door.PendingOwnerSteamId != 0 )
		{ caller.Notify( NotificationType.Error, "Cette porte est déjà réservée." ); return; }

		if ( MoneySystem.Get( caller ) < door.Price )
		{ caller.Notify( NotificationType.Error, "Vous n'avez pas assez d'argent." ); return; }

		MoneySystem.Remove( caller, door.Price );
		door.Owner = caller;
		door.PendingOwnerSteamId = 0;
		door.CoOwners = new List<Client>();
		door.IsLocked = false;
		door.State = DoorState.Closed;
		door.LastUse = 0.0f;

		caller.Notify( NotificationType.Success, $"Vous avez acheté cette porte pour {door.Price}$." );
		ObjectEffects.BuyEffect( caller, door.GameObject );
		door.SyncChildrenOwnership();

		// Donne une clé physique au joueur avec les GUIDs de cette porte (+ enfants)
		var container = caller.PlayerPawn?.GameObject.Components
			.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		if ( container != null )
		{
			var guids = new List<string> { door.GameObject.Id.ToString() };
			foreach ( var childGo in door.DoorsList ?? new() )
			{
				if ( childGo != null ) guids.Add( childGo.Id.ToString() );
			}
			var attrs = new Dictionary<string, string>
			{
				[InventoryItem.AttrDoorGuids] = string.Join( ",", guids )
			};

			InventoryContainer.Add( container, "doorkey", 1, attrs );
			Log.Info( $"[Door] Clé donnée à {caller.DisplayName} pour '{door.GameObject.Name}' (GUIDs: {string.Join( ",", guids )})" );
		}
	}

	[Rpc.Host]
	public static void TryToSell( Door door )
	{
		var caller = Rpc.Caller.GetClient();
		if ( door.Owner != caller )
		{ caller.Notify( NotificationType.Error, "Vous ne possédez pas cette porte." ); return; }

		door.Owner = null;
		door.CoOwners = new List<Client>();
		door.IsLocked = false;
		door.PendingOwnerSteamId = 0;

		MoneySystem.Add( caller, door.Price / 2 );
		caller.Notify( NotificationType.Success, $"Vous avez vendu cette porte pour {door.Price / 2}$." );
		door.SyncChildrenOwnership();
	}

	[Rpc.Host]
	public static void ShareDoor( Door door, Client target )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;

		if ( door.Owner == null ) { caller.Notify( NotificationType.Error, "Cette porte n'appartient à personne." ); return; }
		if ( door.Owner != caller ) { caller.Notify( NotificationType.Error, "Tu n'es pas le propriétaire." ); return; }
		if ( !target.IsValid ) { caller.Notify( NotificationType.Error, "Joueur introuvable." ); return; }
		if ( target == door.Owner ) { caller.Notify( NotificationType.Error, "Vous ne pouvez pas vous partager la clé à vous-même." ); return; }
		if ( door.CoOwners.Contains( target ) ) { caller.Notify( NotificationType.Warning, "Ce joueur a déjà les clés." ); return; }

		door.CoOwners = new List<Client>( door.CoOwners ) { target };
		caller.Notify( NotificationType.Success, $"Tu as partagé la porte avec {target.DisplayName}." );
		target.Notify( NotificationType.Success, $"Tu as reçu les clés de {caller.DisplayName}." );
		door.SyncChildrenOwnership();
	}

	[Rpc.Host]
	public static void RemoveShareDoor( Door door, Client target )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;
		if ( door.Owner == null ) { caller.Notify( NotificationType.Error, "Cette porte n'appartient à personne." ); return; }
		if ( !target.IsValid ) { caller.Notify( NotificationType.Error, "Joueur introuvable." ); return; }
		if ( target == door.Owner ) { caller.Notify( NotificationType.Error, "Le propriétaire doit revendre pour se retirer." ); return; }

		var isCallerOwner = door.Owner == caller;
		var isSelfUnshare = target == caller;

		if ( !(isCallerOwner || isSelfUnshare) ) { caller.Notify( NotificationType.Error, "Tu n'as pas la permission." ); return; }
		if ( !door.CoOwners.Contains( target ) ) { caller.Notify( NotificationType.Warning, "Ce joueur n'a pas de clé." ); return; }

		door.CoOwners = door.CoOwners.Where( c => c != target ).ToList();

		if ( isSelfUnshare && !isCallerOwner )
			caller.Notify( NotificationType.Success, "Tu as rendu la clé." );
		else
			caller.Notify( NotificationType.Success, $"Tu as retiré l'accès à {target.DisplayName}." );

		target.Notify( NotificationType.Warning, $"Ton accès à la porte de {door.Owner?.DisplayName} a été retiré." );
		door.SyncChildrenOwnership();
	}

	[Rpc.Host]
	public static void Lock( Door door )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;

		string playerJob = caller.Data.Job ?? "";

		if ( door.CanBeAllowedJob && !string.IsNullOrEmpty( door.JobName ) )
		{
			bool hasJobAccess = string.Equals( door.JobName, playerJob, StringComparison.OrdinalIgnoreCase );
			bool isOwner = door.Owner == caller || door.CoOwners.Contains( caller );

			if ( !hasJobAccess && !isOwner )
			{ caller.Notify( NotificationType.Error, "Vous n'avez pas le métier requis." ); return; }
		}

		if ( door.IsLocked ) { caller.Notify( NotificationType.Info, "Cette porte est déjà verrouillée." ); return; }

		door.IsLocked = true;
		door.SyncChildrenOwnership();
		if ( door.LockSound is not null ) door.PlaySound( door.LockSound );
		caller.Notify( NotificationType.Info, "Vous avez verrouillé la porte." );
	}

	[Rpc.Host]
	public static void Unlock( Door door )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;
		if ( !door.IsLocked ) { caller.Notify( NotificationType.Info, "Cette porte est déjà déverrouillée." ); return; }

		door.IsLocked = false;
		door.SyncChildrenOwnership();
		if ( door.UnlockSound is not null ) door.PlaySound( door.UnlockSound );
		caller.Notify( NotificationType.Info, "Vous avez déverrouillé la porte." );
	}

	[Rpc.Host]
	public static void Toggle( Door door )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller?.PlayerPawn is not PlayerPawn pawn ) return;
		if ( door.State is not (DoorState.Open or DoorState.Closed) ) return;
		door.OnUse( pawn );
	}

	[Rpc.Host]
	public static void GiveDoorKey( Door door, Client target )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;

		if ( !caller.IsAdmin && door.Owner != caller )
		{ caller.Notify( NotificationType.Error, "Vous n'avez pas la permission." ); return; }

		if ( !target.IsValid() || target.PlayerPawn == null )
		{ caller.Notify( NotificationType.Error, "Joueur introuvable." ); return; }

		var container = target.PlayerPawn.GameObject.Components
			.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		if ( container == null )
		{ caller.Notify( NotificationType.Error, $"{target.DisplayName} n'a pas d'inventaire." ); return; }

		var guids = new List<string> { door.GameObject.Id.ToString() };
		foreach ( var childGo in door.DoorsList ?? new() )
		{
			if ( childGo != null ) guids.Add( childGo.Id.ToString() );
		}

		InventoryContainer.Add( container, "doorkey", 1, new Dictionary<string, string>
		{
			[InventoryItem.AttrDoorGuids] = string.Join( ",", guids )
		} );

		caller.Notify( NotificationType.Success, $"Clé de '{door.GameObject.Name}' donnée à {target.DisplayName}." );
		target.Notify( NotificationType.Info, $"Vous avez reçu une clé de la porte de {caller.DisplayName}." );
	}

	[Rpc.Host]
	public static void Knock( Door door )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;

		if ( door.Owner == null && door.PendingOwnerSteamId == 0 )
		{ caller.Notify( NotificationType.Info, "Cette porte n'appartient à personne." ); return; }

		if ( door.Owner == caller || door.CoOwners.Contains( caller ) )
		{ caller.Notify( NotificationType.Info, "Vous avez accès à cette porte." ); return; }

		if ( door.KnockSound is not null ) door.PlaySound( door.KnockSound );

		if ( door.Owner != null )
			door.Owner.Notify( NotificationType.Info, $"{caller.DisplayName} toque à votre porte." );

		foreach ( var coOwner in door.CoOwners )
			coOwner.Notify( NotificationType.Info, $"{caller.DisplayName} toque à votre porte." );
	}

	public void OnUse( PlayerPawn player )
	{
		if ( IsLocked ) return;

		LastUse = 0.0f;

		if ( State == DoorState.Closed )
		{
			State = DoorState.Opening;
			if ( OpenSound is not null ) PlaySound( OpenSound );

			if ( OpenAwayFromPlayer )
			{
				var doorToPlayer = (player.WorldPosition - PivotPosition).Normal;
				var doorForward = Transform.Local.Rotation.Forward;
				ReverseDirection = Vector3.Dot( doorToPlayer, doorForward ) > 0;
			}
		}
		else if ( State == DoorState.Open )
		{
			State = DoorState.Closing;
			if ( CloseSound is not null ) PlaySound( CloseSound );
		}
	}
}
