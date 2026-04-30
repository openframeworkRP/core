using Sandbox;

namespace OpenFramework.Systems.Npc;

public sealed class PlayRandomEmoteNode : BaseBehaviorNode
{
	private readonly float _min, _max;
	private bool _init; private TimeUntil _until;
	private Angles _startAngles, _targetAngles;

	public PlayRandomEmoteNode( float durationMin = 0.8f, float durationMax = 1.8f ) { _min = durationMin; _max = durationMax; }

	protected override NodeResult OnEvaluate( NpcContext ctx )
	{
		if ( !_init )
		{
			_init = true;
			_until = (_min == _max) ? _min : Game.Random.Float( _min, _max );
			_startAngles = ctx.Pawn.EyeAngles;
			var yawOffset = Game.Random.Float( -40f, 40f );
			_targetAngles = _startAngles.WithYaw( _startAngles.yaw + yawOffset );
		}

		ctx.Pawn.EyeAngles = ctx.Pawn.EyeAngles.LerpTo( _targetAngles, Time.Delta * 2f );
		if ( _until > 0f ) return NodeResult.Running;

		_init = false;
		return NodeResult.Success;
	}
}
