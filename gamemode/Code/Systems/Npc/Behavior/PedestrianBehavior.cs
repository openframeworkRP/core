// Behavior/PedestrianBehavior.cs
namespace OpenFramework.Systems.Npc;

public class PedestrianBehavior : BaseNpcBehavior
{
	[Property] public float WalkSpeed { get; set; } = 120f;
	[Property] public float Repath { get; set; } = 0.4f;      // ré-ordre MoveTo périodique
	[Property] public float ArrivalDist { get; set; } = 60f;   // distance d’arrivée
	[Property] public float EmoteChance { get; set; } = 0.35f;
	[Property] public float EmoteDurMin { get; set; } = 0.8f;
	[Property] public float EmoteDurMax { get; set; } = 1.8f;

	private IBehaviorNode _tree;

	public override float Score( NpcContext ctx ) => 12f; // fond (Combat > Vendor > 12)

	protected override void OnInitialize()
	{
		_tree = new RepeatForeverNode( () =>    // retourne toujours Running : boucle
			new SequenceNode(
				new EnsureWalkNode( WalkSpeed ),
				new GetRandomPointNode(), // ← ton node existant: écrit "target_position"
				new MoveToNode( arrivalDistance: ArrivalDist, faceDirection: true ),
				new FallbackNode(
					new RandomChanceNode( EmoteChance, new PlayRandomEmoteNode( EmoteDurMin, EmoteDurMax ) ),
					new WaitNode( 0.4f, 1.2f )
				)
			)
		);
	}

	public override NodeResult Update( NpcContext ctx )
	{
		_tree.Evaluate( ctx );
		return NodeResult.Running; // garde la main jusqu’à ce qu’un score plus haut prenne le relais
	}
}
