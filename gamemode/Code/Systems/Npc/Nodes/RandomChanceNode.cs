namespace OpenFramework.Systems.Npc;

public sealed class RandomChanceNode : BaseBehaviorNode
{
	private readonly float _chance;
	private readonly IBehaviorNode _child;
	private bool _init, _doChild;

	public RandomChanceNode( float chance, IBehaviorNode child ) { _chance = chance; _child = child; }

	protected override NodeResult OnEvaluate( NpcContext ctx )
	{
		if ( !_init ) { _init = true; _doChild = Game.Random.Float( 0f, 1f ) <= _chance; }
		if ( !_doChild ) { _init = false; return NodeResult.Failure; }

		var res = _child?.Evaluate( ctx ) ?? NodeResult.Success;
		if ( res != NodeResult.Running ) _init = false;
		return res;
	}
}
