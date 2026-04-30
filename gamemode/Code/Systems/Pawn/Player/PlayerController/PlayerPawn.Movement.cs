using Facepunch;
using Sandbox;
using Sandbox.Events;
using OpenFramework.Command;
using OpenFramework.GameLoop;
using OpenFramework.Systems.Weapons;
using OpenFramework.Utility;
using System.Reflection.Metadata;

namespace OpenFramework.Systems.Pawn;


/// <summary>
/// Called when the player enters AFK state.
/// </summary>
public record PlayerEnterAFKState( PlayerPawn pawn ) : IGameEvent;

public partial class PlayerPawn : IGameEventHandler<WeaponShotEvent>
{
	/// <summary>
	/// Called when the player jumps.
	/// </summary>
	[Property] public Action OnJump { get; set; }

	/// <summary>
	/// The player's box collider, so people can jump on other people.
	/// </summary>
	[Property] public BoxCollider PlayerBoxCollider { get; set; }

	[Sync(SyncFlags.FromHost)] public RealTimeUntil TimeUntilUnfreeze { get; set; } = 0f;
	[Sync( SyncFlags.FromHost )] public bool FreezeIndefinite { get; set; } = false;

	[RequireComponent] public TagBinder TagBinder { get; set; }

	/// <summary>
	/// How tall are we?
	/// </summary>
	[Property, Group( "Config" )] public float Height { get; set; } = 64f;

	[Property, Group( "Fall Damage" )] public float MinimumFallVelocity { get; set; } = 500f;
	[Property, Group( "Fall Damage" )] public float MinimumFallSoundVelocity { get; set; } = 300f;
	[Property, Group( "Fall Damage" )] public float FallDamageScale { get; set; } = 4.5f;
	[Property, Group( "Fall Damage" )] public float FallDamageExponent { get; set; } = 2f;

	[Property, Group( "Sprint" )] public float SprintMovementDampening { get; set; } = 0.35f;

	/// <summary>
	/// Noclip movement speed
	/// </summary>
	[Property] public float NoclipSpeed { get; set; } = 1000f;

	public Constants Global => Constants.Instance;

	/// <summary>
	/// Look direction of this player. Smoothly interpolated for networked players.
	/// </summary>
	public override Angles EyeAngles
	{
		get => IsProxy ? _smoothEyeAngles : _rawEyeAngles;
		set
		{
			_rawEyeAngles = value;
			// Pour nous, pas de lissage, sinon ça saccade !
			if ( !IsProxy ) _smoothEyeAngles = value;
		}
	}
	[Sync] private Angles _rawEyeAngles { get; set; }
	private Angles _smoothEyeAngles;

	/// <summary>
	/// Is the player crouching?
	/// </summary>
	[Sync] public bool IsCrouching { get; set; }

	/// <summary>
	/// Is the player slow walking?
	/// </summary>
	[Sync] public bool IsSlowWalking { get; set; }

	/// <summary>
	/// Are we sprinting?
	/// </summary>
	[Sync] public bool IsSprinting { get; set; }

	/// <summary>
	/// Is the player noclipping?
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool IsNoclipping { get; set; } 

	/// <summary>
	/// If true, we're not allowed to move.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool IsFrozen { get; set; }

