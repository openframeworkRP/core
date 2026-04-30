using Sandbox;
using OpenFramework.Extension;
using System.Collections.Generic;
using System.Linq;

namespace OpenFramework;

/// <summary>
/// Chaise / canape. Un meme component peut gerer plusieurs places via Seats /
/// Exits (listes paralleles). Pour une chaise simple, on remplit SeatPosition /
/// ExitPoint (retro-compat) ; le component se ramene alors a 1 place.
///
/// Architecture multijoueur (serveur dedie) :
/// - Autorite host-only sur SlotOccupants via [Sync(SyncFlags.FromHost)] +
///   RPCs d'entree/sortie en [Rpc.Host]. Le host est seul juge de qui est
///   assis ou — anti-duplication strict, deux clients qui cliquent en meme
///   temps ne peuvent pas finir sur la meme place.
/// - Position de l'occupant : pas de reparenting du pawn (cause des NRE sur
///   le sync state du pawn et de ses equipements). On colle la WorldPosition
///   chaque OnFixedUpdate sur la machine du sitting player ; le sync Transform
///   propage aux autres clients (calque sur SeatComponent du vehicule).
/// - Visuel (anim "sit", controller off) applique sur tous les clients via
///   [Rpc.Broadcast] declenche par le host apres validation.
/// </summary>
public sealed class ChairComponent : Component, IUse
{
	// --- Multi-place : Seats[i] ↔ Exits[i] (memes index = meme place) ---
	[Property] public List<GameObject> Seats { get; set; } = new();
	[Property] public List<GameObject> Exits { get; set; } = new();

	// --- Retro-compat chaise simple : utilises si Seats est vide ---
	[Property] public GameObject SeatPosition { get; set; }
	[Property] public GameObject ExitPoint { get; set; }

	[Property] public Vector3 SittingOffset { get; set; } = new Vector3( 0, 0, -20f );
	[Property] public Rotation SittingRotation { get; set; } = new Rotation( 0, 0 );
	[Property, Sync] public CameraComponent ChairCamera { get; set; }

	/// <summary>
	/// Mappage slotIndex → joueur assis. Mutation host-only (anti-duplication).
	/// On utilise NetDictionary pour permettre des "trous" (slot libre = entree
	/// absente) sans devoir gerer des nulls dans une NetList.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public NetDictionary<int, PlayerPawn> SlotOccupants { get; set; } = new();

	private TimeSince TimeSinceSat = 0;

	/// <summary>Liste effective des seats (Seats si rempli, sinon [SeatPosition]).</summary>
	public List<GameObject> EffectiveSeats
	{
		get
		{
			if ( Seats != null && Seats.Count > 0 ) return Seats;
			return SeatPosition.IsValid() ? new List<GameObject> { SeatPosition } : new();
		}
	}

	/// <summary>Liste effective des exits (parallele a EffectiveSeats).</summary>
	public List<GameObject> EffectiveExits
	{
		get
		{
			if ( Exits != null && Exits.Count > 0 ) return Exits;
			return ExitPoint.IsValid() ? new List<GameObject> { ExitPoint } : new();
		}
	}

	public int SlotCount => EffectiveSeats.Count;
	public int FreeSlotCount
	{
		get
		{
			int count = 0;
			int total = SlotCount;
			for ( int i = 0; i < total; i++ )
				if ( !IsSlotOccupied( i ) ) count++;
			return count;
		}
	}
	public bool HasFreeSlot => FreeSlotCount > 0;
	/// <summary>True si TOUTES les places sont occupees (compat ancien API).</summary>
	public bool IsOccupied => SlotCount > 0 && !HasFreeSlot;

	public bool IsSlotOccupied( int slot )
	{
		return SlotOccupants.TryGetValue( slot, out var p ) && p.IsValid();
	}

	/// <summary>Renvoie le slot occupe par ce joueur, ou -1 si pas trouve.</summary>
	public int FindSlotOf( PlayerPawn pawn )
	{
		if ( pawn == null ) return -1;
		foreach ( var kv in SlotOccupants )
			if ( kv.Value == pawn ) return kv.Key;
		return -1;
	}

	/// <summary>Renvoie le slot libre le plus proche d'une position, ou -1.</summary>
	private int FindFreeSlotNear( Vector3 nearPos )
	{
		var seats = EffectiveSeats;
		int best = -1;
		float bestDist = float.MaxValue;
		for ( int i = 0; i < seats.Count; i++ )
		{
			if ( IsSlotOccupied( i ) ) continue;
			var pos = seats[i].IsValid() ? seats[i].WorldPosition : WorldPosition;
			var d = pos.DistanceSquared( nearPos );
			if ( d < bestDist ) { bestDist = d; best = i; }
		}
		return best;
	}

