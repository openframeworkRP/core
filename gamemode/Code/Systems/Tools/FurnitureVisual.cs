using Sandbox;

namespace OpenFramework;

public sealed class FurnitureVisual : Component
{
	[Property,Sync] public ModelRenderer Renderer { get; set; }
	[Property] public HighlightOutline Outline { get; set; }
	[Property] public Rigidbody Rb { get; set; }
	[Property, Sync] public bool IsLocked { get; set; } = false;

	/// <summary>
	/// SteamId du joueur qui a place ce meuble (0 si meuble de map, pose par
	/// un systeme, ou pre-existant). Utilise par PlacedPropsCleanup pour
	/// supprimer les props d'un joueur deconnecte > 5 min.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )] public ulong PlacedBySteamId { get; set; } = 0;

	/// <summary>
	/// Verrou de propriete : tant que true, seul le placeur (PlacedBySteamId)
	/// peut deplacer / ramasser / fixer / interagir avec ce meuble. Mis a true
	/// par defaut au placement par PropPlacer. Le proprietaire peut basculer
	/// cet etat via RPC_SetFurnitureOwnerLock pour autoriser les autres.
	/// Distinct de IsLocked qui ne gere que le freeze physique du Rigidbody.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )] public bool OwnerLocked { get; set; } = false;

	/// <summary>True si le client est le proprietaire de ce meuble.</summary>
	public bool IsOwnedBy( Client client )
	{
		if ( PlacedBySteamId == 0 ) return false;
		if ( client == null ) return false;
		return client.SteamId == PlacedBySteamId;
	}

	/// <summary>
	/// True si le client a le droit de manipuler/interagir avec ce meuble.
	/// Meuble de map (PlacedBySteamId=0) → tout le monde.
	/// OwnerLocked=false → tout le monde.
	/// OwnerLocked=true → uniquement le proprietaire.
	/// </summary>
	public bool CanBeManipulatedBy( Client client )
	{
		if ( PlacedBySteamId == 0 ) return true;
		if ( !OwnerLocked ) return true;
		return IsOwnedBy( client );
	}

	private bool _lastLockedState;

	// On synchronise la couleur pour que tout le monde ait la m�me
	[Sync] public Color CurrentColor { get; set; }

	protected override void OnStart()
	{
		// Tag requis pour que la trace ActionMenu (touche E) détecte ce meuble
		GameObject.Tags.Add( "furniture" );

		// 1. Initialisation de la couleur
		if ( Renderer.IsValid() )
		{
			CurrentColor = Renderer.Tint;
		}

		// 2. D�tachement de l'outline du r�seau pour le survol local
		if ( Outline.IsValid() )
		{
			Outline.Enabled = false;
		}
		_lastLockedState = IsLocked;
		if ( Rb.IsValid() ) Rb.MotionEnabled = !IsLocked;
	}

	protected override void OnUpdate()
	{
		// On applique en permanence la couleur synchronis�e au rendu
		if ( Renderer.IsValid() )
		{
			Renderer.Tint = CurrentColor;
		}


		if ( Rb.IsValid() && IsLocked != _lastLockedState )
		{
			Rb.MotionEnabled = !IsLocked;
			_lastLockedState = IsLocked;

			if ( !IsLocked ) Rb.Sleeping = false; // R�veil forc�
		}
	}

	/// <summary>
	/// Appel r�seau pour changer la couleur (diffus� � tous les clients)
	/// </summary>
	[Rpc.Broadcast]
	public void UpdateColor( Color newColor )
	{
		CurrentColor = newColor;
	}

	/// <summary>
	/// Contr�le de l'outline (Local uniquement)
	/// </summary>
	public void SetHover( bool active, Color color )
	{
		if ( !Outline.IsValid() ) return;
		Outline.Enabled = active;
		Outline.Color = color;
	}


	/// <summary>
	/// Applique le freeze sur tous les clients
	/// </summary>
	[Rpc.Broadcast]
	public void UpdateFreeze( bool lockState )
	{
		IsLocked = lockState;

		if ( Rb.IsValid() )
		{
			// On d�sactive la physique si Locked
			Rb.MotionEnabled = !lockState;

			if ( lockState )
			{
				// On stoppe tout mouvement r�siduel
				Rb.Velocity = Vector3.Zero;
				Rb.AngularVelocity = Vector3.Zero;
			}
			else
			{
				// On r�veille l'objet s'il est d�gel�
				Rb.Sleeping = false;
			}
		}
	}
}