	/// <summary>
	/// Last time this player moved or attacked.
	/// </summary>
	[Sync(SyncFlags.FromHost)] public TimeSince TimeSinceLastInput { get; private set; }
	[Sync(SyncFlags.FromHost)] public TimeSince TimeUntilAfkKick { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsAFK { get; set; }

	/// <summary>
	/// If true, the player is in an NPC interaction menu (camera locked, no movement).
	/// </summary>
	public bool IsInNpcMenu { get; set; }

	/// <summary>
	/// What's our holdtype?
	/// </summary>
	[Sync] AnimationHelper.HoldTypes CurrentHoldType { get; set; } = AnimationHelper.HoldTypes.None;

	/// <summary>
	/// Valeur lissée du paramètre d'anim "thrill" (mains en l'air). Interpolée localement sur chaque client à partir de IsHandsUp (déjà sync).
	/// </summary>
	private float _thrillAmount = 0f;

	/// <summary>
	/// Durée (en secondes) de la transition du slider thrill entre 0 et 1.
	/// </summary>
	private const float ThrillTransitionDuration = 0.6f;

	/// <summary>
	/// How quick do we wish to go?
	/// </summary>
	public Vector3 WishVelocity { get; set; }

	/// <summary>
	/// Are we on the ground?
	/// </summary>
	public bool IsGrounded { get; set; }

	/// <summary>
	/// How quick do we wish to go (normalized)
	/// </summary>
	public Vector3 WishMove { get; set; }

	/// <summary>
	/// How much friction to apply to the aim eg if zooming
	/// </summary>
	public float AimDampening { get; set; } = 1.0f;

	/// <summary>
	/// An accessor to get the camera controller's aim ray.
	/// </summary>
	public Ray AimRay => CameraController.AimRay;

	private float _smoothEyeHeight;
	private Vector3 _previousVelocity;
	private bool _isTouchingLadder;
	private Vector3 _ladderNormal;


	[Sync] public bool IsSitting { get; set; } = false;
	[Sync] public GameObject CurrentChair { get; set; }

	[Sync] private float _eyeHeightOffset { get; set; }

	[Sync] public bool IsRotatingObject { get; set; } = false;
	/*
	private void UpdateEyes()
	{
		if ( IsLocallyControlled )
		{
			if ( !IsRotatingObject )
			{
				// Calcul de la cible (0 debout, -16 accroupi)
				var target = GetEyeHeightOffset();

				// Gestion du plafond pour ne pas se relever dans un bloc
				if ( IsCrouching && target > _smoothEyeHeight )
				{
					var trace = TraceBBox( WorldPosition, WorldPosition, 0, 10f );
					if ( trace.Hit )
					{
						target = _smoothEyeHeight;
						// On reste accroupi car le plafond bloque
						IsCrouching = true;
					}
				}

				// LISSAGE DE LA CAMÉRA (La correction que tu attendais)
				_smoothEyeHeight = _smoothEyeHeight.LerpTo( target, Time.Delta * 10f );
				_eyeHeightOffset = _smoothEyeHeight;
			}
		}
		else
		{
			_smoothEyeHeight = _eyeHeightOffset;
		}

		if ( PlayerBoxCollider.IsValid() )
		{
			// On ajuste le collider pour qu'il suive la vue
			PlayerBoxCollider.Center = new( 0, 0, 32 + (_smoothEyeHeight / 2f) );
			PlayerBoxCollider.Scale = new( 32, 32, 64 + _smoothEyeHeight );
		}
	}*/


	/// <summary>
	///  Crouching Prevent Block UP
	/// </summary>
	private void UpdateEyes()
	{
		if ( IsLocallyControlled )
		{
			if ( !IsRotatingObject )
			{
				var target = GetEyeHeightOffset();

				// OPTIMISATION TRACE : On ne trace que si on essaie de se relever
				if ( !Input.Down( "Duck" ) && IsCrouching )
				{
					// On trace une boîte pour voir si on peut se tenir debout (Height = 64)
					// On trace depuis les pieds jusqu'à la tête debout
					var standTrace = TraceBBox( WorldPosition, WorldPosition + Vector3.Up * 20f );

					if ( standTrace.Hit )
					{
						// Quelque chose bloque au dessus ! 
						// On force le maintien de l'état accroupi
						IsCrouching = true;
						target = -16f;
					}
					else
					{
						// L'espace est libre, on autorise le redressement
						IsCrouching = false;
					}
				}

				// On stocke l'ancienne valeur pour comparer
				float oldHeight = _smoothEyeHeight;
				_smoothEyeHeight = _smoothEyeHeight.LerpTo( target, Time.Delta * 10f );
				_eyeHeightOffset = _smoothEyeHeight;

				// OPTIMISATION COLLIDER : On ne met à jour que si le changement est visible
				if ( PlayerBoxCollider.IsValid() && MathF.Abs( oldHeight - _smoothEyeHeight ) > 0.01f )
				{
					PlayerBoxCollider.Center = new( 0, 0, 32 + (_smoothEyeHeight / 2f) );
					PlayerBoxCollider.Scale = new( 32, 32, 64 + _smoothEyeHeight );
				}
			}
		}
		else
		{
			_smoothEyeHeight = _eyeHeightOffset;

			// Pour les proxies (autres joueurs), on évite aussi de recalculer le collider à chaque frame
			if ( PlayerBoxCollider.IsValid() && PlayerBoxCollider.Scale.z != (64 + _smoothEyeHeight) )
			{
				PlayerBoxCollider.Center = new( 0, 0, 32 + (_smoothEyeHeight / 2f) );
				PlayerBoxCollider.Scale = new( 32, 32, 64 + _smoothEyeHeight );
			}
		}
	}


	TimeUntil TimeUntilAccelerationRecovered = 0;
	float AccelerationAddedScale = 0;

	private void ApplyAcceleration()
	{
		var relative = TimeUntilAccelerationRecovered.Fraction.Clamp( 0, 1 );
		var acceleration = GetAcceleration();

		acceleration *= (relative + AccelerationAddedScale).Clamp( 0, 1 );

		CharacterController.Acceleration = acceleration;
	}

	private void OnUpdateMovement()
	{
		var cc = CharacterController;

		// Sit : on traite l'assise EN TOUT PREMIER pour court-circuiter le reste
		// de OnUpdateMovement (notamment l'ecriture de CurrentHoldType qui
		// declenchait un NRE sur le pawn assis — sync state instable apres
		// les mutations IsSitting/CurrentChair). On ne reparent plus le pawn :
		// la position est imposee chaque OnFixedUpdate par ChairComponent ; on
		// retrouve la chaise via CurrentChair (synced) au lieu de GameObject.Parent.
		if ( IsSitting )
		{
			_smoothEyeAngles = Angles.Lerp( _smoothEyeAngles, _rawEyeAngles, Time.Delta );

			if ( IsPossessed && IsLocallyControlled && HealthComponent.IsValid() && HealthComponent.State == LifeState.Alive )
			{
				EyeAngles += Input.AnalogLook * AimDampening;
				EyeAngles = EyeAngles.WithPitch( EyeAngles.pitch.Clamp( -90, 90 ) );
				if ( CameraController.IsValid() )
					CameraController.UpdateFromEyes( _smoothEyeHeight );
			}

			if ( Body.IsValid() )
			{
				Body.UpdateRotation( WorldRotation );
				foreach ( var helper in Body.AnimationHelpers )
				{
					if ( !helper.IsValid() ) continue;
					helper.WithLook( EyeAngles.Forward );
					helper.WithVelocity( Vector3.Zero );
					helper.IsGrounded = true;
					helper.HoldType = AnimationHelper.HoldTypes.None;
					// b_sit doit etre pousse en continu sur owner ET proxies — sinon
					// l'animation graph ne switch pas en pose assise sur les autres clients.
					helper.IsSitting = true;
				}
			}
			return;
		}

		CurrentHoldType = CurrentEquipment.IsValid() ? CurrentEquipment.GetHoldType() : AnimationHelper.HoldTypes.None;

		_smoothEyeAngles = Angles.Lerp( _smoothEyeAngles, _rawEyeAngles, Time.Delta  );
		// 1. Mise à jour de la caméra (Toujours en premier)
		if ( IsPossessed && IsLocallyControlled && HealthComponent.State == LifeState.Alive )
		{
			if ( !IsRotatingObject && !IsInNpcMenu )
			{
				EyeAngles += Input.AnalogLook * AimDampening;
				EyeAngles = EyeAngles.WithPitch( EyeAngles.pitch.Clamp( -90, 90 ) );
			}

			// La caméra suit maintenant le _smoothEyeHeight lissé
			CameraController.UpdateFromEyes( _smoothEyeHeight );
		}

		// 🚗 If in a car → block all normal player updates
		if ( CurrentCar != null )
		{
			//UpdateWhileInCar();
			return;
		}

		// 3. Mouvement normal (Debout / Accroupi)

		if ( Body.IsValid() && cc.IsValid() )
		{
			WorldRotation = Rotation.From( 0, EyeAngles.yaw, 0 );
			Body.UpdateRotation( Rotation.FromYaw( EyeAngles.yaw ) );

			float thrillTarget = IsHandsUp ? 1f : 0f;
			float thrillStep = Time.Delta / MathF.Max( ThrillTransitionDuration, 0.0001f );
			float diff = thrillTarget - _thrillAmount;
			float move = MathF.Min( MathF.Abs( diff ), thrillStep ) * MathF.Sign( diff );
			_thrillAmount = MathX.Clamp( _thrillAmount + move, 0f, 1f );

			foreach ( var helper in Body.AnimationHelpers )
			{
				if ( !helper.IsValid() ) continue;
				helper.WithVelocity( cc.Velocity );
				helper.WithWishVelocity( WishVelocity );
				helper.IsGrounded = IsGrounded;
				helper.WithLook( EyeAngles.Forward );
				helper.MoveStyle = AnimationHelper.MoveStyles.Run;
				helper.DuckLevel = (MathF.Abs( _smoothEyeHeight ) / 32.0f);
				helper.HoldType = CurrentHoldType;
				helper.Handedness = CurrentEquipment.IsValid() ? CurrentEquipment.Handedness : AnimationHelper.Hand.Both;

				if ( helper.Target.IsValid() )
					helper.Target.Set( "thrill", _thrillAmount );
			}
		}

		AimDampening = 1.0f;
	}

	private float GetMaxAcceleration()
	{
		if ( !CharacterController.IsOnGround ) return Global.AirMaxAcceleration;
		return Global.MaxAcceleration;
	}

	private void ApplyMovement()
	{
		var cc = CharacterController;

		CheckLadder();

		var gravity = Global.Gravity;

		if ( _isTouchingLadder )
		{
			LadderMove();
			return;
		}

		if ( cc.IsOnGround )
		{
			cc.Velocity = cc.Velocity.WithZ( 0 );
			cc.Accelerate( WishVelocity );
		}
		else
		{
			if ( !IsNoclipping )
			{
				cc.Velocity -= gravity * Time.Delta * 0.5f;
			}
			cc.Accelerate( WishVelocity.ClampLength( GetMaxAcceleration() ) );
		}

		if ( !cc.IsOnGround )
		{
			if ( !IsNoclipping )
			{
				cc.Velocity -= gravity * Time.Delta * 0.5f;
			}
		}
		else
		{
			cc.Velocity = cc.Velocity.WithZ( 0 );
		}

		if ( IsNoclipping )
		{
			var vertical = 0f;
			if ( Input.Down( "Jump" ) ) vertical = 1f;
			if ( Input.Down( "Duck" ) ) vertical = -1f;

			// On calcule la direction basée sur la vue
			Vector3 moveDir = WishMove.Normal * EyeAngles.ToRotation();
			moveDir += Vector3.Up * vertical;

			// IMPORTANT : On déplace directement le Transform.Position
			// On ne passe pas par cc.Move() pour ignorer physiquement les murs
			WorldPosition += moveDir * NoclipSpeed * Time.Delta;
			cc.Velocity = Vector3.Zero;
			cc.IsOnGround = false;
			/*
			 * 
			cc.Velocity = WishMove.Normal * EyeAngles.ToRotation() * NoclipSpeed;
			cc.Velocity += Vector3.Up * vertical * NoclipSpeed;
			*/

			float finalSpeed = NoclipSpeed;
			if ( Input.Down( "Run" ) ) finalSpeed *= 2.5f; // Turbo noclip
			if ( Input.Down( "Walk" ) ) finalSpeed *= 0.2f; // Précision

			WorldPosition += moveDir * finalSpeed * Time.Delta;

			return;
		}

		if ( IsPassingDoor )
		{
			cc.Velocity = cc.Velocity.WithZ( 0 );
			WorldPosition += WishVelocity * Time.Delta; // déplace sans collision
			return;
		}

		cc.ApplyFriction( GetFriction() );
		cc.Move();
	}

	private bool WantsToSprint => Input.Down( "Run" ) && !IsSlowWalking && !HasEquipmentTag( "no_sprint" ) && CharacterController.IsOnGround && (WishMove.x > 0.2f || (MathF.Abs( WishMove.y ) > 0.2f && WishMove.x >= 0f));
	TimeSince TimeSinceSprintChanged { get; set; } = 100;

	private void OnSprintChanged( bool before, bool after )
	{
		TimeSinceSprintChanged = 0;
	}
	public bool HasEquipmentTag( string flag )
	{
		return CurrentEquipment.IsValid() && CurrentEquipment.HasTag( flag );
	}

	private void BuildInput()
	{
		bool wasSprinting = IsSprinting;

		IsSlowWalking = Input.Down( "Walk" ) || HasEquipmentTag( "aiming" );
		IsSprinting = WantsToSprint;

		if ( wasSprinting != IsSprinting )
		{
			OnSprintChanged( wasSprinting, IsSprinting );
		}


		// Permet de lache la touche 
		if ( Input.Down( "Duck" ) && !IsNoclipping )
		{
			IsCrouching = true;
		}


		//IsCrouching = Input.Down( "Duck" ) && !IsNoclipping;
		IsUsing = Input.Down( "Use" );

		// Check if our current weapon has the planting tag and if so force us to crouch.
		var currentWeapon = CurrentEquipment;
		if ( currentWeapon.IsValid() && currentWeapon.Tags.Has( "planting" ) )
			IsCrouching = true;

		if ( Input.Pressed( "Noclip" ) && Game.IsEditor )
		{
			IsNoclipping = !IsNoclipping;
		}

		if ( WishMove.LengthSquared > 0.01f || Input.Down( "Attack1" ) )
		{
			TimeSinceLastInput = 0f;
		}
		
		if ( CharacterController.IsOnGround && !IsFrozen )
		{
			var bhop = Global.BunnyHopping;
			if ( bhop ? Input.Down( "Jump" ) : Input.Pressed( "Jump" ) )
			{
				CharacterController.Punch( Vector3.Up * Global.JumpPower * 1f );
				BroadcastPlayerJumped();
			}
		}
		
	}

	public SceneTraceResult TraceBBox( Vector3 start, Vector3 end, float liftFeet = 0.0f, float liftHead = 0.0f )
	{
		var bbox = CharacterController.BoundingBox;
		var mins = bbox.Mins;
		var maxs = bbox.Maxs;

		if ( liftFeet > 0 )
		{
			start += Vector3.Up * liftFeet;
			maxs = maxs.WithZ( maxs.z - liftFeet );
		}

		if ( liftHead > 0 )
		{
			end += Vector3.Up * liftHead;
		}

		var tr = Scene.Trace.Ray( start, end )
					.Size( mins, maxs )
					.WithoutTags( CharacterController.IgnoreLayers )
					.IgnoreGameObjectHierarchy( GameObject.Root )
					.Run();
		return tr;
	}

	/// <summary>
	/// A network message that lets other users that we've triggered a jump.
	/// </summary>
	[Rpc.Broadcast]
	public void BroadcastPlayerJumped()
	{
		foreach ( var helper in Body.AnimationHelpers )
		{
			if ( !helper.IsValid() ) continue;
			helper.TriggerJump();
		}

		OnJump?.Invoke();
	}

	public TimeSince TimeSinceGroundedChanged { get; private set; }

	private void GroundedChanged( bool wasOnGround, bool isOnGround )
	{
		if ( !IsLocallyControlled )
			return;

		TimeSinceGroundedChanged = 0;

		if ( !wasOnGround && isOnGround && Global.EnableFallDamage && !IsNoclipping )
		{
			var minimumVelocity = MinimumFallVelocity;
			var vel = MathF.Abs( _previousVelocity.z );

			if ( vel > MinimumFallSoundVelocity )
			{
				PlayFallSound();
			}
			if ( vel > minimumVelocity )
			{
				var velPastAmount = (vel - minimumVelocity) / 100f;
				var damage = MathF.Pow( velPastAmount, FallDamageExponent ) * FallDamageScale;

				TimeUntilAccelerationRecovered = 1f;
				AccelerationAddedScale = 0f;

				using ( Rpc.FilterInclude( Connection.Host ) )
				{
					TakeFallDamage( damage );
				}
			}
		}
	}

	[Property, Group( "Effects" )] public SoundEvent LandSound { get; set; }

	[Rpc.Broadcast]
	private void PlayFallSound()
	{
		var snd = Sound.Play( LandSound, WorldPosition );
		snd.SpacialBlend = IsViewer ? 0 : snd.SpacialBlend;
	}

	[Rpc.Broadcast]
	private void TakeFallDamage( float damage )
	{
		GameObject.TakeDamage( new DamageInfo( this, damage, null, WorldPosition, Flags: DamageFlags.FallDamage ) );
	}

	private void CheckLadder()
	{
		if ( IsNoclipping )
		{
			_isTouchingLadder = false;
			return;
		}

		var cc = CharacterController;
		var wishvel = new Vector3( WishMove.x.Clamp( -1f, 1f ), WishMove.y.Clamp( -1f, 1f ), 0 );
		wishvel *= EyeAngles.WithPitch( 0 ).ToRotation();
		wishvel = wishvel.Normal;

		if ( _isTouchingLadder )
		{
			if ( Input.Pressed( "jump" ) )
			{
				cc.Velocity = _ladderNormal * 100.0f;
				_isTouchingLadder = false;
				return;

			}
			else if ( cc.GroundObject != null && _ladderNormal.Dot( wishvel ) > 0 )
			{
				_isTouchingLadder = false;
				return;
			}
		}

		const float ladderDistance = 1.0f;
		var start = WorldPosition;
		Vector3 end = start + (_isTouchingLadder ? (_ladderNormal * -1.0f) : wishvel) * ladderDistance;

		var pm = Scene.Trace.Ray( start, end )
					.Size( cc.BoundingBox.Mins, cc.BoundingBox.Maxs )
					.WithTag( "ladder" )
					.HitTriggers()
					.IgnoreGameObjectHierarchy( GameObject )
					.Run();

		_isTouchingLadder = false;

		if ( pm.Hit )
		{
			_isTouchingLadder = true;
			_ladderNormal = pm.Normal;
		}
	}

	private void LadderMove()
	{
		var cc = CharacterController;
		cc.IsOnGround = false;

		var velocity = WishVelocity;
		float normalDot = velocity.Dot( _ladderNormal );
		var cross = _ladderNormal * normalDot;
		cc.Velocity = (velocity - cross) + (-normalDot * _ladderNormal.Cross( Vector3.Up.Cross( _ladderNormal ).Normal ));
		cc.Move();
	}

	void IGameEventHandler<WeaponShotEvent>.OnGameEvent( WeaponShotEvent ev )
	{
		TimeSinceLastInput = 0;
	}

	private void BuildWishInput()
	{
		WishMove = 0f;

		if ( IsFrozen || IsInNpcMenu )
			return;

		WishMove += Input.AnalogMove;
	}

	private void BuildWishVelocity()
	{
		WishVelocity = 0f;

		var rot = EyeAngles.WithPitch( 0f ).ToRotation();

		if ( WishMove.Length > 1f )
			WishMove = WishMove.Normal;

		var wishDirection = WishMove * rot;
		wishDirection = wishDirection.WithZ( 0 );
		WishVelocity = wishDirection * GetWishSpeed();
	}

	/// <summary>
	/// Get the current friction.
	/// </summary>
	/// <returns></returns>
	// TODO: expose to global
	private float GetFriction()
	{
		if ( !CharacterController.IsOnGround ) return 0.1f;
		if ( IsSlowWalking ) return Global.SlowWalkFriction;
		if ( IsCrouching ) return Global.CrouchingFriction;
		if ( IsSprinting ) return Global.SprintingFriction;
		return Global.WalkFriction;
	}

	private float GetAcceleration()
	{
		if ( !CharacterController.IsOnGround ) return Global.AirAcceleration;
		else if ( IsSlowWalking ) return Global.SlowWalkAcceleration;
		else if ( IsCrouching ) return Global.CrouchingAcceleration;
		else if ( IsSprinting ) return Global.SprintingAcceleration;

		return Global.BaseAcceleration;
	}

	float GetEyeHeightOffset()
	{
		if ( IsCrouching ) return -16f;
		if ( HealthComponent.State == LifeState.Dead ) return -48f;
		return 0f;
	}

	private float GetSpeedPenalty()
	{
		var wpn = CurrentEquipment;
		if ( !wpn.IsValid() ) return 0;
		return wpn.SpeedPenalty;
	}

	/// <summary>
	/// Override statique pour les tests de vitesse. null = utilise le poids réel.
	/// </summary>
	internal static float? TestWeightRatioOverride;

	// ── Speed/Weight Test ─────────────────────────────────────────────────────
	internal bool SpeedTestActive;
	internal int SpeedTestPhase;        // 0=walk, 1=sprint, 2=slowwalk, 3=crouch
	internal int SpeedTestWeightIndex;  // 0=0%, 1=50%, 2=100%
	internal float SpeedTestTimer;
	internal float SpeedTestMeasureTimer;
	internal float SpeedTestTotalSpeed;
	internal int SpeedTestSamples;
	internal float[][] SpeedTestResults; // [weightIndex][phaseIndex]

	private static readonly float[] SpeedTestWeights = { 0f, 0.5f, 1.0f };
	private static readonly string[] SpeedTestWeightLabels = { "0%", "50%", "100%" };
	private static readonly string[] SpeedTestPhaseLabels = { "Walk", "Sprint", "SlowWalk", "Crouch" };

	internal void UpdateSpeedTest()
	{
		if ( !SpeedTestActive ) return;

		SpeedTestTimer += Time.Delta;

		// Forcer le mouvement vers l'avant
		WishMove = new Vector3( 1f, 0f, 0f );

		// Forcer le mode de déplacement (après BuildInput pour ne pas être écrasé)
		IsSlowWalking = SpeedTestPhase == 2;
		IsCrouching = SpeedTestPhase == 3;
		IsSprinting = SpeedTestPhase == 1;

		// Recalculer WishVelocity avec nos flags forcés
		BuildWishVelocity();

		// Phase de stabilisation (1s pour atteindre la vitesse)
		if ( SpeedTestTimer < 1.0f )
			return;

		// Phase de mesure (0.5s, échantillonner la vitesse)
		SpeedTestMeasureTimer += Time.Delta;
		float speed = CharacterController.Velocity.WithZ( 0 ).Length;
		SpeedTestTotalSpeed += speed;
		SpeedTestSamples++;

		if ( SpeedTestMeasureTimer < 0.5f )
			return;

		// Fin de cette mesure
		float avgSpeed = SpeedTestSamples > 0 ? SpeedTestTotalSpeed / SpeedTestSamples : 0f;
		SpeedTestResults[SpeedTestWeightIndex][SpeedTestPhase] = avgSpeed;

		var inventory = InventoryContainer;
		float maxWeight = inventory?.MaxWeight ?? 50f;
		Log.Info( $"  {SpeedTestPhaseLabels[SpeedTestPhase],-10}: {avgSpeed:F1} u/s (poids {SpeedTestWeightLabels[SpeedTestWeightIndex]} = {maxWeight * SpeedTestWeights[SpeedTestWeightIndex]:F1} kg)" );

		// Passer à la phase suivante
		SpeedTestPhase++;
		if ( SpeedTestPhase > 3 )
		{
			// Passer au palier de poids suivant
			SpeedTestPhase = 0;
			SpeedTestWeightIndex++;

			if ( SpeedTestWeightIndex > 2 )
			{
				// Test terminé
				FinishSpeedTest();
				return;
			}

			TestWeightRatioOverride = SpeedTestWeights[SpeedTestWeightIndex];
			Log.Info( "" );
			Log.Info( $"--- Phase {SpeedTestWeightLabels[SpeedTestWeightIndex]} du poids ({maxWeight * SpeedTestWeights[SpeedTestWeightIndex]:F1} kg) ---" );
		}

		// Reset pour la prochaine mesure
		SpeedTestTimer = 0f;
		SpeedTestMeasureTimer = 0f;
		SpeedTestTotalSpeed = 0f;
		SpeedTestSamples = 0;

		// Arrêter le mouvement brièvement entre les phases
		WishMove = Vector3.Zero;
	}

	private void FinishSpeedTest()
	{
		SpeedTestActive = false;
		TestWeightRatioOverride = null;
		IsCrouching = false;
		IsSlowWalking = false;
		IsSprinting = false;
		WishMove = Vector3.Zero;

		Log.Info( "" );
		Log.Info( "========================================" );
		Log.Info( "   RESUME DES VITESSES MESUREES" );
		Log.Info( "========================================" );
		Log.Info( $"  {"Mode",-10} {"0%",8} {"50%",8} {"100%",8} {"Perte 50%",12} {"Perte 100%",12}" );

		for ( int m = 0; m < 4; m++ )
		{
			float v0 = SpeedTestResults[0][m];
			float v50 = SpeedTestResults[1][m];
			float v100 = SpeedTestResults[2][m];
			string diff50 = v0 > 0 ? $"{((v50 - v0) / v0 * 100f):+0.0;-0.0}%" : "N/A";
			string diff100 = v0 > 0 ? $"{((v100 - v0) / v0 * 100f):+0.0;-0.0}%" : "N/A";
			Log.Info( $"  {SpeedTestPhaseLabels[m],-10} {v0,8:F1} {v50,8:F1} {v100,8:F1} {diff50,12} {diff100,12}" );
		}

		Log.Info( "========================================" );
		Log.Info( "   Test terminé." );
	}

	private float GetWeightSpeedPenalty()
	{
		float ratio;
		if ( TestWeightRatioOverride.HasValue )
		{
			ratio = MathF.Min( TestWeightRatioOverride.Value, 1f );
		}
		else
		{
			var inventory = InventoryContainer;
			if ( inventory == null ) return 0f;
			if ( inventory.MaxWeight <= 0f ) return 0f;
			ratio = MathF.Min( inventory.CurrentWeight / inventory.MaxWeight, 1f );
		}
		return ratio * Global.MaxWeightSpeedPenalty;
	}

	public float GetWishSpeed()
	{
		var weightPenalty = GetWeightSpeedPenalty();
		if ( IsSlowWalking ) return Global.SlowWalkSpeed - (weightPenalty * 0.3f);
		if ( IsCrouching ) return Global.CrouchingSpeed - (weightPenalty * 0.3f);
		if ( IsSprinting ) return Global.SprintingSpeed - (GetSpeedPenalty() * 0.5f) - (weightPenalty * 0.5f);
		return Global.WalkSpeed - GetSpeedPenalty() - weightPenalty;
	}

	private void DebugUpdate()
	{
		DebugText.Update();
		DebugText.Write( $"Player", Color.White, 20 );
		DebugText.Write( $"Velocity: {CharacterController.Velocity}" );
		DebugText.Write( $"Speed: {CharacterController.Velocity.Length}" );
		DebugText.Spacer();
		DebugText.Write( $"Weapon Info", Color.White, 20 );
		DebugText.Write( $"Spread: {Spread}" );
	}

	private void UpdateAFK()
	{
		if ( !Networking.IsHost ) return;

		if ( Client == null || Client.Connection == null )
			return;

		// AFK detection
		if ( !IsAFK && TimeSinceLastInput > Constants.Instance.AfkDelay )
		{
			IsAFK = true;
			TimeUntilAfkKick = 0;
			Scene?.Dispatch( new PlayerEnterAFKState( this ) );
		}
		else if ( IsAFK && TimeSinceLastInput <= Constants.Instance.AfkDelay )
		{
			IsAFK = false;
			TimeUntilAfkKick = 0;
		}

		// Kick if countdown expired
		if ( TimeUntilAfkKick >= Constants.Instance.AfkKickDelay )
		{
			if ( Client.Connection.IsHost )
				return;

			TimeUntilAfkKick = 0;
			IsAFK = false;
			Commands.RPC_Kick( Client, "Kicked due to AFK Inactivity" );
		}
	}

}
