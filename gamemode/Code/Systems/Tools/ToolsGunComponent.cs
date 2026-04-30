using Sandbox;
using System;
using System.Threading.Tasks;

namespace OpenFramework;

public sealed class ToolsGunComponent : Component
{
	[Property, Group( "Settings" )] public float MaxRange { get; set; } = 20f;
	[Property, Group( "Settings" )] public float MaxWheelsRange { get; set; } = 150f;
	[Property, Group( "Settings" )] public float MoveSpeed { get; set; } = 0.2f;
	[Property, Group( "Settings" )] public float RotateSpeed { get; set; } = 0.1f;

	[Property, Group( "References" )] public BeamEffect Beam { get; set; }
	[Property, Group( "References" )] public GameObject Muzzle { get; set; }

	[Sync] public GameObject SelectedObject { get; set; }
	[Sync] public float HoldDistance { get; set; } = 150.0f;
	[Sync( SyncFlags.Interpolate )] public Vector3 SyncTargetPos { get; set; }
	[Sync( SyncFlags.Interpolate )] public Rotation SyncTargetRot { get; set; }

	private Rotation _relativeRotation;
	private Vector3 _localOffset;

	private Rigidbody _selectedRb;
	private ControlJoint _selectedJoint;

	protected override void OnUpdate()
	{
		if ( Client.Local?.PlayerPawn != null )
			Client.Local.PlayerPawn.IsRotatingObject = false;

		HandleInput();

		if ( SelectedObject.IsValid() )
		{
			if ( IsProxy )
			{
				UpdateProxyTransform();
			}
			else
			{
				UpdateOwnerLogic();
			}

			UpdateBeam();
		}
	}

