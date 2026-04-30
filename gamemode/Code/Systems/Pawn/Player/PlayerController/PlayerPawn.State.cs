using Facepunch;
using Facepunch.UI;
using Sandbox.Diagnostics;
using Sandbox.Events;
using OpenFramework;
using OpenFramework.Command;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.Inventory.UI;
using OpenFramework.Systems;
using OpenFramework.Systems.Dispatch;
using OpenFramework.Systems.Jobs;
using OpenFramework.UI;
using OpenFramework.Utility;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Pawn;

public record OnPlayerRagdolledEvent : IGameEvent
{
	public float DestroyTime { get; set; } = 0f;
}

public partial class PlayerPawn
{
	[RequireComponent] public ArmorComponent ArmorComponent { get; private set; }
	[RequireComponent] public PlayerInventory Inventory { get; private set; }

	[Sync( SyncFlags.FromHost )] public TimeSince TimeSinceLastRespawn { get; private set; }

	private GameObject _ragdoll;

	/// <summary>
	/// True quand le timer de respawn est écoulé — le joueur PEUT choisir de respawn.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool CanRespawnManually { get; private set; } = false;

	/// <summary>
	/// True si le joueur attend volontairement les EMS (a décidé de ne pas respawn auto).
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool WaitingForEMS { get; private set; } = false;

	/// <summary>
	/// ID de l'appel dispatch EMS créé à la mort de ce joueur (host-only).
	/// -1 si aucun appel actif. Permet de clôturer automatiquement l'appel
	/// au respawn / à la réanimation pour faire disparaître le waypoint.
	/// </summary>
	private int _activeEMSDispatchCallId = -1;

	// ─────────────────────────────────────────────
	//  UPDATE MORT — appelé depuis UpdateDead() dans PlayerPawn.cs
	// ─────────────────────────────────────────────

	public void UpdateDeadRespawnLogic()
	{
		// Seul l'hôte traite la logique de respawn pour éviter les désynchronisations
		if ( !Networking.IsHost ) return;

		var remaining = Client.GetRemaining( Client.RespawnEndTime );

		// Vérification si le délai de mort est expiré
		if ( remaining <= 0f && !CanRespawnManually )
		{
			Log.Info( $"[Respawn] Délai expiré pour {Client.DisplayName}. Analyse de la présence EMS..." );

			CanRespawnManually = true;
			int emsCount = GetEMSOnline();

			if ( emsCount > 0 )
			{
				// Cas avec médecins : on attend une action manuelle ou un soin
				WaitingForEMS = true;
				Log.Info( $"[Respawn] {emsCount} EMS en ligne. Attente d'un soin ou respawn manuel pour {Client.DisplayName}." );
				NotifyRespawnAvailableOwner();
			}
			else
			{
				// Cas sans médecins : nettoyage automatique
				Log.Info( $"[Respawn] Aucun EMS en ligne. Envoi de {Client.DisplayName} à l'hôpital (perte d'items)." );
				RespawnAtHospitalWithoutItems();
			}
		}

		// Sécurité : Si le dernier EMS quitte le serveur pendant que le joueur attend
		if ( WaitingForEMS && GetEMSOnline() <= 0 )
		{
			Log.Info( $"[Respawn] Plus d'EMS disponibles, arrêt de l'attente pour {Client.DisplayName}." );
			WaitingForEMS = false;
			RespawnAtHospitalWithoutItems();
		}
	}

	// ─────────────────────────────────────────────
	//  RESPAWN MANUEL (joueur appuie sur la touche)
	// ─────────────────────────────────────────────

	/// <summary>
	/// Appelé quand le joueur choisit de respawn manuellement après le timer.
	/// Le joueur respawn à l'hôpital SANS ses items.
	/// </summary>
	[Rpc.Host]
	public static void RequestManualRespawn( PlayerPawn pawn )
	{
		if ( !Networking.IsHost || !pawn.IsValid() ) return;
		if ( pawn.HealthComponent.State != LifeState.Dead ) return;
		if ( !pawn.CanRespawnManually ) return;

		pawn.RespawnAtHospitalWithoutItems();
	}

