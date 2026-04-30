using OpenFramework.Inventory;
using OpenFramework.Inventory.UI;
using OpenFramework.Systems.Jobs;
using static Facepunch.NotificationSystem;

namespace OpenFramework.UI.QuickMenuSystem;

public record PlayerToPlayerActionMenu( PlayerPawn player ) : IQuickMenuInterface
{
	public string Title => player.DisplayName;
	public string SubTitle => "Interaction face à face";
	public QuickMenuStyle Style => new();

	// règles d’accès
	Client _self => Client.Local;

	public List<MenuItem> BuildMenu()
	{
		var list = new List<MenuItem>();


		// --- Social simple ---
		list.Add( new MenuItem( "Saluer", () =>
		{
			if ( !RequireProximity( player, 100f ) )
			{
				_self.Notify( NotificationType.Error, $"Vous êtes trop loin de {player.DisplayName}" );
				QuickMenu.Close();
				return;
			}

			player.Client.Notify( NotificationType.Generic, $"{_self.DisplayName} vous salue." );
		} ) );

		list.Add( new MenuItem( "Poignée de main 🤝", () =>
		{
			if ( !RequireProximity( player, 100f ) )
			{
				_self.Notify( NotificationType.Error, $"Vous êtes trop loin de {player.DisplayName}" );
				QuickMenu.Close();
				return;
			}

			player.Client.Notify( NotificationType.Generic, $"{_self.DisplayName} vous salue." );
		} ) );

		// --- Fouiller (seulement si la cible a les mains levées) ---
		list.Add( new MenuItem( "Fouiller", () =>
		{
			if ( !RequireProximity( player, 100f ) )
			{
				_self.Notify( NotificationType.Error, $"Vous êtes trop loin de {player.DisplayName}" );
				QuickMenu.Close();
				return;
			}

			if ( !player.IsHandsUp )
			{
				_self.Notify( NotificationType.Error, $"{player.DisplayName} n'a pas les mains en l'air." );
				QuickMenu.Close();
				return;
			}

			player.RequestFrisk( _self.PlayerPawn as PlayerPawn );
		}, Enabled: player.IsHandsUp, CloseMenuOnSelect: true ) );

		// --- Sac de tête (retrait uniquement — la pose se fait via clic droit inventaire après fouille) ---
		var selfPawn = _self.PlayerPawn as PlayerPawn;

		if ( player.HasHeadBagEquipped )
		{
			list.Add( new MenuItem( "Retirer le sac de la tête", () =>
			{
				if ( !RequireProximity( player, 100f ) )
				{
					_self.Notify( NotificationType.Error, $"Vous êtes trop loin de {player.DisplayName}" );
					QuickMenu.Close();
					return;
				}

				player.RequestRemoveBagFromHead( selfPawn );
			}, CloseMenuOnSelect: true ) );
		}

		// 👇 Sous-menu Give Item
		// --- Give Item (catégories -> items -> quantités) ---
		InventoryContainer Container = _self.PlayerPawn.GameObject.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		var giveItemChildren =
			Container.Items
				.Select( item => new MenuItem(
					item.Name,
					null,
					new List<MenuItem>   // Quantités
					{
								new($"1× {item.Name}",   () => RequestTrade(item, 1)),
								new($"5× {item.Name}",   () => RequestTrade(item, 5)),
								new($"10× {item.Name}",  () => RequestTrade(item, 10)),
					}
				) )
				.ToList();

		if ( giveItemChildren.Count > 0 )
			list.Add( new MenuItem( "Échanger / Trade", null, giveItemChildren ) );

		var job = JobSystem.GetJob( _self.Data.Job );
		var jobInteractionsList = job?.InteractionActions( player );

		if ( jobInteractionsList != null && jobInteractionsList.Count > 0 )
		{
			list.AddRange( jobInteractionsList );
		}


		// --- Identité / Documents (toujours avec consentement) ---
		/*list.Add( new MenuItem( "Montrer ma carte d'identité", () =>
			Documents.ShowMyIDTo( Client.Local, player ) ) );

		list.Add( new MenuItem( "Demander sa carte d'identité", () =>
			Requests.TryStart( "show_id", Client.Local, player, 10f,
				onAccept: () => Documents.ShowID( player, to: Client.Local ),
				askTargetText: $"{Client.Local.DisplayName} demande à voir votre carte d'identité. Accepter ?" ) )
		);

		// --- Aide / soins (ex: si métier = médecin) ---
		if ( Jobs.IsMedic( Client.Local ) )
		{
			list.Add( new MenuItem( "Soigner (25)", () =>
				Cooldown.Run( "heal_" + player.SteamId, 3f, () => Commands.Heal( player, 25 ) ) ) );
			list.Add( new MenuItem( "Réanimer", () =>
				Requests.TryStart( "revive", Client.Local, player, 8f,
					onAccept: () => Commands.Revive( player ),
					askTargetText: $"{Client.Local.DisplayName} veut vous réanimer. Accepter ?" ) ) );
		}

		// --- Porter / Escorter (ex: policier OU si la cible est consentante) ---
		var targetCuffed = Restraint.IsCuffed( player );
		if ( targetCuffed || Jobs.IsPolice( Client.Local ) )
		{
			list.Add( new MenuItem( "Porter / Escorter", () =>
			{
				if ( targetCuffed )
					Commands.StartEscort( Client.Local, player ); // pas besoin de consentement si menotté (RP)
				else
					Requests.TryStart( "escort", Client.Local, player, 8f,
						onAccept: () => Commands.StartEscort( Client.Local, player ),
						askTargetText: $"{Client.Local.DisplayName} souhaite vous escorter. Accepter ?" );
			} ) );
		}*/

		// --- Utilitaires ---
		//list.Add( new MenuItem( "Pinger sa position", () => Pings.FromTo( Client.Local, player ) ) );
		//list.Add( new MenuItem( "Envoyer un message", () => Commands.DM( Client.Local, player ) ) );
		//list.Add( new MenuItem( "Bloquer (mute perso)", () => Social.ToggleBlock( Client.Local, player ) ) );
		//list.Add( new MenuItem( "Signaler", () => UI.OpenReport( Client.Local, player ) ) );

		return list;
	}

	// ---------- Helpers proximité / visibilité / cooldown ----------
	public static bool RequireProximity( PlayerPawn target, float maxMeters )
		=> Vector3.DistanceBetween( Client.Local.PlayerPawn.WorldPosition, target.WorldPosition ) <= maxMeters;

	private void RequestTrade( InventoryItem item, int quantity)
	{

	}

	/*private static class Requests
	{
		// Système minimal de requête/consentement (à adapter au tien)
		public static bool TryStart( string kind, IClient from, PlayerPawn to, float timeout,
									Action onAccept, string askTargetText )
		{
			if ( from == null || to?.Client == null ) return false;

			// affiche une UI côté cible avec Accept/Refuse et timeout
			UI.ShowRequest( to.Client, new RequestData
			{
				Kind = kind,
				FromName = from.DisplayName,
				Message = askTargetText,
				TimeoutSeconds = timeout,
				OnAccept = onAccept
			} );
			from.Notify( NotificationType.Info, "Demande envoyée." );
			return true;
		}
	}*/
}
