using Sandbox.Rendering;

namespace OpenFramework.Systems.Weapons;

[Title( "2D Scope" ), Group( "Weapon Components" )]
public class FlatScope : WeaponInputAction
{
	[Property] public Material ScopeOverlay { get; set; }
	[Property] public SoundEvent ZoomSound { get; set; }
	[Property] public SoundEvent UnzoomSound { get; set; }

	private int ZoomLevel { get; set; } = 0;
	public bool IsZooming => ZoomLevel > 0;
	private float BlurLerp { get; set; } = 1.0f;

	private Angles LastAngles;
	private Angles AnglesLerp;
	[Property] private float AngleOffsetScale { get; set; } = 0.01f;
	[Property] public List<int> ZoomLevels { get; set; } = new();

	// ── CommandList persistant ────────────────────
	private CommandList _scopeCommandList;
	private CameraComponent _camera;

	protected void StartZoom( int level = 0 )
	{
		StopZoom();

		if ( !Equipment.IsValid() || !Equipment.Owner.IsValid() )
			return;

		_camera = Equipment.Owner.CameraController?.Camera;
		if ( _camera == null ) return;

		if ( ScopeOverlay != null )
		{
			// Crée le CommandList une seule fois
			_scopeCommandList = new CommandList( "FlatScope" );

			// Blit avec les attributes locaux du CommandList
			// Les valeurs dynamiques (BlurLerp, Offset) seront mises à jour
			// chaque frame via _scopeCommandList.Attributes.Set(...)
			_scopeCommandList.Blit( ScopeOverlay, null );

			// Stage.AfterTransparent = équivalent de l'ancien AddHookAfterTransparent
			_camera.AddCommandList( _scopeCommandList, Stage.AfterTransparent, 100 );
		}

		if ( ZoomSound != null )
			Sound.Play( ZoomSound, Equipment.GameObject.WorldPosition );

		ZoomLevel = level;
		Equipment.SetTag( "aiming", true );
	}

	protected void StopZoom()
	{
		if ( _scopeCommandList != null && _camera != null )
		{
			_camera.RemoveCommandList( _scopeCommandList, Stage.AfterTransparent );
			_scopeCommandList = null;
		}

		_camera = null;
	}

	protected void EndZoom()
	{
		StopZoom();

		if ( ZoomLevel != 0 && UnzoomSound != null && Equipment.IsValid() )
			Sound.Play( UnzoomSound, Equipment.GameObject.WorldPosition );

		ZoomLevel = 0;
		AnglesLerp = new Angles();
		BlurLerp = 1.0f;
		Equipment.SetTag( "aiming", false );
	}

	// ─────────────────────────────────────────────
	//  Input
	// ─────────────────────────────────────────────

	protected override void OnInputDown()
	{
		if ( ZoomLevel < ZoomLevels.Count )
			StartZoom( ZoomLevel + 1 );
		else
			EndZoom();
	}

	protected virtual bool CanAim()
	{
		if ( Tags.Has( "reloading" ) ) return false;
		return true;
	}

	// ─────────────────────────────────────────────
	//  Lifecycle
	// ─────────────────────────────────────────────

	protected override void OnDisabled()
	{
		base.OnDisabled();
		EndZoom();
	}

	protected override void OnEnabled()
	{
		BindTag( "scoped", () => IsZooming );
	}

	protected override void OnParentChanged( GameObject oldParent, GameObject newParent )
	{
		base.OnParentChanged( oldParent, newParent );
		EndZoom();
	}

	// ─────────────────────────────────────────────
	//  Update
	// ─────────────────────────────────────────────

	public float GetFOV()
	{
		if ( ZoomLevel < 1 ) return 0f;
		return ZoomLevels[Math.Clamp( ZoomLevel - 1, 0, ZoomLevels.Count - 1 )];
	}

	protected override void OnUpdate()
	{
		if ( !IsZooming ) return;

		var camera = Equipment?.Owner?.CameraController;
		if ( !camera.IsValid() )
			return;

		if ( !CanAim() || Equipment.Owner.CurrentEquipment != Equipment )
		{
			EndZoom();
			return;
		}

		Equipment.Owner.AimDampening /= ZoomLevel * ZoomLevel + 1;

		var cc = Equipment.Owner.CharacterController;
		float velocity = cc.Velocity.Length / 25.0f;
		float blur = (1.0f / (velocity + 1.0f)).Clamp( 0.1f, 1.0f );

		if ( !cc.IsOnGround ) blur = 0.1f;

		BlurLerp = BlurLerp.LerpTo( blur, Time.Delta * (blur > BlurLerp ? 1.0f : 10.0f) );

		var angles = Equipment.Owner.EyeAngles;
		var delta = angles - LastAngles;
		AnglesLerp = AnglesLerp.LerpTo( delta, Time.Delta * 10.0f );
		LastAngles = angles;

		// Met à jour les attributs du CommandList chaque frame
		// Ces valeurs seront lues lors du Blit au moment du rendu
		if ( _scopeCommandList != null )
		{
			_scopeCommandList.Attributes.Set( "BlurAmount", BlurLerp );
			_scopeCommandList.Attributes.Set( "Offset", new Vector2( AnglesLerp.yaw, -AnglesLerp.pitch ) * AngleOffsetScale );
		}
	}
}
