using Sandbox;
using OpenFramework.Dialog;

namespace OpenFramework;

public sealed class NpcInteractionManager : Component
{
	public static NpcInteractionManager Instance { get; private set; }

	public NpcLogical CurrentNpc { get; set; }
	public NpcMenuState State { get; set; } = NpcMenuState.Closed;
	public DialogueNode CurrentDialogueNode { get; set; }

	// NPC déjà rencontrés par ce client — on ne montre le greeting qu'une fois
	private HashSet<NpcLogical> GreetedNpcs { get; set; } = new();

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public void OpenInteraction( NpcLogical npc )
	{
		npc?.ClientDebug( $"OpenInteraction npc={npc?.PnjName} HasFullDialogue={npc?.HasFullDialogue} dejaVu={GreetedNpcs.Contains( npc )}" );
		CurrentNpc = npc;
		CurrentDialogueNode = null;

		if ( npc.HasFullDialogue )
		{
			State = NpcMenuState.Interaction;
		}
		else if ( !GreetedNpcs.Contains( npc ) )
		{
			// Première interaction → présentation + voix TTS
			GreetedNpcs.Add( npc );
			State = NpcMenuState.Greeting;
			npc.ClientDebug( $"State=Greeting pour {npc.PnjName}" );

			// NPC dit son greeting à voix haute
			NpcTtsService.Instance?.SpeakAt( npc );
		}
		else
		{
			// Déjà rencontré → ouvre directement le panel sans bloquer le joueur
			StopNpcLooking( npc );
			npc.ClientDebug( $"DirectOpen UsePanel={(npc.UsePanel != null ? npc.UsePanel.GetType().Name : "NULL")} Enabled avant={npc.UsePanel?.Enabled}" );
			if ( npc.UsePanel != null )
			{
				npc.UsePanel.Enabled = true;
				npc.ClientDebug( $"DirectOpen Enabled apres={npc.UsePanel.Enabled}" );
			}
			State = NpcMenuState.DirectOpen;
			CurrentNpc = null;
			return;
		}
	}

	public void StartDialogue()
	{
		if ( CurrentNpc?.DialogueTree == null ) return;
		CurrentDialogueNode = CurrentNpc.DialogueTree;
		State = NpcMenuState.Dialogue;
	}

	public void SelectChoice( DialogueChoice choice )
	{
		choice.Action?.Invoke();
		CurrentDialogueNode = choice.NextNode;
		if ( CurrentDialogueNode == null )
		{
			Close();
		}
	}

	public void OpenUsePanel()
	{
		CurrentNpc?.ClientDebug( $"OpenUsePanel CurrentNpc={CurrentNpc?.PnjName} UsePanel={(CurrentNpc?.UsePanel != null ? CurrentNpc.UsePanel.GetType().Name : "NULL")} EnabledAvant={CurrentNpc?.UsePanel?.Enabled}" );
		RestorePlayer();
		StopNpcLooking( CurrentNpc );
		if ( CurrentNpc?.UsePanel != null )
		{
			CurrentNpc.UsePanel.Enabled = true;
			CurrentNpc.ClientDebug( $"OpenUsePanel EnabledApres={CurrentNpc.UsePanel.Enabled} GameObject={CurrentNpc.UsePanel.GameObject?.Name} Valid={CurrentNpc.UsePanel.IsValid()}" );
		}
		else
		{
			CurrentNpc?.ClientDebug( "OpenUsePanel: UsePanel est NULL, impossible d'ouvrir" );
		}
		State = NpcMenuState.Closed;
		CurrentNpc = null;
	}

	public void Close()
	{
		StopNpcLooking( CurrentNpc );
		RestorePlayer();
		CloseAllOpenUsePanels();
		State = NpcMenuState.Closed;
		CurrentDialogueNode = null;
		CurrentNpc = null;
	}

	// Ferme les UsePanel encore ouverts sur n'importe quel NPC de la scene.
	// Necessaire parce qu'en DirectOpen/OpenUsePanel on remet CurrentNpc=null,
	// donc on perd la reference au NPC proprietaire du panel actif.
	private void CloseAllOpenUsePanels()
	{
		foreach ( var npc in Scene.GetAllComponents<NpcLogical>() )
		{
			var panel = npc?.UsePanel;
			if ( panel != null && panel.IsValid() && panel.Enabled )
			{
				panel.Enabled = false;
			}
		}
	}

	private void StopNpcLooking( NpcLogical npc )
	{
		if ( npc == null ) return;
		npc.StopLookingOnHost();
	}

	private void RestorePlayer()
	{
		var pawn = Client.Local?.PlayerPawn;
		if ( pawn != null )
		{
			pawn.IsInNpcMenu = false;
		}
	}
}

public enum NpcMenuState
{
	Closed,
	DirectOpen,
	Greeting,
	Interaction,
	Dialogue
}
