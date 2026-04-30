// Nodes/EnsureWalkNode.cs
using Facepunch;
using Sandbox.Citizen;

namespace OpenFramework.Systems.Npc;

public sealed class EnsureWalkNode : BaseBehaviorNode
{
	private readonly float _speed;

	public EnsureWalkNode( float speed = 120f )
	{
		_speed = speed;
	}

	protected override NodeResult OnEvaluate( NpcContext ctx )
	{
		// 1) Limiter la vitesse de déplacement
		if ( ctx.MeshAgent != null )
			ctx.MeshAgent.MaxSpeed = _speed;

		// 2) Forcer le style d’anim en marche (si helper citoyen présent)
		var anim = ctx.Controller.GameObject.Components.Get<AnimationHelper>(FindMode.EverythingInSelfAndChildren);
		if ( anim != null )
			anim.MoveStyle = AnimationHelper.MoveStyles.Walk; // Walk | Run | Auto

		//Log.Info( anim.MoveStyle );

		return NodeResult.Success;
	}
}
