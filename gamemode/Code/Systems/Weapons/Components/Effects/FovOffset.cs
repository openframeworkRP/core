using Facepunch;
using Sandbox.Events;
using OpenFramework.Systems.Weapons;
using OpenFramework.Utility;

namespace OpenFramework.Systems.Weapons.Effects;

[Title( "On Shot - FOV Offset" ), Icon( "pending" ), Group( "Weapon Components" )]
public class FovOffset : EquipmentComponent, IGameEventHandler<WeaponShotEvent>
{
	[Property] public float Length { get; set; } = 0.3f;
	[Property] public float Size { get; set; } = 1.05f;
	[Property] public Curve Curve { get; set; }

	void IGameEventHandler<WeaponShotEvent>.OnGameEvent( WeaponShotEvent eventArgs )
	{
		var shake = new ScreenShake.Fov( Length, Size, Curve );
		ScreenShaker.Main?.Add( shake );
	}
}
