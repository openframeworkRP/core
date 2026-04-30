using Sandbox;
using Sandbox.Events;
using OpenFramework.Models;
using OpenFramework.Systems.Weapons;
using System.Collections.Generic;
using System.Linq;

namespace OpenFramework.GameLoop;

/// <summary>
/// Global Constants System - Gère toutes les configurations du serveur.
/// Plus besoin d'être attaché à un GameObject, s'exécute au niveau de la Scene.
/// </summary>
public sealed class Constants : Component, IGameEventHandler<ModifyDamageGlobalEvent>
{
	// L'instance est récupérée via le système de gestion des systèmes de s&box
	public static Constants Instance => Game.ActiveScene.GetComponentInChildren<Constants>();

	// --- Debug System ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Debug" )]
	public bool Debug { get; set; } = false;

	public static bool DebugMode() => Instance != null && Instance.Debug && Game.IsEditor;

	// --- Player Interaction ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Interaction" )]
	public float InteractionDistance { get; set; } = 200f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Steal" )]
	public float StealDistance { get; set; } = 1.5f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Steal" )]
	public float StealCooldown { get; set; } = 10f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Steal" )]
	public float StealSuccessChance { get; set; } = 0.45f;

	// --- Respawn & AFK ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Respawn System" )]
	public float RespawnDelay { get; set; } = 15f;

	// Delai d'attente EMS quand au moins un medecin est en ligne au moment de la mort.
	// Apres expiration, le bouton F apparait pour respawn manuel a l'hopital.
	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Respawn System" )]
	public float EMSWaitDelay { get; set; } = 150f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Afk System" )]
	public float AfkDelay { get; set; } = 300f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Afk System" )]
	public float AfkKickDelay { get; set; } = 60f;

	// --- Needs & Survival ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Needs" )]
	public float HungerDecayRate { get; set; } = 0.033f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Needs" )]
	public float ThirstDecayRate { get; set; } = 0.047f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Needs" )]
	public float StarvationDamage { get; set; } = 1.0f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Needs" )]
	public float MaxStamina { get; set; } = 100f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Needs" )]
	public float StaminaDrainRate { get; set; } = 10f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Needs" )]
	public float StaminaRegenRate { get; set; } = 5f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Needs" )]
	public float StaminaRegenDelay { get; set; } = 0.5f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Needs" )]
	public float ExhaustionRecoveryThreshold { get; set; } = 30f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Player" ), Group( "Needs" )]
	public float StaminaJumpCost { get; set; } = 20f;

	// --- Combat & Medical ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Medical" )]
	public float ReviveTime { get; set; } = 5f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Medical" )]
	public float BleedOutTime { get; set; } = 60f;

	// --- Economy ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Economy" ), Group( "Starting Values" )]
	public int DefaultCash { get; set; } = 0;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Economy" ), Group( "Starting Values" )]
	public int DefaultBank { get; set; } = 500;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Economy" ), Group( "Negatif Values" )]
	public int LimitBankNegative { get; set; } = -10000;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Economy" ), Group( "Rates" )]
	public float TaxRate { get; set; } = 0.05f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Economy" ), Group( "Rates" )]
	public float ItemSellMultiplier { get; set; } = 0.5f;

	

	// --- Jail / Law ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Law" ), Group( "Warrants" )]
	public float WarrantDuration { get; set; } = 900f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Law" ), Group( "Reasons" ), InlineEditor]
	public List<JailReason> JailReasons { get; set; } = new()
	{
		new() { Name = "Garde à vue", Duration = 900, Fine = 0 },
		new() { Name = "Meurtre", Duration = -1, Fine = 0 },
		new() { Name = "Tentative de meurtre", Duration = -1, Fine = 0 },
		new() { Name = "Mutinerie / Évasion", Duration = -1, Fine = 0 },
		new() { Name = "Vol", Duration = 3600, Fine = 500 },
		new() { Name = "Nuisance sonore", Duration = 900, Fine = 100 },
		new() { Name = "Insultes envers les forces de l'ordre", Duration = 1800, Fine = 150 },
		new() { Name = "Obstruction envers la police", Duration = 2700, Fine = 200 },
		new() { Name = "Fuite / Refus d'obtempérer", Duration = 1800, Fine = 250 },
		new() { Name = "Abus de force", Duration = 3600, Fine = 300 },
		new() { Name = "Possession d'armes illégales", Duration = 5400, Fine = 500 },
		new() { Name = "Intrusion propriété privée", Duration = 2700, Fine = 200 },
		new() { Name = "Intrusion poste de police", Duration = 3600, Fine = 300 },
		new() { Name = "Trafic illégal", Duration = 5400, Fine = 1000 },
		new() { Name = "Tir dans la rue", Duration = 3600, Fine = 400 },
		new() { Name = "Imprimantes illégales", Duration = 3600, Fine = 300 },
		new() { Name = "Crochetage", Duration = 1800, Fine = 150 },
		new() { Name = "Vente de produits illicites", Duration = 5400, Fine = 800 }
	};

