using Sandbox;
using Sandbox.Events;
using OpenFramework.Inventory;
using OpenFramework.Systems.Pawn;
using OpenFramework.Systems.Weapons;
using OpenFramework.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenFramework.Systems.Grab_System;

public sealed class GrabSystem : Component
{
	[Property, Group( "Settings" )] public float HoldDistance { get; set; } = 40f;
	[Property, Group( "Settings" )] public float HoldDistanceMin { get; set; } = 20f;
	[Property, Group( "Settings" )] public float HoldDistanceMax { get; set; } = 150f;
	[Property, Group( "Settings" )] public float HoldDistanceWheelStep { get; set; } = 8f;

	/// <summary>Vitesse lineaire max appliquee a l'objet pour rejoindre la cible (units/s).</summary>
	[Property, Group( "Settings" )] public float MaxFollowSpeed { get; set; } = 600f;

	/// <summary>Gain proportionnel pour la velocite de suivi (plus haut = plus reactif mais peut osciller).</summary>
	[Property, Group( "Settings" )] public float FollowGain { get; set; } = 12f;

	/// <summary>Vitesse de rotation appliquee a l'objet via R+souris (degres par delta look).</summary>
	[Property, Group( "Settings" )] public float RotationSensitivity { get; set; } = 1.5f;

	/// <summary>Vitesse angulaire max appliquee a l'objet (rad/s).</summary>
	[Property, Group( "Settings" )] public float MaxAngularSpeed { get; set; } = 12f;

	[Property] public float GrabDistance { get; set; } = 80f;

	public enum GrabingState { Hover, UnHover }

	[Sync] public GrabingState State { get; set; } = GrabingState.UnHover;

	private Rigidbody _grabbedBody;

	// Rotation cible relative a la camera, modifiable par R+souris.
	// Capture au StartGrab pour que l'objet conserve son orientation initiale.
	private Rotation _grabRotationFromCam = Rotation.Identity;

	// Distance de hold effective utilisee pendant le grab. Initialisee au snap
	// (apres le sweep cam→cible qui peut raccourcir si obstacle), modifiable
	// via la molette. Sans ca, leve la camera apres avoir grab un item au sol
	// faisait s'eloigner l'objet (le HoldDistance par defaut etait plus grand
	// que la distance effective post-clamp sol).
	private float _currentHoldDistance;

	// Etat physique sauvegarde pour restoration au drop.
	private bool _ccdWasEnabled;
	private float _linearDampingBefore;
	private float _angularDampingBefore;

	private PlayerPawn Player => Components.Get<PlayerPawn>( FindMode.EverythingInSelfAndAncestors );

	/// <summary>True si le joueur tient actuellement un objet via le grab.</summary>
	public bool IsGrabbing => _grabbedBody.IsValid();

	// Drop sur re-press F pendant le grip. La touche "grab" (F par defaut)
	// est dediee : elle declenche aussi le start grab quand on vise un objet
	// grabbable, sans passer par le radial menu de E.
	private bool DropPressed() => Input.Pressed( "grab" );

	public GrabAction GetGrabAction()
	{
		if ( _grabbedBody.IsValid() ) return GrabAction.SweepRight;
		return GrabAction.SweepDown;
	}

	/// <summary>
	/// Demarre un grab sur la cible. Appele depuis le radial menu (option "Grab")
	/// ou tout autre point d'entree de gameplay. La cible doit avoir un Rigidbody
	/// motion-enabled accessible via GetInParentOrSelf<Rigidbody>().
	/// </summary>
	public void StartGrab( GameObject target )
	{
		if ( IsProxy ) return;
		if ( !target.IsValid() ) return;

		if ( _grabbedBody.IsValid() )
		{
			Log.Info( "[Grab] StartGrab appele alors qu'un objet est deja tenu → drop d'abord" );
			Drop();
		}

		var vmRenderer = GetVmRenderer();
		if ( vmRenderer.IsValid() ) vmRenderer.Set( "b_grab", true );

		TryGrab( target );
	}

