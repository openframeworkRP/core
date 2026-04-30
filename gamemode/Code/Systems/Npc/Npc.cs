using Sandbox.Events;
using OpenFramework.GameLoop;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Npc;

public partial class Npc : Component, IUse, IGameEventHandler<ModifyDamageGlobalEvent>, IGameEventHandler<KillEvent>
{
	// ===== Rôle & état =====
	public enum NpcRole { Vendor, Civilian, Zombie }

	public enum NpcState { Idle, Patrol, Roam, Chase, ReturnHome }

	[Property] public NpcRole Role { get; set; } = NpcRole.Vendor;

	// ===== Réglages communs =====
	[Property] public string Name { get; set; } = "NPC";
	[Property] public bool Invulnerable { get; set; } = true;

	// Déplacement / Anim
	[Property] public float StopDistance { get; set; } = 80f;
	[Property] public float RepathCooldown { get; set; } = 0.35f;

	// Patrol / Roam
	[Property] public bool EnablePatrol { get; set; } = false;
	[Property] public List<GameObject> PatrolPoints { get; set; } = new();
	[Property] public bool EnableRoam { get; set; } = false;
	[Property] public float RoamRadius { get; set; } = 1000f;
	[Property] public float IdleMin { get; set; } = 1.5f;
	[Property] public float IdleMax { get; set; } = 4.0f;

	// Perception / Aggro
	[Property] public float SightRange { get; set; } = 1200f;
	[Property] public float AttackRange { get; set; } = 90f;
	[Property] public float AttackCooldown { get; set; } = 0.9f;
	[Property] public float AttackDamage { get; set; } = 15f;

	// Filtres de « coups » (utiles pour Vendor)
	[Property] public bool CountOnlyMelee { get; set; } = false;
	[Property] public float MinDamageToCount { get; set; } = 0f;

	// ===== Sanction Vendor =====
	[Property] public int HitsThreshold { get; set; } = 3;
	[Property] public float WindowSeconds { get; set; } = 10f;

	[Property, Group( "Equipments" )] public List<EquipmentResource> Weapons { get; set; }

	/// <summary>
	/// Does this NPC have a target PlayerPawn to focus ON ?
	/// </summary>
	[Property] public Pawn.Pawn Target { get; set; } = null;

	[Property] private NpcPawnController Controller { get; set; }

	private TimeSince _sinceLastMoveTo;

	// ================= État (par NPC) =================
	private readonly Dictionary<ulong, HitWindow> _hits = new();
	private readonly Dictionary<ulong, TimeUntil> _bans = new();

	protected override async void OnAwake()
	{
		Controller = GameObject.GetComponent<NpcPawnController>();

		await GameTask.Delay( 10 );

		if(Controller.Pawn != null && Weapons != null)
		{
			foreach ( var w in Weapons )
			{
				Controller.Pawn.Inventory.Give( w );
			}
		}
		
	}

	// =============== Écoute globale (marche sans HealthComponent) ===============
	public void OnGameEvent( ModifyDamageGlobalEvent e )
	{
		if ( !Networking.IsHost ) return; // autorité serveur

		var di = e.DamageInfo;

		// Ce NPC est-il la victime (ou un child) ?
		var victim = di.Victim;
		if ( victim is null ) return;

		var npcOnRoot = victim.GameObject?.Root?.Components?.Get<Npc>( includeDisabled: true );
		if ( npcOnRoot != this ) return;

		// Filtres
		if ( CountOnlyMelee && !di.Flags.HasFlag( DamageFlags.Melee ) ) return;
		if ( di.Damage < MinDamageToCount ) return;

		// Attaquant
		var attackerClient = ResolveClientFromDamage( di );
		var attackerPawn = attackerClient?.Pawn;
		if ( attackerClient is null || attackerPawn is null ) return;

		Target = attackerPawn;
		Controller.Context.SetData( "target_position", Target.WorldPosition );
		Controller.Context.SetData( "current_target", Target );
		Controller.Context.SetData( "visible_enemies", new List<Pawn.Pawn>() { attackerPawn } );

		// Déjà banni ?
		if ( IsBanned( attackerClient, out var left ) )
		{
			attackerClient.Notify( NotificationType.Error, $"« {Name} » refuse de vous servir encore {left:0}s." );
			if ( Invulnerable ) e.ClearDamage();
			return;
		}

		// Invulnérable ?
		if ( Invulnerable ) e.ClearDamage();

		// Enregistrer le coup
		var sid = attackerClient.SteamId;
		var hw = GetHitWindow( sid, WindowSeconds );
		hw.AddHit();

		// Avertissement pré-seuil
		if ( hw.Count == HitsThreshold - 1 )
		{
			attackerClient.Notify( NotificationType.Error, $"Encore un coup sur « {Name} » et tu seras blacklist temporairement." );
		}

		// Seuil atteint -> sanction
		var banDuration = Constants.Instance.NpcBanSeconds;
		if ( hw.Count >= HitsThreshold )
		{
			_bans[sid] = banDuration; // TimeUntil
			hw.Clear();
			attackerClient.Notify( NotificationType.Error, $"« {Name} » te refuse le service pendant {banDuration:0}s." );
		}
	}

