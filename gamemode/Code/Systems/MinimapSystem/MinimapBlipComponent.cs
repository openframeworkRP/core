using Facepunch;
using Sandbox;
using OpenFramework.Systems.Pawn;
namespace OpenFramework.Systems.MinimapSystem;

public enum BlipType
{
	Shop,
	Job,
	ATM,
	Vehicle,
	PointOfInterest,
	Custom
}


public sealed class MinimapBlipComponent : Component, IMinimapIcon, IMinimapLabel, ICustomMinimapIcon
{
	[Property, Group( "Blip" )] public Vector3 PositionOffset { get; set; } = Vector3.Zero;

	public new Vector3 WorldPosition => GameObject.WorldPosition + PositionOffset;

	[Property, Group( "Blip" )] public BlipType Type { get; set; } = BlipType.PointOfInterest;
	[Property, Group( "Blip" )] public string Label { get; set; } = "📍 Point of Interest";
	[Property, Group( "Blip" )] public Color LabelColor { get; set; } = Color.White;
	[Property, Group( "Blip" )] public bool ShowOnMinimap { get; set; } = true;
	[Property, Group( "Blip" )] public bool ShowOnBigMap { get; set; } = true;

	[Property, Group( "Icon" )] public int IconSize { get; set; } = 22;
	public int IconOrder => (int)Type;

	public string TypeEmoji => Type switch
	{
		BlipType.Shop => "🛒",
		BlipType.Job => "💼",
		BlipType.ATM => "💰",
		BlipType.Vehicle => "🚗",
		BlipType.PointOfInterest => "📍",
		BlipType.Custom => "⭐",
		_ => "📍"
	};
	public string FullLabel => $"{TypeEmoji} {Label}";
	// Pas d'image, un point coloré par type
	public string IconPath => "";
	public string CustomStyle => $"width: {IconSize}px; height: {IconSize}px;";

	public bool IsVisible( Pawn.Pawn viewer )
	{
		if ( viewer is not PlayerPawn player ) return false;
		if ( player.Client?.IsAdmin ?? false ) return true;
		return ShowOnMinimap;
	}

	public bool IsVisibleOnBigMap( PlayerPawn player )
	{
		if ( player.Client?.IsAdmin ?? false ) return true;
		return ShowOnBigMap;
	}
}
