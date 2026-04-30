using System;
using System.Collections.Generic;
using System.Text;

namespace OpenFramework.World;

public class Battery : Component
{
	[RequireComponent, Property]
	public ConnectionSwitch _ConnectionSwitch { get; set; }

	[Property, Sync(SyncFlags.FromHost)]
	public bool IsCharging { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public int MaxChargingCycle { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public int ChargingCycle { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public float CurrentCharge { get; set; }
	[Property] 
	public float IdleDrain { get; set; } = 0.01f;
	[Property] public float MaxCharge { get; set; } = 100f;

	protected override void OnUpdate()
	{
		// Une batterie perd toujours un tout petit peu d'énergie
		if ( CurrentCharge > 0 )
		{
			CurrentCharge -= IdleDrain * Time.Delta;
			CurrentCharge = MathF.Max( 0, CurrentCharge );
		}
	}

	/// <summary>
	/// Appelée par la gazinière pour tirer du jus
	/// </summary>
	public bool Consume( float amount )
	{
		if ( CurrentCharge <= 0 ) return false;

		CurrentCharge -= amount;
		CurrentCharge = MathF.Max( 0, CurrentCharge );
		return true;
	}
}