	// --- IUse (compat hold-E direct) ---
	public UseResult CanUse( PlayerPawn player ) => HasFreeSlot;

	public void OnUse( PlayerPawn player )
	{
		// IUse.OnUse est invoque cote host par s&box.
		SitHost( player );
	}

	/// <summary>
	/// Appel client → host : demande a s'asseoir. Le host valide (au moins une
	/// place libre, joueur pas deja assis, meuble gele) et applique l'etat.
	/// </summary>
	[Rpc.Host]
	public void RequestSit()
	{
		var pawn = Rpc.Caller.GetClient()?.PlayerPawn as PlayerPawn;
		SitHost( pawn );
	}

	/// <summary>
	/// Logique d'assise host-only. Strict authority pour eviter la duplication.
	/// </summary>
	private void SitHost( PlayerPawn pawn )
	{
		if ( !Networking.IsHost ) { Log.Info( $"[Chair:SitHost] reject: !IsHost (chair='{GameObject.Name}')" ); return; }
		if ( pawn == null || !pawn.IsValid() ) { Log.Info( $"[Chair:SitHost] reject: pawn invalide (chair='{GameObject.Name}')" ); return; }
		if ( pawn.IsSitting ) { Log.Info( $"[Chair:SitHost] reject: pawn deja assis ({pawn.DisplayName})" ); return; }

		var fv = GameObject.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndAncestors );
		if ( fv != null && !fv.IsLocked ) { Log.Info( $"[Chair:SitHost] reject: meuble pas fixe (chair='{GameObject.Name}')" ); return; }

		int slot = FindFreeSlotNear( pawn.WorldPosition );
		Log.Info( $"[Chair:SitHost] chair='{GameObject.Name}' slotCount={SlotCount} freeSlots={FreeSlotCount} → trouve slot={slot}" );
		if ( slot < 0 ) return; // canape totalement plein

