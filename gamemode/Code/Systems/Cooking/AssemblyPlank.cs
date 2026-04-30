using System.Collections.Generic;
using OpenFramework.Inventory;

namespace OpenFramework.Systems.Cooking;

/// <summary>
/// Planche d'assemblage de burger. Les ingrédients qui touchent sa surface
/// sont consommés (Destroy) et leur type est ajouté au burger en cours.
///
/// Comportement :
///   - 1er ingrédient (BunBottom) → spawn un Burger enfant + ajoute BunBottom
///   - Ingrédients suivants → ajoute au burger
///   - BunTop → finalize, le burger se détache de la planche
///
/// Si l'ingrédient n'est pas accepté (ex: tomate avant bun_bottom), il reste sur
/// la planche sans être consommé — le joueur peut le récupérer.
///
/// Multi-safe :
///   - Toutes les transformations passent par le host
///   - Le burger est NetworkSpawn pour que tous les clients le voient
/// </summary>
public sealed class AssemblyPlank : Component, Component.ITriggerListener
{
	[Property] public string DisplayName { get; set; } = "Planche d'assemblage";

	[Property]
	[Description( "Prefab du burger spawné quand le BunBottom est posé. Doit avoir un Burger component." )]
	public PrefabFile BurgerPrefab { get; set; }

	[Property]
	[Description( "Position locale où le burger spawn (au-dessus de la planche)." )]
	public Vector3 BurgerSpawnOffset { get; set; } = new Vector3( 0, 0, 5f );

	[Property]
	[Description( "Prefab du produit fini spawné quand le BunTop est posé (ex: items/food/hamburger.prefab). " +
	              "Si null, le burger en cours est juste détaché (comportement physique brut). " +
	              "Si renseigné : la TotalCalories du burger est inscrite dans l'attribut 'calories' du nouvel item." )]
	public PrefabFile FinalizedOutputPrefab { get; set; }

	/// <summary>
	/// Recette frites : prefab de l'item final spawné quand une pochette FriesPouch
	/// + une portion RawFries Cooked sont déposées sur la planche. Calories inscrites
	/// dans l'attribut 'calories' à partir de l'EffectiveCalories des frites cuites.
	/// </summary>
	[Property]
	[Description( "Prefab du paquet de frites finalisé (ex: prefabs/props/fries.prefab). " +
	              "Si null, la recette frites est désactivée." )]
	public PrefabFile FriesOutputPrefab { get; set; }

	[Sync( SyncFlags.FromHost )]
	public Burger CurrentBurger { get; set; }

	/// <summary>Pochette FriesPouch posée sur la planche, en attente de frites cuites.</summary>
	[Sync( SyncFlags.FromHost )]
	public Ingredient PouchOnPlank { get; set; }

