namespace OpenFramework.Systems.Pawn;

/// <summary>
/// Attache au ragdoll d'un joueur mort pour retrouver le Client proprietaire.
///
/// On reference le <see cref="Client"/> et non directement le PlayerPawn :
/// lors d'un respawn a l'hopital, l'ancien pawn est DETRUIT et un NOUVEAU pawn
/// est cree (voir Client.Spawning.cs). Referencer le pawn rendrait donc
/// impossible l'identification du ragdoll depuis le nouveau pawn au moment
/// du DestroyRagdoll. Le Client est lui stable a travers les respawns.
/// </summary>
public sealed class RagdollOwner : Component
{
	public Client OwnerClient { get; set; }

	/// <summary>
	/// Pawn actuellement associe au Client proprietaire (peut etre null/mort).
	/// Utilise par le defibrillateur pour cibler le joueur mort.
	/// </summary>
	public PlayerPawn OwnerPawn => OwnerClient?.PlayerPawn;
}
