using Facepunch;
using OpenFramework.Api;
using OpenFramework.Command;
using OpenFramework.GameLoop;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Inventory.UI;
using OpenFramework.Models;
using OpenFramework.UI;
using OpenFramework.UI.QuickMenuSystem;
using OpenFramework.Utility;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Pawn;

public sealed partial class PlayerPawn : Pawn, IDescription, IAreaDamageReceiver
{
	/// <summary>
	/// The player's body
	/// </summary>
	[Property] public PlayerBody Body { get; set; }

	/// <summary>
	/// A reference to the player's head (the GameObject)
	/// </summary>
	[Property] public GameObject Head { get; set; }

	/// <summary>
	/// The current character controller for this player.
	/// </summary>
	[RequireComponent] public CharacterController CharacterController { get; set; }

	/// <summary>
	/// The current camera controller for this player.
	/// </summary>
	[RequireComponent] public CameraController CameraController { get; set; }

	/// <summary>
	/// The outline effect for this player.
	/// </summary>
	[RequireComponent] public HighlightOutline Outline { get; set; }

	/// <summary>
	/// The spotter for this player.
	/// </summary>
	[RequireComponent] public Spotter Spotter { get; set; }

	/// <summary>
	/// The spottable for this player.
	/// </summary>
	[RequireComponent] public Spottable Spottable { get; set; }

	/// <summary>
	/// Where are weapons on the player?
	/// </summary>
	[Property]
	public GameObject HoldGameObject { get; set; }

	[Property]
	public bool IsHandcuffed => Client?.Data?.IsCuffed ?? false;

	/// <summary>
	/// Le joueur a les mains en l'air (peut être fouillé)
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool IsHandsUp { get; set; } = false;

	/// <summary>
	/// Sound Hundry
	/// </summary>
	[Property] public SoundEvent HundrySound         { get; set; }
	[Property] public SoundEvent HungerWarnSound     { get; set; }
	[Property] public SoundEvent HungerCriticalSound { get; set; }
	[Property] public SoundEvent ThirstWarnSound     { get; set; }
	[Property] public SoundEvent ThirstCriticalSound { get; set; }
	[Property] public SoundEvent BreathSound         { get; set; }

