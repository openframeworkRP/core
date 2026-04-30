using Sandbox.Diagnostics;

namespace OpenFramework.Systems.Tools;

/// <summary>
/// Lampe placeable allumable/eteignable via le menu radial (touche E).
/// L'etat IsOn est l'autorite host, propage a tous les clients via [Sync].
/// </summary>
public sealed class LampComponent : Component
{
	[Property] public PointLight Light { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public bool IsOn { get; set; } = false;

	protected override void OnStart()
	{
		ApplyState();
	}

	protected override void OnUpdate()
	{
		// On applique en permanence l'etat synchronise (aucun cout si deja coherent)
		ApplyState();
	}

	private void ApplyState()
	{
		// Retry chaque frame tant que la ref n'est pas resolue : sur serveur dedie, l'enfant
		// LightSource peut ne pas encore etre network-spawne au OnStart cote client.
		if ( !Light.IsValid() )
			Light = Components.Get<PointLight>( FindMode.EverythingInSelfAndDescendants );

		if ( Light.IsValid() )
			Light.Enabled = IsOn;
	}

	/// <summary>
	/// Bascule l'etat allume/eteint. Doit etre appele cote host uniquement
	/// (via Commands.RPC_ToggleLamp).
	/// </summary>
	public void SetOn( bool on )
	{
		Assert.True( Networking.IsHost, "LampComponent.SetOn doit etre appele cote host" );
		IsOn = on;
	}
}
