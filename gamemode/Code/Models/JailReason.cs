namespace OpenFramework.Models;

/// <summary>
/// Represents a jailable offense with a name, default duration, and optional fine.
/// </summary>
public struct JailReason
{
	/// <summary>
	/// The display name of the offense.
	/// </summary>
	[Property]
	public string Name { get; set; }

	/// <summary>
	/// Default jail duration (in seconds) for this offense.
	/// </summary>
	[Property]
	public float Duration { get; set; }

	/// <summary>
	/// Optional fine amount associated with the offense.
	/// </summary>
	[Property]
	public int Fine { get; set; }
}
