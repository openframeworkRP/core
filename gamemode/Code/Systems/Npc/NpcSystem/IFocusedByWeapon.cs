// ─── IFocusedByWeapon.cs ─────────────────────────────────────────────────────
namespace OpenFramework.Systems.Pawn;

/// <summary>
/// Interface à implémenter sur les entités qui réagissent quand un joueur
/// les vise avec une arme (NPC braquables, coffres, etc.).
/// </summary>
public interface IFocusedByWeapon
{
	/// <summary>Appelé côté host quand un joueur commence à braquer cette entité.</summary>
	void OnFocusedByWeapon( PlayerPawn player );

	/// <summary>Appelé côté host quand le joueur arrête de braquer cette entité.</summary>
	void OnFocusLost( PlayerPawn player );
}