		SlotOccupants[slot] = pawn;
		Log.Info( $"[Chair:SitHost] {pawn.DisplayName} assis sur chair='{GameObject.Name}' slot={slot}, SlotOccupants.Count={SlotOccupants.Count}" );
		BroadcastSit( pawn, slot );
	}

	/// <summary>
	/// Visuel et state local sur tous les clients (pose, anim, controller off).
	/// Declenche par le host apres validation.
	/// </summary>
	[Rpc.Broadcast]
	private void BroadcastSit( PlayerPawn player, int slot )
	{
		if ( !player.IsValid() ) return;

		// Reset du debounce d'ejection sur chaque client
		TimeSinceSat = 0;

		if ( player.CharacterController.IsValid() )
			player.CharacterController.Enabled = false;

		// Pose initiale a la position du seat[slot]. Le tracking continu est
		// fait dans OnFixedUpdate (sur la machine de l'occupant uniquement).
		var seats = EffectiveSeats;
		var seatGo = (slot >= 0 && slot < seats.Count) ? seats[slot] : null;
		if ( seatGo.IsValid() && !player.IsProxy )
		{
			player.WorldPosition = seatGo.WorldPosition + SittingOffset;
			player.WorldRotation = seatGo.WorldRotation * SittingRotation;
		}

		if ( player.BodyRenderer.IsValid() )
			ApplySitParams( player.BodyRenderer );

		// IsSitting / CurrentChair : [Sync] possedes par le joueur. Seule sa
		// propre machine peut les muter ; les autres clients recevront via sync.
		if ( !player.IsProxy )
		{
			player.IsSitting = true;
			player.CurrentChair = this.GameObject;
		}
	}

	/// <summary>
	/// Appel client → host : demande a se lever. Seul l'occupant peut se lever
	/// lui-meme (pas un autre joueur).
	/// </summary>
	[Rpc.Host]
	public void RequestEject()
	{
		var pawn = Rpc.Caller.GetClient()?.PlayerPawn as PlayerPawn;
		if ( pawn == null ) return;
		int slot = FindSlotOf( pawn );
		if ( slot < 0 ) return;
		EjectHostSlot( slot );
	}

	/// <summary>
	/// Force la sortie cote host (cleanup, deconnexion, debug). Vide tous les slots.
	/// </summary>
	public void ForceEject()
	{
		if ( !Networking.IsHost ) return;
		var slots = SlotOccupants.Keys.ToList();
		foreach ( var s in slots ) EjectHostSlot( s );
	}

	private void EjectHostSlot( int slot )
	{
		if ( !Networking.IsHost ) return;
		if ( !SlotOccupants.TryGetValue( slot, out var p ) || !p.IsValid() ) return;
		SlotOccupants.Remove( slot );
		BroadcastEject( p, slot );
	}

	[Rpc.Broadcast]
	private void BroadcastEject( PlayerPawn player, int slot )
	{
		if ( !player.IsValid() ) return;

		if ( player.CharacterController.IsValid() )
			player.CharacterController.Enabled = true;

		// Sortie au point de sortie associe au slot. Si Exits[slot] manquant,
		// on retombe sur ExitPoint. Si tout est null, on laisse le pawn ou il est.
		var exits = EffectiveExits;
		GameObject exitGo = null;
		if ( slot >= 0 && slot < exits.Count ) exitGo = exits[slot];
		if ( !exitGo.IsValid() ) exitGo = ExitPoint;

		if ( !player.IsProxy && exitGo.IsValid() )
		{
			player.WorldPosition = exitGo.WorldPosition;
			player.WorldRotation = exitGo.WorldRotation;
		}

		if ( player.Body.IsValid() )
		{
			ClearSitParams( player.Body.Renderer );
			player.Body.UpdateRotation( player.WorldRotation );
		}

		if ( !player.IsProxy )
		{
			player.IsSitting = false;
			player.CurrentChair = null;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( SlotOccupants.Count == 0 ) return;
		var seats = EffectiveSeats;

		// Tracking de la position : pour chaque slot occupe, on colle l'occupant
		// au seat correspondant. Seul l'owner du pawn pousse le set ; le sync
		// Transform propage aux autres clients (host inclus).
		foreach ( var kv in SlotOccupants )
		{
			int slot = kv.Key;
			var pawn = kv.Value;
			if ( !pawn.IsValid() || pawn.IsProxy ) continue;
			if ( slot < 0 || slot >= seats.Count ) continue;
			var seat = seats[slot];
			if ( !seat.IsValid() ) continue;
			pawn.WorldPosition = seat.WorldPosition + SittingOffset;
			pawn.WorldRotation = seat.WorldRotation * SittingRotation;
		}
	}

	protected override void OnUpdate()
	{
		if ( SlotOccupants.Count == 0 ) return;

		// Force les params anim "assis" en continu sur owner ET proxies — sinon
		// les proxies voient une anim de marche puisque les params ne se
		// synchronisent pas automatiquement.
		foreach ( var kv in SlotOccupants )
		{
			var p = kv.Value;
			if ( !p.IsValid() ) continue;
			if ( p.BodyRenderer.IsValid() )
				ApplySitParams( p.BodyRenderer );
		}

		// Inputs de sortie : uniquement sur la machine de l'occupant local.
		var local = Client.Local?.PlayerPawn as PlayerPawn;
		if ( !local.IsValid() ) return;
		int mySlot = FindSlotOf( local );
		if ( mySlot < 0 ) return;
		if ( TimeSinceSat <= 0.5f ) return;

		bool escape = Input.EscapePressed;
		if ( Input.Pressed( "Jump" ) || Input.Pressed( "Use" ) || escape )
		{
			if ( escape ) Input.EscapePressed = false;
			RequestEject();
		}
	}

	/// <summary>
	/// Set complet des parametres anim "assis" — calque sur PlayerSeat.cs (vehicule)
	/// qui marche bien en multijoueur. Pousse en continu sur owner ET proxies dans
	/// OnUpdate pour empecher l'AnimationHelper de re-interpreter une velocity > 0
	/// et faire jouer une anim de marche.
	/// </summary>
	private void ApplySitParams( SkinnedModelRenderer r )
	{
		r.Set( "b_sit", true );
		r.Set( "sit", 1 );
		r.Set( "sit_pose", 0 );
		r.Set( "b_grounded", true );
		r.Set( "move_speed", 0f );
		r.Set( "move_groundspeed", 0f );
		r.Set( "wish_speed", 0f );
		r.Set( "wish_groundspeed", 0f );
		r.Set( "holdtype", 0 );
	}

	/// <summary>
	/// Reset des parametres anim a la sortie de chaise. Symetrique a ApplySitParams
	/// (les valeurs de mouvement seront recalculees normalement par AnimationHelper).
	/// </summary>
	private void ClearSitParams( SkinnedModelRenderer r )
	{
		r.Set( "b_sit", false );
		r.Set( "sit", 0 );
	}
}
