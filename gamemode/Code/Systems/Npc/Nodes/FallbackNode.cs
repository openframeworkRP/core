namespace OpenFramework.Systems.Npc;

public sealed class FallbackNode : BaseBehaviorNode
{
	private readonly IBehaviorNode _primary, _fallback;
	public FallbackNode( IBehaviorNode primary, IBehaviorNode fallback ) { _primary = primary; _fallback = fallback; }

	protected override NodeResult OnEvaluate( NpcContext ctx )
	{
		var r = _primary?.Evaluate( ctx ) ?? NodeResult.Success;
		return (r == NodeResult.Success || r == NodeResult.Running) ? r : (_fallback?.Evaluate( ctx ) ?? NodeResult.Success);
	}
}
