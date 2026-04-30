namespace OpenFramework.Models;

public struct Fine
{
	/// <summary>
	/// When the fine was issued.
	/// </summary>
	[Property]
	public DateTime IssuedAt { get; set; }

	/// <summary>
	/// When the fine is due (can be calculated from IssuedAt + grace period).
	/// </summary>
	[Property]
	public DateTime DueAt { get; set; }

	/// <summary>
	/// Fine amount in in-game currency.
	/// </summary>
	[Property]
	public int Amount { get; set; }

	/// <summary>
	/// Reason for the fine (offense name, etc.).
	/// </summary>
	[Property]
	public string Reason { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the fine has been paid or not
	/// </summary>
	[Property]
	public bool Paid { get; set; }


	/// <summary>
	/// Gets or sets the date and time when the payment was made.
	/// </summary>
	[Property]
	public DateTime PaidAt { get; set; }
}