	[Property, Sync( SyncFlags.FromHost ), Feature( "Law" ), Group( "Reasons" ), InlineEditor]
	public List<FineReason> FineReasons { get; set; } = new()
	{
		new() { Name = "Excès de vitesse (1–20 km/h)", Amount = 150 },
		new() { Name = "Excès de vitesse (20–40 km/h)", Amount = 300 },
		new() { Name = "Excès de vitesse (+40 km/h)", Amount = 600 },
		new() { Name = "Conduite dangereuse", Amount = 350 },
		new() { Name = "Conduite sous influence (alcool)", Amount = 500 },
		new() { Name = "Conduite sous influence (drogues)", Amount = 850 },
		new() { Name = "Feux rouges brûlés", Amount = 200 },
		new() { Name = "Non-respect du stop", Amount = 150 },
		new() { Name = "Refus de priorité", Amount = 180 },
		new() { Name = "Usage du téléphone au volant", Amount = 120 },
		new() { Name = "Conduite sans permis", Amount = 1200 },
		new() { Name = "Absence d’assurance", Amount = 900 },
		new() { Name = "Absence de contrôle technique", Amount = 200 },
		new() { Name = "Plaques illisibles / absentes", Amount = 120 },
		new() { Name = "Véhicule non conforme", Amount = 160 },
		new() { Name = "Nuisances sonores (véhicule)", Amount = 100 },
		new() { Name = "Stationnement interdit", Amount = 60 },
		new() { Name = "Stationnement gênant", Amount = 90 },
		new() { Name = "Stationnement handicapé sans autorisation", Amount = 300 },
		new() { Name = "Fuite lors d’un contrôle", Amount = 700 },
		new() { Name = "Refus d'obtempérer", Amount = 600 },
		new() { Name = "Entrave à la circulation", Amount = 200 },
		new() { Name = "Trouble à l'ordre public", Amount = 150 },
		new() { Name = "Bruit excessif sur la voie publique", Amount = 80 },
		new() { Name = "Regroupement illégal", Amount = 100 },
		new() { Name = "Manifestation non autorisée", Amount = 150 },
		new() { Name = "Possession de stupéfiants (faible quantité)", Amount = 250 },
		new() { Name = "Possession d'arme blanche", Amount = 300 },
		new() { Name = "Possession d’arme illégale", Amount = 900 },
		new() { Name = "Insultes envers un agent", Amount = 400 },
		new() { Name = "Outrage à agent", Amount = 650 },
		new() { Name = "Tentative de vol", Amount = 350 },
		new() { Name = "Vol simple (faible valeur)", Amount = 500 },
		new() { Name = "Transport d'objets volés", Amount = 450 },
		new() { Name = "Falsification de documents", Amount = 700 },
		new() { Name = "Dégradations légères", Amount = 200 },
		new() { Name = "Intrusion sur propriété privée", Amount = 150 },
		new() { Name = "Jet d’objets sur la voie publique", Amount = 80 },
		new() { Name = "Agression légère", Amount = 450 }
	};

