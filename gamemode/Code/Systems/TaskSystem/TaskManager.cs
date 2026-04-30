using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace OpenFramework;

public static class TaskManager
{
	public static List<TaskDefinition> Tasks { get; set; } = new();

	public static int ActiveTaskIndex { get; set; } = 0;

	public static TaskDefinition ActiveTask => ActiveTaskIndex < Tasks.Count ? Tasks[ActiveTaskIndex] : null;

	public static bool IsInitialized => Tasks.Count > 0;

	/// <summary>
	/// Complete a specific step of the active task by its id.
	/// </summary>
	public static void CompleteStep( string stepId )
	{
		var task = ActiveTask;
		if ( task == null ) return;

		var step = task.Steps.FirstOrDefault( s => s.Id == stepId );
		if ( step == null || step.IsCompleted ) return;

		step.IsCompleted = true;
		Log.Info( $"[TaskSystem] Step completed: {stepId}" );

		if ( task.Steps.All( s => s.IsCompleted ) )
		{
			Log.Info( $"[TaskSystem] Task completed: {task.Title}" );
			AdvanceTask();
		}
	}

	/// <summary>
	/// Check if a step is completed.
	/// </summary>
	public static bool IsStepCompleted( string stepId )
	{
		var task = ActiveTask;
		if ( task == null ) return false;
		return task.Steps.Any( s => s.Id == stepId && s.IsCompleted );
	}

	private static void AdvanceTask()
	{
		ActiveTaskIndex++;
		if ( ActiveTaskIndex >= Tasks.Count )
		{
			Log.Info( "[TaskSystem] All tasks completed!" );
		}
	}

	/// <summary>
	/// Clear all tasks (used when selecting an existing character).
	/// </summary>
	public static void Clear()
	{
		Tasks.Clear();
		ActiveTaskIndex = 0;
	}

	/// <summary>
	/// Reset all tasks.
	/// </summary>
	public static void ResetAll()
	{
		ActiveTaskIndex = 0;
		foreach ( var task in Tasks )
		{
			foreach ( var step in task.Steps )
				step.IsCompleted = false;
		}
	}

	/// <summary>
	/// Initialize the tutorial tasks for a new player.
	/// </summary>
	public static void InitNewPlayerTasks()
	{
		// Reset pour repartir propre
		Tasks.Clear();
		ActiveTaskIndex = 0;

		Tasks = new List<TaskDefinition>
		{
			new TaskDefinition
			{
				Title = "Bienvenue a Union City",
				Icon = "star",
				Steps = new List<TaskStep>
				{
					new TaskStep
					{
						Id = "exit_metro",
						Description = "Sortir du metro"
					},
					new TaskStep
					{
						Id = "check_inventory",
						Description = "Ouvrir son inventaire"
					},
					new TaskStep
					{
						Id = "open_wiki",
						Description = "Consulter le wiki (Echap -> Wiki)"
					}
				}
			}
		};

		Log.Info( "[TaskSystem] New player tasks initialized" );
	}
}
