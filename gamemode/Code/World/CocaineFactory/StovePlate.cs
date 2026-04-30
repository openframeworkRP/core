using Sandbox.Internal;

namespace OpenFramework.World.CocaineFactory;

[Category( "RealityON" )]
public sealed class StovePlatePart : Component
{
}

// =====================
// StovePlate Component
// =====================
[Category( "RealityON" )]
public sealed class StovePlate : Component, Component.ICollisionListener
{
	public enum FireLevel
	{
		Off = 0,
		Small,
		Elevate
	}

	[Property, Sync( SyncFlags.FromHost )] public int Index { get; set; }
	[Property] public bool On => Flame != FireLevel.Off;
	[Property, Sync( SyncFlags.FromHost )] public int Heat { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public GameObject Pot { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public GameObject PotAttachment { get; private set; }

	private FireLevel _flame;
	[Property, Sync( SyncFlags.FromHost )] 
	public FireLevel Flame
	{
		get => _flame;
		set
		{
			if ( Stove == null ) return;
			if ( !Draw ) return;

			_flame = value;

			if( value == FireLevel.Small )
				Stove?.ModelRenderer.SetBodyGroup( $"{BodyGroupName}_fire", 1 );
			else if(  value == FireLevel.Elevate )
				Stove?.ModelRenderer.SetBodyGroup( $"{BodyGroupName}_fire", 2 );
			else
				Stove?.ModelRenderer.SetBodyGroup( $"{BodyGroupName}_fire", 0 );
		}
	}

	// New: Draw flag controls the plate bodygroup visibility/active look
	private bool _draw;
	[Property, Sync( SyncFlags.FromHost )]
	public bool Draw
	{
		get => _draw;
		set
		{
			if ( Stove == null ) return;

			_draw = value;
			ApplyBodyGroup( Stove?.ModelRenderer );
		}
	}

	// New: Draw flag controls the plate bodygroup visibility/active look
	private bool _thermostat;
	[Property, Sync( SyncFlags.FromHost )]
	public bool DisplayThermostat
	{
		get => _thermostat;
		set
		{
			if ( Stove == null ) return;
			if ( !Draw ) return;
			if ( Pot == null || !Pot.IsValid() ) return;

			_thermostat = value;
			Stove?.ModelRenderer?.SetBodyGroup( $"thermo_{Index}", (value ? 1 : 0) );
		}
	}

	[Property] public string BodyGroupName { get; set; } = $"plate_1";
	[Property] public Vector3 LocalPotAnchor { get; set; } = Vector3.Zero;

	/// Référence au poêle parent pour consommer le gaz et constants.
	[Property] public GasStove Stove { get; set; }

	public void ApplyBodyGroup( ModelRenderer mr )
	{
		mr?.SetBodyGroup( $"{BodyGroupName}_cover", Draw ? 1 : 0 );
		mr?.SetBodyGroup( BodyGroupName, Draw ? 1 : 0 );
	}

    protected override void OnAwake()
    {
		PotAttachment = Stove?.ModelRenderer?.GetAttachmentObject($"plate_{Index}_pot");
		Log.Info( $"plate_{Index}_pot" );
		Log.Info( Stove?.ModelRenderer?.GetAttachmentObject( $"plate_{Index}_pot" ) );
    }

	protected override void OnUpdate()
	{
		if ( IsProxy || Stove == null ) return;


		/*float dt = Time.Delta;
		// Chauffe si allumée ET gaz dispo
		if ( On && Stove.GasAmount > 0 )
		{
			int delta = (int)MathF.Round( Stove.HeatStepEverySec * dt );
			if ( delta != 0 ) Heat = Math.Min( 255, Heat + delta );
			// Consommer le gaz (granulaire)
			Stove.GasAmount = Math.Max( 0, Stove.GasAmount - (int)MathF.Round( Stove.GasUsePerSecPlate * dt ) );
		}
		else if ( !On && Heat > 0 )
		{
			int cool = (int)MathF.Round( Stove.CoolStepEverySec * dt );
			if ( cool != 0 ) Heat = Math.Max( 0, Heat - cool );
		}


		// Ensure the visual matches the Draw flag each frame
		ApplyBodyGroup( Stove?.ModelRenderer );*/
	}

	public void OnCollisionStart( Collision collision )
	{
		var obj = collision.Other.GameObject;
		var collider = collision.Self.GameObject;

		Log.Info( $"CollisionStart with {obj}" );
		Log.Info( $"CollisionStart with {collider}" );

		if( obj == GameObject )
		{
			Pot = obj;
		}
	}
}