	private void RespawnAtHospitalWithoutItems()
	{
		Assert.True( Networking.IsHost );

		// Drop les armes equipees au sol (DroppedEquipment) + Inventory.Clear()
		// Avant le drop du container : sinon les InventoryItem lies aux armes equipees
		// seraient dans le sac ET au sol = duplication.
		var dropper = Scene.GetComponentInChildren<EquipmentDropper>();
		dropper?.DropEquipmentForPlayer( this );

		// Fusion des items du joueur dans un SEUL sac de mort : inventaire principal
		// + vetements equipes. CreateDroppedInventory agrandit dynamiquement la
		// Capacity du sac si le total depasse 24 (jusqu'a 24 + 10 = 34 slots).
		var mainInventory = InventoryContainer;
		var clothingEquip = Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
		var clothingInventory = clothingEquip?.Container;

		var mergedItems = new List<InventoryItem>();
		if ( mainInventory != null )
			mergedItems.AddRange( mainInventory.Items );
		if ( clothingInventory != null )
			mergedItems.AddRange( clothingInventory.Items );

		if ( mergedItems.Count > 0 )
		{
			Log.Info( $"[RespawnAtHospital] Drop {mergedItems.Count} items (inventaire + vetements) de {DisplayName} à {Client.DeathPosition}" );
			// On utilise le container principal s'il existe, sinon celui des vetements,
			// comme source de l'appel host : CreateDroppedInventory transfere les items
			// via MoveItem peu importe leur container d'origine.
			var sourceContainer = mainInventory ?? clothingInventory;
			sourceContainer?.CreateDroppedInventory( mergedItems, Client.DeathPosition, true );
		}

		CanRespawnManually = false;
		WaitingForEMS = false;

		Commands.RPC_RespawnHospital( Client );
	}

	// ─────────────────────────────────────────────
	//  HELPERS
	// ─────────────────────────────────────────────

	public static int GetEMSOnline()
	{
		var job = JobSystem.GetJob( "medic" );
		if ( job?.Employees == null ) return 0;

		int count = 0;
		foreach ( var emp in job.Employees )
		{
			try
			{
				if ( emp?.IsValid == true &&
					 emp.PlayerPawn?.IsValid == true &&
					 emp.PlayerPawn.HealthComponent.State != LifeState.Dead )
					count++;
			}
			catch { }
		}
		return count;
	}

	[Rpc.Owner]
	private void NotifyRespawnAvailableOwner()
	{
		Client.Local?.Notify( NotificationType.Warning,
			"Le timer est écoulé. Appuyez sur F pour respawn à l'hôpital (sans vos items), ou attendez les EMS." );
	}

	// ─────────────────────────────────────────────
	//  MORT
	// ─────────────────────────────────────────────

	public override void OnKill( DamageInfo damageInfo )
	{
		if ( Networking.IsHost )
		{
			ArmorComponent.HasHelmet = false;
			ArmorComponent.Armor = 0f;
			CanRespawnManually = false;
			WaitingForEMS = false;

			if ( Client.IsValid() )
			{
				Client.OnKill( damageInfo );
				Client.IsChatMuted = true;
				DroppedInventory.Release( Client );

				SendEMSDispatch( damageInfo );
			}

			CreateRagdoll();
		}

		PlayerBoxCollider.Enabled = true;

		if ( !IsProxy )
		{
			Log.Info( $"[DiagMort] OnKill client: pawn={GameObject.Name}, HealthState={HealthComponent?.State}, IsPossessed={IsPossessed}, CameraMode={CameraController?.Mode}" );
			_previousVelocity = Vector3.Zero;
			CameraController.Mode = CameraMode.ThirdPerson;
			CloseAllPlayerMenus();
		}
	}

