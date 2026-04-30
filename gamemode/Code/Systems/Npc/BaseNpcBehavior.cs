using System.Threading;
using System.Threading.Tasks;

namespace OpenFramework.Systems.Npc;

/// <summary>
/// Interface for bot behaviors that can be composed together
/// </summary>
public interface INpcBehavior
{
	/// <summary>
	/// Initialize the behavior with the bot controller
	/// </summary>
	void Initialize( NpcPawnController controller );

	/// <summary>
	/// Update the behavior. Return true if this behavior handled the update.
	/// </summary>
	NodeResult Update( NpcContext context );

	/// <summary>
	/// Priority of this behavior (higher priority behaviors are checked first)
	/// </summary>
	int Priority { get; }

	/// <summary>
	/// Score this behavior for selection purposes.
	/// </summary>
	/// <returns></returns>
	float Score( NpcContext context );
}

/// <summary>
/// Base class for bot behaviors providing common functionality
/// </summary>
public abstract class BaseNpcBehavior : Component, INpcBehavior
{
	protected NpcPawnController Controller { get; private set; }
	protected PlayerPawn Player => Controller.Pawn;
	protected NavMeshAgent MeshAgent => Controller.MeshAgent;

	public virtual int Priority => 0;

	void INpcBehavior.Initialize( NpcPawnController controller )
	{
		Controller = controller;
		OnInitialize();
	}

	protected virtual void OnInitialize() { }

	public abstract NodeResult Update( NpcContext ctx );

	public virtual float Score( NpcContext ctx ) => 0f;

	/// <summary>
	/// Executes tasks in parallel with different cancellation/success modes.
	/// </summary>
	protected async Task<bool> RunParallel( CancellationToken externalToken, params Func<CancellationToken, Task<bool>>[] tasks )
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource( externalToken );
		var token = cts.Token;

		var taskList = tasks.Select( task => task( token ) ).ToList();

		// Await the first task to complete
		var firstCompletedTask = await Task.WhenAny( taskList );

		// Cancel the other tasks
		cts.Cancel();

		try
		{
			// Get the result of the first completed task
			bool result = await firstCompletedTask;

			// This ensures that all task exceptions are observed
			await Task.WhenAll( taskList );

			return result;
		}
		catch ( OperationCanceledException )
		{
			// Handle task cancellation
			return firstCompletedTask.Result;
		}
		catch ( Exception )
		{
			// Handle any exceptions from the tasks
			return firstCompletedTask.Result;
		}
	}

	/// <summary>
	/// Executes tasks in sequence until one returns true.
	/// </summary>
	protected async Task<bool> RunSelector( CancellationToken token, params Func<CancellationToken, Task<bool>>[] tasks )
	{
		foreach ( var task in tasks )
		{
			if ( token.IsCancellationRequested )
				return false;

			if ( await task( token ) )
			{
				return true;
			}
		}
		return false;
	}
}
