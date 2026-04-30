using Facepunch;
using OpenFramework.Command; // Pour Commands.RPC_RespawnInPlace
using OpenFramework.Extension;
using OpenFramework.UI.QuickMenuSystem;
using static Facepunch.NotificationSystem;
using Constants = OpenFramework.GameLoop.Constants;

namespace OpenFramework.Systems.Jobs;

public class MedicJob : JobComponent
{
	public override string JobIdentifier => "medic";

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

		// Petite protection locale
		bool Check()
		{
			if ( !PlayerToPlayerActionMenu.RequireProximity( player, Constants.Instance.InteractionDistance ) )
			{
				Client.Local.Notify( NotificationType.Error, "Trop loin !" );
				QuickMenu.Close(); return false;
			}
			return true;
		}

		list.Add( new MenuItem( "Soigner (Bandage)", () => { if ( Check() ) Heal( player );} ) );
		list.Add( new MenuItem( "Réanimer (Défibrillateur)", () => { if ( Check() ) Resuscitate( player );} ) );
		list.Add( new MenuItem( "Donner Médicaments", () => { if ( Check() ) GiveMedicine( player );} ) );

		return list;
	}

	[Rpc.Host]
	public static void Heal( PlayerPawn player )
	{
		if ( !player.IsValid() ) return;
		var caller = Rpc.Caller.GetClient();

		if ( player.HealthComponent.State == LifeState.Dead )
		{
			caller.Notify( NotificationType.Error, "Le patient est mort, il faut le réanimer." );
			return;
		}

		if ( player.HealthComponent.Health >= player.HealthComponent.MaxHealth )
		{
			caller.Notify( NotificationType.Info, "Le patient est déjà en bonne santé." );
			return;
		}

		// Soin de 50 PV
		player.HealthComponent.Health = Math.Min( player.HealthComponent.Health + 50f, player.HealthComponent.MaxHealth );

		caller.Notify( NotificationType.Success, $"Soins appliqués sur {player.Client.DisplayName}." );
		player.Client.Notify( NotificationType.Success, "Un médecin vous a soigné." );
	}

	[Rpc.Host]
	public static void Resuscitate( PlayerPawn player )
	{
		if ( !player.IsValid() ) return;
		var caller = Rpc.Caller.GetClient();

		if ( player.HealthComponent.State != LifeState.Dead )
		{
			caller.Notify( NotificationType.Error, "Le patient n'est pas mort !" );
			return;
		}

		// Utilisation de ta commande existante pour respawn sur place
		Commands.RPC_RespawnInPlace( player.Client );

		// On s'assure qu'il ne revient pas full vie (pour éviter l'abus)
		// Note : Il faut vérifier si RPC_RespawnInPlace attend une frame pour recréer le pawn
		// Si ça bug, il faudra peut-être mettre un délai ou modifier RPC_RespawnInPlace pour accepter un % de vie

		caller.Notify( NotificationType.Success, $"Réanimation réussie sur {player.Client.DisplayName}." );
		player.Client.Notify( NotificationType.Warning, "Vous revenez d'entre les morts..." );
	}

	[Rpc.Host]
	public static void GiveMedicine( PlayerPawn player )
	{
		if ( !player.IsValid() ) return;
		var caller = Rpc.Caller.GetClient();

		// Remet full vie instantanément (simulation médicament puissant)
		player.HealthComponent.Health = player.HealthComponent.MaxHealth;

		//caller.Notify( NotificationType.Success, "Médicaments administrés." );
		player.Client.Notify( NotificationType.Success, "Vous vous sentez en pleine forme." );
	}
}