	// Envoie un appel dispatch EMS + un Notify direct à chaque médecin connecté.
	// L'identité de la victime est volontairement masquée pour l'EMS : on annonce
	// "un citoyen" et seules la position et la nature de la blessure remontent.
	private void SendEMSDispatch( DamageInfo damageInfo )
	{
		Assert.True( Networking.IsHost );

		const string AnonymousCaller = "Citoyen";
		const string Description = "Un citoyen est à terre, intervention médicale requise.";

		// Appel dans le dispatch panel des EMS (anonyme — pas de nom de victime ni d'agresseur)
		// On retient l'ID pour pouvoir clôturer l'appel automatiquement au respawn / revive.
		_activeEMSDispatchCallId = DispatchSystem.SendCallFromServer( DispatchType.MedicalEmergency, Description, AnonymousCaller, WorldPosition );

		// Notification popup directe à chaque EMS en vie
		var medicJob = JobSystem.GetJob( "medic" );
		if ( medicJob?.Employees == null ) return;
		foreach ( var emp in medicJob.Employees )
		{
			try
			{
				if ( emp?.IsValid == true &&
				     emp.PlayerPawn?.IsValid == true &&
				     emp.PlayerPawn.HealthComponent.State != LifeState.Dead )
				{
					emp.Notify( NotificationType.Warning, "🚑 Un citoyen est à terre !" );
				}
			}
			catch { }
		}
	}

	// Ferme tous les menus/panels qu'un joueur peut avoir ouvert quand il meurt,
	// sinon ils restent affiches pendant l'ecran de mort et apres respawn a l'hosto.
	private void CloseAllPlayerMenus()
	{
		NpcInteractionManager.Instance?.Close();

		if ( FullInventory.Instance != null )
			FullInventory.Instance.IsOpen = false;

		InventorySlot.CloseActiveContextMenu();

		if ( PropsMenu.Instance != null )
			PropsMenu.Close();

		HudSettingsUI.Instance?.Close();

		// Cabine d'essayage : desactive le ScreenPanel au cas ou le joueur mourrait a l'interieur.
		var dressingRoom = Scene?.GetAllComponents<ShopDressingRoom>().FirstOrDefault();
		if ( dressingRoom?.ScreenPanelDressing != null && dressingRoom.ScreenPanelDressing.Enabled )
		{
			dressingRoom.ScreenPanelDressing.Enabled = false;
			dressingRoom.IsPlayerInside = false;
			dressingRoom.ApplyCameraSettings( false );
		}
	}

	// ─────────────────────────────────────────────
	//  RESPAWN (inchangé)
	// ─────────────────────────────────────────────

	public void SetSpawnPoint( SpawnPointInfo spawnPoint )
	{
		SpawnPosition = spawnPoint.Position;
		SpawnRotation = spawnPoint.Rotation;
		SpawnPointTags.Clear();
		foreach ( var tag in spawnPoint.Tags )
			SpawnPointTags.Add( tag );
	}

	public override void OnRespawnInHospital()
	{
		Assert.True( Networking.IsHost );
		OnHostRespawnInHopital();
		OnClientRespawn();
		CloseActiveEMSDispatch();
	}

	public override void OnRespawnInPrison()
	{
		Assert.True( Networking.IsHost );
		OnHostRespawnInHopital();
		OnClientRespawn();
		CloseActiveEMSDispatch();
	}

	public override void OnRespawnInPlace()
	{
		Assert.True( Networking.IsHost );
		OnHostRespawnInPlace();
		OnClientRespawn();
		CloseActiveEMSDispatch();
	}

	public override void OnRespawn()
	{
		Assert.True( Networking.IsHost );
		OnHostRespawn();
		OnClientRespawn();
		CloseActiveEMSDispatch();
	}

