using Facepunch;
using Facepunch.UI;
using Sandbox.Events;
using OpenFramework.Inventory;
using OpenFramework.Systems.Weapons;
using OpenFramework.Utility;
using System;

namespace OpenFramework.Systems.Pawn;

public enum CameraMode
{
	FirstPerson,
	ThirdPerson
}

public interface ICameraSetup : ISceneEvent<ICameraSetup>
{
	public void PreSetup( CameraComponent cc ) { }
	public void Setup( CameraComponent cc ) { }
	public void PostSetup( CameraComponent cc ) { }
}

public sealed class CameraController : PawnCameraController, IGameEventHandler<DamageTakenEvent>
{
	[Property] public PlayerPawn Player { get; set; }

	[Property, Group( "Config" )] public bool ShouldViewBob { get; set; } = true;

	[Property] public float ThirdPersonDistance { get; set; } = 64f;
	[Property] public float AimFovOffset { get; set; } = -5f;

	private CameraMode _mode;
	public CameraMode Mode
	{
		get => _mode;
		set
		{
			if ( _mode == value ) return;
			_mode = value;
			OnModeChanged();
		}
	}
	public float MaxBoomLength { get; set; }

	// On s�pare l'offset dynamique (ex: explosion, effet temporaire) de l'offset d'�tat (ex: vis�e)
	private float DynamicFieldOfViewOffset = 0f;
	private float TargetFieldOfView = 90f;

	public Ray AimRay
	{
		get
		{
			if ( Camera.IsValid() )
			{
				return new( Camera.WorldPosition + Camera.WorldRotation.Forward, Camera.WorldRotation.Forward );
			}
			return new( WorldPosition + Vector3.Up * 64f, Player.EyeAngles.ToRotation().Forward );
		}
	}

	public void AddFieldOfViewOffset( float degrees )
	{
		// On ajoute � l'offset dynamique pour ne pas qu'il soit �cras�
		DynamicFieldOfViewOffset -= degrees;
	}

	private void UpdateRotation()
	{
		var isSitting = Player.IsSitting;
		if ( isSitting )
		{
			// En mode assis, on laisse le Boom tranquille ou on le g�re diff�remment
			// On ne force PAS la rotation du Boom sur les EyeAngles ici
			return;
		}

		//Boom.WorldRotation = Player.EyeAngles.ToRotation();
		Boom.LocalRotation = Rotation.FromPitch( Player.EyeAngles.pitch );
	}

	public override void SetActive( bool isActive )
	{
		base.SetActive( isActive );
		OnModeChanged();
		Boom.WorldRotation = Player.EyeAngles.ToRotation();
	}

	/// <summary>
	/// Noclip avec le tag "hidden"
	/// </summary>
	protected override void OnStart()
	{
		if ( Camera.IsValid() )
		{
			// On dit � la cam�ra de NE PAS afficher ce qui a le tag "hidden"
			Camera.RenderExcludeTags.Add( "hidden" );
		}
	}

	protected override void OnPreRender()
	{
		if ( !Camera.IsValid() ) return;

		ICameraSetup.Post( x => x.PreSetup( Camera ) );
		ICameraSetup.Post( x => x.Setup( Camera ) );
		ICameraSetup.Post( x => x.PostSetup( Camera ) );
	}

	internal void UpdateFromEyes( float eyeHeight )
	{
		// Skip if another system (e.g. VehicleCameraController) has taken over
		if ( !IsActive || !Camera.IsValid() ) return;

		Camera.LocalPosition = Vector3.Zero;
		Camera.LocalRotation = Rotation.Identity;
		
		var isSitting = Player.IsSitting;

		if ( Mode == CameraMode.ThirdPerson && !Player.IsLocallyControlled )
		{
			var angles = Boom.WorldRotation.Angles();
			angles += Input.AnalogLook;
			Boom.WorldRotation = angles.WithPitch( angles.pitch.Clamp( -90, 90 ) ).ToRotation();
		}else if (isSitting)
		{
			Boom.WorldRotation = Player.EyeAngles.ToRotation();
		}
		else
		{
			UpdateRotation();
		}

		if ( MaxBoomLength > 0 )
		{
			var traceStart = Boom.WorldPosition;
			var traceEnd = Boom.WorldRotation.Backward * MaxBoomLength;
			traceEnd += Boom.WorldRotation.Right * 25f;

			var tr = Scene.Trace.Ray( traceStart, traceStart + traceEnd )
				.IgnoreGameObjectHierarchy( GameObject.Root )
				.WithoutTags( "trigger", "player", "ragdoll" )
				.Run();

			Camera.WorldPosition = tr.EndPosition;
		}

		if ( ShouldViewBob ) ViewBob();

		Update( eyeHeight );
	}

