using OpenFramework.Api;
using OpenFramework.Command;
using OpenFramework.Inventory;
using OpenFramework.Inventory.UI;
using OpenFramework.Systems;
using OpenFramework.Systems.Jobs;
using System.Numerics;
using System.Threading.Tasks;
using static Facepunch.NotificationSystem;

namespace OpenFramework.UI.QuickMenuSystem;

public record PersonalActionMenu() : IQuickMenuInterface
{
	public string Title => "Menu Personnel";
	public string SubTitle => "";
	public QuickMenuStyle Style => new();

	Client _self => Client.Local;

	public int GetRebuildHash()
	{
		var container = Client.Local?.PlayerPawn?.InventoryContainer;

		Log.Info( container );
		return HashCode.Combine(
			MoneySystem.Get(),
			container?.Items.Count() ?? 0,
			container?.Items.Sum( x => x.Quantity ) ?? 0,
			_self?.IsAdmin ?? false
		);
	}

	public List<MenuItem> BuildMenu()
	{
		var list = new List<MenuItem>();

		/*InventoryContainer Container = _self.PlayerPawn.GameObject.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		if ( Container is not null )
		{
			var inventoryChildren = Container.Slots
				.Where( entry => !entry.Value.IsEmpty )
				.OrderBy( entry => entry.Key )
				.Select( entry =>
				{
					var slotIndex = entry.Key;
					var item = entry.Value;

					// Label principal de l'item
					string label = item.Stackable ? $"{item.Name} ×{item.Quantity}" : item.Name;

					// Définition des actions pour cet item spécifique
					var itemActions = new List<MenuItem>();

					// Option : Utiliser
					itemActions.Add( new MenuItem( "✔️ Utiliser", async () =>
					{
						var meta = ItemMetadata.All.FirstOrDefault( x => x.ResourceName == item.FromResourceName );

						// --- LE PASS HACK VISUEL ---
						// On réduit la quantité immédiatement pour l'affichage local
						item = item with { Quantity = item.Quantity - 1 };

						// Lancement du RPC (Host)
						InventoryContainer.Use( item.Id, Container );

						// Utilisation de la durée définie dans les métadonnées (en secondes -> ms)
						if ( meta != null && meta.UseDuration > 0 )
						{
							// On attend la durée précise + un petit battement pour la sync réseau
							await Task.Delay( (int)(meta.UseDuration * 1000) + 200 );
						}

					}, GoBackOnSelect: true ) );

					// Option : Jeter (Spawn)
					itemActions.Add( new MenuItem( "📦 Jeter au sol", async () =>
					{
						// Le jet est instantané, on réduit juste la quantité visuelle
						item = item with { Quantity = item.Quantity - 1 };

						Transform tr = _self.PlayerPawn.Transform.World;
						tr.Position += tr.Rotation.Forward * 50 + Vector3.Up * 10;

						InventoryContainer.Drop( item.Id, Container, tr );

						// Pour le jet, on attend juste un court instant pour la synchronisation
						await Task.Delay( 150 );

					}, GoBackOnSelect: true ) );

					// On retourne un MenuItem qui contient le sous-menu des actions
					return new MenuItem( label, null, itemActions );
				} )
				.ToList();

			list.Add( new MenuItem( " 💼 Inventaire", null, inventoryChildren ) );
		}*/
		/*
		list.Add( new MenuItem( " 💼 Inventaire", () => FullInventory.Toggle(), CloseMenuOnSelect: true ) );
		*/
		// Menu creation:

		var finesList = _self.Data.Fines
			.Select( ( fine, index ) =>
			{
				// Label in fines overview
				var label = $"{(fine.Paid ? "✅" : "⚠️")} {fine.Reason} - {fine.Amount}$";

				// Details submenu
				var fineDetails = new List<MenuItem>()
				{
					// Issued date
					new MenuItem(
						$"📄 Émise le: {fine.IssuedAt:dd/MM/yyyy HH:mm}",
						null,
						null,
						Enabled: false
					),

					// Remaining time
					new MenuItem(
						"⏳ Temps restant: " +
						((fine.DueAt - DateTime.Now) <= TimeSpan.Zero
							? "Échue"
							: $"{(fine.DueAt - DateTime.Now).Days}j {(fine.DueAt - DateTime.Now).Hours}h {(fine.DueAt - DateTime.Now).Minutes}m"),
						null,
						null,
						Enabled: false
					)
				};

				// Add PAY only if not paid
				if ( !fine.Paid )
				{
					var capturedId = fine.Id;
					fineDetails.Insert( 0,
						new MenuItem(
							"💰 Payer",
							() =>
							{
								// Vérification légère côté client (UX) — la vraie validation est host-side
								if ( !MoneySystem.CanAfford( fine.Amount ) )
								{
									_self.Notify( NotificationType.Error, "Vous n'avez pas assez d'argent." );
									return;
								}
								PlayerApiBridge.PayFine( capturedId );
							},
							null,
							Enabled: true,
							CloseMenuOnSelect: true,
							GoBackOnSelect: true
						)
					);
				}
				else
				{
					fineDetails.Insert( 0,
						new MenuItem(
							$"✔️ Payée le {fine.PaidAt:dd/MM/yyyy HH:mm}",
							null,
							null,
							Enabled: false
						)
					);
				}

				// Main fine entry
				return new MenuItem(
					label,
					null,
					fineDetails,
					Enabled: !fine.Paid // disable if already paid
				);
			} )
			.ToList();
		/*
		list.Add(
			new MenuItem(
				" 🗃️ Mon portefeuille",
				null,
				new()
				{
					new MenuItem(
						"💵 Mon argent",
						() =>
						{
							var cl = _self;
							cl.Notify(
								NotificationType.Info,
								$"Cash: {cl.Data.Money}$ | Banque: {cl.Data.Bank}$"
							);
						}
					),
					new MenuItem(
						"🧾 Mes amendes",
						null,
						finesList,
						Enabled: finesList.Count >=1
					),
				}
			)
		);*/

		var equipment = _self.PlayerPawn.CurrentEquipment;
		// Dans la section où tu affiches les actions
		if ( equipment != null )
		{
			var item = ItemMetadata.All.Where( x => x.IsWeapon && x.WeaponResource == equipment.Resource ).FirstOrDefault();
			if ( item != null )
			{
				list.Add( new MenuItem( $" ⚔️ Ranger dans le sac", () =>
				{

					var containerRef = _self?.PlayerPawn?.GameObject.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
					//InventoryContainer.HostAdd( containerRef, item.ResourceName );
					Commands.RPC_GiveItem( Client.Local, item.ResourceName, 1 );
					Spawnable.Destroy( equipment.GameObject );
					_self.Notify( NotificationType.Success, $"Vous avez ranger {equipment.GameObject.Name} dans votre inventaire" );
				} ) );
			}
		}
		
		// Vérifie si le joueur a une arme déployée
		var deployed = _self.PlayerPawn.Inventory.Equipment
						 .FirstOrDefault( e => e.IsDeployed );
		
		// 3ème personne réservée aux admins
		if ( _self.IsAdmin )
		{
			// On autorise la 3ème personne uniquement si aucune arme ou si c'est les mains/punch
			bool canThirdPerson = deployed == null || deployed.Resource.Name == "hand" || deployed.Resource.Name == "Punch";

			// Si on est en troisième personne
			if ( _self.PlayerPawn.CameraController.Mode == CameraMode.ThirdPerson )
			{
				list.Add( new MenuItem( $" 👁️‍🗨️ Première personne", () =>
				{
					Commands.Thirdperson();
				},
				null,
				Enabled: true  // Toujours possible de revenir en 1P
				) );
			}
			else // Si on est en première personne
			{
				list.Add( new MenuItem( $" 📹 Troisième personne", () =>
				{
					Commands.Thirdperson();
				},
				null,
				Enabled: canThirdPerson  // Active seulement si mains ou punch
				) );
			}
		}
		

		var radio = _self.PlayerPawn.Components.Get<RadioComponent>();
		if ( radio is not null )
		{
			list.Add( new MenuItem( " 📻 Radio", null, new()
			{
				// On appelle la fonction RPC au lieu de faire IsActivate = !IsActivate
				new MenuItem( radio.IsActivate ? "🔴 Éteindre" : "🟢 Allumer", () => radio.ToggleRadio() ),

				new MenuItem( $"Changer de Fréquence", () => RadioMenu.Open() ),
				
			} ) );
		}


		if ( _self.IsAdmin )
		{
			list.Add( new MenuItem( " 🦺 Menu Admin", () =>
			{
				QuickMenu.OpenLocal<AdminActionMenu>();
			} ) );
		}
		list.Add( new MenuItem( " 🔍 Options", null, new()
		{
			//new("Changer skin", () => PlayerCustomization.OpenSkinMenu(_self)),
			new(" 👀 Réglages HUD", () => HudSettingsUI.Toggle(), CloseMenuOnSelect: true)
		} ) );

		var job = JobSystem.GetJob( _self.Data.Job );

		List<MenuItem> JobSub = new List<MenuItem>();
		JobSub.AddRange( job?.PersonalActions() );

		if ( !string.IsNullOrEmpty( _self.Data.Job ) && _self.Data.Job != "citizen" )
		{
			JobSub.Add( new MenuItem( $" ❌ Quitter mon job ({_self.Data.Job})", () =>
			{
				JobSystem.LeaveJob();
			} ) );
		}

		list.Add( new MenuItem( "Job", null, JobSub ));

		return list;
	}

}
