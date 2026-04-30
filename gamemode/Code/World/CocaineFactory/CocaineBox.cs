namespace OpenFramework.World.CocaineFactory;


/// <summary>
/// Cocaine box with up to 4 packs inside and an open/closed cover.
/// </summary>
[Category( "RealityON" )]
public sealed class CocaineBox : Component
{
	public enum BoxCover 
	{
		None = 0,
		Open,
		Closed
	}


	#region Visuals & Tags
	[Property, Group( "Visuals" )] public ModelRenderer ModelRenderer => GameObject.GetComponent<ModelRenderer>();
	#endregion

	// Show box cover bodygroup
	private BoxCover _boxCover;
	[Property, Sync( SyncFlags.FromHost )]
	public BoxCover Cover
	{
		get => _boxCover;
		set
		{
			_boxCover = value;
			ModelRenderer?.SetBodyGroup( "cover", (int)value );
		}
	}

	// Show cocaine pack 1 bodygroup inside the box
	private bool _cocainePack1;
	[Property, Sync( SyncFlags.FromHost )]
	public bool CocainePack1
	{
		get => _cocainePack1;
		set
		{
			_cocainePack1 = value;

			if( value )
			{
				CocainePack2 = false;
				CocainePack3 = false;
				CocainePack4 = false;
			}

			ModelRenderer?.SetBodyGroup( "cocaine_pack_1", (value ? 1 : 0) );
		}
	}

	// Show cocaine pack 2 bodygroup inside the box
	private bool _cocainePack2;
	[Property, Sync( SyncFlags.FromHost )]
	public bool CocainePack2
	{
		get => _cocainePack2;
		set
		{
			_cocainePack2 = value;

			if( value )
			{
				CocainePack1 = false;
				CocainePack3 = false;
				CocainePack4 = false;
			}

			ModelRenderer?.SetBodyGroup( "cocaine_pack_2", (value ? 1 : 0) );
		}
	}

	// Show cocaine pack 3 bodygroup inside the box
	private bool _cocainePack3;
	[Property, Sync( SyncFlags.FromHost )]
	public bool CocainePack3
	{
		get => _cocainePack3;
		set
		{
			_cocainePack3 = value;

			if( value )
			{
				CocainePack1 = false;
				CocainePack2 = false;
				CocainePack4 = false;
			}

			ModelRenderer?.SetBodyGroup( "cocaine_pack_3", (value ? 1 : 0) );
		}
	}

	// Show cocaine pack 4 bodygroup inside the box
	private bool _cocainePack4;
	[Property, Sync( SyncFlags.FromHost )]
	public bool CocainePack4
	{
		get => _cocainePack4;
		set
		{
			_cocainePack4 = value;

			if( value )
			{
				CocainePack1 = false;
				CocainePack2 = false;
				CocainePack3 = false;
			}

			ModelRenderer?.SetBodyGroup( "cocaine_pack_4", (value ? 1 : 0) );
		}
	}
}
