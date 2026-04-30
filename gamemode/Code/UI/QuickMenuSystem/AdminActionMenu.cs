using Facepunch;
using Facepunch.UI;
using Sandbox;
using OpenFramework.Command;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using OpenFramework.Systems.Jobs;
using OpenFramework.Systems.Vehicles;
using static Facepunch.NotificationSystem;
using static Sandbox.PhysicsContact;

namespace OpenFramework.UI.QuickMenuSystem;

public record AdminActionMenu() : IQuickMenuInterface
{
	public string Title => "Admin";
	public string SubTitle => "";
	public QuickMenuStyle Style => new();
	private JobSystem _jobSystem => Game.ActiveScene.GetComponentInChildren<JobSystem>();

	public List<MenuItem> BuildMenu()
	{
		var list = new List<MenuItem>();

		// Sous-menu Joueurs
		var players = GameUtils.AllPlayers
			.OrderBy( p => p.DisplayName )
			.Select( p =>
			{
				var actions = new List<MenuItem>();

				// ⭐ Mise en avant des joueurs importants dans le label
				bool isLocal = p == Client.Local;
				bool isDead = p.PlayerPawn.IsValid && p.PlayerPawn.HealthComponent.State == LifeState.Dead;
				bool isFrozen = p.PlayerPawn.IsValid && p.PlayerPawn.IsFrozen;
				bool isMuted = p.IsGlobalVocalMuted;
				// Adapte ça à ton système admin :
				bool isAdmin = p.IsAdmin; // <-- ou ton propre flag (Staff, Admin, etc.)

				string prefix = "👤";

				if ( isAdmin )
					prefix = "🛡️";
				else if ( isLocal )
					prefix = "⭐";
				else if ( isDead )
					prefix = "☠️";
				else if ( isFrozen )
					prefix = "❄️";

				if ( !p.PlayerPawn.IsValid )
					prefix += " (❓)";

				string playerLabel = $"{prefix} {p.DisplayName}";

				if ( p.PlayerPawn.IsValid )
				{
					// Freeze / Unfreeze
					if ( !p.PlayerPawn.IsFrozen )
						actions.Add( new MenuItem( "❄️ Freeze", null, new()
						{
						new("♾️ Indéfiniment", () => Commands.Freeze(p, -1)),
						new("⏱️ 30 sec",       () => Commands.Freeze(p, 30f)),
						new("⏱️ 1 min",        () => Commands.Freeze(p, 60f)),
						new("⏱️ 2 min",        () => Commands.Freeze(p, 120f)),
						new("⏱️ 5 min",        () => Commands.Freeze(p, 300f)),
						new("⏱️ 10 min",       () => Commands.Freeze(p, 600f)),
						new("⏱️ 15 min",       () => Commands.Freeze(p, 900f)),
						new("⏱️ 30 min",       () => Commands.Freeze(p, 1800f)),
						new("⏱️ 1 heure",      () => Commands.Freeze(p, 3600f)),
						}, GoBackOnSelect: true ) );
					else
						actions.Add( new( "🔥 Unfreeze", () => Commands.Freeze( p, -1 ) ) );

					// Respawn
					if ( p.PlayerPawn.HealthComponent.State == LifeState.Dead )
						actions.Add( new( "💀 Respawn", () => Commands.Respawn( p ) ) );

					// Mute / Unmute
					if ( !p.IsGlobalVocalMuted )
						actions.Add( new MenuItem( "🔇 Mute", null, new()
						{
						new("♾️ Indéfiniment", () => Commands.Mute(p, -1)),
						new("⏱️ 30 sec",       () => Commands.Mute(p, 30f)),
						new("⏱️ 1 min",        () => Commands.Mute(p, 60f)),
						new("⏱️ 2 min",        () => Commands.Mute(p, 120f)),
						new("⏱️ 5 min",        () => Commands.Mute(p, 300f)),
						new("⏱️ 10 min",       () => Commands.Mute(p, 600f)),
						new("⏱️ 15 min",       () => Commands.Mute(p, 900f)),
						new("⏱️ 30 min",       () => Commands.Mute(p, 1800f)),
						new("⏱️ 1h",           () => Commands.Mute(p, 3600f)),
						}, GoBackOnSelect: true ) );
					else
						actions.Add( new( "🔊 Unmute", () => Commands.Unmute( p ) ) );

					// Slaps
					actions.Add( new MenuItem( "👋 Slap", null, new()
					{
					new("💢 Petit slap (10hp)",  () => Commands.Slap(p, 10f)),
					new("💥 Moyen slap (25hp)",  () => Commands.Slap(p, 25f)),
					new("🔥 Gros slap (50hp)",   () => Commands.Slap(p, 50f)),
					new("☠️ Slap fatal (100hp)", () => Commands.Slap(p, 100f)),
					} ) );

					// Noclip
					if ( !p.PlayerPawn.IsNoclipping )
						actions.Add( new( "✈️ Enable Noclip", () => Commands.Noclip( p ) ) );
					else
						actions.Add( new( "🛑 Disable Noclip", () => Commands.Noclip( p ) ) );
				}

				//
				// ℹ️ Métadonnée (stats désactivées pour l’instant)
				//
				var metaChildren = new List<MenuItem>
				{
					new MenuItem($"❤️ Santé : {p.PlayerPawn.HealthComponent.Health}",   null, null, Enabled: true),
					new MenuItem($"💲 Cash : {MoneySystem.Get(p)}",     null, null, Enabled: true),
					new MenuItem($"🏦 Banque : {p.Data.Bank}",   null, null, Enabled: true),
					new MenuItem($"👔 Métier : {JobSystem.GetJob(p.Data.Job)?.DisplayName ?? "?"}",   null, null, Enabled: true),
					new MenuItem("⭐ Grade : (à venir)",    null, null, Enabled: true),
					new MenuItem("📍 Distance : (à venir)", null, null, Enabled: true),
					new MenuItem("🔊 Vocal : (à venir)",    null, null, Enabled: true),

					new MenuItem($"🍗 Faim : {p.Data.Hunger:0}%",  null, null, Enabled: false),
					new MenuItem($"🥤 Soif : {p.Data.Thirst:0}%", null, null, Enabled: false),
				};

				actions.Add( new MenuItem( "ℹ️ Métadonnée", null, metaChildren ) );

				//
				// 💸 Give Money
				//
				var capturedPlayer = p;
				actions.Add( new MenuItem( "💸 Give Money",
					InputPrompt: "Montant à donner",
					OnInputConfirm: ( input ) =>
					{
						if ( int.TryParse( input, out var amount ) && amount > 0 )
							Commands.GiveMoney( capturedPlayer, amount );
					}
				) );

				//
				// 🎁 Give Item
				//
				var giveItemChildren =
					ItemMetadata.All
						.GroupBy( m => m.ItemCategoryType )
						.OrderBy( g => g.Key )
						.Select( g => new MenuItem(
							$"📦 {g.Key}",
							null,
							g.OrderBy( m => m.Name )
							 .Select( m => new MenuItem(
								 $"🎁 {m.Name}",
								 null,
								 new()
								 {
								 new($"➕ 1× {m.Name}",  () => Commands.GiveItem(p, m.ResourceName, 1)),
								 new($"➕ 5× {m.Name}",  () => Commands.GiveItem(p, m.ResourceName, 5)),
								 new($"➕ 10× {m.Name}", () => Commands.GiveItem(p, m.ResourceName, 10)),
								 }
							 ) )
							 .ToList()
						) )
						.ToList();

				if ( giveItemChildren.Count > 0 )
					actions.Add( new MenuItem( "🎁 Give Item", null, giveItemChildren ) );

				//
				// 👷 Gestion avancée des jobs
				//
				var jobsList = _jobSystem.Jobs
					.OrderBy( j => j.IsDefault )
					.Select( job =>
					{
						// Occupation globale
						int currentEmployees = job.Employees.Count;
						int maxPlayers = job.GetMaxPlayers(); // ta méthode déjà existante
						string capText = maxPlayers <= 0 ? "∞" : maxPlayers.ToString();
						bool jobFull = maxPlayers > 0 && currentEmployees >= maxPlayers;

						// Icônes selon type de job (adapter si tu veux)
						string jobIcon = "👷";
						if ( job.JobIdentifier.Contains( "police", StringComparison.OrdinalIgnoreCase ) )
							jobIcon = "👮";
						else if ( job.JobIdentifier.Contains( "medic", StringComparison.OrdinalIgnoreCase ) ||
								 job.JobIdentifier.Contains( "ems", StringComparison.OrdinalIgnoreCase ) )
							jobIcon = "🧑‍⚕️";
						else if ( job.JobIdentifier.Contains( "taxi", StringComparison.OrdinalIgnoreCase ) )
							jobIcon = "🚕";
						else if ( job.JobIdentifier.Contains( "mechanic", StringComparison.OrdinalIgnoreCase ) )
							jobIcon = "🛠️";

						string whitelistIcon = job.WhitelistOnly ? "🔒 " : "";
						string fullIcon = jobFull ? "⛔ " : "";

						// Label global job
						string jobLabelBase = $"{jobIcon} {whitelistIcon}{job.DisplayName} ({currentEmployees}/{capText})";

						// Jobs sans grades → simple sélection
						if ( !job.HasGrades || job.Grades == null || job.Grades.Count == 0 )
						{
							return new MenuItem(
								fullIcon + jobLabelBase,
								() => JobSystem.SetJob( p, job.JobIdentifier, null ),
								null,
								Enabled: !jobFull
							);
						}

						// Jobs avec grades
						var gradeItems = job.Grades.Select( grade =>
						{
							int count = grade.Employees.Count;
							bool gradeFull = grade.MaxPlayers > 0 && count >= grade.MaxPlayers;
							string gradeCapText = grade.MaxPlayers == 0 ? "∞" : grade.MaxPlayers.ToString();
							string fullGradeIcon = gradeFull ? "⛔ " : "";

							string label = $"{fullGradeIcon}⭐ {grade.Name} ({count}/{gradeCapText})";

							return new MenuItem(
								label,
								() => JobSystem.SetJob( p, job.JobIdentifier, grade.Name ),
								null,
								Enabled: !gradeFull
							);
						} ).ToList();

						return new MenuItem(
							fullIcon + jobLabelBase,
							null,
							gradeItems,
							Enabled: !jobFull
						);
					} )
					.ToList();

				actions.Add( new MenuItem( "👷 Set Job", null, jobsList, Enabled: jobsList.Count >= 1 ) );

				actions.Add( new MenuItem( "🍗 Set Hunger", null, new()
				{
					new("0% (Affamé)",      () => Client.RemoveHunger( 999f )), // gros retrait → clamp à 0
					new("25%",              () => { var diff = 25f  - p.Data.Hunger; if (diff >= 0) Client.AddHunger(diff); else Client.RemoveHunger(-diff); }),
					new("50%",              () => { var diff = 50f  - p.Data.Hunger; if (diff >= 0) Client.AddHunger(diff); else Client.RemoveHunger(-diff); }),
					new("75%",              () => { var diff = 75f  - p.Data.Hunger; if (diff >= 0) Client.AddHunger(diff); else Client.RemoveHunger(-diff); }),
					new("100% (Rassasié)",  () => Client.AddHunger( 999f )), // très grand → clamp à 100
				}, GoBackOnSelect: true ) );


								actions.Add( new MenuItem( "🥤 Set Thirst", null, new()
				{
					new("0% (Déshydraté)",   () => Client.RemoveThirst( 999f )),
					new("25%",               () => { var diff = 25f  - p.Data.Thirst; if (diff >= 0) Client.AddThirst(diff); else Client.RemoveThirst(-diff); }),
					new("50%",               () => { var diff = 50f  - p.Data.Thirst; if (diff >= 0) Client.AddThirst(diff); else Client.RemoveThirst(-diff); }),
					new("75%",               () => { var diff = 75f  - p.Data.Thirst; if (diff >= 0) Client.AddThirst(diff); else Client.RemoveThirst(-diff); }),
					new("100% (Hydraté)",    () => Client.AddThirst( 999f )),
				}, GoBackOnSelect: true ) );

				//
				// 🦶 Kick / 🔨 Ban
				//
				actions.Add( new( "🦶 Kick - AFK", () => Commands.Kick( p, "AFK" ) ) );
				actions.Add( new( "🦶 Kick - Abuse", () => Commands.Kick( p, "Abus" ) ) );
				actions.Add( new( "🔨 Ban - 1h", () => Commands.Ban( p, TimeSpan.FromHours( 1 ).Seconds, "Ban 1h" ) ) );
				actions.Add( new( "⛔ Ban Permanent", () => Commands.Ban( p, 0, "Ban permanent" ) ) );

				//
				// 🚗 Véhicules (spawn / couleur OEM) pour ce joueur
				//
				var vehicleResources = ResourceLibrary.GetAll<VehiclePrefabResource>()
					.OrderBy( v => v.Brand.ToString() )
					.ThenBy( v => v.Model )
					.ToList();

				if ( vehicleResources.Count > 0 )
				{
					// On regroupe par marque pour que ce soit plus clean dans le menu
					var byBrand = vehicleResources
						.GroupBy( v => v.Brand )
						.OrderBy( g => g.Key.ToString() );

					var vehicleMenuChildren = new List<MenuItem>();

					foreach ( var brandGroup in byBrand )
					{
						var brandLabel = brandGroup.Key == VehicleBrand.None
							? "Autres"
							: brandGroup.Key.ToString();

						var brandVehiclesItems = new List<MenuItem>();

						foreach ( var vehicle in brandGroup )
						{
							// Sous-menu actions pour un modèle précis
							var vehicleActions = new List<MenuItem>();

							// 👉 À ADAPTER à tes commandes réelles (noms à titre d'exemple)
							vehicleActions.Add( new MenuItem(
								"🚗 Spawn ici",
								() => Commands.SpawnVehicle( p, vehicle.ResourcePath, false ) // à adapter
							) );

							vehicleActions.Add( new MenuItem(
								"🚗 Spawn & mettre dedans",
								() => Commands.SpawnVehicle( p, vehicle.ResourcePath, true ) // à adapter
							) );

							//
							// 🎨 Couleurs OEM disponibles pour cette marque
							//
							/*var colorPresets = ResourceLibrary.GetAll<VehicleColorPreset>()
								.Where( c => c.Brand == vehicle.Brand )
								.OrderBy( c => c.DisplayName )
								.ToList();

							if ( colorPresets.Count > 0 )
							{
								var colorItems = colorPresets
									.Select( preset => new MenuItem(
										$"🎨 {preset.DisplayName} ({preset.ColorCode})",
										() => Commands.SetVehicleColor( p, preset, vehicle ) // à adapter à ta logique
									) )
									.ToList();

								vehicleActions.Add( new MenuItem(
									"🎨 Couleurs OEM",
									null,
									colorItems
								) );
							}*/

							brandVehiclesItems.Add( new MenuItem(
								$"🚗 {vehicle.Brand} {vehicle.Model}",
								null,
								vehicleActions
							) );
						}

						vehicleMenuChildren.Add( new MenuItem(
							$"🏷 {brandLabel}",
							null,
							brandVehiclesItems
						) );
					}

					actions.Add( new MenuItem(
						"🚗 Véhicules",
						null,
						vehicleMenuChildren,
						Enabled: vehicleMenuChildren.Count > 0
					) );
				}

				return new MenuItem( playerLabel, null, actions );
			} )
			.ToList();

		if ( players.Count > 0 )
			list.Add( new MenuItem( "👥 Joueurs", null, players ) );


		list.Add( new MenuItem( "🌍 Monde", null ) );
		list.Add( new MenuItem( "🖥️ Serveur", null ) );

		// Outils rapides (sur soi)
		if ( Client.Local.PlayerPawn.IsValid )
		{
			list.Add( new MenuItem( "✈️ Toggle Noclip (moi)", () => Commands.Noclip( Client.Local ) ) );
		}

		return list;
	}

}
