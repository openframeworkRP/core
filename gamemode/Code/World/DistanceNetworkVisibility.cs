using Sandbox;
using OpenFramework.Extension;

namespace OpenFramework.World;

/// <summary>
/// Distance-based network visibility (INetworkVisible).
/// Culls les Sync Var et Transform updates pour les connections trop loin de l'objet.
///
/// Quand culled cote un client :
///  - L'objet est Disabled localement chez ce client
///  - Plus de OnFixedUpdate, rendu, ni physique pour cet objet sur cette machine
///  - Plus de Sync Var ni Transform updates jusqu'au retour a portee
///  - Les RPCs restent delivered (ownership/lock/buy continuent a marcher)
///
/// PRE-REQUIS — sinon le component est ignore par le moteur :
///   Sur le GameObject root du prefab, dans le panel Network (icone nuage/wifi a
///   cote du nom dans l'inspector), DECOCHER "Always Transmit". Sans ca,
///   IsVisibleToConnection n'est jamais consulte et l'objet transmet toujours.
///
/// Multi serveur dedie :
///  - IsVisibleToConnection est appele uniquement cote owner (host pour les world objects)
///  - Les RPCs ([Rpc.Host] sur TryToBuy, Lock, Toggle) restent delivered
///  - L'autorite ne change pas → pas de scenario de double-buy ni de duplication
/// </summary>
[Title( "Distance Network Visibility" )]
[Category( "Networking" )]
[Icon( "visibility_off" )]
public sealed class DistanceNetworkVisibility : Component, Component.INetworkVisible
{
	/// <summary>
	/// Distance maximale (en units = inches s&box) sous laquelle l'objet est visible
	/// pour la connection. Au-dela, l'objet est culled.
	/// 1 inch ≈ 2.54 cm. 3000 inches ≈ 76 metres.
	/// </summary>
	[Property, Range( 500f, 20000f )] public float MaxVisibilityDistance { get; set; } = 3000f;

	/// <summary>
	/// Comportement quand le pawn de la connection n'est pas trouve (joueur en train
	/// de se connecter, en respawn, en spectate). True = on transmet (conservatif),
	/// False = on cull (potentiellement on cache des objets utiles).
	/// </summary>
	[Property] public bool VisibleWhenPawnUnknown { get; set; } = true;

	public bool IsVisibleToConnection( Connection connection, in BBox worldBounds )
	{
		if ( connection == null ) return VisibleWhenPawnUnknown;

		var client = connection.GetClient();
		var pawn = client?.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() )
			return VisibleWhenPawnUnknown;

		var pawnPos = pawn.WorldPosition;

		// Defensive : pendant le spawn initial sur dedie, le pawn peut exister mais
		// etre encore a (0,0,0) avant son teleport au spawn point. Si on cull a ce
		// moment, l'objet reste Disabled cote client jusqu'a ce qu'on revienne dans
		// la zone — ce qui peut ne jamais arriver si la map est loin de l'origine.
		// On considere que le pawn n'est pas encore spawn et on transmet par defaut.
		if ( pawnPos == Vector3.Zero )
			return VisibleWhenPawnUnknown;

		// Distance min entre le pawn et la BBox de l'objet (0 si pawn dans la box).
		// Plus precis qu'une distance centre-a-centre pour les gros objets.
		var dist = worldBounds.ClosestPoint( pawnPos ).Distance( pawnPos );
		return dist <= MaxVisibilityDistance;
	}
}