	/// <summary>
	/// Trace devant le joueur pour trouver un objet grabbable (tag "grab" +
	/// Rigidbody motion-enabled) puis lance le grab. Permet a F de remplir
	/// le meme role que l'option "Attraper" du radial.
	/// </summary>
	private void TryStartGrabFromAim()
	{
		var player = Player;
		if ( !player.IsValid() ) return;

		var hits = Scene.Trace.Ray( player.AimRay, GrabDistance )
			.Size( 5f )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( "player", "ghost" )
			.RunAll();

		foreach ( var hit in hits )
		{
			var go = hit.GameObject;
			if ( go == null ) continue;

			var rootTags = go.Root?.Tags;
			bool hasGrabTag = (go.Tags?.Has( "grab" ) ?? false) || (rootTags?.Has( "grab" ) ?? false);
			if ( !hasGrabTag ) continue;

			var body = go.Components.GetInParentOrSelf<Rigidbody>();
			if ( body == null || !body.MotionEnabled ) continue;

			StartGrab( body.GameObject );
			return;
		}
	}

	private SkinnedModelRenderer _vmRenderer;

	private SkinnedModelRenderer GetVmRenderer()
	{
		if ( _vmRenderer.IsValid() ) return _vmRenderer;
		_vmRenderer = GameObject.Root.Components
			.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants )
			.FirstOrDefault( x => x.Tags.Has( "viewmodel" ) );
		return _vmRenderer;
	}

	private Dictionary<WeaponInputAction, List<string>> _originalKeys = new();

	private void SetWeaponsEnabled( bool state )
	{
		var myWeapons = Scene.GetAllComponents<WeaponInputAction>().Where( x => !x.IsProxy );

		foreach ( var wpn in myWeapons )
		{
			if ( !state )
			{
				if ( !_originalKeys.ContainsKey( wpn ) )
				{
					_originalKeys[wpn] = wpn.InputActions.ToList();
					wpn.InputActions = new List<string>();
				}
			}
			else
			{
				if ( _originalKeys.TryGetValue( wpn, out var keys ) )
				{
					wpn.InputActions = keys.ToList();
					_originalKeys.Remove( wpn );
				}
			}
		}
	}


	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		var weaponVM = GameObject.Root.Components.GetInChildren<OpenFramework.Systems.Weapons.WeaponModel>( true );

		// Hors grab : F (action "grab") declenche un grab direct sur l'objet
		// vise s'il porte le tag "grab" et a un Rigidbody motion-enabled.
		// Le radial menu (long-press E) reste disponible en parallele pour
		// les furniture/decor mais F court-circuite ce flow.
		if ( !_grabbedBody.IsValid() )
		{
			if ( weaponVM.IsValid() && weaponVM.ModelRenderer.IsValid() )
			{
				weaponVM.ModelRenderer.Set( "b_grab", false );
			}
			State = GrabingState.UnHover;

			if ( Input.Pressed( "grab" ) )
				TryStartGrabFromAim();

			return;
		}

		// On porte un objet : gere animation, ajustement distance/rotation, drop.
		if ( weaponVM.IsValid() && weaponVM.ModelRenderer.IsValid() )
		{
			weaponVM.ModelRenderer.Set( "b_grab", true );
		}

		// Molette : ajustement de la distance de hold (rapprocher / eloigner).
		// On modifie _currentHoldDistance (la valeur effective post-snap), pas
		// HoldDistance qui est juste la valeur par defaut au prochain grab.
		// Le switch d'arme via molette est bloque cote PlayerInventory tant que le tag is_grabbing est present.
		var wheelY = Input.MouseWheel.y;
		if ( wheelY != 0f )
		{
			_currentHoldDistance = Math.Clamp( _currentHoldDistance + wheelY * HoldDistanceWheelStep, HoldDistanceMin, HoldDistanceMax );
		}

		// R + souris : rotation libre de l'objet, on freeze le viewangle pour ce tick.
		if ( Input.Keyboard.Down( "R" ) )
		{
			var look = Input.AnalogLook;
			_grabRotationFromCam = Rotation.FromAxis( Vector3.Up, -look.yaw * RotationSensitivity )
				* Rotation.FromAxis( Vector3.Right, look.pitch * RotationSensitivity )
				* _grabRotationFromCam;
			Input.AnalogLook = Angles.Zero;
		}

		// Re-press E pendant le grab → drop simple (pas de throw, l'objet est
		// juste lache). PlayerPawn.UpdateUse se court-circuite quand is_grabbing
		// est pose, donc le press E ici n'a plus aucun effet de bord ailleurs.
		if ( DropPressed() )
		{
			Log.Info( "[Grab] Drop simple (re-press E)" );
			Drop();
			return;
		}

		GameObject.Root.Tags.Add( "no_shooting" );
		GameObject.Root.Tags.Add( "reloading" );
	}

	private void TryGrab( GameObject target )
	{
		if ( !target.IsValid() ) return;

		// On cherche le Rigidbody sur l'objet lui-m�me, ou dans ses parents
		var body = target.Components.GetInParentOrSelf<Rigidbody>();

		if ( body.IsValid() )
		{
			// IMPORTANT: S'assurer que le Rigidbody n'est pas "Static"
			if ( body.MotionEnabled )
			{
				target.Network.TakeOwnership();
				_grabbedBody = body;
				_grabbedBody.Gravity = false;

				// Sauvegarde + force CCD et damping pour eviter tunneling/overshoot.
				_ccdWasEnabled = _grabbedBody.EnhancedCcd;
				_linearDampingBefore = _grabbedBody.LinearDamping;
				_angularDampingBefore = _grabbedBody.AngularDamping;
				_grabbedBody.EnhancedCcd = true;
				_grabbedBody.LinearDamping = MathF.Max( _linearDampingBefore, 4f );
				_grabbedBody.AngularDamping = MathF.Max( _angularDampingBefore, 4f );

				// Capture la rotation initiale relative a la camera pour qu'elle soit
				// preservee pendant le grab (l'objet ne saute pas a une orientation arbitraire).
				var cam = Scene?.Camera;
				if ( cam.IsValid() )
				{
					_grabRotationFromCam = Rotation.Difference( cam.WorldRotation, _grabbedBody.WorldRotation );

					// Snap immediat de l'objet devant la camera : evite que le rigidbody
					// continue a frotter par terre pendant que la velocity le tween vers
					// la cible (visible quand on grab un item au sol — il glissait au lieu
					// de venir directement en main). On clamp via un sweep pour ne pas
					// snapper dans un mur/sol si la position cible est obstruee.
					var snapDesired = cam.WorldPosition + cam.WorldRotation.Forward * HoldDistance;
					var snapRoot = GameObject.Root ?? GameObject;
					var snapTr = Scene.Trace.Sphere( 6f, cam.WorldPosition, snapDesired )
						.IgnoreGameObjectHierarchy( snapRoot )
						.IgnoreGameObjectHierarchy( target.Root ?? target )
						.WithoutTags( "trigger" )
						.Run();
					var snapPos = snapTr.Hit
						? cam.WorldPosition + cam.WorldRotation.Forward * MathF.Max( 0f, snapTr.Distance - 4f )
						: snapDesired;
					_grabbedBody.WorldPosition = snapPos;
					_grabbedBody.Velocity = Vector3.Zero;
					_grabbedBody.AngularVelocity = Vector3.Zero;

					// Memorise la distance effective post-snap pour le suivi
					// (sinon lever la cam fait s'eloigner l'objet vers HoldDistance).
					var effectiveDist = (snapPos - cam.WorldPosition).Length;
					_currentHoldDistance = MathF.Max( HoldDistanceMin, effectiveDist );
				}
				else
				{
					_grabRotationFromCam = Rotation.Identity;
				}

				GameObject.Root.Tags.Add( "is_grabbing" );
				SetWeaponsEnabled( false );

				Log.Info( $"Objet saisi : {target.Name}" );
			}
			else
			{
				Log.Warning( "L'objet a un Rigidbody mais il est statique (Motion Disabled) !" );
			}
		}
		else
		{
			Log.Error( "Impossible de trouver un Rigidbody sur la cible !" );
		}
	}


	private void Drop()
	{
		if ( !_grabbedBody.IsValid() ) return;

		var vmRenderer = GetVmRenderer();
		if ( vmRenderer.IsValid() ) vmRenderer.Set( "b_grab", false );

		_grabbedBody.Gravity = true;
		_grabbedBody.EnhancedCcd = _ccdWasEnabled;
		_grabbedBody.LinearDamping = _linearDampingBefore;
		_grabbedBody.AngularDamping = _angularDampingBefore;

		// Drop pur : on pose l'objet la ou il est, sans torque ni impulsion.
		// On garde uniquement l'inertie du joueur pour qu'il ne reste fige
		// si le joueur est en mouvement.
		var playerVel = GameObject.Root.Components.GetInParentOrSelf<Rigidbody>()?.Velocity ?? Vector3.Zero;
		_grabbedBody.Velocity = playerVel;
		_grabbedBody.AngularVelocity = Vector3.Zero;

		_grabbedBody = null;
		GameObject.Root.Tags.Remove( "is_grabbing" );
		GameObject.Root.Tags.Remove( "no_shooting" );
		GameObject.Root.Tags.Remove( "reloading" );

		SetWeaponsEnabled( true );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !_grabbedBody.IsValid() ) return;
		if ( !_grabbedBody.GameObject.Network.IsOwner ) { _grabbedBody = null; return; }

		var camRot = Scene.Camera.WorldRotation;
		var camPos = Scene.Camera.WorldPosition;
		var desiredPos = camPos + camRot.Forward * _currentHoldDistance;

		// Sweep cam → desiredPos pour eviter que l'objet traverse mur/sol/plafond
		// quand on le rapproche ou qu'on regarde fortement vers le bas. On utilise
		// une sphere de 6u pour avoir une marge sur les objets larges. On ignore
		// la hierarchie du pawn ET du rigidbody grabbed (sinon le sweep tape sur
		// l'objet lui-meme et le clamp toujours a 0).
		var pawnRoot = GameObject.Root ?? GameObject;
		var collisionTr = Scene.Trace.Sphere( 6f, camPos, desiredPos )
			.IgnoreGameObjectHierarchy( pawnRoot )
			.IgnoreGameObjectHierarchy( _grabbedBody.GameObject.Root ?? _grabbedBody.GameObject )
			.WithoutTags( "trigger" )
			.Run();

		var targetPos = desiredPos;
		if ( collisionTr.Hit )
		{
			// On s'arrete juste avant la collision (offset 4u pour eviter le clip).
			var safeDist = MathF.Max( 0f, collisionTr.Distance - 4f );
			targetPos = camPos + camRot.Forward * safeDist;
		}

		var direction = targetPos - _grabbedBody.WorldPosition;

		// Velocite lineaire clampee : evite le tunneling/clip a travers le sol quand on
		// teleporte la cible loin (rotation cam rapide, distance soudaine, etc.).
		var desiredVel = direction * FollowGain;
		if ( desiredVel.Length > MaxFollowSpeed )
			desiredVel = desiredVel.Normal * MaxFollowSpeed;
		_grabbedBody.Velocity = desiredVel;

		// Rotation : on calcule la velocite angulaire a partir de la difference de rotation,
		// puis on l'applique au rigidbody (jamais de set direct WorldRotation, sinon tunneling).
		var targetRot = camRot * _grabRotationFromCam;
		var deltaRot = Rotation.Difference( _grabbedBody.WorldRotation, targetRot );

		// Quaternion → axis * angle. Si w < 0, on inverse pour prendre le chemin court.
		float w = deltaRot.w;
		var v = new Vector3( deltaRot.x, deltaRot.y, deltaRot.z );
		if ( w < 0f ) { w = -w; v = -v; }
		float vLen = v.Length;
		Vector3 angVel;
		if ( vLen > 0.0001f )
		{
			float angleRad = 2f * MathF.Atan2( vLen, w );
			var axis = v / vLen;
			angVel = axis * angleRad * 10f;
			if ( angVel.Length > MaxAngularSpeed )
				angVel = angVel.Normal * MaxAngularSpeed;
		}
		else
		{
			angVel = Vector3.Zero;
		}
		_grabbedBody.AngularVelocity = angVel;
	}
}
