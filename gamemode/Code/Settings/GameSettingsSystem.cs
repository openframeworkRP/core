using Sandbox;
using Sandbox.Audio;
using System;

namespace Facepunch;

public class GameSettings
{
	[Title( "Field Of View" ), Group( "Game" ), Range( 65, 110 )]
	public float FieldOfView { get; set; } = 90; // Ta nouvelle valeur par défaut

	[Title( "Master" ), Group( "Volume" ), Range( 0, 100 )]
	public float MasterVolume { get; set; } = 100;

	[Title( "Music" ), Group( "Volume" ), Range( 0, 100 )]
	public float MusicVolume { get; set; } = 100;

	[Title( "SFX" ), Group( "Volume" ), Range( 0, 100 )]
	public float SFXVolume { get; set; } = 100;

	[Title( "UI" ), Group( "Volume" ), Range( 0, 100 )]
	public float UIVolume { get; set; } = 100;

	[Title( "Radio" ), Group( "Volume" ), Range( 0, 100 )]
	public float RadioVolume { get; set; } = 100;

	[Title( "Voice" ), Group( "Volume" ), Range( 0, 100 )]
	public float VoiceVolume { get; set; } = 100;

	[Title( "View Bob" ), Group( "Game" ), Range( 0, 100 )]
	public float ViewBob { get; set; } = 100f;

	[Group( "Crosshair" )] public bool ShowCrosshairDot { get; set; } = true;
	[Group( "Crosshair" )] public bool DynamicCrosshair { get; set; } = true;
	[Group( "Crosshair" )] public float CrosshairLength { get; set; } = 15;
	[Group( "Crosshair" )] public float CrosshairWidth { get; set; } = 2;
	[Group( "Crosshair" )] public float CrosshairDistance { get; set; } = 15;
	[Group( "Crosshair" )] public Color CrosshairColor { get; set; } = Color.White;
}

public partial class GameSettingsSystem
{
	public static Action OnSettingsChanged { get; set; } // Événement pour le temps réel

	private static GameSettings current { get; set; }
	public static GameSettings Current
	{
		get
		{
			if ( current is null ) Load();
			return current;
		}
		set
		{
			current = value;
			OnSettingsChanged?.Invoke();
		}
	}

	public static string FilePath => "gamesettings.json";

	public static void Save()
	{
		Mixer.Master.Volume = Current.MasterVolume / 100;
		var channel = Mixer.Master.GetChildren();
		channel[0].Volume = Current.MusicVolume / 100;
		channel[1].Volume = Current.SFXVolume / 100;
		channel[2].Volume = Current.UIVolume / 100;
		channel[3].Volume = Current.RadioVolume / 100;
		channel[4].Volume = Current.VoiceVolume / 100;

		FileSystem.Data.WriteJson( FilePath, Current );
	}

	public static void Load()
	{
		Current = FileSystem.Data.ReadJson<GameSettings>( FilePath, new() );
	}

	[ConCmd( "settings_reset")]
	public static void ResetToDefault()
	{
		Current = new GameSettings(); // Reprend le 110 par défaut du code
		//Save();
		Log.Info( "Settings reset to defaults (FOV 110)!" );
	}
}
