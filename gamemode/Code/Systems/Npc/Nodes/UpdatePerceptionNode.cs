using Facepunch;

namespace OpenFramework.Systems.Npc;

public class UpdatePerceptionNode : BaseBehaviorNode
{
	private readonly float _range;
	private readonly bool _writePeople;

	private const string ENEMIES_KEY = "visible_enemies";
	private const string PEOPLE_KEY = "visible_people";
	private const string ITEMS_KEY = "visible_items";

	public UpdatePerceptionNode( float range = 2048f, bool writePeopleList = false )
	{
		_range = range;
		_writePeople = writePeopleList;
	}

	protected override NodeResult OnEvaluate( NpcContext context )
	{
		var me = context.Pawn;
		var origin = me.WorldPosition;
		var rangeSqr = _range * _range;

		var enemies = new List<Pawn.Pawn>();
		List<Pawn.Pawn> people = _writePeople ? new List<Pawn.Pawn>() : null;
		var items = new List<Component>();

		// See *all* pawns (players + NPCs)
		foreach ( var other in me.Scene.GetAll<Pawn.Pawn>() )
		{
			if ( other == me ) continue;

			// alive (if HealthComponent exists)
			if ( other.HealthComponent.IsValid() && other.HealthComponent.Health <= 0 )
				continue;

			// in range?
			if ( other.WorldPosition.DistanceSquared( origin ) > rangeSqr )
				continue;

			// line of sight?
			if ( !IsInLineOfSight( me, other ) )
				continue;

			// optional list of visible people (for social systems)
			people?.Add( other );

			// enemies only if your rule says so
			if ( IsEnemy( me, other ) )
				enemies.Add( other );
		}

		// Items / pickups
		foreach ( var pickup in me.Scene.GetAll<DroppedEquipment>() )
		{
			if ( pickup.WorldPosition.DistanceSquared( origin ) > rangeSqr )
				continue;

			// If you want LOS for items too, uncomment:
			// if ( !IsInLineOfSight( me, pickup ) ) continue;

			items.Add( pickup );
		}

		// Write BB
		context.SetData( ENEMIES_KEY, enemies );
		if ( _writePeople ) context.SetData( PEOPLE_KEY, people );
		context.SetData( ITEMS_KEY, items );

		// Always Success so memory/search nodes can still run after
		return NodeResult.Success;
	}

	private bool IsInLineOfSight( Pawn.Pawn from, Component obj )
	{
		var targetPos = (obj as Pawn.Pawn)?.EyePosition ?? obj.WorldPosition;

		var tr = from.Scene.Trace
			.Ray( from.EyePosition, targetPos + Vector3.Down * 32f )
			.IgnoreGameObjectHierarchy( from.GameObject.Root )
			.Run();

		return tr.Hit && tr.GameObject.Root == obj.GameObject.Root;
	}

	// Replace with your actual hostility rules.
	// Examples:
	// - WantedLevelService.IsWanted(other)
	// - other has tag "zombie"
	// - ReputationService below threshold & attacking
	// - BannedByVendor && currently assaulting NPC, etc.
	private bool IsEnemy( Pawn.Pawn me, Pawn.Pawn other )
	{
		// Default RP: everyone friendly unless flagged hostile
		//return other.GameObject.Tags.Has( "zombie" );

		return true;
	}
}
