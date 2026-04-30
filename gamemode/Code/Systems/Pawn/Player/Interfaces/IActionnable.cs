namespace OpenFramework.Systems.Pawn;

public interface IActionnable
{
	UseResult CanAction( PlayerPawn player );
	void OnAction( PlayerPawn player );
}
