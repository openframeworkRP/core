using Facepunch;

namespace OpenFramework.Systems.Npc;

public class CombatBehavior : BaseNpcBehavior
{
	private IBehaviorNode _behavior;

	protected override void OnInitialize()
	{
		_behavior = new SequenceNode(
			new HasVisibleEnemiesNode(),
			new SelectTargetNode(),
			new ReloadWeaponNode(),
			new ParallelNode(
				new MoveToFiringPositionNode(),
				new AimAtTargetNode(),
				new ShootTargetNode()
			)
		);
	}

	public override float Score( NpcContext ctx )
	{
		// Use perception data that NpcPawnController already populated
		if ( !ctx.HasData( "visible_enemies" ) )
			return 0f;

		var enemies = ctx.GetData<List<Pawn.Pawn>>( "visible_enemies" );
		if ( enemies == null || enemies.Count == 0 )
			return 0f;

		// Simple scoring: higher when enemies are closer
		float baseScore = 100f;
		float closestDist = enemies.Min( e => e.WorldPosition.Distance( ctx.Pawn.WorldPosition ) );
		float proximityBonus = MathF.Max( 0, 50f * (1f - closestDist / 1000f) );

		return baseScore + proximityBonus;
	}

	public override NodeResult Update( NpcContext ctx )
	{
		// Just run the tree using the already-populated blackboard
		return _behavior.Evaluate( ctx );
	}
}