	/// <summary>
	/// NPC DEALER & RESELL INTERVAL
	/// </summary>
	/// 
	[Property, Sync( SyncFlags.FromHost ),Feature( "NPC DEALER / RESELL" ), Group( "General" )]
	public float NpcChangeInterval { get; set; } = 300f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "NPC DEALER Spawn" ), Group( "General" )] 
	public List<GameObject> DealerSpawnPoints { get; set; } = new();

	[Property, Sync( SyncFlags.FromHost ), Feature( "NPC Reseller Spawn" ), Group( "General" )] 
	public List<GameObject> ResellerSpawnPoints { get; set; } = new();

	[Property, Sync( SyncFlags.FromHost ), Feature( "NPC Medic Spawn" ), Group( "General" )]
	public GameObject MedicSpawnPoint { get; set; }

	// --- Vehicles ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Vehicle" ), Group( "General" )]
	public float VehicleInteractionDistance { get; set; } = 300f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Vehicle" ), Group( "Fuel" )]
	public float DefaultFuelCapacity { get; set; } = 60f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Vehicle" ), Group( "Fuel" )]
	public float FuelConsumptionRate { get; set; } = 0.1f;

	// --- Props Limit ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Props" ), Group( "Limits" )]
	public int MinProps { get; set; } = 0;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Props" ), Group( "Limits" )]
	public int MaxProps { get; set; } = 20;

	// --- Communication ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Chat" ), Group( "Distances" )]
	public float LocalChatDistance { get; set; } = 15f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Chat" ), Group( "Distances" )]
	public float EmoteChatDistance { get; set; } = 6f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Voice" ), Group( "Distances" )]
	public float VoiceDistanceDefault { get; set; } = 12f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Voice" ), Group( "Distances" )]
	public float VoiceWhisperDistance { get; set; } = 3f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Voice" ), Group( "Distances" )]
	public float VoiceShoutDistance { get; set; } = 25f;

	// --- Crime & Stealth ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Crime" ), Group( "Interactions" )]
	public float FriskTime { get; set; } = 2.5f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Crime" ), Group( "Interactions" )]
	public float LockpickTime { get; set; } = 8f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Crime" ), Group( "Chances" )]
	public float PickpocketRevealChanceOnFail { get; set; } = 0.9f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Crime" ), Group( "Chances" )]
	public float WitnessRadius { get; set; } = 12f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Crime" ), Group( "Rewards" )]
	public int StealMinCash { get; set; } = 15;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Crime" ), Group( "Rewards" )]
	public int StealMaxCash { get; set; } = 60;

	// --- Inventory System ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Inventory" ), Group( "Limits" )]
	public float InventoryMaxWeight { get; set; } = 30f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Inventory" ), Group( "Limits" )]
	public int PocketMaxSlots { get; set; } = 6;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Inventory" ), Group( "Cleanup" )]
	public float DropLifetime { get; set; } = 300f;

	[Property, Feature( "Inventory" )] public GameObject BagPrefab { get; set; }
	[Property, Feature( "Inventory" )] public GameObject BoxPrefab { get; set; }

	// --- UI & Notifications ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "UI" ), Group( "Timeouts" )]
	public float RequestTimeoutDefault { get; set; } = 8f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "UI" ), Group( "Timeouts" )]
	public float NotificationDuration { get; set; } = 4f;

	// --- Jobs & NPCs ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "NPC" ), Group( "Sanctions" )]
	public float NpcBanSeconds { get; set; } = 300f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Job" ), Group( "Sanctions" )]
	public int JobMaxGarbageSpawn { get; set; } = 10;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Job" ), Group( "Finance" )]
	public int JobStartCapital { get; set; } = 100000;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Positions" ), Group( "Medical" )]
	public List<GameObject> HospitalRespawnPositions { get; set; } = new();

	[Property, Sync( SyncFlags.FromHost ), Feature( "Positions" ), Group( "Police" )]
	public List<GameObject> PrisonSpawnPositions { get; set; } = new();

	// --- Combat Damage System ---
	public class HitboxConfig
	{
		public HitboxTags Tags { get; set; }
		public float DamageScale { get; set; } = 1f;
		public bool ArmorProtects { get; set; }
		public bool HelmetProtects { get; set; }
	}

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Health" )]
	public float MaxHealth { get; private set; } = 100f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Health" )]
	public float MaxArmor { get; private set; } = 100f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Damage Scaling" )]
	public float BaseArmorReduction { get; set; } = 0.775f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Damage Scaling" )]
	public float BaseHelmetReduction { get; set; } = 0.775f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Weapon Spread" )]
	public float AimSpread { get; set; } = 0f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Weapon Spread" )]
	public float AimVelocitySpreadScale { get; set; } = 0.5f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Weapon Spread" )]
	public float BaseSpreadAmount { get; set; } = 0.05f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Weapon Spread" )]
	public float SpreadVelocityLimit { get; set; } = 350f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Weapon Spread" )]
	public float VelocitySpreadScale { get; set; } = 0.1f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Weapon Spread" )]
	public float CrouchSpreadScale { get; set; } = 0.5f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Weapon Spread" )]
	public float AirSpreadScale { get; set; } = 2.0f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Hitboxes" )]
	public List<HitboxConfig> Hitboxes { get; set; } = new()
	{
		new HitboxConfig { Tags = HitboxTags.Head, DamageScale = 5f, HelmetProtects = true },
		new HitboxConfig { Tags = HitboxTags.UpperBody | HitboxTags.Arm, ArmorProtects = true },
		new HitboxConfig { Tags = HitboxTags.LowerBody, DamageScale = 1.25f },
		new HitboxConfig { Tags = HitboxTags.Leg, DamageScale = 0.75f }
	};

	[Property, Sync( SyncFlags.FromHost ), Feature( "Combat" ), Group( "Mechanics" )]
	public bool RemoveHelmetOnHeadshot { get; set; } = true;

	// --- Movement System ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Environment" )]
	public Vector3 Gravity { get; set; } = new Vector3( 0, 0, 800 );

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "General" )]
	public float JumpPower { get; set; } = 290f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "General" )]
	public bool BunnyHopping { get; set; } = false;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "General" )]
	public bool EnableFallDamage { get; set; } = true;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Acceleration" )]
	public float AirAcceleration { get; set; } = 16f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Acceleration" )]
	public float BaseAcceleration { get; set; } = 9f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Acceleration" )]
	public float SlowWalkAcceleration { get; set; } = 10;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Acceleration" )]
	public float CrouchingAcceleration { get; set; } = 10;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Acceleration" )]
	public float SprintingAcceleration { get; set; } = 8f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Acceleration" )]
	public float MaxAcceleration { get; set; } = 10f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Acceleration" )]
	public float AirMaxAcceleration { get; set; } = 80f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Speed" )]
	public float WalkSpeed { get; set; } = 220f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Speed" )]
	public float SlowWalkSpeed { get; set; } = 100f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Speed" )]
	public float CrouchingSpeed { get; set; } = 100f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Speed" )]
	public float SprintingSpeed { get; set; } = 260f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Speed" )]
	public float MaxWeightSpeedPenalty { get; set; } = 60f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Friction" )]
	public float WalkFriction { get; set; } = 7f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Friction" )]
	public float SlowWalkFriction { get; set; } = 4f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Friction" )]
	public float CrouchingFriction { get; set; } = 4f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Friction" )]
	public float SprintingFriction { get; set; } = 4f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Lerp" )]
	public float CrouchLerpSpeed { get; set; } = 10f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Movement" ), Group( "Lerp" )]
	public float SlowCrouchLerpSpeed { get; set; } = 0.5f;

	// --- Global Server Settings ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Server" ), Group( "Damage Modifiers" )]
	public float TakeDamageAccelerationDampenTime { get; set; } = 1f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Server" ), Group( "Damage Modifiers" )]
	public float TakeDamageAccelerationOffset { get; set; } = 0.5f;

	// --- Discord Bridge ---
	[Property, Sync( SyncFlags.FromHost ), Feature( "Server" ), Group( "Discord" )]
	public bool EnableDiscordBridge { get; set; } = true;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Server" ), Group( "Discord" )]
	public float DiscordPollInterval { get; set; } = 5f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Server" ), Group( "Adverts" )]
	public bool EnableChatAdverts { get; set; } = true;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Server" ), Group( "Adverts" )]
	public float ChatAdvertsDelay { get; set; } = 120f;

	[Property, Sync( SyncFlags.FromHost ), Feature( "Server" ), Group( "Adverts" )]
	public List<string> ChatAdverts { get; set; } = new();

	/// <summary>
	/// Spawn Players
	/// </summary>
	[Property, Sync( SyncFlags.FromHost ), Feature( "Spawn" ), Group( "SpawnList" )]
	public List<SpawnPoint> SpawnPlayers { get; set; } = new();

	/// <summary>
	/// Gestion globale des dégâts (Events)
	/// </summary>
	[Early]
	void IGameEventHandler<ModifyDamageGlobalEvent>.OnGameEvent( ModifyDamageGlobalEvent eventArgs )
	{
		if ( eventArgs.DamageInfo.WasFallDamage && !EnableFallDamage )
		{
			eventArgs.ClearDamage();
			return;
		}

		var resource = (eventArgs.DamageInfo.Inflictor as Equipment)?.Resource;

		GetDamageModifications(
			eventArgs.DamageInfo.Flags, eventArgs.DamageInfo.Hitbox,
			resource?.ArmorReduction ?? BaseArmorReduction,
			resource?.HelmetReduction ?? BaseHelmetReduction,
			Hitboxes,
			out var damageScale, out var armorReduction, out var removeHelmet );

		eventArgs.ScaleDamage( damageScale );
		eventArgs.ApplyArmor( armorReduction );

		if ( removeHelmet )
		{
			eventArgs.RemoveHelmet();
		}
	}

	public static void GetDamageModifications(
		DamageFlags damageFlags,
		HitboxTags hitboxTags,
		float baseArmorReduction,
		float baseHelmetReduction,
		IReadOnlyList<HitboxConfig> hitboxConfigs,
		out float damageScale,
		out float armorReduction,
		out bool removeHelmet )
	{
		damageScale = 1f;
		armorReduction = 1f;
		removeHelmet = false;

		if ( hitboxConfigs.FirstOrDefault( x => (x.Tags & hitboxTags) != 0 ) is not { } config )
		{
			return;
		}

		damageScale = config.DamageScale;

		if ( config.HelmetProtects && (damageFlags & DamageFlags.Helmet) != 0 )
		{
			armorReduction = baseHelmetReduction;
			removeHelmet = true;
		}
		else if ( config.ArmorProtects && (damageFlags & DamageFlags.Armor) != 0 )
		{
			armorReduction = baseArmorReduction;
		}
	}
}
