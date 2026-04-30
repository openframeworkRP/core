using Sandbox;
using OpenFramework.Dialog;
using OpenFramework.Systems.Pawn;

namespace OpenFramework;

public sealed class NpcLogical : Component, IUse
{
	[ConVar( "core_debug_npc", Help = "Active les logs de debug NPC" )]
	public static bool DebugNpcLogs { get; set; } = false;

	[Property]
	public string PnjName { get; set; }

	[Property, Description( "Message d'accueil du NPC quand le joueur interagit" )]
	public string GreetingMessage { get; set; } = "Bonjour, que puis-je faire pour vous ?";

	[Property]
	public TextRenderer TextRenderer { get; set; }

	[Property]
	public DialogueNode DialogueTree { get; set; }

	[Property]
	public PanelComponent UsePanel { get; set; }

	[Property, Description( "Son pré-généré pour le greeting du NPC" )]
	public SoundEvent GreetingSound { get; set; }

	/// <summary>
	/// Si true, le NPC a un dialogue complet (dealer, drug buyer...).
	/// Si false, le NPC montre juste la présentation puis Utiliser (epicier, meublio, medic, job...).
	/// </summary>
	[Property]
	public bool HasFullDialogue { get; set; } = false;

	private Sandbox.Citizen.CitizenAnimationHelper _animHelper;
	private Sandbox.Citizen.CitizenAnimationHelper AnimHelper
	{
		get
		{
			if ( _animHelper == null || !_animHelper.IsValid() )
			{
				_animHelper = GameObject.Components.GetInDescendantsOrSelf<Sandbox.Citizen.CitizenAnimationHelper>();
			}
			return _animHelper;
		}
	}

	private bool IsLooking { get; set; }
	private float LookWeight { get; set; }
	private float TargetLookWeight { get; set; }

	// ScreenPanels d'UI du NPC — desactives par defaut, re-actives uniquement quand un PanelComponent fille est ouvert.
	// Sinon les ScreenPanel de tous les NPC de la scene restent actifs en permanence avec ZIndex=100 et
	// masquent les overlays engine (console F2, modal Settings, etc.).
	private List<Sandbox.ScreenPanel> _uiScreenPanels = new();
	private List<PanelComponent>      _uiPanelComponents = new();

	protected override void OnAwake()
	{
		base.OnAwake();
		TextRenderer?.Text = PnjName;

		// Disable all PanelComponents on this NPC to prevent them showing on spawn
		_uiPanelComponents = GameObject.Components.GetAll<PanelComponent>( FindMode.InDescendants ).ToList();
		foreach ( var panel in _uiPanelComponents )
		{
			panel.Enabled = false;
		}

		// On recupere aussi tous les ScreenPanel parents des UI du NPC et on les desactive par defaut.
		// Ils seront re-actives a la volee dans OnUpdate quand un PanelComponent fille devient enabled.
		_uiScreenPanels = GameObject.Components.GetAll<Sandbox.ScreenPanel>( FindMode.InDescendants ).ToList();
		foreach ( var sp in _uiScreenPanels )
		{
			sp.Enabled = false;
		}

		// Fallback cote client dedie: la reference prefab [Property] UsePanel peut etre null
		// lors du spawn reseau. On la resout dynamiquement.
		ResolveUsePanelIfNull();
	}

	private void SyncUiScreenPanels()
	{
		if ( _uiScreenPanels.Count == 0 ) return;

		bool anyOpen = false;
		for ( int i = 0; i < _uiPanelComponents.Count; i++ )
		{
			var pc = _uiPanelComponents[i];
			if ( pc != null && pc.IsValid() && pc.Enabled ) { anyOpen = true; break; }
		}

		for ( int i = 0; i < _uiScreenPanels.Count; i++ )
		{
			var sp = _uiScreenPanels[i];
			if ( sp == null || !sp.IsValid() ) continue;
			if ( sp.Enabled != anyOpen ) sp.Enabled = anyOpen;
		}
	}

	private void ResolveUsePanelIfNull()
	{
		if ( UsePanel != null && UsePanel.IsValid() ) return;

		var found = GameObject.Components.GetAll<PanelComponent>( FindMode.InDescendants ).FirstOrDefault();
		if ( found != null )
		{
			UsePanel = found;
			if ( DebugNpcLogs )
				Log.Info( $"[NPC-DEBUG] UsePanel resolu dynamiquement pour {PnjName}: {found.GetType().Name}" );
		}
		else
		{
			Log.Warning( $"[NPC-DEBUG] Impossible de resoudre UsePanel pour {PnjName}: aucun PanelComponent trouve" );
		}
	}

