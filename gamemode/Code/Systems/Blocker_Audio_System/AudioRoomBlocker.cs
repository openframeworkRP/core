using Facepunch;
using Sandbox;
using System.Xml.Linq;

namespace OpenFramework;

public sealed class AudioRoomBlocker : Component, Component.ITriggerListener
{
	public static List<AudioRoomBlocker> All { get; private set; } = new();

	[Property] public string RoomName { get; set; } = "Station_Metro";

	// Liste des joueurs présents dans n'importe quel volume de cette pièce
	[Property] public List<GameObject> PlayersInRoom { get; private set; } = new();

	protected override void OnEnabled() => All.Add( this );
	protected override void OnDisabled() => All.Remove( this );

	// Cette méthode est appelée peu importe quel collider (box ou mesh) est touché
	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var player = other.GameObject.Root; // On remonte au Root pour être sûr
		if ( player.Tags.Has( "player" ) )
		{
			if ( !PlayersInRoom.Contains( player ) )
				PlayersInRoom.Add( player );
			//Log.Info( $"JOUEUR ENTRE DANS : {RoomName}" );
		}
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		var player = other.GameObject.Root;
		if ( player.Tags.Has( "player" ) )
		{
			// On vérifie si un AUTRE collider du même joueur est encore dans la zone
			// Pour éviter les sorties accidentelles
			PlayersInRoom.Remove( player );
		}
	}
}
