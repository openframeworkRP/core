using Facepunch;
using OpenFramework.Utility;

namespace OpenFramework.World;

/// <summary>
/// Cooking pot used in cooking food items or drugs.
/// </summary>
public class CookingPot : Component, IDescription
{
	[Property]
	public string DisplayName { get; set; } = "Cooking Pot";

	#region Visuals & Tags
	[Property, Group( "Visuals" )]
	public ModelRenderer ModelRenderer => GameObject.GetComponent<ModelRenderer>();
	#endregion

	// --- Direct state flags ---
	[Property, Sync( SyncFlags.FromHost ), Sandbox.Change]
	public bool HasWater { get; set; }

	[Property, Sync( SyncFlags.FromHost ), Sandbox.Change]
	public bool HasBakingSoda { get; set; }

	// Computed state: mixed when both are present
	public bool IsMixed => HasWater && HasBakingSoda;

	public void OnHasWaterChanged( bool oldValue, bool newValue) => UpdateVisuals();
	public void OnHasBakingSodaChanged( bool oldValue, bool newValue) => UpdateVisuals();

	// Whenever something changes, call this:
	public void UpdateVisuals()
	{
		if ( ModelRenderer == null ) return;

		// Reset bodygroups
		ModelRenderer.SetBodyGroup( "water", 0 );
		ModelRenderer.SetBodyGroup( "soda", 0 );
		ModelRenderer.SetBodyGroup( "mix", 0 );

		// Priority: Mixed overrides individual ingredients
		if ( IsMixed )
		{
			ModelRenderer.SetBodyGroup( "mix", 1 );
			return;
		}

		if ( HasWater )
			ModelRenderer.SetBodyGroup( "water", 1 );

		if ( HasBakingSoda )
			ModelRenderer.SetBodyGroup( "soda", 1 );
	}


}
