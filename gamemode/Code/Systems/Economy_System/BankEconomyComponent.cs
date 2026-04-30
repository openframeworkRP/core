using Sandbox;

namespace OpenFramework;

public sealed class BankEconomyComponent : Component
{
	// La réserve intouchable (utilisée pour garantir la solvabilité)
	[Property, Sync( SyncFlags.FromHost )]
	public int ReserveFunds { get; set; } = 1000000;

	// Le fonds de roulement (utilisé pour les prêts et c'est ce qui est VOLABLE)
	[Property, Sync( SyncFlags.FromHost )]
	public int OperatingFunds { get; set; } = 50000;

	// Taux d'intérêt actuel géré par les banquiers
	[Property, Sync( SyncFlags.FromHost )]
	public float LoanInterestRate { get; set; } = 0.05f;

	[Rpc.Host]
	public void TransferToOperating( int amount )
	{
		if ( ReserveFunds < amount ) return;

		ReserveFunds -= amount;
		OperatingFunds += amount;

		Log.Info( $"La banque a débloqué {amount}$ pour les opérations." );
	}

	[Rpc.Host]
	public void RobFunds( int amount )
	{
		// Seul le fonds de roulement peut être braqué
		OperatingFunds = Math.Max( 0, OperatingFunds - amount );
		Log.Warning( $"ALERTE : {amount}$ ont été volés dans le fonds de roulement !" );
	}
}
