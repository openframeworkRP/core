using Facepunch;
using OpenFramework.UI;

namespace OpenFramework.Systems.Pawn;

/// <summary>
/// A component that handles the state of the player's marker on the minimap and the HUD.
/// </summary>
public partial class PlayerMarker : Component, IMarkerObject, IDirectionalMinimapIcon
{
	/// <summary>
	/// The player.
	/// </summary>
	[RequireComponent] PlayerPawn Player { get; set; }

	/// <summary>
	/// An accessor to see if the player is alive or not.
	/// </summary>
	private bool IsAlive => Player.HealthComponent.State == LifeState.Alive;

	bool IMarkerObject.ShouldShow() => false;

	private Vector3 DistOffset
	{
		get
		{
			if ( !Scene.Camera.IsValid() ) return 0f;

			var dist = Scene.Camera.WorldPosition.DistanceSquared( WorldPosition );
			dist *= 0.00000225f;
			return Vector3.Up * dist;
		}
	}

	/// <summary>
	/// Where is the marker?
	/// </summary>
	Vector3 IMarkerObject.MarkerPosition => WorldPosition + Vector3.Up * 70f + DistOffset;

	/// <summary>
	/// What type of icon are we using on the minimap?
	/// </summary>
	string IMinimapIcon.IconPath
	{
		get
		{
			if ( !IsAlive ) return "ui/minimaps/icon-map_skull.png";
			return "ui/minimaps/player_icon.png";
		}
	}


	/// <summary>
	/// Is this a directional icon?
	/// </summary>
	bool IDirectionalMinimapIcon.EnableDirectional => IsAlive;

	/// <summary>
	/// What direction should we be facing? Surely this could be a float?
	/// </summary>
	Angles IDirectionalMinimapIcon.Direction => !IsAlive ? Angles.Zero : Player.EyeAngles;

	/// <summary>
	/// Defines a custom css style for this minimap icon.
	/// </summary>
	string ICustomMinimapIcon.CustomStyle
	{
		get
		{
			return $"background-image-tint: white";
		}
	}

	/// <summary>
	/// The minimap element's position in the world.
	/// </summary>
	Vector3 IMinimapElement.WorldPosition => IsMissing ? Player.Spottable.LastSeenPosition : WorldPosition;

	/// <summary>
	/// Did we spot this player recently?
	/// </summary>
	bool IsMissing => Player.Spottable.WasSpotted;

	/// <summary>
	/// Admin-only toggle to show all player icons on their minimap.
	/// </summary>
	public static bool ShowPlayersOnMinimap { get; set; } = false;

	/// <summary>
	/// Should we render this element at all?
	/// </summary>
	/// <param name="viewer"></param>
	/// <returns></returns>
	bool IMinimapElement.IsVisible( Pawn viewer )
	{
		if ( Player.HealthComponent.State != LifeState.Alive )
			return false;

		// Are we possessing this player?
		if ( Player.IsPossessed )
			return false;

		// Admin with minimap toggle enabled can see all players
		if ( ShowPlayersOnMinimap && ( viewer?.Client?.IsAdmin ?? false ) )
			return true;

		// Players don't see each other on the minimap
		return false;
	}
}
