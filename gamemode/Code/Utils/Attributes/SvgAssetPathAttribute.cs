using Sandbox.Internal;

namespace OpenFramework.Utility;

[AttributeUsage( AttributeTargets.Property )]
[Description( "When added to a string property, will become an image string selector" )]
[SourceLocation( "Editor\\Hammer\\PropertyAttributes.cs", 79 )]
public class SvgAssetPathAttribute : AssetPathAttribute
{
	[SourceLocation( "Editor\\Hammer\\PropertyAttributes.cs", 82 )]
	public override string AssetTypeExtension => "svg";
}
