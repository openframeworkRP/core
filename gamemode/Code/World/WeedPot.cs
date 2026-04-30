using Facepunch;
using Sandbox.Events;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.Utility;

namespace OpenFramework.World;

public enum WeedType
{
	Haze, // Correspond au groupe "default"
	Purple // Correspond au groupe "purple"
}

[Icon( "grass" )]
[Title( "Weed Pot" )]
[Category( "Roleplay" )]
[EditorHandle( "editor/component_icons/weed_pot.svg" )]
public class WeedPot : 
	Component, 
	Component.ICollisionListener, 
	IDescription, IUse, IInventoryProvider,
	IGameEventHandler<KillEvent>
{
	// --- Section Informations ---
	[Property, Group( "Info" )]
	public string DisplayName { get; set; } = "Weed Pot";

	[Property] public string PersistenceId { get; set; } // Assigné via un système de sauvegarde monde
	[Property] public InventoryContainer Inventory { get; set; }

	// Implémentation de l'interface
	public string ContainerId => $"pot_{PersistenceId}";

	public string GetDescription()
	{
		if ( !HasSoil ) return "Besoin de terre";
		if ( !HasSoil ) return "Besoin de terre";
		if ( WaterLevel <= 0 ) return "Besoin d'eau";
		if ( GrowStage >= MaxStage ) return "Prêt pour la récolte [E]";
		return $"Amnésia {Type} : {GrowStage}/{MaxStage}";
	}

	public GrabAction GetGrabAction() => GrabAction.SweepLeft;

	// --- Section Visuelle ---
	[Property, Group( "Visuals" ), Title( "Model Renderer" )]
	[ReadOnly, RequireComponent]
	public SkinnedModelRenderer Renderer { get; set; }

	// --- Section Paramètres de Pousse ---
	[Property, Group( "Growth" ), Range( 1, 12 )]
	public int MaxStage { get; set; } = 12;

	[Property, Group( "Growth" ), Change, Sync( SyncFlags.FromHost )]
	[Range( 0, 12 )]
	public int GrowStage { get; set; }

	[Property, Group( "Growth" ), Title( "Seconds Per Stage" )]
	public float GrowDelay { get; set; } = 10f;

	[Property, Group( "Growth" ), ReadOnly, Sync( SyncFlags.FromHost )]
	public TimeUntil GrowTimer { get; set; }

	[Property, Group( "Growth" ), ReadOnly]
	public PrefabFile GrowEffect { get; set; }

	private TimeSince _lastSunCheck = 0;

	[Sync( SyncFlags.FromHost ), Property, ReadOnly]
	public float GrowthPercent { get; set; } = 0f; // 0.0 à 1.0

	[Property, Category( "Growth" )]
	public float SunMultiplier { get; set; } = 2.0f; // Pousse 2x plus vite au soleil

	[Property, ReadOnly, Category( "Growth" ), Sync( SyncFlags.FromHost )]
	public bool IsInSunlight { get; private set; }

	[Property, ReadOnly]
	public float TotalGrowthPercent
	{
		get
		{
			if ( MaxStage <= 0 ) return 0;

			// 1. Progression basée sur les étapes déjà terminées
			float basePercent = ((float)GrowStage / MaxStage);

			// 2. Progression fluide à l'intérieur de l'étape actuelle
			// On calcule combien de % l'étape en cours a parcouru
			float currentStepProgress = ((GrowDelay - GrowTimer.Relative) / GrowDelay).Clamp( 0, 1 );

			// On ajoute ce petit morceau (divisé par le nombre d'étapes total)
			float fluidProgress = (currentStepProgress / MaxStage);

			return ((basePercent + fluidProgress) * 100f).Clamp( 0, 100 );
		}
	}

	[Property, Group( "Harvest" )] 
	public GameObject FinishedPrefab { get; set; }
	[Property, Group( "Harvest" )]
	public bool DestroyWhenFinish { get; set; } = false;

	// --- Paramètres d'Eau ---
	[Property, Group( "Hydration" ), Range( 0, 100 ), Title( "Water Level (%)" )]
	[Change, Sync( SyncFlags.FromHost )] public float WaterLevel { get; set; } = 100f;

	// --- Section État ---
	[Property, Group( "State" ), Change, Sync( SyncFlags.FromHost )]
	public bool HasSoil { get; set; }

	[Property, Group( "State" ), Change, Sync( SyncFlags.FromHost )]
	public bool HasSeed { get; set; }

	[Property, Group( "Genetic" ), Change, Sync( SyncFlags.FromHost )]
	public WeedType Type { get; set; } = WeedType.Haze;

	[Property, Group( "Visuals/Colors" )]
	public bool AllowColoring { get; set; } = true;

	[Property, Group( "Visuals/Colors" )]
	public Color HazeColor { get; set; } = Color.Green;

	[Property, Group( "Visuals/Colors" )]
	public Color PurpleColor { get; set; } = Color.Green;

	[Property, Group( "Visuals/Colors" )]
	public Color DryColor { get; set; } = Color.White;

	// --- Logique ---

	public void OnTypeChanged() => UpdateVisuals();
	public void OnHasSeedChanged() => UpdateVisuals(); // Optionnel : ajouter un visuel de graine ?
	public void OnHasSoilChanged()
	{
		Renderer?.SetBodyGroup( "dirt", HasSoil ? 1 : 0 );
		ResetGrowTimer();
	}
	public void OnGrowStageChanged() => UpdateVisuals();
	public void OnWaterLevelChanged() => UpdateVisuals();

	/// <summary>
	/// Centralise toute la logique visuelle (BodyGroups + Couleurs)
	/// </summary>
	private void UpdateVisuals()
	{
		if ( Renderer == null ) return;

		// 1. Appliquer le Material Group (Texture)
		Renderer.MaterialGroup = (Type == WeedType.Purple) ? "purple" : "default";

		// 2. Gérer les Body Groups cumulatifs
		int currentLevel = GrowStage.Clamp( 0, MaxStage );
		for ( int i = 1; i <= MaxStage; i++ )
		{
			Renderer.SetBodyGroup( $"plant{i}", (i <= currentLevel) ? 1 : 0 );
		}

		// 3. Calcul de la couleur (Maturité x Hydratation)
		float growthPercent = (float)currentLevel / MaxStage;
		float waterPercent = WaterLevel / 100f;

		if( AllowColoring )
		{
			// La couleur cible dépend du type
			Color healthyColor = (Type == WeedType.Purple) ? PurpleColor : HazeColor;

			// On mélange d'abord la couleur saine avec la couleur sèche selon l'eau
			Color hydrationColor = Color.Lerp( DryColor, healthyColor, waterPercent );

			// Enfin, on applique la progression de croissance
			// (Une petite plante est toujours un peu "marron/terre" au début)
			Renderer.Tint = Color.Lerp( DryColor, hydrationColor, growthPercent );
		}
	}

	private void ResetPot()
	{
		WaterLevel = 0;
		GrowStage = 0;
		HasSoil = false;
		HasSeed = false;
		Type = WeedType.Haze;
		ResetGrowTimer();
	}

	protected override void OnStart()
	{
		// On initialise le premier timer au lancement
		ResetGrowTimer();
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		// 1. Conditions d'arrêt (besoins de base)
		if ( !HasSeed || !HasSoil || GrowStage >= MaxStage || WaterLevel <= 0 )
		{
			// Si les conditions ne sont pas remplies, on reset le timer pour "pauser" la pousse
			if ( GrowStage < MaxStage ) ResetGrowTimer();
			return;
		}

		// 2. Consommation d'eau passive (évaporation/absorption)
		WaterLevel -= 1f * Time.Delta;

		// 3. Vérification du soleil (optimisée toutes les secondes)
		if ( _lastSunCheck > 1.0f )
		{
			CheckSunlight();
			_lastSunCheck = 0;
		}

		// 4. Logique de pousse
		// Si on est au soleil, on "triche" en avançant le timer plus vite
		if ( IsInSunlight )
		{
			// On soustrait du temps supplémentaire au TimeUntil
			// Si SunMultiplier est 2.0, on veut que le temps passe 2x plus vite.
			// On a déjà 1s qui passe naturellement, on ajoute donc (Multiplier - 1) * Delta
			GrowTimer -= (SunMultiplier - 1f) * Time.Delta;
		}

		// 5. Changement d'étape
		if ( GrowTimer ) // Si le TimeUntil est arrivé à <= 0
		{
			GrowStage++;
			UpdateVisuals(); // On met à jour le modèle 3D

			ResetGrowTimer();

			// Consommation d'eau lors du pic de croissance
			WaterLevel = (WaterLevel - 5f).Clamp( 0, 100 );
		}
	}

	private void ResetGrowTimer() => GrowTimer = GrowDelay;

	private void CheckSunlight()
	{
		// Rayon vers le haut sur 3000 unités pour couvrir les bâtiments les plus hauts.
		// On ignore le pot lui-même et les objets non-pertinents.
		var from = WorldPosition + Vector3.Up * 20f;
		var to   = WorldPosition + Vector3.Up * 3000f;

		var tr = Scene.Trace.Ray( from, to )
			.WithoutTags( "player", "trigger", "ragdoll", "grab" )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		// On est dehors (soleil) si le rayon ne touche rien OU s'il touche un objet
		// non-statique (meuble, prop, rigidbody). Seule la géométrie statique réelle
		// (murs/plafonds/toit de bâtiment Hammer) compte comme "intérieur".
		IsInSunlight = !tr.Hit || tr.Body?.BodyType != PhysicsBodyType.Static;
	}

	public UseResult CanUse( PlayerPawn player )
	{
		// On ne peut utiliser le pot que si la plante est au max de sa croissance
		return GrowStage >= MaxStage;
	}

	public void OnUse( PlayerPawn player )
	{
		// 1. On spawn le modèle de pot fini à la place de celui-ci
		if ( FinishedPrefab.IsValid() )
		{
			// Position par défaut
			Vector3 spawnPos = WorldPosition;

			// Si on ne détruit pas le pot, on spawn l'objet un peu à côté ou au dessus
			if ( !DestroyWhenFinish )
			{
				// Par exemple : 15 unités vers l'avant et 5 unités vers le haut
				spawnPos += WorldRotation.Forward * 15f + Vector3.Up * 5f;
			}

			var finishedObject = FinishedPrefab.Clone( spawnPos, WorldRotation );

			// Optionnel : On peut ajouter une petite force pour que l'objet "saute" du pot
			if ( finishedObject.Components.TryGet<Rigidbody>( out var rb ) )
			{
				rb.Velocity = Vector3.Up * 50f + WorldRotation.Forward * 20f;
			}
		}

		if ( DestroyWhenFinish )
			GameObject.Destroy();
		else
			ResetPot();
	}

	public void OnCollisionStart( Collision collision )
	{
		// Host-only authority for attachment
		if ( !Networking.IsHost ) return;
		if ( IsProxy ) return;

		var other = collision.Other.GameObject;
		if ( other == null ) return;

		if ( Constants.Instance.Debug )
			Log.Info( $"WeedPot: OnCollisionStart with {other}" );

		// 1. Ajout de la TERRE
		if ( !HasSoil && other.Tags.Has( "soil_bag" ) )
		{
			HasSoil = true;
			other.Destroy();
			return;
		}

		if ( !HasSeed && HasSoil )
		{
			if ( other.Tags.Has( "weed_seed_purple" ) )
			{
				Type = WeedType.Purple;
				HasSeed = true;
				other.Destroy();
				return;
			}

			if ( other.Tags.Has( "weed_seed_haze" ) )
			{
				Type = WeedType.Haze;
				HasSeed = true;
				other.Destroy();
				return;
			}
		}

		if ( other.Tags.Has( "water" ) )
		{
			if ( WaterLevel < 100f )
			{
				WaterLevel = (WaterLevel + 25f).Clamp( 0, 100 );
				other.Destroy();
			}
			return;
		}
	}

	public void OnGameEvent( KillEvent eventArgs )
	{
		if ( eventArgs.DamageInfo.Victim.GameObject == GameObject )
		{
			GameObject.Destroy();
		}
	}
}
