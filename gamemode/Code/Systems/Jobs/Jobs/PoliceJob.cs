using Facepunch;
using OpenFramework.Api;
using OpenFramework.Command;
using OpenFramework.Extension;
using OpenFramework.GameLoop;
using OpenFramework.Models;
using OpenFramework.UI.QuickMenuSystem;
using OpenFramework.Utility;
using static Facepunch.NotificationSystem;
using static Sandbox.ModelPhysics;

namespace OpenFramework.Systems.Jobs;

public sealed class PoliceJob : JobComponent
{
	public override string JobIdentifier => "police";

	// Basic anti-abuse (seconds)
	private const float CuffCooldown = 3f;
	private const float FineCooldown = 3f;
	private const float JailCooldown = 3f;

	public override List<MenuItem> PersonalActions()
	{
		var list = base.PersonalActions();
		list.Add( new MenuItem( "Dispatch", () => OpenFramework.Systems.DispatchUI.Toggle() ) );
		return list;
	}

	public override List<MenuItem> InteractionActions( PlayerPawn player )
	{
		base.InteractionActions( player );

		var list = new List<MenuItem>();

		bool TooFar() => !PlayerToPlayerActionMenu.RequireProximity( player, Constants.Instance.InteractionDistance );

		// Vérification dynamique de l'état menotté
		var cuffComp = player.Components.Get<HandcuffComponent>();
		bool isCuffed = cuffComp != null && cuffComp.IsCuffed;

		list.Add( new MenuItem(
			isCuffed ? "Libérer (Démenotter)" : "Menotter",
			() => { 
				if ( TooFar() ) 
					return;
				
				RequestCuff( player );
			},
			CloseMenuOnSelect: true
		) );

		list.Add( new MenuItem(
			"Fouiller",
			() =>
			{
				if ( TooFar() ) { 
					NotifyLocalTooFar( player );
					return;
				}

				RequestSearch( player );
			},
			CloseMenuOnSelect: true
		) );

		list.Add( new MenuItem(
			"Transporter En Cellule",
			() =>
			{
				if ( TooFar() )
				{
					return;
				}

				RequestTpToCell( player );
			},
			CloseMenuOnSelect: true
		) );


		// Sous-menu des amendes basées sur Constants.Instance.FineReasons
		var fineChildren = Constants.Instance.FineReasons
			.Select( reason => new MenuItem(
				$"{reason.Name} ({reason.Amount}$)",
				() =>
				{
					if ( TooFar() ) { 
						NotifyLocalTooFar( player );
						return;
					}
					RequestFine( player, reason );
				},
				CloseMenuOnSelect: true,
				GoBackOnSelect: false
			) )
			.ToList();

		list.Add( new MenuItem( "Mettre une amende", null, fineChildren ) );

		return list;
	}

	private static void NotifyLocalTooFar( PlayerPawn target )
		=> Client.Local?.Notify( NotificationType.Error, $"Vous êtes trop loin de {target.DisplayName}" );

	private static bool ValidateTargets( out Client caller, PlayerPawn target, bool requireAlive = true )
	{
		caller = Rpc.Caller?.GetClient();
		if ( caller == null || caller.Pawn is not PlayerPawn callerPawn || !callerPawn.IsValid() )
			return false;

		if ( target == null || !target.IsValid() )
		{
			caller?.Notify( NotificationType.Error, "Cible invalide." );
			return false;
		}

		if ( requireAlive )
		{
			if ( target.HealthComponent.State != LifeState.Alive )
			{
				caller?.Notify( NotificationType.Error, "La cible n'est pas en état." );
				return false;
			}
			if ( callerPawn.HealthComponent.State != LifeState.Alive )
			{
				caller?.Notify( NotificationType.Error, "Vous n'êtes pas en état." );
				return false;
			}
		}

		if ( target.Client == caller )
		{
			caller.Notify( NotificationType.Error, "Action sur soi-même interdite." );
			return false;
		}

		var dist = Vector3.DistanceBetween( caller.Pawn.WorldPosition, target.WorldPosition );
		if ( dist > (Constants.Instance?.InteractionDistance ?? 150f) )
		{
			caller.Notify( NotificationType.Warning, $"Approchez-vous de {target.DisplayName}." );
			return false;
		}

		return true;
	}

	// ---------- CLIENT -> SERVER REQUESTS ----------

