using Sandbox;
using System.Numerics;

namespace OpenFramework.Systems.Jobs;

public  class HandcuffComponent : Component
{
	[Property, Sync] public bool IsCuffed { get; set; } = false;

	protected override void OnUpdate()
	{
		var player = GameObject.Components.Get<PlayerPawn>();
		if ( !player.IsValid() || !player.Body.IsValid() ) return;

		foreach ( var helper in player.Body.AnimationHelpers )
		{
			if ( !helper.IsValid() ) continue;

			if ( IsCuffed )
			{
				helper.CuffedAnimation();
				player.Body.ShowHandcuffModel( true );
				
			}
			else
			{
				helper.ClearIk( "hand_left" );
				helper.ClearIk( "hand_right" );
				player.Body.ShowHandcuffModel( false );
			}
		}
	}

}
