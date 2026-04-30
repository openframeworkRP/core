using Facepunch;
using Sandbox.Events;
using static Facepunch.NotificationSystem;

namespace Sandbox.WeatherSystem;

public class WeatherSystem : Component
{
	public enum WeatherType
	{
		Sunny,
		Cloudy,
		Night,
		Apocalypse
	}

	public enum WeatherFogType
	{
		Fog,
		ClearFog,
	}

	[Property, Sync( SyncFlags.FromHost ), Change( "OnCurrentWeatherChange" )]
	public WeatherType CurrentWeather { get; set; } = WeatherType.Sunny;

	[Property, Sync( SyncFlags.FromHost ), Change( "OnCurrentWeatherFogChange" )]
	public WeatherFogType CurrentWeatherFog { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public int Day { get; set; } = 1;

	[Property, Sync( SyncFlags.FromHost )]
	public RealTimeUntil EachSecond { get; set; }

	// ── Horloge syncée (host → clients) ──────────────────────────────
	[Property, Sync( SyncFlags.FromHost )]
	public float Hour1 { get; set; } = 0;

	[Property, Sync( SyncFlags.FromHost )]
	public float Hour2 { get; set; } = 8;

	[Property, Sync( SyncFlags.FromHost )]
	public float Minute1 { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public float Minute2 { get; set; }

	[Property]
	public GameObject FogPrefab { get; set; }

	[Property]
	public GameObject ClearFogPrefab { get; set; }

	[Property]
	public GameObject CloudyPrefab { get; set; }

	[Property]
	public GameObject SunnyPrefab { get; set; }

	[Property]
	public GameObject ApocalypsePrefab { get; set; }

	// DirectionalLight supprimé ici — géré par DayNightCycle

	public GameObject currentWeatherPrefab { get; set; }
	public GameObject currentDayPrefab { get; set; }

	private GameObject currentWeatherInstance;
	private GameObject currentWeatherFogInstance;

	public void OnCurrentWeatherChange( WeatherType oldValue, WeatherType newValue )
	{
		currentWeatherInstance?.Destroy();

		var prefab = newValue switch
		{
			WeatherType.Cloudy => CloudyPrefab,
			WeatherType.Sunny => SunnyPrefab,
			WeatherType.Apocalypse => ApocalypsePrefab,
			_ => null
		};

		if ( prefab != null )
		{
			currentWeatherInstance = prefab.Clone( GameObject.WorldTransform );
			currentWeatherInstance.SetParent( GameObject, keepWorldPosition: true );
		}
	}

	public void OnCurrentWeatherFogChange( WeatherFogType oldValue, WeatherFogType newValue )
	{
		currentWeatherFogInstance?.Destroy();

		var prefab = newValue switch
		{
			WeatherFogType.Fog => FogPrefab,
			WeatherFogType.ClearFog => ClearFogPrefab,
			_ => null
		};

		if ( prefab != null )
		{
			currentWeatherFogInstance = prefab.Clone( GameObject.WorldTransform );
			currentWeatherFogInstance.SetParent( GameObject, keepWorldPosition: true );
		}
	}

	protected override void OnAwake()
	{
		if ( !Networking.IsHost ) return;
		EachSecond = 1;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		if ( !EachSecond ) return;

		EachSecond = 0.2;

		Minute2++;

		if ( Minute2 > 9 )
		{
			Minute2 = 0;
			Minute1++;

			if ( Minute1 > 5 )
			{
				Minute1 = 0;
				Hour2++;

				if ( Hour2 > 9 )
				{
					Hour2 = 0;
					Hour1++;
				}
			}
		}

		// Minuit : 24h00 → 00h00
		if ( Hour1 >= 2 && Hour2 >= 4 && Minute1 == 0 && Minute2 == 0 )
		{
			Hour1 = 0;
			Hour2 = 0;
			Minute1 = 0;
			Minute2 = 0;
			Day++;
			OnNewDay();
		}

		// La rotation du soleil est gérée par DayNightCycle qui lit
		// Hour1/Hour2/Minute1/Minute2 en temps réel sur tous les clients.
	}

	public string GetWeatherIcon()
	{
		return CurrentWeather switch
		{
			WeatherType.Sunny => "ui/weather/sunny.svg",
			WeatherType.Cloudy => "ui/weather/cloudy.svg",
			WeatherType.Night => "ui/weather/night.svg",
			WeatherType.Apocalypse => "weather/apocalypse.svg",
			_ => "sun"
		};
	}

	public string GetFormattedTime()
	{
		return $"{Hour1}{Hour2}:{Minute1}{Minute2}";
	}

	public void OnNewDay()
	{
		Scene.Dispatch( new OnNewDayEvent() );
		//GameUtils.AllPlayers.ToList().ForEach( p => p.Notify( NotificationType.Info, "Nouveau Jour" ) );
	}
}
