using Facepunch;
using OpenFramework.Systems.Pawn;
using OpenFramework.World.Devices;
using static Facepunch.NotificationSystem;

namespace OpenFramework.World;

[Title( "Police Computer" ), Icon( "computer" ), Group( "World" )]
public sealed class PoliceComputer : BaseDevice
{
	[Property] public HighlightOutline Outline { get; set; }
	[Property] public float MaxUseDistance { get; set; } = 80f;

	/// <summary>
	/// True si le terminal police est ouvert pour le client local.
	/// Utilisé par PlayerInventory pour bloquer scroll + changement d'arme.
	/// </summary>
	public static bool IsAnyOpenLocally { get; private set; }

	protected override void OnStart()
	{
		base.OnStart();

		if ( Outline.IsValid() )
			Outline.Enabled = false;

		if ( ScreenUI.IsValid() )
			ScreenUI.Enabled = false;
	}

	protected override void OnUpdate()
	{
		if ( !IsOn ) return;

		// Fermeture auto si le joueur s'éloigne trop
		// (l'Échap est intercepté directement dans PoliceComputerScreen.razor)
		var pawn = Client.Local?.Pawn as PlayerPawn;
		if ( pawn != null && WorldPosition.Distance( pawn.WorldPosition ) > MaxUseDistance )
		{
			Log.Info( "[PoliceComputer] Auto-fermeture — distance dépassée" );
			PowerOff();
		}
	}

	/// <summary>Ouvre le terminal côté client. Vérifie job, distance et arme en main.</summary>
	public void Open()
	{
		var job = Client.Local?.Data?.Job?.ToLower();
		if ( job != "police" )
		{
			Client.Local?.Notify( NotificationType.Error, "Accès réservé à la police." );
			return;
		}

		var pawn  = Client.Local?.Pawn as PlayerPawn;
		var equip = pawn?.CurrentEquipment;
		var slot  = equip?.Resource?.Slot ?? EquipmentSlot.Undefined;
		bool hasWeapon = equip.IsValid() && slot is EquipmentSlot.Primary
		                                             or EquipmentSlot.Secondary
		                                             or EquipmentSlot.Melee
		                                             or EquipmentSlot.Throwable;
		if ( hasWeapon )
		{
			Client.Local?.Notify( NotificationType.Warning, "Rangez votre arme pour accéder au terminal." );
			return;
		}

		PowerOn();
	}

	public override void PowerOn()
	{
		IsAnyOpenLocally = true;
		base.PowerOn();
	}

	public override void PowerOff()
	{
		IsAnyOpenLocally = false;
		base.PowerOff();
	}

	/// <summary>Active ou désactive le surlignage de survol.</summary>
	public void SetHover( bool active )
	{
		if ( !Outline.IsValid() ) return;
		Outline.Enabled = active;
	}
}
