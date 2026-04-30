namespace OpenFramework.World.CocaineFactory;


/// <summary>
/// Bucket used in the cocaine factory process.
/// Can show/hide its content (cocaine) via bodygroup.
/// </summary>
[Category( "RealityON" )]
public sealed class Bucket : Component
{
	#region Visuals & Tags
	[Property, Group( "Visuals" )] public ModelRenderer ModelRenderer => GameObject.GetComponent<ModelRenderer>();
	#endregion

	// New: Draw flag controls the plate bodygroup visibility/active look
	private bool _showContent;
	[Property, Sync( SyncFlags.FromHost )]
	public bool ShowContent
	{
		get => _showContent;
		set
		{
			_showContent = value;
			ModelRenderer?.SetBodyGroup( "bucket_content", value ? 1 : 0 );
		}
	}
}