	[Property]
	public InventoryContainer InventoryContainer
	{
		get
		{
			// Exclut le ClothingContainer (sinon Get<InventoryContainer> peut tomber dessus
			// selon l'ordre de traversee → money/items atterrissent dans les slots de vetements)
			var clothing = GameObject.Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren )?.Container;
			return GameObject.Components.GetAll<InventoryContainer>( FindMode.EnabledInSelfAndChildren )
				.FirstOrDefault( c => c != clothing );
		}
	}

	[Property]
	public Dresser DresserContainer => GameObject.Components.Get<Dresser>( FindMode.EnabledInSelfAndChildren );

	[Property]
	public bool IsNpc { get; set; } = false;

	[Property, Sync(SyncFlags.FromHost)]
	public Component CurrentCar { get; set; }

	/// <summary>
	/// Delay de la faim du joueur
	/// </summary>
	public RealTimeUntil hungerAffectDelay { get; set; } = 0f;

	/// <summary>
	/// Delay de la soif du joueur
	/// </summary>
	public RealTimeUntil thirstAffectDelay { get; set; } = 0f;

	[Property, Sync( SyncFlags.FromHost )]
	public RealTimeUntil ReleaseFromJail { get; set; } = 0f;

	[Property]
	public bool IsInJail => ReleaseFromJail > 0f;

	/// <summary>
	/// for spawning room passing door don't touch is important
	/// </summary>
	public bool IsPassingDoor { get; set; }

	/// <summary>
	/// Positionné à true par Client.Spawning avant un destroy de respawn,
	/// pour que OnDestroy ne supprime pas le token API du joueur.
	/// </summary>
	public bool IsDestroyedForRespawn { get; set; } = false;


	/// <summary>
	/// Stamina 
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public bool IsExhausted { get; set; } = false;

	private RealTimeUntil _staminaRegenDelayTimer;
	private RealTimeUntil _nextBreathTime;
	private float _lastHungerForSound = -1f;
	private float _lastThirstForSound = -1f;


	Constants constants = Constants.Instance;

	/// <summary>
	/// Get a quick reference to the real Camera GameObject.
	/// </summary>
	public GameObject CameraGameObject
	{
		get
		{
			if ( !CameraController.IsValid() )
				return null;

			if ( !CameraController.Camera.IsValid() )
				return null;

			return CameraController.Camera.GameObject;
		}
	}


	protected override async void OnDestroy()
	{
		if ( Networking.IsHost && Client.IsValid() )
		{
			var steamId     = Client.SteamId;
			var characterId = PlayerApiBridge.GetActiveCharacter( steamId );

			if ( characterId != null )
			{
				Log.Info( $"[PlayerPawn] Sauvegarde position {steamId} → {WorldPosition}" );
				await ApiComponent.Instance.UpdatePosition( steamId, characterId, WorldPosition );
				Log.Info( $"[PlayerPawn] Position sauvegardée ✓" );
			}

			// Attend la fin d'une save snapshot inventaire en cours (declenchee
			// par Client.SavePositionAndDestroyPawn a la deco) avant de revoquer
			// le token API. Sans ca, la boucle AddInventoryItem echoue en cours
			// de route avec "Joueur non authentifie" => items perdus.
			var pendingInv = OpenFramework.Systems.InventoryApiSystem.Instance?.GetPendingSnapshotSave( steamId );
			if ( pendingInv != null )
			{
				Log.Info( $"[PlayerPawn] Attente save snapshot inventaire pour {steamId} avant RemoveToken..." );
				try { await pendingInv; }
				catch ( Exception ex ) { Log.Warning( $"[PlayerPawn] Save snapshot a echoue pour {steamId}: {ex.Message}" ); }
				Log.Info( $"[PlayerPawn] Save snapshot terminee pour {steamId}, RemoveToken OK." );
			}

			// Nettoyage APRÈS la requête — sauf si le pawn est détruit pour un respawn
			// (token toujours valide, nouveau pawn va le réutiliser)
			if ( !IsDestroyedForRespawn )
				ApiComponent.Instance?.RemoveToken( steamId );
		}

		base.OnDestroy();
	}

	/// <summary>
	/// Finds the first <see cref="SkinnedModelRenderer"/> on <see cref="Body"/>
	/// </summary>
	public SkinnedModelRenderer BodyRenderer => Body?.Components?.Get<SkinnedModelRenderer>();

	// IDescription
	string IDescription.DisplayName => DisplayName;
	Color IDescription.Color => Color.White;

	// IAreaDamageReceiver
	void IAreaDamageReceiver.ApplyAreaDamage( AreaDamage component )
	{
		var dmg = new DamageInfo( component.Attacker, component.Damage, component.Inflictor, component.WorldPosition,
			Flags: component.DamageFlags );

		HealthComponent.TakeDamage( dmg );
	}

	/// <summary>
	/// Pawn
	/// </summary>
	// CameraComponent Pawn.Camera => CameraController.Camera;

	public override CameraComponent Camera => CameraController.Camera;

	public override string NameType => "Player";

	public void PlayerHunger()
	{
		if ( !Networking.IsHost ) return;

		if ( Client.Data.Hunger <= 0 )
		{
			Client.Data.Hunger = 0f;

			if ( hungerAffectDelay.Absolute <= 0 ) hungerAffectDelay = 10f;

			if ( hungerAffectDelay )
			{
				Client.PlayerPawn.HealthComponent.TakeDamage( new( this, 0.5f ) );
				hungerAffectDelay = 10f;
				GameUtils.PlaySoundFrom( HundrySound.ResourcePath, Client.Pawn.GameObject );
			}
			return;
		}

		float decayRate = Constants.Instance.HungerDecayRate * Time.Delta;
		float runMultiplier = Input.Down( "Run" ) ? 1.5f : 1.0f;
		Client.Data.Hunger = MathF.Max( 0f, Client.Data.Hunger - decayRate * runMultiplier );

		PlayNeedThresholdSound( ref _lastHungerForSound, Client.Data.Hunger, HungerWarnSound, HungerCriticalSound );
	}

	public void PlayerThirst()
	{
		if ( !Networking.IsHost ) return;

		if ( Client.Data.Thirst <= 0 )
		{
			Client.Data.Thirst = 0f;

			if ( thirstAffectDelay.Absolute <= 0 ) thirstAffectDelay = 10f;

			if ( thirstAffectDelay )
			{
				Client.PlayerPawn.HealthComponent.TakeDamage( new( this, 0.5f ) );
				thirstAffectDelay = 10f;
			}
			return;
		}

		float decayRate = Constants.Instance.ThirstDecayRate * Time.Delta;
		float runMultiplier = Input.Down( "Run" ) ? 1.5f : 1.0f;
		Client.Data.Thirst = MathF.Max( 0f, Client.Data.Thirst - decayRate * runMultiplier );

		PlayNeedThresholdSound( ref _lastThirstForSound, Client.Data.Thirst, ThirstWarnSound, ThirstCriticalSound );
	}


	public void PlayerStamina()
	{
		if ( !Networking.IsHost ) return;

		// Coût fixe au saut
		if ( Input.Pressed( "Jump" ) && Client.Data.Stamina >= constants.StaminaJumpCost )
		{
			Client.Data.Stamina -= constants.StaminaJumpCost;
			_staminaRegenDelayTimer = constants.StaminaRegenDelay;
		}

		// Bloque le saut si pas assez de stamina
		if ( Client.Data.Stamina < constants.StaminaJumpCost )
			Input.Clear( "Jump" );

		if ( IsSprinting && !IsExhausted )
		{
			Client.Data.Stamina -= constants.StaminaDrainRate * Time.Delta;
			_staminaRegenDelayTimer = constants.StaminaRegenDelay;

			if ( Client.Data.Stamina <= 0f )
			{
				Client.Data.Stamina = 0f;
				IsExhausted = true;
			}

			// Respiration crescendo : intervalle décroissant de 2.5s (60%) à 0.5s (0%)
			if ( Client.Data.Stamina < 60f && _nextBreathTime && BreathSound != null )
			{
				float t = 1f - (Client.Data.Stamina / 60f);
				_nextBreathTime = MathF.Max(0.5f, 2.5f - t * 2f);
				GameUtils.PlaySoundFrom( BreathSound.ResourcePath, Client.Pawn.GameObject );
			}
		}
		else
		{
			if ( _staminaRegenDelayTimer )
			{
				Client.Data.Stamina = MathF.Min( Client.Data.Stamina + constants.StaminaRegenRate * Time.Delta, constants.MaxStamina );

				if ( IsExhausted && Client.Data.Stamina >= constants.ExhaustionRecoveryThreshold )
					IsExhausted = false;
			}
		}
	}

	void PlayNeedThresholdSound( ref float last, float current, SoundEvent warn, SoundEvent critical )
	{
		if ( last < 0f ) { last = current; return; }
		if ( warn     != null && last > 50f && current <= 50f )
			GameUtils.PlaySoundFrom( warn.ResourcePath,     Client.Pawn.GameObject );
		if ( critical != null && last > 25f && current <= 25f )
			GameUtils.PlaySoundFrom( critical.ResourcePath, Client.Pawn.GameObject );
		last = current;
	}

	[Rpc.Broadcast]
	public void ForceTeleport( Vector3 pos )
	{
		// 1. On nettoie les résidus de mouvement précédent
		GameObject.Transform.ClearInterpolation();

		// 2. On déplace le Transform (Serveur et Client seront synchronisés)
		WorldPosition = pos;

		// 3. On synchronise la position INTERNE du controller
		if ( CharacterController.IsValid() )
		{
			CharacterController.Velocity = Vector3.Zero;
			CharacterController.WorldPosition = pos; // On écrase la position interne
		}
	}

	protected override void OnStart()
	{
		// TODO: expose these parameters please
		TagBinder.BindTag( "no_shooting", () => IsSprinting || TimeSinceSprintChanged < 0.25f || TimeSinceWeaponDeployed < 0.66f );
		TagBinder.BindTag( "no_aiming", () => IsSprinting || TimeSinceSprintChanged < 0.25f || TimeSinceGroundedChanged < 0.25f );

		GameObject.Name = $"Player ({DisplayName})";

		CameraController.SetActive( IsViewer );

		// Late-join : si on est sur un client (incl. proxy) et que le pawn vient
		// d'etre replique, l'etat d'apparence est deja dans Client.SavedClothingJson
		// (sync) mais le dresser local a ete clone avec celui par defaut du prefab.
		// On rejoue Apply pour repeupler le dresser depuis le JSON synced — sans ca
		// les vetements equipes AVANT notre connexion ne s'affichent pas.
		if ( !Networking.IsHost && Client.IsValid() )
		{
			Client.ApplyAppearanceFromSync();
		}
	}

	public SceneTraceResult CachedEyeTrace { get; private set; }
	private bool _lastNoclipState;

	// Timer pour le push périodique de position (live map admin)
	private TimeSince _timeSinceLastPositionPush = 0;
	private const float PositionPushInterval = 10f; // secondes

	protected override void OnUpdate()
	{
		// Live map : push la position toutes les 10s côté host pour tous les pawns
		// vivants. Permet au panel admin web d'avoir une carte temps réel sans
		// attendre la déconnexion du joueur (qui était la seule sauvegarde existante).
		if ( Networking.IsHost
		     && Client.IsValid()
		     && HealthComponent?.State != LifeState.Dead
		     && _timeSinceLastPositionPush >= PositionPushInterval )
		{
			_timeSinceLastPositionPush = 0;
			var steamId     = Client.SteamId;
			var characterId = PlayerApiBridge.GetActiveCharacter( steamId );
			if ( characterId != null && ApiComponent.Instance != null )
			{
				var pos = WorldPosition;
				_ = ApiComponent.Instance.UpdatePosition( steamId, characterId, pos );
			}
		}

		if ( HealthComponent.State == LifeState.Dead )
		{
			UpdateDead();
			return;
		}


		/// NOCLIP 
		if ( BodyRenderer.IsValid() && IsNoclipping != _lastNoclipState )
		{
			if ( IsNoclipping )
			{
				// Visuel & Collision
				BodyRenderer.Tags.Add( "hidden" );
				GameObject.Tags.Remove( "player" );
				GameObject.Tags.Add( "no_player" );

				// Désactivation des effets (Blood, Headshot)
				if ( BloodEffect.IsValid() ) BloodEffect.Enabled = false;
				if ( HeadshotEffect.IsValid() ) HeadshotEffect.Enabled = false;
				if ( HeadshotWithHelmetEffect.IsValid() ) HeadshotWithHelmetEffect.Enabled = false;
				// Godmode
				//Client.Viewer.Pawn.HealthComponent.IsGodMode = true;
			}
			else
			{
				

				BodyRenderer.Tags.Remove( "hidden" );
				GameObject.Tags.Remove( "no_player" );
				GameObject.Tags.Add( "player" );

				// Réactivation des effets
				if ( BloodEffect.IsValid() ) BloodEffect.Enabled = true;
				if ( HeadshotEffect.IsValid() ) HeadshotEffect.Enabled = true;
				if ( HeadshotWithHelmetEffect.IsValid() ) HeadshotWithHelmetEffect.Enabled = true;

				//Client.Viewer.Pawn.HealthComponent.IsGodMode = false;
			}
			_lastNoclipState = IsNoclipping;
		}

		OnUpdateMovement();
		//UpdateAFK();

		if ( IsProxy )
		{
			// On lisse l'angle pour les autres joueurs
			// Time.Delta assure que le mouvement est fluide peu importe les FPS
			_smoothEyeAngles = Angles.Lerp( _smoothEyeAngles, _rawEyeAngles, Time.Delta * 29.005f );

			// IsSitting : ne PAS ecraser la rotation du corps avec le yaw du regard.
			// OnUpdateMovement vient deja de poser Body.WorldRotation = pawn.WorldRotation
			// (= rotation du siege). Si on l'ecrase ici, sur les autres clients le corps
			// du joueur assis pivote avec sa souris au lieu de rester dans l'axe du siege.
			if ( Body.IsValid() && !IsSitting )
			{
				// On applique uniquement le Yaw (horizontal) au corps
				Body.WorldRotation = Rotation.FromYaw( _smoothEyeAngles.yaw );
			}
		}



		_smoothEyeHeight = _smoothEyeHeight.LerpTo( _eyeHeightOffset * (IsCrouching ? 1 : 0), Time.Delta * 10f );
		CharacterController.Height = Height + _smoothEyeHeight;

		if ( IsLocallyControlled )
		{
			DebugUpdate();
		}

		if(!IsNpc && HealthComponent.State != LifeState.Dead )
		{
			PlayerHunger();
			PlayerThirst();
			PlayerStamina();
		}

		if(Input.Pressed( "PersonalActionMenu" ) )
		{
			PersonalRadialMenu.Open();
		}

		if(IsFrozen && !FreezeIndefinite && TimeUntilUnfreeze )
		{
			IsFrozen = false;
			Client.Notify(NotificationType.Success, "Vous n'etes plus freeze !" );
		}
	}

	/*
	private bool UpdateSitPlayer()
	{
		// On cherche si on est sur une chaise
		var chair = GameObject.Parent?.Components.GetInAncestorsOrSelf<ChairComponent>();

		if ( chair.IsValid() )
		{
			// 1. On vérifie l'input de sortie ICI
			if ( Input.Pressed( "Jump" ) )
			{
				chair.Eject();
				return false; // On renvoie false pour que OnUpdate continue et reprenne le mouvement
			}

			return true; // Bloque le reste du OnUpdate tant qu'on ne saute pas
		}

		return false;
	}
	*/

	private float DeathcamSkipTime => 5f;
	private float DeathcamIgnoreInputTime => 1f;

	// deathcam
	private void UpdateDead()
	{
		UpdateDeadRespawnLogic(); // ← ajoute ça

		if ( !IsLocallyControlled )
			return;

		if ( !Client.IsValid() && !IsNpc )
			return;

		if ( Client.LastDamageInfo is null )
			return;

		var killer = Client.GetLastKiller();

		if ( killer.IsValid() )
		{
			//EyeAngles = Rotation.LookAt( killer.WorldPosition - WorldPosition, Vector3.Up );
			var cameraUp = WorldPosition + Vector3.Up * 100;
			EyeAngles = Rotation.LookAt( WorldPosition - cameraUp, Vector3.Up );

		}

		if ( ((Input.Pressed( "attack1" ) || Input.Pressed( "attack2" )) && !Client.IsRespawning) || IsNpc || Client.LastDamageInfo.TimeSinceEvent > DeathcamSkipTime )
		{
			// Don't let players immediately switch
			if ( Client.LastDamageInfo.TimeSinceEvent < DeathcamIgnoreInputTime ) return;

			//GameObject.Destroy();
			return;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Client.IsValid() && !IsNpc )
			return;

		var cc = CharacterController;
		if ( !cc.IsValid() ) return;

		var wasGrounded = IsGrounded;
		IsGrounded = cc.IsOnGround;

		if ( IsGrounded != wasGrounded )
		{
			GroundedChanged( wasGrounded, IsGrounded );
		}

		UpdateEyes();
		UpdateZones();
		UpdateOutline();

		// CachedEyeTrace etait calcule chaque fixed update (50Hz) avec un raycast 100000 unites
		// + UseHitboxes() — extremement couteux. Property n'est lu nulle part dans le projet
		// (grep "CachedEyeTrace" → seulement la declaration et l'ecriture). Desactive jusqu'a
		// reactivation explicite par un consommateur. Garde le property au cas ou un addon
		// le consomme via reflection.
		// if ( IsViewer )
		// {
		// 	CachedEyeTrace = Scene.Trace.Ray( AimRay, 100000f )
		// 		.IgnoreGameObjectHierarchy( GameObject )
		// 		.WithoutTags( "ragdoll", "movement" )
		// 		.UseHitboxes()
		// 		.Run();
		// }

		if ( HealthComponent.State != LifeState.Alive )
		{
			return;
		}

		// 🚗 In a car? Completely block character movement physics
		if ( CurrentCar != null )
		{
			// make sure we don't keep old velocity around
			cc.Velocity = 0;
			return;
		}

		/// sit
		if ( IsSitting )
		{
			cc.Velocity = 0;
			return;
		}


		if ( Networking.IsHost && IsNpc )
		{
			// BuildWishVelocity();

			// If we're a bot call these so they don't float in the air.
			ApplyAcceleration();
			ApplyMovement();
			return;
		}

		if ( !IsLocallyControlled )
		{
			return;
		}

		if ( IsHandcuffed || IsHandsUp )
		{
			Input.Clear( "Run" );
			Input.Clear( "attack1" );
			Input.Clear( "attack2" );

			_previousVelocity = cc.Velocity;
			BuildWishInput();
			BuildWishVelocity();
			BuildInput();
			ApplyAcceleration();
			ApplyMovement();

			return;
		}

		if ( IsExhausted )
			Input.Clear( "Run" );

		_previousVelocity = cc.Velocity;
		UpdatePlayArea();
		UpdateUse();
		BuildWishInput();
		BuildWishVelocity();
		BuildInput();
		UpdateSpeedTest();

		UpdateRecoilAndSpread();
		UpdateFocusSomeThingWithWeapon();
		ApplyAcceleration();

		ApplyMovement();
	}

	[Sync( SyncFlags.FromHost )] public bool InPlayArea { get; set; } = true;
	[Sync( SyncFlags.FromHost )] public RealTimeUntil TimeUntilPlayAreaKill { get; set; } = 10f;
	[Property] public float OutOfPlayAreaKillTime { get; set; } = 5f;

	// Cache du PlayAreaSystem (resolution lazy) — evite un Scene.GetSystem<>() chaque tick
	private PlayAreaSystem _cachedPlayAreaSystem;
	private PlayAreaSystem PlayAreaSystemRef => _cachedPlayAreaSystem ??= Scene?.GetSystem<PlayAreaSystem>();

	void UpdatePlayArea()
	{
		if ( !Networking.IsHost ) return;

		// Don't have any play areas, dont bother.
		var playAreaSystem = PlayAreaSystemRef;
		if ( playAreaSystem == null || playAreaSystem.Count < 1 )
			return;

		var playArea = GetZone<PlayArea>();
		if ( !playArea.IsValid() )
		{
			if ( InPlayArea )
			{
				Log.Info( $"No longer in play area, {OutOfPlayAreaKillTime}" );
				// not in the play area, kill them soon
				InPlayArea = false;
				TimeUntilPlayAreaKill = OutOfPlayAreaKillTime;
			}
		}
		else if ( !InPlayArea )
		{
			InPlayArea = true;
		}

		if ( !InPlayArea && TimeUntilPlayAreaKill )
		{
			HealthComponent.TakeDamage( new DamageInfo( this, 999, Hitbox: HitboxTags.Chest ) );
		}
	}

	/// <summary>
	/// Toggle les mains en l'air (appelé par le joueur lui-même)
	/// </summary>
	[Rpc.Host]
	public void ToggleHandsUp()
	{
		var pawn = Rpc.Caller.GetClient()?.PlayerPawn as PlayerPawn;
		if ( pawn == null || pawn != this ) return;

		IsHandsUp = !IsHandsUp;

		// Range l'arme pour que l'anim thrill (bras levés) ne soit pas ecrasee par un holdtype
		if ( IsHandsUp && CurrentEquipment.IsValid() )
			Holster();
	}

	/// <summary>
	/// Permet à un autre joueur de fouiller celui-ci (ouvre l'inventaire de la cible)
	/// </summary>
	[Rpc.Host]
	public void RequestFrisk( PlayerPawn searcher )
	{
		if ( !IsHandsUp ) return;
		if ( searcher == null || searcher == this ) return;

		float dist = Vector3.DistanceBetween( searcher.WorldPosition, WorldPosition );
		if ( dist > 100f ) return;

		var targetContainer = Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		if ( targetContainer == null ) return;

		// Ouvre l'inventaire de la cible chez le fouilleur
		searcher.OpenFriskInventory( targetContainer );
	}

	[Rpc.Broadcast]
	public void OpenFriskInventory( InventoryContainer container )
	{
		if ( !IsLocallyControlled ) return;

		if ( FullInventory.Instance == null ) return;
		FullInventory.Instance.NearbyContainer = container;
		FullInventory.Instance.IsOpen = true;
	}

	private const string HeadBagResourceName = "head-bag";

	/// <summary>
	/// Vrai si le joueur a un sac de tête équipé (empêche la lecture de la minimap, etc.).
	/// </summary>
	public bool HasHeadBagEquipped
	{
		get
		{
			var equip = Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
			var head = equip?.GetEquipped( ClothingEquipment.Slot.Head );
			return head?.Metadata?.ResourceName == HeadBagResourceName;
		}
	}

	/// <summary>
	/// Met de force un sac sur la tête de ce joueur. L'agresseur doit être proche et
	/// la cible doit avoir les mains en l'air ou être menottée. Le sac est pris dans
	/// l'inventaire principal de l'agresseur puis équipé sur la cible.
	/// </summary>
	[Rpc.Host]
	public void RequestPutBagOnHead( PlayerPawn bagger )
	{
		if ( !Networking.IsHost ) return;
		if ( bagger == null || bagger == this ) return;
		if ( !(IsHandsUp || IsHandcuffed) ) return;
		if ( Vector3.DistanceBetween( bagger.WorldPosition, WorldPosition ) > 100f ) return;

		var baggerInv = bagger.InventoryContainer;
		var bagItem = baggerInv?.Items.FirstOrDefault( x => x.Metadata?.ResourceName == HeadBagResourceName );
		if ( bagItem == null ) return;

		var targetEquip = Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
		var targetInv = InventoryContainer;
		if ( targetEquip == null || targetInv == null ) return;

		// Pose l'item côté cible puis l'équipe (Equip gère le déplacement vers le slot tête
		// et remet l'éventuel chapeau précédent dans l'inventaire principal de la cible).
		InventoryContainer.MoveItem( bagItem, targetInv, -1, 1 );
		ClothingEquipment.Equip( targetEquip, bagItem );
	}

	/// <summary>
	/// Retire le sac de la tête de ce joueur et le donne à celui qui le retire.
	/// </summary>
	[Rpc.Host]
	public void RequestRemoveBagFromHead( PlayerPawn remover )
	{
		if ( !Networking.IsHost ) return;
		if ( remover == null || remover == this ) return;
		if ( Vector3.DistanceBetween( remover.WorldPosition, WorldPosition ) > 100f ) return;

		var targetEquip = Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
		var headItem = targetEquip?.GetEquipped( ClothingEquipment.Slot.Head );
		if ( headItem?.Metadata?.ResourceName != HeadBagResourceName ) return;

		ClothingEquipment.Unequip( targetEquip, headItem, remover.InventoryContainer );
	}
}
