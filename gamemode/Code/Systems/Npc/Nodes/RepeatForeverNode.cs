using OpenFramework.GameLoop;

namespace OpenFramework.Systems.Npc;

public sealed class RepeatForeverNode : BaseBehaviorNode
{
	private readonly Func<IBehaviorNode> _factory;
	private IBehaviorNode _child;
	private bool _inited;
	private Constants _constants => Constants.Instance;

	public RepeatForeverNode( Func<IBehaviorNode> factory )
	{
		_factory = factory ?? throw new ArgumentNullException( nameof( factory ) );
	}

	protected override NodeResult OnEvaluate( NpcContext ctx )
	{
		// (Ré)initialiser le sous-arbre au premier tick (ou après un cycle terminé)
		if ( !_inited || _child is null )
		{
			_child = _factory();
			_inited = true;

			// si la factory renvoie null, on ne peut pas avancer
			if ( _child is null )
				return NodeResult.Failure;
		}

		// Évaluer le sous-arbre courant
		var r = _child.Evaluate( ctx );

		// Tant que le sous-arbre RUN, on RUN
		if ( r == NodeResult.Running )
			return NodeResult.Running;

		// Le sous-arbre a fini (Success/Failure) -> on en recrée un nouveau et on continue à RUN
		_child = _factory();
		if( _constants.Debug)
			Log.Info( $"[NPC {ctx.Controller}] Repeat Forever node" );
		return NodeResult.Running;
	}

	/// <summary>
	/// Optionnel : reset manuel si tu veux forcer un nouveau cycle au prochain tick.
	/// </summary>
	public void Reset()
	{
		_child = null;
		_inited = false;
	}
}
