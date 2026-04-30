using Sandbox.Events;
using OpenFramework.Systems.Weapons;

namespace OpenFramework.Systems.Pawn;

public partial class PlayerPawn :
	IGameEventHandler<EquipmentDeployedEvent>,
	IGameEventHandler<EquipmentHolsteredEvent>
{
	/// <summary>
	/// What weapon are we using?
	/// </summary>
	[Property, ReadOnly] public Equipment CurrentEquipment { get; private set; }

	public GameObject ViewModelGameObject => CameraController.CameraObject;

	/// <summary>
	/// How inaccurate are things like gunshots?
	/// </summary>
	public float Spread { get; set; }

	private void UpdateRecoilAndSpread()
	{
		bool isAiming = CurrentEquipment.IsValid() && HasEquipmentTag( "aiming" );

		var spread = Global.BaseSpreadAmount;
		var scale = Global.VelocitySpreadScale;
		if ( isAiming ) spread *= Global.AimSpread;
		if ( isAiming ) scale *= Global.AimVelocitySpreadScale;

		var velLen = CharacterController.Velocity.Length;
		spread += velLen.Remap( 0, Global.SpreadVelocityLimit, 0, 1, true ) * scale;

		if ( IsCrouching && IsGrounded ) spread *= Global.CrouchSpreadScale;
		if ( !IsGrounded ) spread *= Global.AirSpreadScale;

		Spread = spread;
	}
	
	
	private IFocusedByWeapon _currentFocusTarget;

	// Throttle a 10Hz : raycast 200f UseHitboxes() purement visuel (focus NPC),
	// 50Hz est inutile — l'oeil ne percoit pas la difference.
	private RealTimeUntil _nextFocusCheck;

	private void UpdateFocusSomeThingWithWeapon()
	{
		// 1. On ne veut exécuter ça que sur la machine qui contrôle le joueur (Owner)
		// Sur un serveur dédié, l'Owner est le joueur distant.
		if ( IsProxy ) return;

		if ( !CurrentEquipment.IsValid() )
		{
			RequestClearFocus();
			return;
		}

		// Vérification des munitions/items (Côté client pour la réactivité)
		var item = CurrentEquipment.LinkedItem;
		if ( item == null || !item.Attributes.ContainsKey( "primary_ammo" ))
		{
			RequestClearFocus();
			return;
		}

		// Throttle apres les early-exits pour que clear-focus reste reactif
		if ( !_nextFocusCheck ) return;
		_nextFocusCheck = 0.1f;

		var ray = new Ray( EyePosition, EyeAngles.Forward );
		var result = Scene.Trace
			.Ray( ray, 200f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "ragdoll", "movement" )
			.UseHitboxes()
			.Run();

		if ( !result.Hit )
		{
			RequestClearFocus();
			return;
		}
		
		var target = result.GameObject?.Components.GetInAncestorsOrSelf<RobbableNpc>();

		if ( target == null )
		{
			if ( _currentFocusTarget != null ) // ← envoie le RPC UNE seule fois
			{
				_currentFocusTarget = null;
				RequestClearFocus();
			}
			return;
		}

		if ( target != _currentFocusTarget ) // ← stocke côté client
		{
			_currentFocusTarget = target; // ← assigné ici sur le client
			RequestSetFocus( target.GameObject );
		}
	}

// Demander au serveur de changer la cible de focus
	[Rpc.Host] // Ou [Rpc.Host] selon si vous voulez que les autres voient l'effet
	private void RequestSetFocus( GameObject targetObj )
	{
		var target = targetObj?.Components.GetInAncestorsOrSelf<RobbableNpc>() as IFocusedByWeapon;
		if ( target == null ) return;

		ClearFocusTarget(); // Nettoie l'ancien focus sur le serveur
		_currentFocusTarget = target;
		target.OnFocusedByWeapon( this );
	}

	[Rpc.Host]
	private void RequestClearFocus()
	{
		ClearFocusTarget();
	}
 
	private void ClearFocusTarget()
	{
		if ( _currentFocusTarget == null ) return;
		_currentFocusTarget.OnFocusLost( this );
		_currentFocusTarget = null;
	}

	void IGameEventHandler<EquipmentDeployedEvent>.OnGameEvent( EquipmentDeployedEvent eventArgs )
	{
		CurrentEquipment = eventArgs.Equipment;
	}

	void IGameEventHandler<EquipmentHolsteredEvent>.OnGameEvent( EquipmentHolsteredEvent eventArgs )
	{
		if ( eventArgs.Equipment == CurrentEquipment )
			CurrentEquipment = null;
	}

	[Rpc.Owner]
	private void SetCurrentWeapon( Equipment equipment )
	{
		SetCurrentEquipment( equipment );
	}

	[Rpc.Owner]
	private void ClearCurrentWeapon()
	{
		if ( CurrentEquipment.IsValid() ) CurrentEquipment.Holster();
	}

	public void Holster()
	{
		if ( IsProxy )
		{
			if ( Networking.IsHost )
				ClearCurrentWeapon();

			return;
		}

		CurrentEquipment?.Holster();
	}

	public TimeSince TimeSinceWeaponDeployed { get; private set; }

	public void SetCurrentEquipment( Equipment weapon )
	{
		if ( weapon == CurrentEquipment ) 
			return;

		ClearCurrentWeapon();

		if ( IsProxy )
		{
			if ( Networking.IsHost )
				SetCurrentWeapon( weapon );

			return;
		}

		TimeSinceWeaponDeployed = 0;

		weapon.Deploy();
	}

	public void ClearViewModel()
	{
		foreach ( var weapon in Inventory.Equipment )
		{
			weapon.DestroyViewModel();
		}
	}

	public void CreateViewModel( bool playDeployEffects = true )
	{
		if ( CameraController.Mode != CameraMode.FirstPerson )
			return;

		var weapon = CurrentEquipment;
		if ( weapon.IsValid() )
			weapon.CreateViewModel( playDeployEffects );
	}
}
