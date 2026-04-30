using Sandbox;
using System.Text.Json;

namespace OpenFramework;

public sealed class PlayerAppearance : Component
{
	[Property] public PlayerPawn Pawn { get; set; }
	[Property] public PlayerBody MaleBody   { get; set; }
	[Property] public PlayerBody FemaleBody { get; set; }

	[Sync(SyncFlags.FromHost), Change(nameof(OnAppearanceChanged))]
	public bool IsFemale { get; set; } = false;

	[Sync(SyncFlags.FromHost), Change(nameof(OnAppearanceChanged))]
	public int HeadIndex { get; set; } = 0;

	[Sync(SyncFlags.FromHost), Change(nameof(OnAppearanceChanged))]
	public string SkinTone { get; set; } 

	[Sync(SyncFlags.FromHost), Change(nameof(OnAppearanceChanged))]
	public string MorphsJson { get; set; } = "{}";

	private PlayerBody ActiveBody => IsFemale ? FemaleBody : MaleBody;

	// ── Appelé côté host après fetch API ──────────────────────────────────
	public void SetAppearanceFromServer( bool female, string morphsJson,
		int headIndex , string skinTone )
	{
		if ( !Networking.IsHost ) return;

		IsFemale   = female;
		HeadIndex  = headIndex;
		SkinTone   = skinTone;
		MorphsJson = morphsJson;

		// Le callback [Change] ne se déclenche pas côté émetteur (host), uniquement
		// côté récepteur (clients). On force Apply() ici pour que Pawn.Body et
		// l'état Enabled des GameObjects soient corrects sur le host — sans quoi
		// le host sync MaleBody.Enabled=true vers tous les clients, qui voient
		// l'homme même si IsFemale=true.
		Apply();
	}

	// ── Sync callbacks ────────────────────────────────────────────────────
// Dans PlayerAppearance.cs

	private void OnAppearanceChanged() 
	{
		// On attend la fin de la frame pour être sûr que toutes les variables Sync 
		// sont arrivées avant d'appliquer, pour éviter de build 4 fois le model
		Apply();
	}

	protected override void OnStart()
	{
		Apply();
	}

	private void Apply()
	{
		// IMPORTANT: Vérifie que nous avons les références nécessaires
		if ( MaleBody == null || FemaleBody == null ) return;

		// Log pour debugger sur les clients
		// Log.Info($"Applying Appearance: Female={IsFemale}, Tone={SkinTone}");

		if ( IsFemale )
		{
			MaleBody.GameObject.Enabled = false;
			FemaleBody.GameObject.Enabled = true;
			if ( Pawn.IsValid() ) Pawn.Body = FemaleBody;
        
			FemaleBody.ApplyAppearance( HeadIndex, SkinTone, MorphsJson );
		}
		else
		{
			MaleBody.GameObject.Enabled = true;
			FemaleBody.GameObject.Enabled = false;
			if ( Pawn.IsValid() ) Pawn.Body = MaleBody;

			MaleBody.ApplyAppearance( HeadIndex, SkinTone, MorphsJson );
		}
	}
}