namespace OpenFramework.Database;

[AttributeUsage( AttributeTargets.Property )]
public class ForeignKeyAttribute : Attribute
{
	public string ForeignKeyName { get; }

	public ForeignKeyAttribute( string foreignKeyName )
	{
		ForeignKeyName = foreignKeyName;
	}
}