	float walkBob = 0;
	private float LerpBobSpeed = 0;

	void ViewBob()
	{
		if ( Mode != CameraMode.FirstPerson ) return;

		var bobSpeed = Player.CharacterController.Velocity.Length.LerpInverse( 0, 300 );
		if ( !Player.IsGrounded ) bobSpeed *= 0.1f;
		if ( !Player.IsSprinting ) bobSpeed *= 0.3f;

		LerpBobSpeed = LerpBobSpeed.LerpTo( bobSpeed, Time.Delta * 10f );
		walkBob += Time.Delta * 10.0f * LerpBobSpeed;

		var yaw = MathF.Sin( walkBob ) * 0.5f;
		var pitch = MathF.Cos( -walkBob * 2f ) * 0.5f;

		Boom.LocalRotation *= Rotation.FromYaw( -yaw * LerpBobSpeed );
		Boom.LocalRotation *= Rotation.FromPitch( -pitch * LerpBobSpeed * 0.5f );
	}

	private float GetWeaponFovOffset()
	{
		if ( !Player.IsValid() || !Player.CurrentEquipment.IsValid() ) return 0;

		float offset = 0;

		// Offset de vis�e classique
		if ( Player.CurrentEquipment.HasTag( "aiming" ) )
		{
			offset += Player.CurrentEquipment.AimFovOffset;
		}

		// Offset de lunette (Scope)
		if ( Player.CurrentEquipment.GetComponentInChildren<FlatScope>() is { } scope )
		{
			offset -= scope.GetFOV();
		}

		return offset;
	}

	bool fetchedInitial = false;
	float defaultSaturation = 1f;

	private void Update( float eyeHeight )
	{
		if ( !Player.IsValid() ) return;

		// 1. R�cup�rer le FOV de base des options
		var baseFov = GameSettingsSystem.Current.FieldOfView;

		// 2. Calculer l'offset d'�tat (Vis�e, Mort)
		float stateOffset = GetWeaponFovOffset();
		if ( Player.HealthComponent.State == LifeState.Dead )
		{
			stateOffset += AimFovOffset;
		}

		// 3. Appliquer les effets de couleur
		if ( ColorAdjustments.IsValid() )
		{
			if ( !fetchedInitial )
			{
				defaultSaturation = ColorAdjustments.Saturation;
				fetchedInitial = true;
			}
			ColorAdjustments.Saturation = ColorAdjustments.Saturation.MoveToLinear( defaultSaturation, 1f );
		}

		/*
		if ( Player.Body.IsValid() && Player.Body.Renderer.IsValid() )
		{
			// On r�cup�re le transform de l'os "head" via le SceneModel
			// C'est la m�thode correcte pour acc�der aux donn�es de squelette
			var headBone = Player.Body.Renderer.SceneModel.GetBoneWorldTransform( "head" );

			// On applique la position mondiale de l'os au Boom
			Boom.WorldPosition = headBone.Position;

			// Optionnel : On avance un peu la cam�ra pour ne pas voir l'int�rieur du cou
			if ( Mode == CameraMode.FirstPerson )
			{
				Boom.WorldPosition += Boom.WorldRotation.Forward * 2.0f;
			}
		}
		else
		{
			// Fallback classique si le mod�le n'est pas trouv�
			Boom.LocalPosition = Vector3.Zero.WithZ( eyeHeight );
		}
		*/

		ApplyRecoil();

		// 4. Positionnement du bras de cam�ra (Boom)
		Boom.LocalPosition = Vector3.Zero.WithZ( eyeHeight );

		ApplyCameraEffects();
		ApplyHeadBagOverlay();
		//ApplyDrunkEffect(); // effet drunk
		ScreenShaker?.Apply( Camera );

		// 5. Application finale du FOV avec Lerp fluide
		// On combine le FOV de base + l'offset de l'arme + l'offset dynamique (secousses, etc.)
		float finalTargetFov = baseFov + stateOffset + DynamicFieldOfViewOffset;
		TargetFieldOfView = TargetFieldOfView.LerpTo( finalTargetFov, Time.Delta * 5f );
		Camera.FieldOfView = TargetFieldOfView;

		// 6. On r�duit progressivement l'offset dynamique pour qu'il revienne � 0
		DynamicFieldOfViewOffset = DynamicFieldOfViewOffset.LerpTo( 0, Time.Delta * 5f );
	}

