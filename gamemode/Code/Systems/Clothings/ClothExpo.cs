using Sandbox;
using OpenFramework.Inventory;
using System;

namespace OpenFramework.Systems.Clothings;

public class ClothExpo : Component, Component.ITriggerListener
{
	[Property] public ItemMetadata ClothingsItems { get; set; }
	[Property] public bool IsSteal { get; set; } = false;

	public static ClothExpo CurrentActive { get; private set; }

	private bool _isPlayerNear = false;
	private RealTimeSince _lastTrace = 0;
	public GameObject _hoveredObject { get; private set; }

	// Variable critique pour empêcher le reset de couleur
	public Color _activeTint = Color.White;

	protected override void OnStart()
	{
		ExtractModelFromPrefabFile();
	}

	/// <summary>
	/// Extrait le modèle du prefab et restaure la couleur active.
	/// </summary>
	public void ExtractModelFromPrefabFile()
	{
		if ( ClothingsItems?.WorldObjectPrefab == null ) return;

		var prefabScene = SceneUtility.GetPrefabScene( ClothingsItems.WorldObjectPrefab );
		if ( !prefabScene.IsValid() ) return;

		var sourceRenderer = prefabScene.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndChildren );
		var localRenderer = Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndChildren );

		if ( sourceRenderer.IsValid() && localRenderer.IsValid() )
		{
			// 1. On ne fait rien si le modèle est déjà le bon
			if ( localRenderer.Model == sourceRenderer.Model ) return;
			
			// 3. On applique le nouveau modèle
			localRenderer.Model = sourceRenderer.Model;

			// 4. On ré-applique la couleur immédiatement pour éviter le reset au blanc
			sourceRenderer.Tint = _activeTint;

			Log.Info( $"[ClothExpo] Modèle mis à jour sans doublon." );
		}
	}

	public void ApplyTint( Color color )
	{
		_activeTint = color;

		// On cherche le composant qui gère le visuel
		var visuals = Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndChildren );

		if ( visuals.IsValid() )
		{
			// On met à jour la couleur synchronisée
			visuals.CurrentColor = color;
			// Et on propage via RPC pour les autres
			visuals.UpdateColor( color );
		}
	}

	protected override void OnUpdate()
	{
		if ( !_isPlayerNear ) return;

		if ( _lastTrace > 0.1f )
		{
			UpdateLooking();
			_lastTrace = 0;
		}

		if ( Input.Pressed( "attack1" ) && _hoveredObject.IsValid() )
		{
			Log.Info( "Interaction ClothExpo" );
		}
	}

	private void UpdateLooking()
	{
		var ray = Scene.Camera.ScreenNormalToRay( 0.5f );
		var tr = Scene.Trace.Ray( ray, 150f ).Size( 5f ).WithTag( "clothexpo" ).Run();

		if ( tr.Hit && tr.GameObject.IsValid() )
		{
			if ( _hoveredObject != tr.GameObject )
			{
				SetHoverEffect( _hoveredObject, false, Color.White );
				_hoveredObject = tr.GameObject;
				SetHoverEffect( _hoveredObject, true, Color.Green );

				// On définit l'instance active pour l'UI Razor
				CurrentActive = this;
			}
		}
		else if ( _hoveredObject.IsValid() )
		{
			SetHoverEffect( _hoveredObject, false, Color.White );
			_hoveredObject = null;
			if ( CurrentActive == this ) CurrentActive = null;
		}
	}

	public void SetHoverEffect( GameObject obj, bool active, Color color )
	{
		if ( !obj.IsValid() ) return;
		var visuals = obj.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndAncestors );
		if ( visuals.IsValid() ) visuals.SetHover( active, color );
	}

	public void OnTriggerEnter( Collider other ) { if ( other.GameObject.Tags.Has( "player" ) ) _isPlayerNear = true; }
	public void OnTriggerExit( Collider other ) 
	{ 
		if ( other.GameObject.Tags.Has( "player" ) )
			SetHoverEffect( _hoveredObject, false, Color.White );
		_isPlayerNear = false;
			_hoveredObject = null;
			if ( CurrentActive == this ) CurrentActive = null;
	}
}