	protected override void OnUpdate()
	{
		// Synchronise l'etat des ScreenPanel d'UI avec l'etat des PanelComponent fille.
		SyncUiScreenPanels();

		if ( AnimHelper == null ) return;

		// Lerp progressif du poids
		LookWeight = MathX.Lerp( LookWeight, TargetLookWeight, Time.Delta * 3f );

		AnimHelper.EyesWeight = LookWeight;
		AnimHelper.HeadWeight = LookWeight;
		AnimHelper.BodyWeight = LookWeight;

		// Quand le poids est presque à 0, on peut retirer la cible
		if ( !IsLooking && LookWeight < 0.01f && AnimHelper.LookAt != null )
		{
			AnimHelper.LookAt = null;
			LookWeight = 0f;
		}
	}

	public void StartLookingAt( GameObject target )
	{
		StartLookingAtBroadcast( target );
	}

	[Rpc.Broadcast]
	private void StartLookingAtBroadcast( GameObject target )
	{
		if ( AnimHelper != null && target.IsValid() )
		{
			AnimHelper.LookAt = target;
			IsLooking = true;
			TargetLookWeight = 1f;
		}
	}

	public void StopLooking()
	{
		StopLookingBroadcast();
	}

	[Rpc.Broadcast]
	private void StopLookingBroadcast()
	{
		IsLooking = false;
		TargetLookWeight = 0f;
	}

	[Rpc.Host]
	public void StopLookingOnHost()
	{
		StopLooking();
	}

	// Helper: log local + remonte au host pour centraliser les logs en serveur dedie
	public void ClientDebug( string msg )
	{
		if ( !DebugNpcLogs ) return;
		Log.Info( $"[NPC-DEBUG] (CLIENT) {msg}" );
		DebugLogOnHost( msg );
	}

	[Rpc.Host]
	private void DebugLogOnHost( string msg )
	{
		if ( !DebugNpcLogs ) return;
		Log.Info( $"[NPC-DEBUG] (relay from CLIENT) {msg}" );
	}

	public UseResult CanUse( PlayerPawn player )
	{
		return new UseResult
		{
			CanUse = true
		};
	}

	public void OnUse( PlayerPawn player )
	{
		if ( DebugNpcLogs )
			Log.Info( $"[NPC-DEBUG] OnUse cote={(Networking.IsHost ? "HOST" : "CLIENT")} npc={PnjName} player={player?.Client?.DisplayName}" );
		if ( !Networking.IsHost || player == null ) return;

		// NPC regarde le joueur (côté host)
		StartLookingAt( player.GameObject );

		if ( DebugNpcLogs )
			Log.Info( $"[NPC-DEBUG] OnUse envoi RPC OpenUiOnClient vers {player.Client?.DisplayName} (npc={PnjName})" );
		using ( Rpc.FilterInclude( player.Client.Connection ) )
		{
			OpenUiOnClient();
		}
	}

	[Rpc.Broadcast]
	private void OpenUiOnClient()
	{
		ResolveUsePanelIfNull();
		ClientDebug( $"OpenUiOnClient recu cote={(Networking.IsHost ? "HOST" : "CLIENT")} npc={PnjName} UsePanel={(UsePanel != null ? UsePanel.GetType().Name : "NULL")} IsOwner={GameObject.Network.IsOwner} IsProxy={GameObject.Network.IsProxy}" );
		var manager = NpcInteractionManager.Instance;
		if ( manager == null )
		{
			ClientDebug( "NpcInteractionManager.Instance est NULL cote client !" );
			return;
		}

		manager.OpenInteraction( this );

		// Si le panel s'est ouvert directement (NPC déjà rencontré), ne pas bloquer le joueur
		if ( manager.State == NpcMenuState.DirectOpen )
		{
			manager.State = NpcMenuState.Closed;
			return;
		}

		// Lock camera sur le NPC
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn == null ) return;

		pawn.IsInNpcMenu = true;

		// Calculer la direction vers le torse du NPC
		var npcPos = WorldPosition + Vector3.Up * 72f;
		var playerEyePos = pawn.WorldPosition + Vector3.Up * 64f;
		var direction = (npcPos - playerEyePos).Normal;
		pawn.EyeAngles = Rotation.LookAt( direction ).Angles();

		// Zoom leger
		var camController = pawn.GameObject.Components.GetInDescendantsOrSelf<CameraController>();
		camController?.AddFieldOfViewOffset( -15f );
	}
}
