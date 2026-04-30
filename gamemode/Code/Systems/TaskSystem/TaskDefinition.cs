using Sandbox;
using System.Collections.Generic;

namespace OpenFramework;

/// <summary>
/// A single task with multiple steps the player must complete.
/// </summary>
public class TaskDefinition
{
	/// <summary>
	/// Display title shown in the HUD.
	/// </summary>
	[Property] public string Title { get; set; }

	/// <summary>
	/// Icon name (material icon or path).
	/// </summary>
	[Property] public string Icon { get; set; } = "star";

	/// <summary>
	/// Ordered list of steps.
	/// </summary>
	[Property] public List<TaskStep> Steps { get; set; } = new();
}

/// <summary>
/// A single step within a task.
/// </summary>
public class TaskStep
{
	/// <summary>
	/// Unique identifier used to complete this step from code.
	/// </summary>
	[Property] public string Id { get; set; }

	/// <summary>
	/// Display text shown in the tracker.
	/// </summary>
	[Property] public string Description { get; set; }

	/// <summary>
	/// Whether this step has sub-items (display only, not functional).
	/// </summary>
	[Property] public List<string> SubItems { get; set; } = new();

	/// <summary>
	/// Runtime state - not saved.
	/// </summary>
	public bool IsCompleted { get; set; } = false;
}
