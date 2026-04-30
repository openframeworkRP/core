using Facepunch;
using Sandbox.Events;
using OpenFramework.GameLoop;

namespace OpenFramework.Systems.Pawn;

/// <summary>
/// A pawn might have armor, which reduces damage.
/// </summary>
public partial class ArmorComponent : Component, IGameEventHandler<ModifyDamageTakenEvent>
{
	[Property, ReadOnly, Sync( SyncFlags.FromHost )]
	public float Armor { get; set; }

	public float MaxArmor => Constants.Instance.MaxArmor;

	[Property, ReadOnly, Sync( SyncFlags.FromHost ), Change( nameof( OnHasHelmetChanged ) )]
	public bool HasHelmet { get; set; }

	protected void OnHasHelmetChanged( bool _, bool newValue )
	{
		GameObject.Root.Dispatch( new HelmetChangedEvent( newValue ) );
	}

	[Early]
	void IGameEventHandler<ModifyDamageTakenEvent>.OnGameEvent( ModifyDamageTakenEvent eventArgs )
	{
		if ( Armor > 0f ) eventArgs.AddFlag( DamageFlags.Armor );
		if ( HasHelmet ) eventArgs.AddFlag( DamageFlags.Helmet );
	}
}

public record HelmetChangedEvent( bool HasHelmet ) : IGameEvent;