	// Clôture l'appel dispatch EMS associé à ce joueur s'il en a un en cours.
	// Appelé sur tous les chemins de respawn / revive pour faire disparaître
	// l'appel du panel et le waypoint GPS chez tous les médecins.
	private void CloseActiveEMSDispatch()
	{
		if ( !Networking.IsHost ) return;
		if ( _activeEMSDispatchCallId < 0 ) return;

		DispatchSystem.CloseCallFromServer( _activeEMSDispatchCallId );
		_activeEMSDispatchCallId = -1;
	}

	private void OnHostRespawnInHopital()
	{
		Assert.True( Networking.IsHost );
		DestroyRagdoll();
		UnmuteOnRespawn();
		_previousVelocity = Vector3.Zero;
		Teleport( Client.HopitalPosition, Client.LocalRotation );
		if ( Body is not null ) Body.DamageTakenForce = Vector3.Zero;
		ArmorComponent.HasHelmet = false;
		ArmorComponent.Armor = 0f;
		HealthComponent.Health = HealthComponent.MaxHealth;
		TimeSinceLastRespawn = 0f;
		ResetBody();
		Scene.Dispatch( new PlayerSpawnedEvent( this ) );
	}

	private void OnHostRespawnInPrison()
	{
		Assert.True( Networking.IsHost );
		DestroyRagdoll();
		UnmuteOnRespawn();
		_previousVelocity = Vector3.Zero;
		Teleport( Client.HopitalPosition, Client.LocalRotation );
		if ( Body is not null ) Body.DamageTakenForce = Vector3.Zero;
		if ( HealthComponent.State != LifeState.Alive )
		{
			ArmorComponent.HasHelmet = false;
			ArmorComponent.Armor = 0f;
		}
		HealthComponent.Health = HealthComponent.MaxHealth;
		TimeSinceLastRespawn = 0f;
		ResetBody();
		Scene.Dispatch( new PlayerSpawnedEvent( this ) );
	}

	private void OnHostRespawnInPlace()
	{
		Assert.True( Networking.IsHost );
		DestroyRagdoll();
		UnmuteOnRespawn();
		_previousVelocity = Vector3.Zero;
		Teleport( Client.DeathPosition, Client.LocalRotation );
		if ( Body is not null ) Body.DamageTakenForce = Vector3.Zero;
		if ( HealthComponent.State != LifeState.Alive )
		{
			ArmorComponent.HasHelmet = false;
			ArmorComponent.Armor = 0f;
		}
		HealthComponent.Health = HealthComponent.MaxHealth;
		TimeSinceLastRespawn = 0f;
		ResetBody();
		Scene.Dispatch( new PlayerSpawnedEvent( this ) );
	}

	private void OnHostRespawn()
	{
		Assert.True( Networking.IsHost );
		DestroyRagdoll();
		UnmuteOnRespawn();
		_previousVelocity = Vector3.Zero;
		Teleport( SpawnPosition, SpawnRotation );
		if ( Body is not null ) Body.DamageTakenForce = Vector3.Zero;
		HealthComponent.Health = HealthComponent.MaxHealth;
		TimeSinceLastRespawn = 0f;
		ResetBody();
		Scene.Dispatch( new PlayerSpawnedEvent( this ) );
	}

	[Rpc.Owner]
	private void OnClientRespawn()
	{
		if ( !Client.IsValid() || IsNpc ) return;
		Possess();
	}

	public void Teleport( Transform transform )
		=> Teleport( WorldPosition, transform.Rotation );

	[Rpc.Owner]
	public void Teleport( Vector3 position, Rotation rotation )
	{
		Transform.World = new( position, rotation );
		Transform.ClearInterpolation();
		EyeAngles = rotation.Angles();
		if ( CharacterController.IsValid() )
		{
			CharacterController.Velocity = Vector3.Zero;
			CharacterController.IsOnGround = true;
		}
	}