	public void OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost ) return;
		if ( other?.GameObject == null ) return;

		// Si un panier de friteuse est posé sur la planche, on vide son contenu :
		// les ingrédients tombent (collider et physique restaurés) et seront
		// détectés par cette même OnTriggerEnter au frame suivant via leur propre
		// collider. Évite de devoir gérer les ingrédients enfants dont le collider
		// est désactivé (ils seraient invisibles pour le trigger de la planche).
		var basket = other.GameObject.Components.Get<FryerBasket>( FindMode.EverythingInSelfAndAncestors );
		if ( basket != null )
		{
			Log.Info( $"[Plank] Panier détecté → vidage du contenu sur la planche" );
			basket.DumpContents();
			return;
		}

		var ing = other.GameObject.Components.Get<Ingredient>( FindMode.EverythingInSelfAndAncestors );
		if ( ing == null ) return; // joueur, burger spawné, etc. — ignoré silencieusement

		Log.Info( $"[Plank] Ingrédient détecté : '{other.GameObject.Name}' SourceType={ing.SourceType} State={ing.State} cal={ing.EffectiveCalories}" );

		// === Recette FRITES : pochette + frites cuites ===

		// Pochette : on la pose sur la planche en attente de frites cuites.
		if ( ing.SourceType == IngredientType.FriesPouch && FriesOutputPrefab != null )
		{
			if ( CurrentBurger.IsValid() )
			{
				Log.Info( "[Plank] Pochette refusée : burger en cours" );
				return;
			}
			if ( PouchOnPlank.IsValid() )
			{
				Log.Info( "[Plank] Pochette déjà présente, ignorée" );
				return;
			}
			AdoptPouch( ing );
			return;
		}

		// Frites cuites + pochette présente → finalize en fries.item
		if ( ing.SourceType == IngredientType.RawFries
		     && ing.State == CookState.Cooked
		     && PouchOnPlank.IsValid()
		     && FriesOutputPrefab != null )
		{
			FinalizeFries( ing );
			return;
		}

		// === Recette BURGER ===

		// Le burger utilise SourceType (ex: RawBeef pour la viande, quel que soit l'état).
		// L'état (Raw/Cooked/Burned) est porté entièrement par la tint et les calories
		// transmises — aucune duplication dans l'enum, extensible aux futures recettes.
		var ingType = ing.SourceType;
		var ingCalories = ing.EffectiveCalories;
		var ingTint = ing.EffectiveTint;

		// 1er ingrédient = doit être BunBottom → spawn le burger
		if ( !CurrentBurger.IsValid() )
		{
			if ( ingType != IngredientType.BunBottom )
			{
				Log.Info( $"[Plank] {ingType} refusé : il faut commencer par BunBottom" );
				return;
			}
			Log.Info( "[Plank] BunBottom détecté en 1er → spawn du burger" );
			SpawnBurger();
			if ( !CurrentBurger.IsValid() )
			{
				Log.Warning( "[Plank] SpawnBurger a échoué !" );
				return;
			}
		}

		if ( CurrentBurger.IsFinalized )
		{
			Log.Info( "[Plank] Burger déjà finalisé, ingrédient ignoré" );
			return;
		}

		bool ok = CurrentBurger.TryAddCondiment( ingType, ingCalories, ingTint );
		Log.Info( $"[Plank] TryAddCondiment({ingType}, +{ingCalories}cal, tint={ingTint}) → {(ok ? "OK" : "REFUSÉ")}, csv='{CurrentBurger.CondimentsCsv}', total={CurrentBurger.TotalCalories}cal" );
		if ( !ok ) return;

		// L'ingrédient est consommé (transformé en couche du burger)
		ing.GameObject.Destroy();

		// Si finalisé (BunTop posé), transforme le burger intermédiaire en produit fini
		if ( CurrentBurger.IsFinalized )
		{
			Log.Info( $"[Plank] ✓ BunTop posé, burger finalisé ({CurrentBurger.TotalCalories} cal) → transformation" );
			FinalizeBurger();
		}
	}

	public void OnTriggerExit( Collider other )
	{
		if ( !Networking.IsHost ) return;
		if ( other?.GameObject == null ) return;
		var ing = other.GameObject.Components.Get<Ingredient>( FindMode.EverythingInSelfAndAncestors );
		if ( ing == null ) return;

		// Si le joueur reprend la pochette avant d'avoir mis les frites, on libère le slot.
		if ( PouchOnPlank.IsValid() && ing == PouchOnPlank )
		{
			Log.Info( "[Plank] Pochette retirée de la planche" );
			ReleasePouch();
		}
	}

	private void SpawnBurger()
	{
		if ( BurgerPrefab == null )
		{
			Log.Warning( "[AssemblyPlank] BurgerPrefab non assigné dans le prefab !" );
			return;
		}

		var transform = new Transform( WorldPosition + WorldRotation * BurgerSpawnOffset, WorldRotation );
		var go = Spawnable.CreateWithReturnFromHost( BurgerPrefab.ResourcePath, transform );
		if ( go == null ) return;

		go.SetParent( GameObject );
		go.LocalPosition = BurgerSpawnOffset;
		go.LocalRotation = Rotation.Identity;

		var burger = go.Components.Get<Burger>( FindMode.EverythingInSelfAndDescendants );
		if ( burger == null )
		{
			Log.Warning( "[AssemblyPlank] BurgerPrefab spawné mais aucun Burger component trouvé !" );
			go.Destroy();
			return;
		}

		// Désactive la physique du burger tant qu'il est sur la planche
		var rb = go.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
		if ( rb.IsValid() ) rb.MotionEnabled = false;

		go.NetworkSpawn();
		CurrentBurger = burger;
	}

	/// <summary>
	/// Quand le burger est finalisé (BunTop posé) :
	/// - Si <see cref="FinalizedOutputPrefab"/> est renseigné : spawn ce prefab à
	///   la position du burger avec l'attribut 'calories' = TotalCalories, puis
	///   détruit le burger intermédiaire. C'est le comportement "recette" — un
	///   nouvel item utilisable en jeu (ex: hamburger.item) apparaît.
	/// - Sinon : juste détacher le burger de la planche (physique réactivée).
	/// </summary>
	private void FinalizeBurger()
	{
		if ( !CurrentBurger.IsValid() ) return;

		var burgerGo = CurrentBurger.GameObject;
		var totalCal = CurrentBurger.TotalCalories;
		var spawnPos = burgerGo.WorldPosition;
		var spawnRot = burgerGo.WorldRotation;

		// Pas de transformation configurée → ancien comportement (détache)
		if ( FinalizedOutputPrefab == null )
		{
			burgerGo.SetParent( null );
			var rb = burgerGo.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
			if ( rb.IsValid() ) rb.MotionEnabled = true;
			CurrentBurger = null;
			return;
		}

		// Détruit l'entité burger intermédiaire et spawn le produit fini
		burgerGo.Destroy();
		CurrentBurger = null;

		var go = Spawnable.CreateWithReturnFromHost( FinalizedOutputPrefab.ResourcePath, new Transform( spawnPos, spawnRot ) );
		if ( go == null )
		{
			Log.Warning( $"[Plank] Spawn de {FinalizedOutputPrefab.ResourceName} a échoué" );
			return;
		}

		// Inscrit les calories dans l'attribut de l'InventoryItem (si présent).
		// Attributes est un NetDictionary déjà initialisé, pas besoin de check null.
		var item = go.Components.Get<InventoryItem>( FindMode.EverythingInSelfAndDescendants );
		if ( item != null )
		{
			item.Attributes["calories"] = totalCal.ToString();
			Log.Info( $"[Plank] {item.Metadata?.Name ?? FinalizedOutputPrefab.ResourceName} produit avec {totalCal} cal en attribut" );
		}
		else
		{
			Log.Warning( $"[Plank] {FinalizedOutputPrefab.ResourceName} n'a pas de InventoryItem component, calories non sauvegardées" );
		}

		go.NetworkSpawn();
	}

	/// <summary>
	/// Pose la pochette sur la planche : disable physique, parente, garde la ref.
	/// La pochette n'est PAS détruite — elle attend des frites cuites pour fusionner.
	/// </summary>
	private void AdoptPouch( Ingredient pouch )
	{
		var rb = pouch.Components.Get<Rigidbody>();
		if ( rb.IsValid() ) rb.MotionEnabled = false;

		pouch.GameObject.SetParent( GameObject );
		pouch.LocalPosition = BurgerSpawnOffset;
		pouch.LocalRotation = Rotation.Identity;

		PouchOnPlank = pouch;
		Log.Info( "[Plank] ✓ Pochette posée sur la planche, en attente de frites cuites" );
	}

	/// <summary>Réactive la physique de la pochette et oublie la ref.</summary>
	private void ReleasePouch()
	{
		if ( PouchOnPlank.IsValid() )
		{
			var rb = PouchOnPlank.Components.Get<Rigidbody>();
			if ( rb.IsValid() ) rb.MotionEnabled = true;
		}
		PouchOnPlank = null;
	}

	/// <summary>
	/// Combine la pochette + 1 portion de frites cuites en un item fries final.
	/// Les frites peuvent être enfants d'un FryerBasket — on les détache d'abord.
	/// Calories de l'item = EffectiveCalories des frites cuites (CookedCalories).
	/// </summary>
	private void FinalizeFries( Ingredient cookedFries )
	{
		if ( !PouchOnPlank.IsValid() ) return;
		if ( FriesOutputPrefab == null ) return;

		var spawnPos = PouchOnPlank.WorldPosition;
		var spawnRot = PouchOnPlank.WorldRotation;
		int totalCal = cookedFries.EffectiveCalories;

		// Détache et détruit les deux ingrédients sources
		cookedFries.GameObject.SetParent( null );
		cookedFries.GameObject.Destroy();

		var pouchGo = PouchOnPlank.GameObject;
		PouchOnPlank = null;
		pouchGo.Destroy();

		// Spawn l'item fries final
		var go = Spawnable.CreateWithReturnFromHost( FriesOutputPrefab.ResourcePath, new Transform( spawnPos, spawnRot ) );
		if ( go == null )
		{
			Log.Warning( $"[Plank] Spawn de {FriesOutputPrefab.ResourceName} a échoué" );
			return;
		}

		var item = go.Components.Get<InventoryItem>( FindMode.EverythingInSelfAndDescendants );
		if ( item != null )
		{
			item.Attributes["calories"] = totalCal.ToString();
			Log.Info( $"[Plank] ✓ Frites finalisées ({totalCal} cal) → {item.Metadata?.Name ?? FriesOutputPrefab.ResourceName}" );
		}
		else
		{
			Log.Info( $"[Plank] ✓ Frites finalisées → {FriesOutputPrefab.ResourceName} (pas d'InventoryItem)" );
		}

		go.NetworkSpawn();
	}
}
