using Sandbox;
using System.Linq;
using System.Numerics;

namespace OpenFramework;

public sealed class ShopDressingRoom : Component, Component.ITriggerListener
{
	public List<ClothingContainer.ClothingEntry> OriginalClothing;
	[Property] public bool IsPlayerInside { get;  set; } = false;
	[Property] public CameraComponent CameraDressing { get; set; }


	[Property] public ScreenPanel ScreenPanelDressing { get; set; }
	/// <summary>
	/// Crée un GameObject vide au centre de ta cabine et glisse-le ici.
	/// </summary>
	[Property] public GameObject PlayerAnchor { get; set; }

	protected override void OnStart()
	{
		if ( CameraDressing.IsValid() )
		{
			CameraDressing.Enabled = true;
			CameraDressing.Priority = -1;
			
		}
	}

	protected override void OnUpdate()
	{
		if ( IsPlayerInside )
		{
			Input.ReleaseActions();
			Input.AnalogMove = Vector2.Zero;
			Mouse.Visibility = MouseVisibility.Visible;
			// 2. On bloque la rotation (Souris)
			// C'est cette ligne qui empêchera ton perso de tourner sur lui-même !
			Input.AnalogLook = Angles.Zero;
		}
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var player = other.GameObject.Components.GetInAncestors<PlayerPawn>();

		// On ne déclenche la logique QUE pour le joueur local
		if ( !player.IsValid() || player.IsProxy ) return;
		ScreenPanelDressing.Enabled = true;
		ScreenPanelDressing.ZIndex = 200;

		var dresser = player.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		if ( dresser.IsValid() )
		{
			// On mémorise la tenue AVANT l'essayage
			OriginalClothing = dresser.Clothing.ToList();
		}

		IsPlayerInside = true;

		if ( PlayerAnchor.IsValid() )
		{
			player.WorldPosition = PlayerAnchor.WorldPosition;
			player.WorldRotation = PlayerAnchor.WorldRotation;
		}

		// On utilise Client.Local pour piloter la caméra
		ApplyCameraSettings( true );
	}
	
	void ITriggerListener.OnTriggerExit( Collider other )
	{
		var player = other.GameObject.Components.GetInAncestors<PlayerPawn>();
		if ( !player.IsValid() || player.IsProxy ) return;
		/*
		var dresser = player.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		if ( dresser.IsValid() && OriginalClothing != null )
		{
			// On remet ses habits d'origine
			dresser.Clothing = OriginalClothing;
			dresser.Apply();
		}
		*/

		ScreenPanelDressing.Enabled = false;
		ScreenPanelDressing.ZIndex = 0;
		IsPlayerInside = false;
		ApplyCameraSettings( false );
	}
	


	public void ApplyCameraSettings( bool state )
	{
		if ( !CameraDressing.IsValid() ) return;

		// Accès direct via Client.Local comme tu l'as fait pour ton switch 1P/3P
		var localPlayer = Client.Local.PlayerPawn;
		if ( !localPlayer.IsValid() ) return;

		if ( state )
		{
			CameraDressing.Priority = 100;
			
		}
		else
		{
			CameraDressing.Priority = -1;
			
		}

		Log.Info( $"Dressing Room : {state}" );
	}
}
