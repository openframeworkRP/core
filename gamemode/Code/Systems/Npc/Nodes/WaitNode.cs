namespace OpenFramework.Systems.Npc;

public sealed class WaitNode : BaseBehaviorNode
{
	private readonly float _min, _max;
	private bool _init;
	private TimeUntil _until;

	public WaitNode( float seconds ) { _min = _max = seconds; }
	public WaitNode( float min, float max ) { _min = min; _max = max; }

	protected override NodeResult OnEvaluate( NpcContext ctx )
	{
		if ( !_init )
		{
			_init = true;
			_until = (_min == _max) ? _min : Game.Random.Float( _min, _max );
		}

		if ( _until > 0f ) return NodeResult.Running;

		_init = false;
		return NodeResult.Success;
	}
}
