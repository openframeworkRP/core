namespace OpenFramework.World.CocaineFactory;


/// <summary>
/// Bucket used in the cocaine factory process.
/// Can show/hide its content (cocaine) via bodygroup.
/// </summary>
[Category( "RealityON" )]
public sealed class Extractor : Component
{
	#region Visuals & Tags
	[Property, Group( "Visuals" )] public ModelRenderer ModelRenderer => GameObject.GetComponent<ModelRenderer>();
	[Property, Group( "Visuals" )] public GameObject GaugeBone { get; set; }
	[Property, Group( "Visuals" ), Range(0f, 1f), Change] public float GaugeLevel { get; set; }
	#endregion

	public void OnGaugeLevelChanged( float oldValue, float newValue )
	{
		if ( GaugeBone == null ) return;

		// Clamp la valeur pour éviter tout dépassement
		var clampedValue = Math.Clamp( newValue, 0f, 1f );

		// Remap de 0 → -45 et 1 → +45
		var angle = -45f + (clampedValue * 90f);

		GaugeBone.LocalRotation = Rotation.From( angle, 0f, 0f );
	}
}