	private void HandleInput()
	{
		// Clic gauche : Porter / Relâcher
		if ( Input.Released( "attack1" ) && SelectedObject.IsValid() )
		{
			Release();
			return;
		}

		if ( Input.Pressed( "attack1" ) && !SelectedObject.IsValid() )
		{
			if ( Mouse.Visibility == MouseVisibility.Visible ) return;
			TryGrab();
		}

		// --- LOGIQUE STYLE GMOD (Clic Droit) ---
		if ( Input.Pressed( "attack2" ) )
		{
			FreezeAction();
		}
	}
	private void TryGrab()
	{
		// On utilise la caméra pour le tracé (plus précis que le muzzle)
		var rayOrigin = Scene.Camera.WorldPosition;
		var rayDir = Scene.Camera.WorldRotation.Forward;

		var trace = Scene.Trace.Ray( rayOrigin, rayOrigin + (rayDir * MaxRange) )
			.Size( 2f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithTag( "furniture" )
			.Run();

		if ( !trace.Hit || !trace.GameObject.IsValid() ) return;

		// Récupérer le bon objet
		var target = trace.GameObject.Root;
		
		SelectedObject = target;
		_localOffset = SelectedObject.WorldTransform.PointToLocal( trace.HitPosition );
		if ( !SelectedObject.Network.IsOwner )
			SelectedObject.Network.TakeOwnership();

		_selectedRb = SelectedObject.GetComponent<Rigidbody>();
		
		// --- STABILISATION ---
		// On définit la distance de maintien par rapport à la caméra
		HoldDistance = Vector3.DistanceBetween( rayOrigin, trace.HitPosition );

		// On initialise SyncTargetPos pile sur l'objet pour éviter le saut
		SyncTargetPos = trace.HitPosition;

		// On enregistre la rotation actuelle pour qu'il ne pivote pas brusquement
		_relativeRotation = Scene.Camera.WorldRotation.Inverse * SelectedObject.WorldRotation;
		/*
		// On fige l'objet SEULEMENT après avoir réglé les positions cibles
		if ( _selectedRb.IsValid() )
		{
			_selectedRb.MotionEnabled = true;
		}*/
	}

	private void FreezeAction()
	{
		GameObject targetToFreeze = null;

		// Cas 1 : On tient déjà un objet (Prioritaire comme dans GMod)
		if ( SelectedObject.IsValid() )
		{
			targetToFreeze = SelectedObject;
		}
		// Cas 2 : On ne tient rien, on regarde si on peut freezer à distance
		else
		{
			var rayOrigin = Scene.Camera.WorldPosition;
			var rayDir = Scene.Camera.WorldRotation.Forward;
			var trace = Scene.Trace.Ray( rayOrigin, rayOrigin + (rayDir * MaxRange) )
				.WithTag( "furniture" )
				.Run();

			if ( trace.Hit && trace.GameObject.IsValid() )
			{
				targetToFreeze = trace.GameObject.Root;
			}
		}

		// Appliquer le freeze via le composant FurnitureVisual
		if ( targetToFreeze.IsValid() )
		{
			var visual = targetToFreeze.GetComponent<FurnitureVisual>();
			if ( visual.IsValid() )
			{
				// On inverse l'état (Toggle)
				bool newState = !visual.IsLocked;
				visual.UpdateFreeze( newState );

				// Si on était en train de le porter, on le relâche après le freeze
				if ( SelectedObject == targetToFreeze )
				{
					Release();
				}

				// Petit flash de l'outline pour le feedback (optionnel)
				_ = FlashAndHide( visual, newState ? Color.Cyan : Color.White );
			}
		}
	}


	async Task FlashAndHide( FurnitureVisual visual, Color color )
	{
		if ( !visual.IsValid() ) return;

		visual.SetHover( true, color );   // Allume
		await Task.Delay( 100 );          // Attend 0.1 seconde
		if ( visual.IsValid() )
			visual.SetHover( false, color ); // Éteint
	}

	private void HandleTranslation()
	{
		// Utilise la molette pour changer la distance
		HoldDistance = (HoldDistance + Input.MouseWheel.y * 15.0f).Clamp( 50.0f, MaxWheelsRange );

		// Position "parfaite" devant le joueur
		Vector3 targetPosition = Scene.Camera.WorldPosition + (Scene.Camera.WorldRotation.Forward * HoldDistance);

		// Si tu veux zéro saut, augmente la vitesse du Lerp ou force la position au premier frame
		SyncTargetPos = Vector3.Lerp( SyncTargetPos, targetPosition, Time.Delta * 25.0f );
	}

	private void UpdateOwnerLogic()
	{
		SelectedObject.Network.AlwaysTransmit = true;

		if ( Input.Down( "Rotate" ) ) // Assure-toi que "Rotate" est bien mappé ou utilise "Use"
		{
			HandleRotation();
			_relativeRotation = Scene.Camera.WorldRotation.Inverse * SyncTargetRot;
		}
		else
		{
			SyncTargetRot = Scene.Camera.WorldRotation * _relativeRotation;
			HandleTranslation();
		}

		// On calcule la position du centre en fonction de l'endroit où on a cliqué
		// Cela empêche le meuble de "sauter" vers son centre au moment du grab
		var centerPosition = SyncTargetPos - SyncTargetRot * _localOffset;

		SelectedObject.WorldPosition = centerPosition;
		SelectedObject.WorldRotation = SyncTargetRot;
	}

	private void HandleRotation()
	{
		if ( Client.Local?.PlayerPawn != null )
			Client.Local.PlayerPawn.IsRotatingObject = true;

		// 1. On récupère les axes de la caméra pour que la rotation soit relative à ce que tu vois
		var cameraRot = Scene.Camera.WorldRotation;

		// 2. On crée des rotations basées sur le mouvement de la souris
		// Mouse.Delta.x fait pivoter l'objet autour de l'axe vertical du monde (ou de la caméra)
		// Mouse.Delta.y fait pivoter l'objet autour de l'axe horizontal (Right) de la caméra
		var yawRotation = Rotation.FromAxis( Vector3.Up, -Mouse.Delta.x * RotateSpeed );
		var pitchRotation = Rotation.FromAxis( cameraRot.Right, Mouse.Delta.y * RotateSpeed );

		// 3. On applique ces rotations à la rotation actuelle
		// L'ordre Multiplication compte : Pitch * Yaw * RotationActuelle
		SyncTargetRot = pitchRotation * yawRotation * SyncTargetRot;
	}

	private void UpdateProxyTransform()
	{
		// Pour les autres joueurs, on force la position synchronisée
		if ( _selectedJoint.IsValid() )
		{
			_selectedJoint.WorldPosition = SyncTargetPos;
			_selectedJoint.WorldRotation = SyncTargetRot;
		}
	}

	private void UpdateBeam()
	{
		if ( !Beam.IsValid() ) return;

		Beam.Enabled = true;
		Beam.WorldPosition = Muzzle.WorldPosition;
		Beam.TargetPosition = SyncTargetPos;
		Beam.Brightness = 50.0f;
		Beam.Looped = true;
		Beam.Alpha = 1;
	}

	private void Release()
	{
		if ( SelectedObject.IsValid() )
		{
			_selectedRb = SelectedObject.GetComponent<Rigidbody>();
			if ( _selectedRb.IsValid() )
			{
				_selectedRb.MotionEnabled =	false;
				_selectedRb.Sleeping = true;
			}

			SelectedObject.Network.AlwaysTransmit = false;
			SelectedObject.Network.DropOwnership();
		}

		SelectedObject = null;
		_selectedJoint = null;
		_selectedRb = null;

		if ( Beam.IsValid() )
		{
			Beam.Enabled = false;
			Beam.Brightness = 0;
		}
	}
}
