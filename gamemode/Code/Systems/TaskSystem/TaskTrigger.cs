using Sandbox;

namespace OpenFramework;

/// <summary>
/// Place this on a trigger collider to auto-complete a task step when the player enters.
/// </summary>
[Title( "Task Trigger" )]
[Category( "Tasks" )]
[Icon( "flag" )]
public sealed class TaskTrigger : Component, Component.ITriggerListener
{
	/// <summary>
	/// The step ID to complete when player enters.
	/// </summary>
	[Property] public string StepId { get; set; }

	/// <summary>
	/// Destroy this GameObject after triggering.
	/// </summary>
	[Property] public bool DestroyOnTrigger { get; set; } = false;

	public void OnTriggerEnter( Collider other )
	{
		if ( !other.GameObject.Tags.Has( "player" ) ) return;

		if ( TaskManager.IsStepCompleted( StepId ) ) return;

		TaskManager.CompleteStep( StepId );

		if ( DestroyOnTrigger )
			GameObject.Destroy();
	}

	public void OnTriggerExit( Collider other ) { }
}
