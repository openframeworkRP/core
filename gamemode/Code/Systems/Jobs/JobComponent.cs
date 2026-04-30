using Facepunch;
using OpenFramework.Api;
using OpenFramework.Database;
using OpenFramework.Database.DTO;
using OpenFramework.Database.Tables;
using OpenFramework.Extension;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.UI.QuickMenuSystem;
using OpenFramework.Utility;
using System.Threading.Tasks;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Jobs;


[Flags]
public enum JobPerms
{
	None = 0,
	CanArrest = 1 << 0,
	CanFine = 1 << 1,
	CanUseLights = 1 << 2,
	CanSpawnGovCar = 1 << 3,
	CanHeal = 1 << 4,
	CanRevive = 1 << 5,
	CanLockpick = 1 << 6,
	CanChangeLaws = 1 << 7,
	CanSetTaxes = 1 << 8,
	CanSellWeapons = 1 << 9,
	CanTransportPlayers = 1 << 10,
	CanExtinguishFires = 1 << 11,
	CanSellDrugs = 1 << 12,
	CanSellFood = 1 << 13,
	CanReportNews = 1 << 14,
	CanProtect = 1 << 15,
	CanHuntCriminals = 1 << 16,
	CanPlayMusic = 1 << 17,
	CanCraftItems = 1 << 18,
	CanSteal = 1 << 19, // 👈 nouveau
}

public abstract partial class JobComponent : Component
{
	public class JobGrade
	{
		[Property] public string Name { get; set; } = string.Empty; // ex: "Officier", "Sergent", "Capitaine"
		[Property] public int Salary { get; set; } = 0; // salaire pour ce grade
		[Property] public int MaxPlayers { get; set; } = 0; // 0 = illimité
		[Property] public JobPerms Permissions { get; set; } = JobPerms.None;

		public virtual List<Client> Employees => GameUtils.AllPlayers.Where( x => x.Data.JobGrade == Name )
			.OrderBy( x => x.SteamName )
			.ToList();
	}

	// Identité
	[Property] public abstract string JobIdentifier { get; }
	[Property, Sync( SyncFlags.FromHost ), Description( "Display Name" )] public string DisplayName { get; set; } = "A definir";
	[Property, Sync( SyncFlags.FromHost ), Description( "Description of the job" )] public string Description { get; set; } = "A definir";
	[Property, Sync( SyncFlags.FromHost ), Description( "Color of the job" )] public Color Color { get; set; } = Color.White;
	[Property, Sync( SyncFlags.FromHost ), Description( "Color of the job" ), ResourceType("jpg")] public string Image { get; set; }
	[Property, Sync( SyncFlags.FromHost ), Description( "Is the job the default one ?" )] public bool IsDefault { get; set; } = false;

	// Règles
	[Property, Sync( SyncFlags.FromHost ), Description( "Max players allowed to join this job" )] public int MaxPlayers { get; set; } = 0;
	[Property, Sync( SyncFlags.FromHost ), Description( "Is this job whitelist ? (it will require to be voted to join it)" )] public bool WhitelistOnly { get; set; } = false;
	[Property, Sync( SyncFlags.FromHost ), Description( "Job switch cooldown that this job imposes globally" )] public float SwitchCooldown { get; set; } = 180;

	// Économie
	[Property, Sync( SyncFlags.FromHost ), Description( "The salary of this job" )] public int Salary { get; set; } = 100;
	[Property, Sync( SyncFlags.FromHost ), Description( "The pay of the salary interval in seconds." )] public float PayInterval { get; set; } = 180f;

	[Sync( SyncFlags.FromHost ), Description( "The job is being voted by the vote system." )] public bool IsBeingVoted { get; set; }


	// Permissions & loadout
	[Property] public JobPerms Permissions { get; set; } = JobPerms.None;
	[Property, Feature( "Equipments" )] public List<EquipmentResource> DefaultWeapons { get; set; }
	[Property, Feature( "Equipments" )] public List<EquipmentResource> DefaultItems { get; set; }
	[Property, Feature( "Equipments" )] public List<ItemMetadata> DefaultClothings { get; set; } 
	[Property, Feature( "Commands" )] public List<string> CommandsAccess { get; set; }

