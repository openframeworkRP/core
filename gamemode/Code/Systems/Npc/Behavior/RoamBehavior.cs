namespace OpenFramework.Systems.Npc;

public class RoamBehavior : BaseNpcBehavior
{
	private IBehaviorNode _behavior;

	public override float Score( NpcContext ctx )
	{
		return 10f;
	}

	protected override void OnInitialize()
	{
		// Build behavior tree
		_behavior = new SequenceNode(
			new GetRandomPointNode(),
			new MoveToNode( 50, true )
		);
	}

	public override NodeResult Update( NpcContext ctx )
	{
		return _behavior.Evaluate( ctx );
	}
}
