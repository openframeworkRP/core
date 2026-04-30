namespace OpenFramework.Wiki;

[AssetType( Name = "Wiki Article", Extension = "wiki", Category = "Roleplay" )]
public sealed class WikiArticle : GameResource
{
	public static HashSet<WikiArticle> All { get; set; } = new();

	[Property, Category( "Definition" )]
	public string Title { get; set; }

	[Property, Category( "Definition" )]
	public string Category { get; set; } = "General";

	[Property, Category( "Definition" ), TextArea]
	public string Summary { get; set; }

	[Property, Category( "Content" ), TextArea]
	public string Content { get; set; }

	[Property, Category( "Definition" ), ImageAssetPath]
	public string Icon { get; set; }

	protected override void PostLoad()
	{
		if ( All.Contains( this ) ) return;
		All.Add( this );
	}
}