	RealTimeSince TimeSinceDamageTaken = 1;
	void IGameEventHandler<DamageTakenEvent>.OnGameEvent( DamageTakenEvent eventArgs ) => TimeSinceDamageTaken = 0;

	private HeadBagOverlay _headBagOverlay;
	public const string HeadBagItemResourceName = "head-bag";

	void ApplyHeadBagOverlay()
	{
		if ( !Player.IsValid() )
		{
			DestroyHeadBagOverlay();
			return;
		}

		var clothing = Player.Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
		var headItem = clothing?.GetEquipped( ClothingEquipment.Slot.Head );
		bool hasBag = headItem?.Metadata?.ResourceName == HeadBagItemResourceName;

		if ( hasBag && !_headBagOverlay.IsValid() )
		{
			_headBagOverlay = GameObject.Components.Create<HeadBagOverlay>();
		}
		else if ( !hasBag && _headBagOverlay.IsValid() )
		{
			DestroyHeadBagOverlay();
		}
	}

	void DestroyHeadBagOverlay()
	{
		if ( _headBagOverlay.IsValid() )
			_headBagOverlay.Destroy();
		_headBagOverlay = null;
	}

	float depthOfFieldScale = 0f;
	void ApplyCameraEffects()
	{
		var timeSinceDamage = TimeSinceDamageTaken.Relative;
		var shortDamageUi = timeSinceDamage.LerpInverse( 0.1f, 0.0f, true );
		ChromaticAberration.Scale = shortDamageUi * 1f;
		Pixelate.Scale = shortDamageUi * 0.2f;

		if ( Player.HasEquipmentTag( "scoped" ) )
		{
			depthOfFieldScale = depthOfFieldScale.LerpTo( 1, Time.Delta * 3f );
			DepthOfField.Enabled = true;
			DepthOfField.BlurSize = depthOfFieldScale.Remap( 0, 1, 0, 25 );
		}
		else
		{
			depthOfFieldScale = depthOfFieldScale.LerpTo( 0, Time.Delta * 15f );
			DepthOfField.BlurSize = depthOfFieldScale.Remap( 0, 1, 0, 25 );
			if ( depthOfFieldScale.AlmostEqual( 0, 0.1f ) ) DepthOfField.Enabled = false;
		}
	}

	void ApplyRecoil()
	{
		if ( Player.IsValid() && Player.CurrentEquipment.IsValid() )
		{
			if ( Player.CurrentEquipment.GetComponentInChildren<ShootRecoil>() is { } fn )
				Player.EyeAngles += fn.Current;
		}
	}

	// effet drunk
	/*
	void ApplyDrunkEffect()
	{
		if ( !Player.IsValid() || Player.Client.Data == null ) return;

		var hunger = Player.Client.Data.Hunger;
		float triggerValue = 65f;

		// Si on est au-dessus de 65, on ne fait rien
		if ( hunger > triggerValue || hunger <= 0 ) return;

		// Calcul de l'intensit� (0 � 1)
		float intensity = 1 - (hunger / triggerValue);

		// --- CALCUL DIRECT DE L'OSCILLATION ---
		// On utilise Time.Now pour que le mouvement continue de bouger � chaque frame
		var roll = MathF.Sin( Time.Now * 1.5f ) * (12f * intensity);
		var pitch = MathF.Cos( Time.Now * 1.0f ) * (5f * intensity);

		// Appliquer la rotation � la cam�ra
		// On utilise *= pour que �a s'ajoute au ViewBob et aux autres rotations
		Camera.LocalRotation *= Rotation.From( pitch, 0, roll );
	}*/


	public void OnModeChanged()
	{
		SetBoomLength( Mode == CameraMode.FirstPerson ? 0.0f : ThirdPersonDistance );
		if ( Camera.IsValid() ) Camera.RenderExcludeTags.Set( "viewer", Mode == CameraMode.FirstPerson );

		var firstPersonPOV = Mode == CameraMode.FirstPerson && IsActive;
		if ( Player.IsValid() && Player.Body.IsValid() ) Player.Body.SetFirstPersonView( firstPersonPOV );

		if ( firstPersonPOV ) Player.CreateViewModel( false );
		else Player.ClearViewModel();
	}

	private void SetBoomLength( float length ) => MaxBoomLength = length;
}
