using Sandbox.Diagnostics;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems.Weapons;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Command;

public static partial class Commands
{
	/// <summary>
	/// Commande admin : test complet du systeme de munitions.
	/// Give arme + chargeur + boite, recharge, tire, verifie la consommation.
	/// </summary>
	[Command( "Test Ammo System", ["testammo"], "Test complet munitions : give, reload, shoot, verify", "ui/icons/item.svg", CommandPermission.Admin )]
	public static void TestAmmo()
	{
		RPC_TestAmmo();
	}

	[Rpc.Host]
	public static async void RPC_TestAmmo()
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Acces refuse !" );
			return;
		}

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() )
		{
			caller.Notify( NotificationType.Error, "Pas de pawn actif." );
			return;
		}

		var container = pawn.GameObject.Components
			.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );

		if ( container == null )
		{
			caller.Notify( NotificationType.Error, "Pas d'inventaire." );
			return;
		}

		int passed = 0;
		int failed = 0;

		void LogStep( string msg ) => Log.Info( $"[TestAmmo] {msg}" );
		void LogOk( string msg ) { Log.Info( $"[TestAmmo] OK : {msg}" ); passed++; }
		void LogFail( string msg ) { Log.Error( $"[TestAmmo] FAIL : {msg}" ); failed++; }

		// ============================================================
		// ETAPE 1 : Donner les items (boite .45 ACP + chargeur USP + arme USP)
		// ============================================================
		LogStep( "Etape 1 — Distribution des items..." );

		InventoryContainer.Add( container, "box_45acp", 1 );
		InventoryContainer.Add( container, "mag_usp", 1 );
		InventoryContainer.Add( container, "usp", 1 );

		// Attendre que OnStart() cree les sous-conteneurs (frame suivante)
		await GameTask.DelaySeconds( 0.2f );

		// ============================================================
		// ETAPE 2 : Verifier la boite de munitions
		// ============================================================
		LogStep( "Etape 2 — Verification boite de munitions..." );

		var boxItem = container.Items
			.FirstOrDefault( x => x.Metadata?.ResourceName == "box_45acp" );

		if ( boxItem == null ) { LogFail( "Boite .45 ACP introuvable dans l'inventaire" ); return; }

		int boxCount = boxItem.PackCount;
		if ( boxCount == 50 )
			LogOk( $"Boite contient {boxCount}/50 balles .45 ACP (pre-remplissage OK)" );
		else
			LogFail( $"Boite devrait contenir 50 balles, contient {boxCount}" );

		// ============================================================
		// ETAPE 3 : Verifier le chargeur (vide)
		// ============================================================
		LogStep( "Etape 3 — Verification chargeur vide..." );

		var magItem = container.Items
			.FirstOrDefault( x => x.Metadata?.ResourceName == "mag_usp" );

		if ( magItem == null ) { LogFail( "Chargeur USP introuvable" ); return; }

		if ( magItem.MagAmmo == 0 )
			LogOk( "Chargeur est vide (normal, pas de pre-remplissage)" );
		else
			LogFail( $"Chargeur devrait etre vide, contient {magItem.MagAmmo}" );

		// ============================================================
		// ETAPE 4 : Remplir directement le chargeur via son attribut
		// ============================================================
		LogStep( "Etape 4 — Remplissage 13 balles dans le chargeur..." );

		magItem.MagAmmo = 13;

		await GameTask.DelaySeconds( 0.1f );

		int magCount = magItem.MagAmmo;
		if ( magCount == 13 )
			LogOk( $"Chargeur rempli : {magCount}/13 balles" );
		else
			LogFail( $"Chargeur devrait avoir 13 balles, a {magCount}" );

		// Verifier que le setter clampe a la capacite max (13 pour mag_usp)
		magItem.MagAmmo = 18;
		await GameTask.DelaySeconds( 0.1f );

		int magOverflow = magItem.MagAmmo;
		if ( magOverflow == 13 )
			LogOk( "Plafond respecte : setter clampe a la capacite du chargeur" );
		else
			LogFail( $"Plafond non respecte : chargeur a {magOverflow} balles au lieu de 13 max" );

		// ============================================================
		// ETAPE 5 : Equiper l'arme et lier le chargeur
		// ============================================================
		LogStep( "Etape 5 — Equipement de l'arme et chargement..." );

		var weaponItem = container.Items
			.FirstOrDefault( x => x.Metadata?.ResourceName == "usp" );

		if ( weaponItem == null ) { LogFail( "Item USP introuvable dans l'inventaire" ); return; }

		// Recuperer la resource EquipmentResource
		var equipResource = weaponItem.Metadata?.WeaponResource;
		if ( equipResource == null ) { LogFail( "WeaponResource null sur l'item USP" ); return; }

		// Donner l'arme physique au joueur (comme le fait le systeme normal)
		var equipment = pawn.Inventory.Give( equipResource );
		if ( equipment == null ) { LogFail( "Impossible de Give l'equipement USP" ); return; }

		// Etablir le lien Equipment <-> InventoryItem
		equipment.LinkedItem = weaponItem;

		await GameTask.DelaySeconds( 0.2f );

		// Recuperer le composant WeaponAmmo
		var weaponAmmo = equipment.GetComponentInChildren<WeaponAmmo>();
		if ( weaponAmmo == null ) { LogFail( "Composant WeaponAmmo introuvable sur l'arme" ); return; }

		// Lier l'item a WeaponAmmo
		weaponAmmo.LinkedItem = weaponItem;

		// Charger le chargeur dans l'arme
		weaponAmmo.LoadMagazine( magItem );

		if ( weaponAmmo.HasMagazine )
			LogOk( "Chargeur insere dans l'arme" );
		else
			LogFail( "Echec de LoadMagazine — HasMagazine = false" );

		if ( weaponAmmo.Ammo == 13 )
			LogOk( $"Ammo synchronise : {weaponAmmo.Ammo}/{weaponAmmo.MaxAmmo}" );
		else
			LogFail( $"Ammo devrait etre 13, est {weaponAmmo.Ammo}" );

		// ============================================================
		// ETAPE 6 : Tirer (consommer des balles)
		// ============================================================
		LogStep( "Etape 6 — Simulation de 3 tirs..." );

		int ammoAvant = weaponAmmo.Ammo;

		weaponAmmo.ConsumeBullet();
		weaponAmmo.ConsumeBullet();
		weaponAmmo.ConsumeBullet();

		int ammoApres = weaponAmmo.Ammo;
		int consumed = ammoAvant - ammoApres;

		if ( consumed == 3 )
			LogOk( $"3 balles consommees : {ammoAvant} -> {ammoApres}" );
		else
			LogFail( $"Devrait avoir consomme 3 balles, en a consomme {consumed} ({ammoAvant} -> {ammoApres})" );

		// Verifier que les attributs du chargeur ont bien diminue
		if ( weaponAmmo.Ammo == 10 )
			LogOk( $"Attributs chargeur OK : {weaponAmmo.Ammo} balles restantes" );
		else
			LogFail( $"Attributs chargeur devrait etre a 10, est a {weaponAmmo.Ammo}" );

		// ============================================================
		// ETAPE 6b : Test de tir reel via Shootable.Shoot()
		// ============================================================
		LogStep( "Etape 6b — Test de tir reel via Shootable..." );

		// Attendre la fin du cooldown de deploiement (no_shooting tag expire apres 0.66s)
		await GameTask.DelaySeconds( 0.7f );

		var shootable = equipment.GetComponentInChildren<Shootable>();
		if ( shootable == null )
		{
			LogFail( "Composant Shootable introuvable sur l'arme" );
		}
		else
		{
			// Verifier que CanShoot() retourne true
			if ( shootable.CanShoot() )
				LogOk( "CanShoot() = true (arme prete a tirer)" );
			else
				LogFail( "CanShoot() = false alors que l'arme est chargee" );

			// Tir reel via Shoot()
			int ammoAvantShoot = weaponAmmo.Ammo;
			shootable.Shoot();
			await GameTask.DelaySeconds( 0.1f );

			int ammoApresShoot = weaponAmmo.Ammo;
			if ( ammoApresShoot == ammoAvantShoot - 1 )
				LogOk( $"Tir reel OK : {ammoAvantShoot} -> {ammoApresShoot}" );
			else
				LogFail( $"Tir reel : ammo devrait etre {ammoAvantShoot - 1}, est {ammoApresShoot}" );

			// Verifier que CanShoot() bloque quand chargeur vide
			int restant = weaponAmmo.Ammo;
			for ( int i = 0; i < restant; i++ )
				weaponAmmo.ConsumeBullet();

			if ( !shootable.CanShoot() )
				LogOk( "CanShoot() = false apres vidage (correct)" );
			else
				LogFail( "CanShoot() devrait etre false sans munitions" );

			// Recharger pour la suite des tests : decharger, refill, recharger
			var refillMag = weaponAmmo.UnloadMagazine();
			if ( refillMag != null )
			{
				refillMag.MagAmmo = 10;
				await GameTask.DelaySeconds( 0.1f );
				weaponAmmo.LoadMagazine( refillMag );
			}
		}

		// ============================================================
		// ETAPE 7 : Vider le chargeur completement
		// ============================================================
		LogStep( "Etape 7 — Vidage complet du chargeur..." );

		int remaining = weaponAmmo.Ammo;
		for ( int i = 0; i < remaining; i++ )
			weaponAmmo.ConsumeBullet();

		if ( weaponAmmo.Ammo == 0 )
			LogOk( "Chargeur entierement vide (0 balles)" );
		else
			LogFail( $"Chargeur devrait etre a 0, est a {weaponAmmo.Ammo}" );

		if ( !weaponAmmo.HasAmmo )
			LogOk( "HasAmmo = false (correct)" );
		else
			LogFail( "HasAmmo devrait etre false" );

		// Tir a vide — ConsumeBullet ne doit pas faire passer Ammo en negatif
		weaponAmmo.ConsumeBullet();
		if ( weaponAmmo.Ammo == 0 )
			LogOk( "Tir a vide : Ammo reste a 0 (correct)" );
		else
			LogFail( $"Tir a vide : Ammo devrait rester a 0, est a {weaponAmmo.Ammo}" );

		// ============================================================
		// ETAPE 8 : Decharger et recharger un nouveau chargeur
		// ============================================================
		LogStep( "Etape 8 — Swap de chargeur..." );

		// Decharger l'ancien
		var oldMag = weaponAmmo.UnloadMagazine();
		if ( oldMag != null )
			LogOk( "Ancien chargeur decharge" );
		else
			LogFail( "UnloadMagazine a retourne null" );

		if ( !weaponAmmo.HasMagazine )
			LogOk( "HasMagazine = false apres decharge" );
		else
			LogFail( "HasMagazine devrait etre false" );

		// Creer un nouveau chargeur plein
		InventoryContainer.Add( container, "mag_usp", 1 );
		await GameTask.DelaySeconds( 0.2f );

		var newMag = container.Items
			.FirstOrDefault( x => x.Metadata?.ResourceName == "mag_usp" && x != oldMag );

		if ( newMag == null ) { LogFail( "Nouveau chargeur introuvable" ); return; }

		// Remplir le nouveau chargeur
		newMag.MagAmmo = 13;
		await GameTask.DelaySeconds( 0.1f );

		// Charger le nouveau chargeur
		weaponAmmo.LoadMagazine( newMag );

		if ( weaponAmmo.Ammo == 13 )
			LogOk( $"Nouveau chargeur charge : {weaponAmmo.Ammo}/{weaponAmmo.MaxAmmo}" );
		else
			LogFail( $"Ammo apres swap devrait etre 13, est {weaponAmmo.Ammo}" );

		// ============================================================
		// RESULTAT FINAL
		// ============================================================
		string result = failed == 0
			? $"TOUS LES TESTS PASSES ({passed}/{passed})"
			: $"ECHECS : {failed} / {passed + failed} tests";

		if ( failed == 0 )
			Log.Info( $"[TestAmmo] === {result} ===" );
		else
			Log.Error( $"[TestAmmo] === {result} ===" );

		// Pas de nettoyage : on laisse les items dans l'inventaire pour permettre
		// au joueur de tester manuellement le drag & drop / menu contextuel / reload.
		Log.Info( "[TestAmmo] Tests termines. Items conserves dans l'inventaire pour tests manuels." );
	}
}
