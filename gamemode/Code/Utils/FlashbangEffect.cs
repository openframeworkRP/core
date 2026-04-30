using Facepunch.UI;
using Sandbox.Audio;
using Sandbox.Rendering;
using Sandbox.Utility;

namespace OpenFramework.Utility;

public class FlashbangEffect : Component
{
	[Property] public SoundEvent SoundEffect { get; set; }
	[Property] public float LifeTime { get; set; }

	private TimeUntil TimeUntilEnd { get; set; }
	private FlashbangOverlay Overlay { get; set; }
	private ChromaticAberration Aberration { get; set; }
	private CameraComponent Camera { get; set; }
	private Bloom Bloom { get; set; }

	private DspProcessor DspProcessor { get; set; }
	private SoundHandle Sound { get; set; }
	private float RenderAlpha { get; set; }

	// ── Nouveau système ───────────────────────────
	private CommandList _commandList;
	//private bool _freezeFrameGrabbed = false;

	protected override void OnEnabled()
	{
		Bloom ??= AddComponent<Bloom>();
		Overlay ??= AddComponent<FlashbangOverlay>();
		Aberration ??= AddComponent<ChromaticAberration>();
		Camera ??= AddComponent<CameraComponent>();
		DspProcessor ??= new( "weird.4" );

		Mixer.Master.AddProcessor( DspProcessor );

		base.OnEnabled();
	}

	protected override void OnDisabled()
	{
		if ( Sound.IsValid() )
			Sound.Volume = 0f;

		Mixer.Master.RemoveProcessor( DspProcessor );

		base.OnDisabled();
	}

	protected override void OnDestroy()
	{
		// Détache et nettoie le CommandList
		if ( _commandList != null && Camera != null )
		{
			Camera.RemoveCommandList( _commandList, Stage.AfterTransparent );
			_commandList = null;
		}

		if ( Bloom.IsValid() ) Bloom.Destroy();
		if ( Overlay.IsValid() ) Overlay.Destroy();
		if ( Aberration.IsValid() ) Aberration.Destroy();

		Sound?.Stop();
	}

	protected override void OnStart()
	{
		TimeUntilEnd = LifeTime;

		Bloom.Mode = SceneCamera.BloomAccessor.BloomMode.Screen;
		Bloom.Threshold = 0f;
		Bloom.Strength = 10f;

		Aberration.Scale = 1f;
		Aberration.Offset = new( 6f, 10f, 3f );

		Overlay.EndTime = LifeTime * 0.6f;

		if ( SoundEffect is not null )
		{
			Sound = Sandbox.Sound.Play( SoundEffect );
			Sound.Volume = 1f;
		}

		RenderAlpha = 1f;
		//_freezeFrameGrabbed = false;

		// Crée le CommandList et l'attache à la caméra
		_commandList = new CommandList( "Flashbang" );
		BuildCommandList();
		Camera.AddCommandList( _commandList, Stage.AfterTransparent, 101 );

		base.OnStart();
	}

	protected override void OnUpdate()
	{
		var f = TimeUntilEnd.Relative / LifeTime;
		Aberration.Scale = f;
		Bloom.Strength = 10f * f;
		DspProcessor.Mix = f;

		if ( Sound.IsValid() )
			Sound.Volume = f;

		RenderAlpha = Easing.EaseOut( f );

		// Met à jour l'alpha dans le CommandList chaque frame
		if ( _commandList != null )
			_commandList.Attributes.Set( "FlashAlpha", RenderAlpha );

		base.OnUpdate();
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( TimeUntilEnd )
			Destroy();
	}

	// ─────────────────────────────────────────────
	//  Construction du CommandList
	// ─────────────────────────────────────────────

	private void BuildCommandList()
	{
		_commandList.Reset();

		// 1. Première frame : on grab le framebuffer comme freeze-frame
		//    GrabFrameTexture stocke la texture dans les attributes du CommandList
		//    sous le nom "Flashbang", accessible via Attributes.Set dans le Blit
		var frameHandle = _commandList.Attributes.GrabFrameTexture( "Flashbang" );

		// 2. On dessine le freeze-frame par-dessus avec l'alpha courant
		//    Le shader Material.UI.Basic lit "Texture" dans les attributes
		_commandList.Attributes.Set( "Texture", frameHandle.ColorTexture );
		_commandList.Attributes.Set( "LayerMat", Matrix.Identity );
		_commandList.Attributes.SetCombo( "D_BLENDMODE", BlendMode.Normal );

		// DrawScreenQuad utilise Graphics.Viewport comme rect → plein écran
		// La couleur contient l'alpha via FlashAlpha (mis à jour dans OnUpdate)
		_commandList.DrawScreenQuad( Material.UI.Basic, Color.White );
	}
}
