using OpenFramework.Systems.Weapons;

namespace OpenFramework.Systems.Npc;

/// <summary>
/// Checks and handles weapon reloading for bots
/// </summary>
public class ReloadWeaponNode : BaseBehaviorNode
{
	private readonly bool _waitForReload;
	public ReloadWeaponNode( bool waitForReload = false )
	{
		_waitForReload = waitForReload;
	}

	protected override NodeResult OnEvaluate( NpcContext context )
	{
		var weapon = context.Pawn.CurrentEquipment;
		if ( !weapon.IsValid() )
			return NodeResult.Failure;

		var reloadable = weapon.GetComponentInChildren<Reloadable>();
		if ( reloadable == null )
			return NodeResult.Success; // weapon doesn’t need reload

		// If currently reloading
		if ( reloadable.IsReloading )
		{
			if ( _waitForReload )
			{
				// stay running until reload completes
				return NodeResult.Running;
			}
			return NodeResult.Success;
		}

		// If ammo is out, trigger reload
		if ( !reloadable.AmmoComponent.HasAmmo )
		{
			reloadable.StartReload();

			if ( _waitForReload )
			{
				return NodeResult.Running;
			}
		}

		return NodeResult.Success;
	}
}