	[Rpc.Host]
	private static void RequestTpToCell( PlayerPawn target )
	{
		var caller = Rpc.Caller.GetClient();
		if ( !target.IsValid() ) return;

		Constants _constants = Constants.Instance;

		if ( target.IsValid() )
		{
			target.Client.Notify( NotificationType.Warning, "Vous avez était transporter en cellule." );
			Commands.RPC_RespawnInPrison( target.Client );
			//target.BodyRenderer.Set( "ik_hand_left_enabled", true );
			// TODO: Ajouter ici : Spawn d'un modèle de menottes sur les poignets (SetParent)
		}
	}

	[Rpc.Host]
	private static void RequestCuff( PlayerPawn target )
	{
		if ( !target.IsValid() ) return;

		var cuffComp = target.Components.GetOrCreate<HandcuffComponent>();
		cuffComp.IsCuffed = !cuffComp.IsCuffed;
		target.Client.Data.IsCuffed = cuffComp.IsCuffed; 

		if ( cuffComp.IsCuffed )
			target.Client.Notify( NotificationType.Warning, "Vous avez été menotté !" );
		else
			target.Client.Notify( NotificationType.Info, "Vos menottes ont été retirées." );
	}
	[Rpc.Host]
	private static void RequestSearch( PlayerPawn target )
	{
		var caller = Rpc.Caller.GetClient();
		if ( !target.IsValid() ) return;

		caller.Notify( NotificationType.Info, $"Fouille de {target.Client.DisplayName}..." );
		target.Client.Notify( NotificationType.Warning, "Un policier vous fouille." );

		// 1. Argent
		caller.Notify( NotificationType.Generic, $"💵 Argent liquide : {MoneySystem.Get(target.Client)}$" );

		// 2. Inventaire
		var container = target.InventoryContainer;
		if ( container != null && container.Items.Count() > 0 )
		{
			int foundItems = 0;
			foreach ( var item in container.Items )
			{
				if ( item != null )
				{
					// Affiche chaque item trouvé au policier
					caller.Notify( NotificationType.Generic, $"🔍 {item.Quantity}x {item.Name}" );
					foundItems++;
				}
			}

			if ( foundItems == 0 )
				caller.Notify( NotificationType.Info, "Les poches sont vides." );
		}
		else
		{
			caller.Notify( NotificationType.Info, "Aucun inventaire accessible." );
		}
	}

	[Rpc.Host]
	private static void RequestFine( PlayerPawn target, FineReason template )
	{
		if ( !ValidateTargets( out var caller, target ) ) return;

		// 1. On vérifie s'il reste du temps (Cooldown actif)
		float remaining = caller.GetRemaining( caller.FineEndTime );

		if ( remaining > 0 )
		{
			caller.Notify( NotificationType.Info,
				$"Patientez {remaining:F0}s avant de mettre une nouvelle amende." );
			return;
		}

		// 2. Si on arrive ici, le cooldown est fini, on en lance un nouveau
		// FineCooldown doit être une durée en secondes (ex: 30f)
		caller.FineEndTime = Time.Now + FineCooldown;

		var amount = Math.Max( template.Amount, 1 ); // pas de montant <= 0
		var reason = template.Name;

		var fine = new Fine
		{
			Id       = Guid.NewGuid().ToString(),
			IssuedAt = DateTime.Now,
			DueAt    = DateTime.Now.AddHours( 24 ),
			Amount   = amount,
			Reason   = reason
		};

		// Synchronise sur le réseau via la NetList du ClientData (host-authoritative)
		target.Client.Data.Fines.Add( fine );

		// Persiste dans l'API backend
		var callerCharId = PlayerApiBridge.GetActiveCharacter( caller.SteamId );
		var targetCharId = PlayerApiBridge.GetActiveCharacter( target.Client.SteamId );
		if ( !string.IsNullOrEmpty( targetCharId ) )
		{
			_ = ApiComponent.Instance.AddFine( targetCharId, new FineDto
			{
				Id                  = fine.Id,
				IssuedAt            = fine.IssuedAt,
				DueAt               = fine.DueAt,
				Amount              = fine.Amount,
				Reason              = fine.Reason,
				IssuedByCharacterId = callerCharId ?? "",
			} );
		}

		caller.Notify( NotificationType.Success,
			$"Amende de {amount}$" );
		target.Client?.Notify( NotificationType.Error,
			$"Vous avez reçu une amende de {amount}$" );

		CloseMenuFor( caller );
	}

	// ---------- CLIENT UI HELPERS ----------

	private static void CloseMenuFor( Client cl )
	{
		using ( Rpc.FilterInclude( cl.Connection ) )
			CloseQuickMenu();
	}

	[Rpc.Broadcast]
	private static void CloseQuickMenu()
	{
		QuickMenu.Close();
	}
}
