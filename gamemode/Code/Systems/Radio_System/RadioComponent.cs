using Sandbox;

namespace OpenFramework;

public sealed class RadioComponent : Component
{
	[Property, Sync] public bool IsActivate { get; set; } = false;

	[Property, Sync] public bool IsRecording { get; set; } = false;
	[Property, Sync, MinMax( 0, 100 )] public float Frequency { get; set; } = 1.0f;


	[Rpc.Broadcast]
	public void ToggleRadio()
	{
		// Seul le propriétaire ou le serveur valide le changement
		IsActivate = !IsActivate;
	}

	[Rpc.Broadcast]
	public void ChangeFrequency( float delta )
	{
		// On ajoute le delta à la fréquence actuelle
		Frequency = delta;

		// Si on dépasse le max, on revient au min
		if ( Frequency > 100f )
		{
			Frequency = 1f;
		}
		// Si on descend sous le min, on va au max
		else if ( Frequency < 1f )
		{
			Frequency = 100f;
		}
	}

	[Rpc.Broadcast]
	public void SetRecording( bool state )
	{
		// On s'assure que seul le propriétaire ou celui qui a le droit modifie la valeur synchronisée
		IsRecording = state;
	}

	// On peut ajouter un nom de canal pour l'UI
	public string FrequencyDisplay => IsActivate ? $"{Frequency:F1} MHz" : "OFF";
}