	[Property, FeatureEnabled( "Grades" )] public bool HasGrades { get; set; } = false;
	[Property, Sync( SyncFlags.FromHost ), Feature( "Grades" ), InlineEditor] public List<JobGrade> Grades { get; set; } = null;
	[Property, Sync( SyncFlags.FromHost )] public Dresser PlayerModel { get; set; }
	private JobDTO Data { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public int Capital
	{
		get => Data?.Capital ?? 0;             // safe si appelé trop tôt
		set
		{
			if ( !Networking.IsHost || Data == null ) return;

			// sécurité basique
			int verif = Math.Max( 0, value );
			if ( Data.Capital == verif ) return;     // évite le bruit réseau

			Data.Capital = verif;                  // la DB verra la modif (référence vivante)
											   // Optionnel: DatabaseManager.MarkDirty(Data); si tu veux flusher sans attendre
		}
	}

	// Spawns & véhicules (optionnel)
	public virtual IReadOnlyList<Vector3> SpawnPoints => Array.Empty<Vector3>();
	public virtual IReadOnlyList<string> VehiclePool => Array.Empty<string>(); // classnames véhicules

	public virtual List<Client> Employees => GameUtils.AllPlayers.Where( x => x.Data.Job == JobIdentifier )
		.OrderBy( x => x.SteamName )
		.ToList();

	// Hooks cycle de vie
	public virtual void OnJoin( Client client ) { }
	public virtual void OnLeave( Client client ) { }
	public virtual void OnSpawn( Client client, PlayerPawn pawn ) { }
	public virtual void OnTick( Client client ) { }

	protected override void OnAwake()
	{
		if ( !Networking.IsHost ) return;   // ✅ host only

		Timer.Host( $"job_{JobIdentifier}_salary", PayInterval, () => GiveSalary(), true );

		InitialiseData();
	}

	private void InitialiseData()
	{
		var table = DatabaseManager.Get<JobTable>();
		if ( table == null ) return;

		// Idéalement: utilise un identifiant stable (JobIdentifier) plutôt que DisplayName
		var row = table.GetAllRows().FirstOrDefault( x => x.Jobname == DisplayName );

		var start = Constants.Instance.JobStartCapital;

		if ( row == null )
		{
			row = new JobDTO
			{
				Id = Guid.NewGuid(),
				Jobname = DisplayName, // ou JobIdentifier
				Capital = Math.Max( 0, start )
			};
			table.InsertRow( row );
		}

		Data = row;
	}

	// Utilitaires
	public void GiveLoadout( PlayerPawn pawn )
	{
		if ( !Networking.IsHost ) return;   // ✅ host only

		if ( !pawn.IsValid() ) return;

		if( DefaultWeapons != null )
		{
			foreach ( var w in DefaultWeapons )
			{
				pawn.Inventory.Give( w );
			}
		}

		if ( DefaultItems != null )
		{
			foreach ( var i in DefaultItems )
			{
				pawn.Inventory.Give( i );

			}
		}
	}


	public void GiveClothing( PlayerPawn pawn )
	{
		if ( !Networking.IsHost ) return;
		if ( !pawn.IsValid() ) return;

		var clothingEquip = pawn.Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
		if ( clothingEquip?.Container == null ) return;

		var mainInventory = pawn.InventoryContainer;
		if ( mainInventory == null ) return;

		var dropTransform = pawn.Transform.World;
		dropTransform.Position += dropTransform.Rotation.Forward * 50 + Vector3.Up * 10;

		// 1) Vider les slots vêtements : retour en inventaire si possible, sinon par terre
		foreach ( var oldItem in clothingEquip.Container.Items.ToList() )
		{
			if ( oldItem?.Metadata == null ) continue;

			int freeSlot = mainInventory.GetFirstFreeSlot();
			bool canFit = freeSlot != -1
				&& mainInventory.AcceptsItem( oldItem.Metadata )
				&& mainInventory.CanFitWeight( oldItem.Metadata, oldItem.Quantity );

			if ( canFit )
			{
				ClothingEquipment.Unequip( clothingEquip, oldItem, mainInventory, freeSlot );
			}
			else
			{
				if ( oldItem.Metadata.ClothingResource != null )
					ClothingEquipment.BroadcastRemoveClothing( pawn.GameObject, oldItem.Metadata.ClothingResource.ResourcePath );

				InventoryContainer.Drop( oldItem, dropTransform, -1 );
			}
		}

		// 2) Équiper les vêtements de fonction dans leurs slots dédiés
		if ( DefaultClothings == null ) return;

		var usedSlots = new HashSet<int>();

		foreach ( var meta in DefaultClothings )
		{
			if ( meta?.ClothingResource == null ) continue;

			var slot = ClothingEquipment.GetSlotForClothing( meta );
			if ( slot == null ) continue;

			int slotIndex = (int)slot.Value;
			if ( !usedSlots.Add( slotIndex ) )
			{
				Log.Warning( $"[JobComponent] {meta.Name} cible le slot {slot.Value} déjà occupé par un autre vêtement de {DisplayName} — ignoré." );
				continue;
			}

			var go = new GameObject( true );
			go.Parent = clothingEquip.Container.GameObject;
			go.Name = $"Equipped_{meta.Name}";

			var invItem = go.Components.Create<InventoryItem>();
			invItem.Metadata = meta;
			invItem.SlotIndex = slotIndex;
			invItem.Quantity = 1;

			go.NetworkSpawn();

			Client.BroadcastEquip( pawn.GameObject, meta.ClothingResource.ResourcePath, Color.White );
		}

		clothingEquip.Container.MarkDirty();
	}



	// Give Salary
	// Persistance via l'API : endpoint /atm/salary protege par JWT serveur (Roles = "GameServer"),
	// donc impossible a appeler depuis un client. Le montant vient du JobComponent host-authoritative
	// (Salary / Grade.Salary syncs FromHost). Aucune entree n'est acceptee depuis le client.
	private async void GiveSalary()
	{
		if ( !Networking.IsHost ) return;   // host only — declenche par Timer.Host uniquement

		// Recolte les paychecks AVANT toute operation async pour capturer un snapshot stable
		// (un joueur qui change de job pendant l'await ne doit pas etre paye deux fois).
		var paychecks = new List<(Client Client, int Amount)>();

		if ( HasGrades && Grades != null )
		{
			foreach ( var g in Grades )
			{
				if ( g.Salary <= 0 ) continue;
				foreach ( var e in g.Employees )
					paychecks.Add( (e, g.Salary) );
			}
		}
		else if ( Salary > 0 )
		{
			foreach ( var e in Employees )
				paychecks.Add( (e, Salary) );
		}

		if ( paychecks.Count == 0 ) return;

		// Paiement parallele : pas d'await sequentiel, sinon avec N joueurs on cumule les latences API.
		var reason = $"Salaire {DisplayName}";
		var tasks = new List<Task>( paychecks.Count );
		foreach ( var p in paychecks )
			tasks.Add( PayOneSalaryAsync( p.Client, p.Amount, reason ) );

		try { await Task.WhenAll( tasks ); }
		catch ( Exception ex ) { Log.Warning( $"[Salary][{DisplayName}] WhenAll: {ex.Message}" ); }
	}

	private static async Task PayOneSalaryAsync( Client e, int amount, string reason )
	{
		if ( e == null || !e.IsValid() || amount <= 0 ) return;

		// Notification + cache local : valeur volatile cote game pour feedback UI immediat.
		// Le compte API reste la source de verite persistante.
		void ApplyLocal()
		{
			e.Data.Bank += amount;
			e.Notify( NotificationType.Success, $"Vous avez reçu votre salaire 😃 (+{amount}$)" );
		}

		var api = ApiComponent.Instance;
		if ( api == null || !api.IsServerAuthenticated )
		{
			ApplyLocal();
			Log.Warning( $"[Salary] API serveur non authentifiee — {amount}$ credites en memoire seulement pour {e.DisplayName}." );
			return;
		}

		var characterId = PlayerApiBridge.GetActiveCharacter( e.SteamId );
		if ( string.IsNullOrEmpty( characterId ) )
		{
			ApplyLocal();
			Log.Warning( $"[Salary] Pas de character actif pour {e.DisplayName} ({e.SteamId}) — {amount}$ credites en memoire seulement." );
			return;
		}

		try
		{
			var account = await api.GetBankAccount( characterId );
			if ( account == null )
			{
				ApplyLocal();
				Log.Warning( $"[Salary] Compte introuvable pour {e.DisplayName} (char {characterId}) — {amount}$ credites en memoire seulement." );
				return;
			}

			var result = await api.PaySalary( account.Id, amount, reason );
			if ( result?.Success == true )
			{
				ApplyLocal();
				return;
			}

			ApplyLocal();
			Log.Warning( $"[Salary] API a refuse pour {e.DisplayName} ({result?.Error ?? "?"}) — {amount}$ credites en memoire seulement." );
		}
		catch ( Exception ex )
		{
			ApplyLocal();
			Log.Warning( $"[Salary] Exception API pour {e.DisplayName}: {ex.Message} — {amount}$ credites en memoire seulement." );
		}
	}

	/*protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;   // ✅ host only

		if (PayTime && isInitialized)
		{
			PayTime = PayInterval;
			GiveSalary();
		}
	}*/

	public int GetMaxPlayers()
	{
		var count = 0;
		if ( HasGrades )
		{
			foreach ( var g in Grades )
				count += g.MaxPlayers;
		}
		else
			count = MaxPlayers;

		return count;
	}

	public bool HasPermission(JobPerms perm) => Permissions.HasFlag( perm );

	public virtual List<MenuItem> InteractionActions( PlayerPawn player )
	{
		var list = new List<MenuItem>();

		var myJob = JobSystem.GetJob( Client.Local.Data.Job );
		if ( myJob.HasPermission( JobPerms.CanSteal ))
		{
			list.Add( new MenuItem( "Voler", () =>
			{
				if ( !PlayerToPlayerActionMenu.RequireProximity( player, Constants.Instance.InteractionDistance ) )
				{
					Client.Local.Notify( NotificationType.Error, $"Vous êtes trop loin de {player.DisplayName}" );
					QuickMenu.Close();
					return;
				}

				TrySteal( player );
			} ) );
		}

		return list;
	}

	public virtual List<MenuItem> PersonalActions()
	{
		var list = new List<MenuItem>();

		return list;
	}

	[Rpc.Host]
	private static void TrySteal( PlayerPawn player )
	{
		// --- Basic validations ---
		if ( player == null || !player.IsValid() || player.HealthComponent.State != LifeState.Alive )
			return;

		var thief = Rpc.Caller?.GetClient();
		if ( thief == null || thief.PlayerPawn == null )
			return;

		var constants = Constants.Instance;
		if ( constants == null )
			return;

		// --- Permissions (server-side, anti-cheat) ---
		var job = JobSystem.GetJob( thief.Data.Job );
		if ( job == null || !job.HasPermission( JobPerms.CanSteal ) )
		{
			thief.Notify( NotificationType.Error, "Vous n'avez pas la permission de voler." );
			return;
		}

		// --- Distance (server-side, anti-cheat) ---
		if ( Vector3.DistanceBetween( thief.PlayerPawn.WorldPosition, player.WorldPosition ) > constants.StealDistance )
		{
			thief.Notify( NotificationType.Warning, $"Approchez-vous de {player.Client.DisplayName}." );
			return;
		}

		// 1. On calcule le temps restant via le EndTime synchronisé
		float remaining = thief.GetRemaining( thief.StealCooldownEndTime );

		if ( remaining > 0f )
		{
			// On utilise ton utilitaire de formatage avec la valeur 'remaining'
			thief.Notify( NotificationType.Info,
				$"Patientez {TimeUtils.DelayToReadable( remaining )} avant de retenter." );
			return;
		}

		// 2. On définit la fin du cooldown (Maintenant + Durée des constantes)
		thief.StealCooldownEndTime = Time.Now + constants.StealCooldown;

		// --- Success roll ---
		bool success = Game.Random.Float( 0f, 1f ) <= constants.StealSuccessChance;

		if ( !success )
		{
			thief.Notify( NotificationType.Error, "Vol raté !" );

			// Reveal to victim on fail (configurable)
			if ( Game.Random.Float() <= constants.PickpocketRevealChanceOnFail )
				player.Client.Notify( NotificationType.Warning, $"{thief.DisplayName} a tenté de vous voler !" );

			NotifyWitnessesCrime( thief.PlayerPawn.WorldPosition, thief.PlayerPawn, player );
			return;
		}

		// --- Success: steal cash (adapt to your economy/inventory) ---
		int roll = Game.Random.Int( constants.StealMinCash, constants.StealMaxCash );
		int amount = Math.Clamp( roll, 0, MoneySystem.Get( player.Client ) );

		if ( amount <= 0 )
		{
			thief.Notify( NotificationType.Info, "Rien d'intéressant à voler…" );
			return;
		}

		MoneySystem.Remove( player.Client, amount );
		MoneySystem.Add( thief, amount );

		thief.Notify( NotificationType.Success, $"Vous avez dérobé {amount}$ à {player.Client.DisplayName}." );
		player.Client.Notify( NotificationType.Error, $"On vous a volé {amount}$ !" );

		NotifyWitnessesCrime( thief.PlayerPawn.WorldPosition, thief.PlayerPawn, player );
	}

	// Optional: notify nearby witnesses for RP flavor
	private static void NotifyWitnessesCrime( Vector3 origin, PlayerPawn crimeComitter, PlayerPawn victim )
	{
		var constants = Constants.Instance;
		if ( constants == null || constants.WitnessRadius <= 0f )
			return;

		foreach ( var cl in GameUtils.AllPlayers )
		{
			if ( cl == null || cl == victim?.Client || cl == crimeComitter?.Client || cl.Pawn == null || cl.Pawn.HealthComponent.State != LifeState.Alive )
				continue;

			if ( Vector3.DistanceBetween( cl.Pawn.WorldPosition, origin ) <= constants.WitnessRadius )
				cl.Notify( NotificationType.Generic, "Vous remarquez un comportement suspect à proximité." );
		}
	}
}
