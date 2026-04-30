using System.Text.Json.Serialization;

namespace OpenFramework.Systems.Npc;

/// <summary>
/// The bot for players
/// </summary>
public class NpcPawnController : Component
{
	[Property]
	public NavMeshAgent MeshAgent { get; set; }

	public PlayerPawn Pawn { get; set; }


	private INpcBehavior[] _behaviors;

	protected override void OnAwake()
	{
		Pawn = GetComponentInParent<PlayerPawn>( true );
		MeshAgent = GetOrAddComponent<NavMeshAgent>();

		// We want to handle this ourselves
		MeshAgent.UpdateRotation = false;
		MeshAgent.UpdatePosition = false;
		MeshAgent.Radius = 48f;
		MeshAgent.Height = 64f;

		_behaviors = GetComponents<INpcBehavior>( true ).ToArray();
		foreach ( var behavior in _behaviors )
		{
			behavior.Initialize( this );
		}
	}

	public INpcBehavior _currentBehavior;
	private NpcContext _frameContext;

	private TimeSince _timeSincePerception = 0;
	private const float _perceptionInterval = 0.5f;

	[Property, JsonIgnore, ReadOnly]
	public NpcContext Context => _frameContext;

	internal void UpdateBehaviors()
	{
		using var _ = Sandbox.Diagnostics.Performance.Scope( "HC1::UpdateBehaviors" );

		// Build or reuse a context for this frame
		if ( _frameContext == null || _frameContext.Controller != this )
			_frameContext = new NpcContext( this );

		// Run perception only if interval has passed
		if ( _timeSincePerception > _perceptionInterval )
		{
			_timeSincePerception = 0;
			var perceptionNode = new UpdatePerceptionNode();
			perceptionNode.Evaluate( _frameContext );
		}

		// Trouve le behavior avec le score le plus haut — sans LINQ ni allocation heap
		INpcBehavior topBehavior = null;
		float topScore = float.MinValue;
		foreach ( var b in _behaviors )
		{
			var score = b.Score( _frameContext );
			if ( score > topScore ) { topScore = score; topBehavior = b; }
		}

		if ( topBehavior == null || topScore <= 0f )
		{
			_currentBehavior = null;
			return;
		}

		// Si un behavior différent tourne et qu'un meilleur apparaît, on change
		if ( _currentBehavior != null && _currentBehavior != topBehavior )
		{
			if ( topScore > _currentBehavior.Score( _frameContext ) )
				_currentBehavior = topBehavior;
		}

		if ( _currentBehavior == null )
			_currentBehavior = topBehavior;

		// --- Tick the chosen behavior using the same context ---
		if ( _currentBehavior != null )
		{
			var result = _currentBehavior.Update( _frameContext );
			if ( result != NodeResult.Running && result != NodeResult.Success )
				_currentBehavior = null;
		}
	}


	/// <summary>
	/// Sometimes we need to synchronize the NavMeshAgent with the physics system.
	/// </summary>
	protected void SyncNavAgentWithPhysics()
	{
		MeshAgent.MaxSpeed = Pawn.GetWishSpeed();
		Pawn.WishVelocity = MeshAgent.WishVelocity;

		// Handle desync between agent and physics
		if ( WorldPosition.WithZ( 0 ).DistanceSquared( MeshAgent.AgentPosition.WithZ( 0 ) ) > MeshAgent.Radius * MeshAgent.Radius )
		{
			MeshAgent.SetAgentPosition( WorldPosition );
		}
		if ( MathF.Abs( WorldPosition.z - MeshAgent.AgentPosition.z ) > MeshAgent.Height * MeshAgent.Height )
		{
			MeshAgent.SetAgentPosition( WorldPosition );
		}

	}

	protected override void OnFixedUpdate()
	{
		SyncNavAgentWithPhysics();
		UpdateBehaviors();
	}
}
