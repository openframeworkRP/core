using Facepunch;
using Sandbox;

public sealed class VoiceDebugComponent : Component
{
	[Property] public PlayerVoiceComponent VoiceComp { get; set; }
	[Property] public ModelRenderer Sphere { get; set; }

	private float sphereDuration = 2f;
	private float _lastChangeTime;
	private PlayerVoiceComponent.VoiceMode lastMode;

	protected override void OnUpdate()
	{
		if ( VoiceComp == null || VoiceComp.Pawn == null || !VoiceComp.Pawn.IsValid() )
			return;

		if ( Sphere == null ) return;

		// --- LOGIQUE STRICTEMENT LOCALE ---
		// IsProxy est VRAI pour les autres joueurs.
		// Si c'est un autre joueur (Proxy), on éteint sa sphère sur notre écran.
		if ( IsProxy )
		{
			Sphere.Enabled = false;
			return;
		}

		// À partir d'ici, le code ne s'exécute QUE pour TOI sur TON personnage.
		UpdateVoiceVisuals();
	}

	public void UpdateVoiceVisuals()
	{
		if ( Sphere == null ) return;

		// Détection du changement de mode pour réinitialiser le timer
		if ( VoiceComp.VoiceType != lastMode )
		{
			lastMode = VoiceComp.VoiceType;
			_lastChangeTime = Time.Now;
		}

		float timeSinceChange = Time.Now - _lastChangeTime;

		if ( timeSinceChange < sphereDuration )
		{
			Sphere.Enabled = true;

			float distance = VoiceComp.GetMaxDistance();
			float baseScale = distance * 0.02f; // Ta base de calcul

			// --- AJOUT DE L'EFFET D'ONDE ---
			// Calcul de la progression (0 au début, 1 à la fin de sphereDuration)
			float progress = timeSinceChange / sphereDuration;

			// L'onde commence à 60% de sa taille et finit à 100% (expansion)
			// On utilise MathF.Pow(progress, 0.3f) pour que l'onde "jaillisse" très vite au début
			float waveExpansion = MathX.Lerp( 0.6f, 1.0f, MathF.Pow( progress, 0.6f ) );

			float finalScale = baseScale * waveExpansion;

			// On applique le nouveau scale avec l'expansion
			Sphere.WorldScale = new Vector3( finalScale, finalScale, 0.1f );

			// --- COULEUR ET FONDU ---
			// Fondu de transparence (Alpha)
			float lerpAlpha = MathX.Lerp( 0.5f, 0f, progress );

			Sphere.Tint = VoiceComp.VoiceType switch
			{
				PlayerVoiceComponent.VoiceMode.Chuchoter => Color.Green.WithAlpha( lerpAlpha ),
				PlayerVoiceComponent.VoiceMode.Normal => Color.Yellow.WithAlpha( lerpAlpha ),
				PlayerVoiceComponent.VoiceMode.Crie => Color.Red.WithAlpha( lerpAlpha ),
				_ => Color.White.WithAlpha( 0.2f * lerpAlpha )
			};

			// Positionnement aux pieds
			Sphere.WorldPosition = VoiceComp.Pawn.WorldPosition + Vector3.Up * 2f;
		}
		else
		{
			Sphere.Enabled = false;
		}
	}
}