	[Rpc.Broadcast]
	private void CreateRagdoll()
	{
		if ( !Body.IsValid() || !Body.Renderer.IsValid() ) return;

		var ragdollObj = new GameObject( true, $"Ragdoll_{DisplayName}" );
		ragdollObj.WorldPosition = Body.Renderer.WorldPosition;
		ragdollObj.WorldRotation = Body.Renderer.WorldRotation;
		ragdollObj.Tags.Add( "ragdoll" );

		var ragdollOwner = ragdollObj.AddComponent<RagdollOwner>();
		ragdollOwner.OwnerClient = Client;

		var newRenderer = ragdollObj.AddComponent<SkinnedModelRenderer>();
		newRenderer.Model = Body.Renderer.Model;
		newRenderer.MaterialOverride = Body.Renderer.MaterialOverride;
		newRenderer.Tint = Body.Renderer.Tint;
		newRenderer.MaterialGroup = Body.Renderer.MaterialGroup;
		newRenderer.BodyGroups = Body.Renderer.BodyGroups;

		var physics = ragdollObj.AddComponent<ModelPhysics>();
		physics.Model = Body.Renderer.Model;
		physics.Renderer = newRenderer;
		physics.Enabled = true;

		foreach ( var child in Body.GameObject.Children )
		{
			var childRenderer = child.Components.Get<SkinnedModelRenderer>();
			if ( childRenderer is null ) continue;

			var accessoryObj = new GameObject( true, child.Name );
			accessoryObj.Parent = ragdollObj;
			accessoryObj.LocalPosition = child.LocalPosition;
			accessoryObj.LocalRotation = child.LocalRotation;

			var accessoryRenderer = accessoryObj.AddComponent<SkinnedModelRenderer>();
			accessoryRenderer.Model = childRenderer.Model;
			accessoryRenderer.MaterialOverride = childRenderer.MaterialOverride;
			accessoryRenderer.Tint = childRenderer.Tint;
			accessoryRenderer.MaterialGroup = childRenderer.MaterialGroup;
			accessoryRenderer.BodyGroups = childRenderer.BodyGroups;
			accessoryRenderer.BoneMergeTarget = newRenderer;
		}

		var playerVelocity = CharacterController.IsValid() ? CharacterController.Velocity : Vector3.Zero;
		foreach ( var b in ragdollObj.Components.GetAll<PhysicsBody>( FindMode.EverythingInSelf ) )
			b.Velocity = playerVelocity;

		Body.GameObject.Enabled = false;
		Body = null;

		_ragdoll = ragdollObj;
	}

	[Rpc.Broadcast]
	private void DestroyRagdoll()
	{
		// On identifie le ragdoll par Client et non par pawn : lors d'un respawn hopital,
		// l'ancien pawn est detruit et un NOUVEAU pawn est cree, puis DestroyRagdoll est
		// appele sur ce nouveau pawn (voir Client.Spawning.cs RespawnInHospital). Une
		// comparaison r.OwnerPawn == this echouerait donc systematiquement en multi, car
		// OwnerPawn pointe sur l'ancien pawn detruit, et `this` est le nouveau pawn.
		// Le Client est la seule reference stable a travers le respawn.
		var client = Client;
		if ( !client.IsValid() )
		{
			_ragdoll = null;
			return;
		}

		var owned = Scene?.GetAllComponents<RagdollOwner>()
			.Where( r => r.IsValid() && r.OwnerClient == client )
			.Select( r => r.GameObject )
			.Where( go => go.IsValid() )
			.ToList();

		if ( owned != null )
		{
			foreach ( var go in owned )
				go.Destroy();
		}

		_ragdoll = null;
	}

	private void UnmuteOnRespawn()
	{
		if ( !Client.IsValid() ) return;
		Client.IsChatMuted = false;
	}

	private void ResetBody()
	{
		if ( Body is not null )
			Body.DamageTakenForce = Vector3.Zero;

		PlayerBoxCollider.Enabled = true;

		foreach ( var outfitter in GetComponentsInChildren<HumanOutfitter>( true ) )
		{
			// outfitter.UpdateFromTeam( Team );
		}
	}
}
