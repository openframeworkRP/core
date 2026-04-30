namespace OpenFramework.Models;

public struct FineReason
{
	[Property] public string Name { get; set; }      // ex: "Dépassement de ligne"
	[Property] public int Amount { get; set; }       // montant de l'amende par défaut

	public FineReason( string name, int amount )
	{
		Name = name;
		Amount = amount;
	}

	public override string ToString() => $"{Name} ({Amount}$)";
}
