namespace OpenFramework.World.CocaineFactory;

/// <summary>
/// Gas bottle that can be attached to a <see cref="GasStove"/> to provide gas for cooking.
/// </summary>
public sealed class GasBottle : Component, Component.ICollisionListener
{
	[Property] public string DisplayName { get; set; }
	[Property, ReadOnly] public GasStove Stove => GameObject?.Parent?.GetComponent<GasStove>();
	[Property, Sync( SyncFlags.FromHost )] public float GasAmount { get; set; } = 100f; // %

	// Keep track of which slot we occupy on the stove, if any
	[Property, ReadOnly] public int StoveSlot { get; private set; } = -1;


	/// <summary>Detach from current stove and re-enable physics.</summary>
	[Rpc.Host]
	public void Detach()
	{
		if ( Stove != null && Stove.IsValid() )
		{
			GasStove.HostRemoveGas( Stove, this );
		}

		StoveSlot = -1;

		// Unparent & drop slightly so it doesn’t clip
		GameObject.SetParent( null );
		WorldPosition += WorldRotation.Up * 2f;

		var rb = Components.Get<Rigidbody>( FindMode.InChildren );
		if ( rb != null )
		{
			rb.Enabled = true;
			rb.Velocity = default;
			rb.AngularVelocity = default;
		}
	}

	protected override void OnDestroy()
	{
		// Ensure cleanup on delete
		if ( Networking.IsHost && Stove != null ) Detach();
	}

	private void SnapToStoveSlot()
	{
		if ( Stove == null || StoveSlot < 0 ) return;

		// Parent to stove so it moves together
		GameObject.SetParent( Stove.GameObject, false );

		// Place at the slot’s transform
		//var slotWorld = Stove.GetSlotTransform( StoveSlot );
		//Transform.World = slotWorld;
	}
}