	// ================= API Shop =================
	public bool IsBanned( Client client, out float secondsLeft )
	{
		secondsLeft = 0f;
		if ( client is null ) return false;

		if ( _bans.TryGetValue( client.SteamId, out var until ) )
		{
			if ( until > 0f )
			{
				secondsLeft = until;
				return true;
			}
			_bans.Remove( client.SteamId );
		}
		return false;
	}

	public bool CanTrade( Client client ) => !IsBanned( client, out _ );

	public void TryOpenShop( Client client )
	{
		if ( CanTrade( client ) ) OpenShopUiFor( client );
		else { IsBanned( client, out var left ); InformRefused( client, left ); }
	}

	// ================= IUse =================
	public UseResult CanUse( PlayerPawn player )
	{
		if ( player is null || player.Client is null )
			return false;

		if ( IsBanned( player.Client, out var left ) )
		{
			player.Client.Notify( NotificationType.Error, $"« {Name} » refuse de vous servir encore {left:0}s." );
			return false;
		}

		return true;
	}

	public void OnUse( PlayerPawn player )
	{
		if ( player is null || player.Client is null ) return;
		TryOpenShop( player.Client );
	}

	// ================= Helpers =================
	private HitWindow GetHitWindow( ulong sid, float windowSeconds )
	{
		if ( !_hits.TryGetValue( sid, out var hw ) )
		{
			hw = new HitWindow( windowSeconds );
			_hits[sid] = hw;
		}
		return hw;
	}

	private static Client ResolveClientFromDamage( in Systems.Pawn.DamageInfo di )
	{
		// 1) Attacker direct
		if ( di.Attacker is Component att )
		{
			var ic = ResolveClientFromComponent( att );
			if ( ic is not null ) return ic;
		}
		// 2) Inflictor/Weapon
		var src = di.Inflictor ?? di.Attacker;
		if ( src is Component inf )
		{
			var ic = ResolveClientFromComponent( inf );
			if ( ic is not null ) return ic;
		}
		return null;
	}

	private static Client ResolveClientFromComponent( Component comp )
	{
		var pp = comp.Components?.Get<PlayerPawn>( includeDisabled: true )
				 ?? comp.GameObject?.Root?.Components?.Get<PlayerPawn>( includeDisabled: true );
		if ( pp?.Client is Client c1 ) return c1;

		var pawn = comp.Components?.Get<Pawn.Pawn>( includeDisabled: true )
				   ?? comp.GameObject?.Root?.Components?.Get<Pawn.Pawn>( includeDisabled: true );
		if ( pawn?.Client is Client c2 ) return c2;

		if ( comp is PlayerPawn ppSelf && ppSelf.Client is Client c3 ) return c3;
		if ( comp is Pawn.Pawn pawnSelf && pawnSelf.Client is Client c4 ) return c4;
		return null;
	}

	private void OpenShopUiFor( Client client )
	{
		// TODO: RPC -> ouvrir l'UI de vente
	}

	private void InformRefused( Client client, float secondsLeft )
	{
		// TODO: RPC -> afficher un toast côté client
	}

	// Animations (tick)
	protected override void OnUpdate()
	{
		if ( Controller == null || Controller.MeshAgent == null ) return;

		// … tes anims existantes …
		if ( UseTraffic )
		{
			// Idling (pause au waypoint)
			//if ( _idleUntilTraffic > 0f ) return;

			// Arrivé au point → planifie le suivant + petite pause
			/*if ( Controller.MeshAgent.TargetPosition )
			{
				GotoNextWaypoint();
			}*/
		}

		
	}

	public void MoveTo( Vector3 position )
	{
		if ( Controller.MeshAgent == null ) return;
		if ( _sinceLastMoveTo < RepathCooldown ) return;

		_sinceLastMoveTo = 0f;
		Controller.MeshAgent.Stop();
		Controller.MeshAgent.MoveTo( position );

		// hint anim: on “souhaite” aller vers la cible pour des bras/jambes plus vivants
		/*if ( Controller != null )
			Controller.WithWishVelocity( (position - WorldPosition).Normal * Agent.MaxSpeed );*/
	}

	public void OnGameEvent( KillEvent eventArgs )
	{
		// Ce NPC est-il la victime (ou un child) ?
		var victim = eventArgs.DamageInfo.Victim;
		if ( victim is null ) return;

		var npcOnRoot = victim.GameObject?.Root?.Components?.Get<Npc>( includeDisabled: true );
		if ( npcOnRoot != this ) return;

		GameObject.Destroy();
	}

	// Fenêtre glissante
	private sealed class HitWindow
	{
		private readonly float _window;
		private readonly Queue<float> _times = new();

		public HitWindow( float windowSeconds ) => _window = windowSeconds;

		public int Count => _times.Count;

		public void AddHit()
		{
			var now = Time.Now;
			_times.Enqueue( now );
			while ( _times.Count > 0 && now - _times.Peek() > _window )
				_times.Dequeue();
		}

		public void Clear() => _times.Clear();
	}
}
