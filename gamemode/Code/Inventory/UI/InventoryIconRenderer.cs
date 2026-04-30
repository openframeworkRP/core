using System;
using Sandbox;

namespace OpenFramework.Inventory.UI;

/// <summary>
/// Genere a runtime des icones (Texture ou bytes PNG) a partir d'un Model,
/// en utilisant CameraComponent.RenderToBitmap() dans une scene transient.
/// Inspire de l'editor tool OSS-IconsEditor (Lone-Pine-inc), adapte runtime.
///
/// Usage :
///   var tex = InventoryIconRenderer.Render(model, 128);
///   var png = InventoryIconRenderer.RenderToPngBytes(model, 128);
///
/// IMPORTANT : a appeler uniquement client-side (jamais sur serveur dedie).
/// </summary>
public static class InventoryIconRenderer
{
	/// <summary>
	/// Rend une icone du model fourni et retourne la Texture associee.
	/// La scene transient est creee, utilisee et detruite immediatement.
	/// Retourne null en cas d'erreur (model invalide, bounds vides, etc.).
	/// </summary>
	public static Texture Render( Model model, int size = 128 )
	{
		var bitmap = RenderToBitmap( model, size );
		return bitmap?.ToTexture( false );
	}

	/// <summary>
	/// Rend une icone du model et retourne directement les bytes PNG, prets
	/// a etre ecrits sur disque.
	/// </summary>
	public static byte[] RenderToPngBytes( Model model, int size = 128 )
	{
		var bitmap = RenderToBitmap( model, size );
		return bitmap?.ToPng();
	}

	/// <summary>
	/// Rend une icone du model dans une scene transient. La scene est detruite
	/// avant le retour. Retourne le Bitmap obtenu (null si echec).
	/// </summary>
	private static Bitmap RenderToBitmap( Model model, int size )
	{
		if ( model == null || !model.IsValid )
			return null;

		Scene scene = null;
		try
		{
			scene = new Scene();
			scene.StartLoading();

			// ── Lights : key warm + fill cool + ambiant ──
			var sunGo = new GameObject( scene );
			sunGo.WorldRotation = Rotation.From( 50, 45, 0 );
			var sun = sunGo.Components.Create<DirectionalLight>();
			sun.LightColor = Color.White * 0.85f;
			sun.Shadows = false;

			var fillGo = new GameObject( scene );
			fillGo.WorldRotation = Rotation.From( 30, -135, 0 );
			var fill = fillGo.Components.Create<DirectionalLight>();
			fill.LightColor = new Color( 0.5f, 0.7f, 1f ) * 0.25f;
			fill.Shadows = false;

			var ambientGo = new GameObject( scene );
			var ambient = ambientGo.Components.Create<AmbientLight>();
			ambient.Color = Color.White * 0.3f;

			// ── Modele a rendre ──
			var modelGo = new GameObject( scene );
			var renderer = modelGo.Components.Create<ModelRenderer>();
			renderer.Model = model;

			// ── Camera + cadrage automatique sur les bounds du model ──
			var camGo = new GameObject( scene );
			var cam = camGo.Components.Create<CameraComponent>();
			cam.FieldOfView = 35f;
			cam.ZNear = 1f;
			cam.ZFar = 10000f;
			cam.EnablePostProcessing = false;
			cam.AutoExposure.Enabled = false;
			cam.BackgroundColor = Color.Transparent;

			var bounds = model.RenderBounds;
			var center = bounds.Center;
			var radius = bounds.Size.Length * 0.5f;
			var fovRad = cam.FieldOfView * MathF.PI / 180f;
			var distance = radius / MathF.Tan( fovRad * 0.5f ) * 1.6f;
			if ( distance < 10f ) distance = 50f;

			// Vue 3/4 (yaw 200° pitch 15°), meme angle que le rendu hover
			var camRot = Rotation.From( 15, 200, 0 );
			camGo.WorldRotation = camRot;
			camGo.WorldPosition = center - camRot.Forward * distance;

			// Capture
			var bitmap = new Bitmap( size, size );
			cam.RenderToBitmap( bitmap );
			return bitmap;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[InventoryIconRenderer] Render failed for {model?.Name}: {ex.Message}" );
			return null;
		}
		finally
		{
			scene?.Destroy();
		}
	}
}
