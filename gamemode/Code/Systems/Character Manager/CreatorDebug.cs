using Sandbox;

namespace OpenFramework;

public static class CreatorDebug
{
	[ConVar( "creator_debug" )]
	public static bool Enabled { get; set; } = false;

	public static void Info( string message )
	{
		if ( Enabled )
			Log.Info( message );
	}

	public static void Warning( string message )
	{
		if ( Enabled )
			Log.Warning( message );
	}
}
