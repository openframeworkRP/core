using Sandbox.Events;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using OpenFramework.Utility;
using static Facepunch.NotificationSystem;

namespace OpenFramework.World;

/// <summary>
/// Rideau metallique / volant montant.
/// Partage la meme logique d'ownership/lock/grace-period que Door, mais l'animation
/// se fait en scalant LocalScale.Z du Panel (le pivot du panel doit etre en haut pour
/// que la porte se retracte vers le rouleau sans deborder dans l'etage du dessus).
///
/// Compatibilite serveur dedie :
///  - State et LastUse sont sync FromHost. Toute mutation passe par RPC.Host (Toggle/Lock/etc).
///  - L'animation tourne dans OnFixedUpdate sur CHAQUE machine (host + clients) qui calcule
///    le scale/pos du Panel localement a partir de l'etat sync. Aucune sync de transform.
///  - Le GameObject Panel DOIT etre en NetworkMode = Snapshot dans le prefab. Snapshot envoie
///    le GO une fois dans le snapshot du monde (sinon en Never le client en dedie ne le
///    receverait jamais et l'encadrement apparait sans la porte), mais ne re-sync pas le
///    transform en continu — donc le scale anime localement n'est pas ecrase par le host.
/// </summary>
public sealed class RollingDoor : Component, IUse, IGameEventHandler<PlayerDisconnectedEvent>, IGameEventHandler<PlayerConnectedEvent>, IDoorAnimated, IDoorGraceable
{
	[Property] public Curve AnimationCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 1f, 1.0f ) );

	[Property, Group( "Sound" )] public SoundEvent OpenSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent OpenFinishedSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent CloseSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent CloseFinishedSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent LockSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent UnlockSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent KnockSound { get; set; }

	/// <summary>
	/// Le panneau qui descend / monte. Son pivot doit etre place en haut pour que
	/// le scale Z=0 retracte la porte vers le rouleau sans deborder vers le haut.
	/// </summary>
	[Property] public GameObject Panel { get; set; }

	/// <summary>
	/// Duree de l'animation d'ouverture/fermeture en secondes.
	/// </summary>
	[Property] public float OpenTime { get; set; } = 1.0f;

	/// <summary>
	/// Scale Z minimum quand la porte est ouverte. 0 = totalement retractee dans le rouleau.
	/// Mettre une petite valeur (~0.02) si on veut garder une bordure visible en haut.
	/// </summary>
	[Property, Range( 0.0f, 0.5f )] public float OpenScaleZ { get; set; } = 0.0f;

	[Sync( SyncFlags.FromHost )] public Client Owner { get; set; } = null;
	[Sync( SyncFlags.FromHost )] public List<Client> CoOwners { get; private set; } = new();

	public ulong PendingOwnerSteamId { get; private set; } = 0;
	public float PendingOwnerExpireAt { get; private set; } = 0f;
	public const float OwnerGracePeriod = 300f;

	[Property, FeatureEnabled( "Purchase" )] public bool CanBePurchased { get; set; } = true;
	[Property, Sync( SyncFlags.FromHost ), Feature( "Purchase" )] public int Price { get; set; } = 500;

	[Property, FeatureEnabled( "Job" )] public bool CanBeAllowedJob { get; set; } = false;
	[Property, Sync( SyncFlags.FromHost ), Feature( "Job" )] public string JobName { get; set; }

	[Property, Sync( SyncFlags.FromHost ), Feature( "Door List" )] public List<GameObject> DoorsList { get; set; }

	public enum DoorState { Open, Opening, Closing, Closed }

	Vector3 PanelStartScale { get; set; } = Vector3.One;
	Vector3 PanelStartLocalPos { get; set; } = Vector3.Zero;
	/// <summary>
	/// Z max du modele du Panel en model-space (utilise pour compenser la position
	/// pendant l'animation : on veut que le sommet du panneau reste fixe pendant
	/// que le scale Z descend, peu importe ou est l'origine du modele — bas, centre, etc).
	/// </summary>
	float PanelTopOffset { get; set; } = 0f;
	[Sync( SyncFlags.FromHost )] public TimeSince LastUse { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsLocked { get; set; } = false;
	[Sync( SyncFlags.FromHost ), Change( nameof( OnStateChanged ) )] public DoorState State { get; set; } = DoorState.Closed;
	private DoorState DefaultState { get; set; } = DoorState.Closed;
	private DoorState _lastLoggedState = DoorState.Closed;

	GrabAction IUse.GetGrabAction() => State == DoorState.Open ? GrabAction.SweepRight : GrabAction.SweepLeft;

	protected override void OnStart()
	{
		Log.Info( $"[RollingDoor][OnStart] go='{GameObject.Name}' isHost={Networking.IsHost} isProxy={IsProxy} panelRef={(Panel is null ? "<null>" : Panel.Name)}" );
		if ( Panel is null )
		{
			// Fallback : retrouver le Panel par nom dans les enfants. Utile si la ref
			// serialisee du prefab ne s'est pas resolue cote client (le Panel etant en
			// NetworkMode=Snapshot peut arriver avec un timing different du parent).
			Panel = GameObject.Children.FirstOrDefault( c => c.Name == "Panel" );
			Log.Info( $"[RollingDoor][OnStart] fallback lookup Panel-by-name → {(Panel is null ? "<null>" : Panel.Name)}" );
		}
		if ( Panel is not null )
		{
			PanelStartScale = Panel.LocalScale;
			PanelStartLocalPos = Panel.LocalPosition;

			// On lit le sommet du modele en model-space pour pouvoir compenser la position
			// pendant le scale (l'origine peut etre en bas, au centre, etc).
			var renderer = Panel.Components.Get<ModelRenderer>( FindMode.EnabledInSelfAndChildren );
			if ( renderer?.Model is not null )
				PanelTopOffset = renderer.Model.Bounds.Maxs.z;
		}

		DefaultState = State;
		SyncChildrenOwnership();

		// Auto-attach DistanceNetworkVisibility si pas deja present.
		// Ne fait rien si AlwaysTransmit n'est pas decoche dans le prefab Network.
		if ( !Components.Get<DistanceNetworkVisibility>().IsValid() )
			Components.Create<DistanceNetworkVisibility>();
	}

	protected override void OnEnabled()
	{
		// Snapshot recu pour un client en cours d'animation, ou host qui restore une scene
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

	private void OnStateChanged( DoorState oldVal, DoorState newVal )
	{
		if ( newVal == DoorState.Opening || newVal == DoorState.Closing )
		{
			DoorAnimationSystem.RegisterAnimator( this );
			if ( _lastLoggedState != newVal )
			{
				_lastLoggedState = newVal;
				Log.Info( $"[RollingDoor][AnimStart] go='{GameObject.Name}' isHost={Networking.IsHost} state={newVal} panel={(Panel is null ? "<NULL>" : Panel.Name)} lastUse={LastUse}" );
			}
		}
		else
		{
			DoorAnimationSystem.UnregisterAnimator( this );
			_lastLoggedState = newVal;
		}
	}

	void IDoorAnimated.TickDoorAnimation()
	{
		if ( Panel is null )
		{
			DoorAnimationSystem.UnregisterAnimator( this );
			return;
		}
		if ( State != DoorState.Opening && State != DoorState.Closing )
		{
			DoorAnimationSystem.UnregisterAnimator( this );
			return;
		}

		var time = LastUse.Relative.Remap( 0.0f, OpenTime, 0.0f, 1.0f );
		var curve = AnimationCurve.Evaluate( time );
		if ( State == DoorState.Closing ) curve = 1.0f - curve;

		// curve = 0 → ferme (scale Z = StartScale.z), curve = 1 → ouvert (scale Z = OpenScaleZ * StartScale.z)
		var zScale = MathX.Lerp( PanelStartScale.z, PanelStartScale.z * OpenScaleZ, curve );
		Panel.LocalScale = new Vector3( PanelStartScale.x, PanelStartScale.y, zScale );

		// On compense la position pour que le sommet du panneau reste fixe (la porte
		// se replie vers le haut au lieu de descendre vers le sol quand l'origine est en bas).
		var zOffset = PanelTopOffset * (PanelStartScale.z - zScale);
		Panel.LocalPosition = new Vector3( PanelStartLocalPos.x, PanelStartLocalPos.y, PanelStartLocalPos.z + zOffset );

		if ( time < 1f ) return;

		// Fin d'animation : Change callback OnStateChanged va nous desinscrire automatiquement
		State = State == DoorState.Opening ? DoorState.Open : DoorState.Closed;

		if ( Networking.IsHost )
		{
			if ( State == DoorState.Open && OpenFinishedSound is not null ) PlaySound( OpenFinishedSound );
			if ( State == DoorState.Closed && CloseFinishedSound is not null ) PlaySound( CloseFinishedSound );
		}
	}

	void IDoorGraceable.TickGraceCheck()
	{
		// Host-only : seul le host pilote l'expiration (etat sync ensuite)
		if ( !Networking.IsHost ) return;
		if ( PendingOwnerSteamId == 0 )
		{
			DoorAnimationSystem.UnregisterGraceable( this );
			return;
		}
		if ( Time.Now < PendingOwnerExpireAt ) return;

		Log.Info( $"[RollingDoor] Grace period expire pour {PendingOwnerSteamId} sur '{GameObject.Name}'" );
		ClearOwnership();
	}

	// ─────────────────────────────────────────────
	//  DECONNEXION / RECONNEXION
	// ─────────────────────────────────────────────

	public void OnGameEvent( PlayerDisconnectedEvent eventArgs )
	{
		if ( !Networking.IsHost ) return;
		if ( eventArgs.Player != Owner ) return;

		PendingOwnerSteamId = Owner.SteamId;
		PendingOwnerExpireAt = Time.Now + OwnerGracePeriod;

		// Inscrit au DoorAnimationSystem pour le check 1Hz pendant la grace
		DoorAnimationSystem.RegisterGraceable( this );

		foreach ( var coOwner in CoOwners )
			coOwner.Notify( NotificationType.Warning, $"Le proprietaire s'est deconnecte. Le rideau expire dans {OwnerGracePeriod / 60:0} minutes." );

		Owner = null;
		Log.Info( $"[RollingDoor] Owner {PendingOwnerSteamId} deconnecte, grace period de {OwnerGracePeriod}s demarre sur '{GameObject.Name}'" );
	}

	public void OnGameEvent( PlayerConnectedEvent eventArgs )
	{
		if ( !Networking.IsHost ) return;
		if ( PendingOwnerSteamId == 0 ) return;

		var client = eventArgs.Client;
		if ( client?.SteamId != PendingOwnerSteamId ) return;

		Owner = client;
		PendingOwnerSteamId = 0;
		PendingOwnerExpireAt = 0f;

		// Plus de grace en attente
		DoorAnimationSystem.UnregisterGraceable( this );

		client.Notify( NotificationType.Success, "Vous avez recupere votre rideau." );
		SyncChildrenOwnership();
		Log.Info( $"[RollingDoor] {client.DisplayName} a recupere son rideau '{GameObject.Name}' apres reconnexion." );
	}

	private void ClearOwnership()
	{
		Owner = null;
		PendingOwnerSteamId = 0;
		PendingOwnerExpireAt = 0f;
		CoOwners.Clear();
		IsLocked = false;
		State = DefaultState;
		LastUse = 0.0f;

		// Plus de grace en attente
		DoorAnimationSystem.UnregisterGraceable( this );

		if ( CloseSound is not null ) PlaySound( CloseSound );
		SyncChildrenOwnership();
	}

	// ─────────────────────────────────────────────
	//  USE / SOUND
	// ─────────────────────────────────────────────

	// On autorise l'interaction dans tous les etats (y compris Opening/Closing) pour
	// permettre d'inverser le sens de l'animation a la volee. La continuite visuelle
	// est assuree dans OnUse en recalculant LastUse pour repartir au meme scale.
	public UseResult CanUse( PlayerPawn player ) => true;

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
			var childDoor = go.Components.Get<RollingDoor>();
			if ( childDoor == null || childDoor == this ) continue;

			childDoor.Owner = this.Owner;
			childDoor.CoOwners = new List<Client>( this.CoOwners );
			childDoor.IsLocked = this.IsLocked;
			childDoor.CanBePurchased = false;
		}
	}

	// ─────────────────────────────────────────────
	//  RPCs (achat, vente, share, lock, knock, toggle)
	// ─────────────────────────────────────────────

	[Rpc.Host]
	public static void TryToBuy( RollingDoor door )
	{
		var caller = Rpc.Caller.GetClient();

		if ( door.CanBeAllowedJob )
		{ caller.Notify( NotificationType.Error, "Ce rideau appartient a un batiment public." ); return; }

		if ( !door.CanBePurchased )
		{ caller.Notify( NotificationType.Error, "Ce rideau ne peut pas etre achete." ); return; }

		if ( door.Owner == caller )
		{ caller.Notify( NotificationType.Error, "Ce rideau vous appartient deja." ); return; }

		if ( door.Owner != null || door.PendingOwnerSteamId != 0 )
		{ caller.Notify( NotificationType.Error, "Ce rideau est deja reserve." ); return; }

		if ( MoneySystem.Get( caller ) < door.Price )
		{ caller.Notify( NotificationType.Error, "Vous n'avez pas assez d'argent." ); return; }

		MoneySystem.Remove( caller, door.Price );
		door.Owner = caller;
		door.PendingOwnerSteamId = 0;
		door.CoOwners.Clear();
		door.IsLocked = false;
		door.State = DoorState.Closed;
		door.LastUse = 0.0f;

		caller.Notify( NotificationType.Success, $"Vous avez achete ce rideau pour {door.Price}$." );
		ObjectEffects.BuyEffect( caller, door.GameObject );
		door.SyncChildrenOwnership();

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
			Log.Info( $"[RollingDoor] Cle donnee a {caller.DisplayName} pour '{door.GameObject.Name}' (GUIDs: {string.Join( ",", guids )})" );
		}
	}

	[Rpc.Host]
	public static void TryToSell( RollingDoor door )
	{
		var caller = Rpc.Caller.GetClient();
		if ( door.Owner != caller )
		{ caller.Notify( NotificationType.Error, "Vous ne possedez pas ce rideau." ); return; }

		door.Owner = null;
		door.CoOwners.Clear();
		door.IsLocked = false;
		door.PendingOwnerSteamId = 0;

		MoneySystem.Add( caller, door.Price / 2 );
		caller.Notify( NotificationType.Success, $"Vous avez vendu ce rideau pour {door.Price / 2}$." );
		door.SyncChildrenOwnership();
	}

	[Rpc.Host]
	public static void ShareDoor( RollingDoor door, Client target )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;

		if ( door.Owner == null ) { caller.Notify( NotificationType.Error, "Ce rideau n'appartient a personne." ); return; }
		if ( door.Owner != caller ) { caller.Notify( NotificationType.Error, "Tu n'es pas le proprietaire." ); return; }
		if ( !target.IsValid ) { caller.Notify( NotificationType.Error, "Joueur introuvable." ); return; }
		if ( target == door.Owner ) { caller.Notify( NotificationType.Error, "Vous ne pouvez pas vous partager la cle a vous-meme." ); return; }
		if ( door.CoOwners.Contains( target ) ) { caller.Notify( NotificationType.Warning, "Ce joueur a deja les cles." ); return; }

		door.CoOwners.Add( target );
		caller.Notify( NotificationType.Success, $"Tu as partage le rideau avec {target.DisplayName}." );
		target.Notify( NotificationType.Success, $"Tu as recu les cles de {caller.DisplayName}." );
		door.SyncChildrenOwnership();
	}

	[Rpc.Host]
	public static void RemoveShareDoor( RollingDoor door, Client target )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;
		if ( door.Owner == null ) { caller.Notify( NotificationType.Error, "Ce rideau n'appartient a personne." ); return; }
		if ( !target.IsValid ) { caller.Notify( NotificationType.Error, "Joueur introuvable." ); return; }
		if ( target == door.Owner ) { caller.Notify( NotificationType.Error, "Le proprietaire doit revendre pour se retirer." ); return; }

		var isCallerOwner = door.Owner == caller;
		var isSelfUnshare = target == caller;

		if ( !(isCallerOwner || isSelfUnshare) ) { caller.Notify( NotificationType.Error, "Tu n'as pas la permission." ); return; }
		if ( !door.CoOwners.Contains( target ) ) { caller.Notify( NotificationType.Warning, "Ce joueur n'a pas de cle." ); return; }

		door.CoOwners.Remove( target );

		if ( isSelfUnshare && !isCallerOwner )
			caller.Notify( NotificationType.Success, "Tu as rendu la cle." );
		else
			caller.Notify( NotificationType.Success, $"Tu as retire l'acces a {target.DisplayName}." );

		target.Notify( NotificationType.Warning, $"Ton acces au rideau de {door.Owner?.DisplayName} a ete retire." );
		door.SyncChildrenOwnership();
	}

	[Rpc.Host]
	public static void Lock( RollingDoor door )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;

		string playerJob = caller.Data.Job ?? "";

		if ( door.CanBeAllowedJob && !string.IsNullOrEmpty( door.JobName ) )
		{
			bool hasJobAccess = string.Equals( door.JobName, playerJob, StringComparison.OrdinalIgnoreCase );
			bool isOwner = door.Owner == caller || door.CoOwners.Contains( caller );

			if ( !hasJobAccess && !isOwner )
			{ caller.Notify( NotificationType.Error, "Vous n'avez pas le metier requis." ); return; }
		}

		if ( door.IsLocked ) { caller.Notify( NotificationType.Info, "Ce rideau est deja verrouille." ); return; }

		door.IsLocked = true;
		door.SyncChildrenOwnership();
		if ( door.LockSound is not null ) door.PlaySound( door.LockSound );
		caller.Notify( NotificationType.Info, "Vous avez verrouille le rideau." );
	}

	[Rpc.Host]
	public static void Unlock( RollingDoor door )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;
		if ( !door.IsLocked ) { caller.Notify( NotificationType.Info, "Ce rideau est deja deverrouille." ); return; }

		door.IsLocked = false;
		door.SyncChildrenOwnership();
		if ( door.UnlockSound is not null ) door.PlaySound( door.UnlockSound );
		caller.Notify( NotificationType.Info, "Vous avez deverrouille le rideau." );
	}

	[Rpc.Host]
	public static void Toggle( RollingDoor door )
	{
		Log.Info( $"[RollingDoor][Toggle][host] called door={(door?.GameObject?.Name ?? "<null>")} doorValid={door.IsValid()}" );
		if ( !door.IsValid() ) { Log.Warning( "[RollingDoor][Toggle][host] door invalid → abort" ); return; }
		var caller = Rpc.Caller.GetClient();
		if ( caller?.PlayerPawn is not PlayerPawn pawn )
		{
			Log.Warning( $"[RollingDoor][Toggle][host] caller={caller?.DisplayName ?? "<null>"} pawn={(caller?.PlayerPawn?.GameObject?.Name ?? "<null>")} → abort" );
			return;
		}
		Log.Info( $"[RollingDoor][Toggle][host] caller={caller.DisplayName} state={door.State} locked={door.IsLocked} → OnUse" );
		// On accepte tous les etats : OnUse gere l'inversion en plein vol.
		door.OnUse( pawn );
		Log.Info( $"[RollingDoor][Toggle][host] post-OnUse state={door.State} lastUse={door.LastUse}" );
	}

	[Rpc.Host]
	public static void Knock( RollingDoor door )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || door == null ) return;

		if ( door.Owner == null && door.PendingOwnerSteamId == 0 )
		{ caller.Notify( NotificationType.Info, "Ce rideau n'appartient a personne." ); return; }

		if ( door.Owner == caller || door.CoOwners.Contains( caller ) )
		{ caller.Notify( NotificationType.Info, "Vous avez acces a ce rideau." ); return; }

		if ( door.KnockSound is not null ) door.PlaySound( door.KnockSound );

		if ( door.Owner != null )
			door.Owner.Notify( NotificationType.Info, $"{caller.DisplayName} toque a votre rideau." );

		foreach ( var coOwner in door.CoOwners )
			coOwner.Notify( NotificationType.Info, $"{caller.DisplayName} toque a votre rideau." );
	}

	public void OnUse( PlayerPawn player )
	{
		if ( IsLocked ) return;

		// Inversion en plein vol : on calcule le temps restant pour que le scale courant
		// reste continu apres le switch. Marche exactement pour une curve lineaire (default),
		// approximatif pour une curve non-lineaire — assez bon pour l'oeil.
		if ( State == DoorState.Opening )
		{
			var elapsed = (float)LastUse.Relative;
			LastUse = MathF.Max( 0f, OpenTime - elapsed );
			State = DoorState.Closing;
			if ( CloseSound is not null ) PlaySound( CloseSound );
		}
		else if ( State == DoorState.Closing )
		{
			var elapsed = (float)LastUse.Relative;
			LastUse = MathF.Max( 0f, OpenTime - elapsed );
			State = DoorState.Opening;
			if ( OpenSound is not null ) PlaySound( OpenSound );
		}
		else if ( State == DoorState.Closed )
		{
			LastUse = 0.0f;
			State = DoorState.Opening;
			if ( OpenSound is not null ) PlaySound( OpenSound );
		}
		else if ( State == DoorState.Open )
		{
			LastUse = 0.0f;
			State = DoorState.Closing;
			if ( CloseSound is not null ) PlaySound( CloseSound );
		}
	}

	// ─────────────────────────────────────────────
	//  BOUTONS DE TEST (editeur + runtime)
	// ─────────────────────────────────────────────

	/// <summary>
	/// Force l'animation runtime (ouvre si fermee, ferme si ouverte).
	/// Ne marche qu'en play mode car depend de OnFixedUpdate.
	/// </summary>
	[Button( "Tester Ouvrir / Fermer" ), Group( "Debug" )]
	public void DebugToggle()
	{
		if ( !Game.IsPlaying )
		{
			Log.Warning( "[RollingDoor] DebugToggle ne fonctionne qu'en play mode. Utilise 'Apercu Ouvert / Ferme' en editeur." );
			return;
		}

		LastUse = 0.0f;
		if ( State == DoorState.Closed ) State = DoorState.Opening;
		else if ( State == DoorState.Open ) State = DoorState.Closing;
	}

	/// <summary>
	/// Apercu instantane en editeur : applique le scale 'ouvert' au panneau.
	/// </summary>
	[Button( "Apercu Ouvert" ), Group( "Debug" )]
	public void DebugPreviewOpen()
	{
		if ( Panel is null ) { Log.Warning( "[RollingDoor] Panel non assigne" ); return; }

		var startScale = Panel.LocalScale;
		var startPos = Panel.LocalPosition;
		var renderer = Panel.Components.Get<ModelRenderer>( FindMode.EnabledInSelfAndChildren );
		var topOffset = renderer?.Model is not null ? renderer.Model.Bounds.Maxs.z : 0f;

		var newZScale = startScale.z * OpenScaleZ;
		var zOffset = topOffset * (startScale.z - newZScale);

		Panel.LocalScale = new Vector3( startScale.x, startScale.y, newZScale );
		Panel.LocalPosition = new Vector3( startPos.x, startPos.y, startPos.z + zOffset );
	}

	/// <summary>
	/// Apercu instantane en editeur : restaure le scale 'ferme' (Z=1) et la position d'origine.
	/// </summary>
	[Button( "Apercu Ferme" ), Group( "Debug" )]
	public void DebugPreviewClosed()
	{
		if ( Panel is null ) { Log.Warning( "[RollingDoor] Panel non assigne" ); return; }

		var s = Panel.LocalScale;
		var p = Panel.LocalPosition;
		var renderer = Panel.Components.Get<ModelRenderer>( FindMode.EnabledInSelfAndChildren );
		var topOffset = renderer?.Model is not null ? renderer.Model.Bounds.Maxs.z : 0f;

		// On annule l'eventuel zOffset applique par DebugPreviewOpen.
		var zOffset = topOffset * (1f - s.z);
		Panel.LocalScale = new Vector3( s.x, s.y, 1f );
		Panel.LocalPosition = new Vector3( p.x, p.y, p.z - zOffset );
	}
}
