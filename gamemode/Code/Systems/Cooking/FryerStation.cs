using System.Collections.Generic;

namespace OpenFramework.Systems.Cooking;

/// <summary>
/// Friteuse — détecte les ingrédients posés (typiquement RawFries) et les
/// transforme après un délai dans le bain d'huile.
///
/// Comportement identique à CuttingStation mais sémantique différente :
///   - cooking time vs prep time
///   - sons dédiés (huile qui frémit / cloche de fin)
///   - on traite en général des portions plutôt que des tranches
///
/// Pour V1, le résultat est un prefab d'item food (ex: prefabs/props/fries.prefab)
/// qui apparaît directement à la place de la portion de frites crues.
/// </summary>
public sealed class FryerStation : Component, Component.ITriggerListener
{
	[Property] public string DisplayName { get; set; } = "Friteuse";

	/// <summary>
	/// État allumé / éteint. Synced du host via Commands.RPC_ToggleFryer (radial menu).
	/// Quand la friteuse est éteinte, aucune cuisson ne progresse, le son d'huile
	/// frémissante s'arrête et les effets visuels (vapeur, glow) sont coupés.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )]
	public bool IsLit { get; set; } = false;

	/// <summary>
	/// GameObjects d'effets visuels (vapeur, glow chaud) à activer/désactiver selon
	/// IsLit. Référencer les enfants du prefab portant ParticleEffect / PointLight.
	/// </summary>
	[Property] public List<GameObject> Effects { get; set; } = new();

	/// <summary>Durée de cuisson par portion (multiplie par OutputCount du mapping).</summary>
	[Property, Range( 0.5f, 30f )] public float CookDurationPerOutput { get; set; } = 5f;

	/// <summary>Mappages source → output. Ex : RawFries → 1× fries.prefab.</summary>
	[Property] public List<FryMapping> FryMappings { get; set; } = new();

	[Property] public SoundEvent FrySound { get; set; }
	[Property] public SoundEvent FryFinishedSound { get; set; }

	private readonly Dictionary<Ingredient, FryMapping> _trackedMapping = new();
	private readonly Dictionary<Ingredient, float> _trackedStartTimes = new();
	private SoundHandle _frySoundHandle;

	/// <summary>
	/// Synced du host vers tous les clients : true tant qu'au moins une cuisson est
	/// en cours dans la friteuse. Sur dédié, le host n'a pas de joueur ; chaque client
	/// doit gérer son propre sound handle local en lisant cet état dans OnUpdate.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool IsFrying { get; set; }

	public void OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost ) return;
		if ( other?.GameObject == null ) return;

		// Si c'est un panier qui entre, on démarre la cuisson de TOUT son contenu
		// d'un coup. Les ingrédients à l'intérieur ont leur Collider désactivé,
		// donc le fryer ne les voit pas individuellement — c'est le panier qui
		// pilote leur cycle.
		var basket = other.GameObject.Components.Get<FryerBasket>( FindMode.EverythingInSelfAndAncestors );
		if ( basket != null )
		{
			StartCookingBasketContents( basket );
			return;
		}

		var ing = other.GameObject.Components.Get<Ingredient>( FindMode.EverythingInSelfAndAncestors );
		if ( ing == null ) return;
		TryStartCooking( ing );
	}

	public void OnTriggerExit( Collider other )
	{
		if ( !Networking.IsHost ) return;
		if ( other?.GameObject == null ) return;

		var basket = other.GameObject.Components.Get<FryerBasket>( FindMode.EverythingInSelfAndAncestors );
		if ( basket != null )
		{
			StopCookingBasketContents( basket );
			return;
		}

		var ing = other.GameObject.Components.Get<Ingredient>( FindMode.EverythingInSelfAndAncestors );
		if ( ing == null ) return;
		StopCooking( ing );
	}

	private void StartCookingBasketContents( FryerBasket basket )
	{
		Log.Info( $"[Fryer] Panier détecté, démarrage cuisson sur le contenu" );
		foreach ( var go in basket.GetContents() )
		{
			var ing = go.Components.Get<Ingredient>( FindMode.EverythingInSelfAndDescendants );
			if ( ing == null ) continue;
			TryStartCooking( ing );
		}
	}

	private void StopCookingBasketContents( FryerBasket basket )
	{
		Log.Info( $"[Fryer] Panier sorti, arrêt cuisson sur le contenu" );
		foreach ( var go in basket.GetContents() )
		{
			var ing = go.Components.Get<Ingredient>( FindMode.EverythingInSelfAndDescendants );
			if ( ing == null ) continue;
			StopCooking( ing );
		}
	}

	private void TryStartCooking( Ingredient ing )
	{
		var mapping = FryMappings?.FirstOrDefault( m => m != null && m.FromType == ing.SourceType );
		if ( mapping == null )
		{
			Log.Info( $"[Fryer] {ing.SourceType} : pas de mapping de cuisson pour ce type" );
			return;
		}
		// Mode in-place (OutputPrefab=null) : on ne re-cuit pas une portion déjà
		// cuite ou brûlée, sinon on retomberait en boucle dès qu'elle ré-entre.
		if ( mapping.OutputPrefab == null && ing.State != CookState.Raw )
		{
			Log.Info( $"[Fryer] {ing.SourceType} déjà {ing.State}, pas de re-cuisson" );
			return;
		}
		if ( _trackedStartTimes.ContainsKey( ing ) ) return;

		// Friteuse éteinte : on track le mapping mais pas le timer. Sera démarré
		// au moment de l'allumage via SetLit.
		_trackedMapping[ing] = mapping;
		if ( !IsLit )
		{
			Log.Info( $"[Fryer] {ing.SourceType} posé sur friteuse éteinte, en attente d'allumage" );
			return;
		}

		_trackedStartTimes[ing] = Time.Now;
		float total = CookDurationPerOutput * Math.Max( 1, mapping.OutputCount );
		string outputLabel = mapping.OutputPrefab?.ResourceName ?? $"{ing.SourceType} (in-place Cooked)";
		Log.Info( $"[Fryer] ✓ {ing.SourceType} commence la cuisson ({total:F1}s pour {mapping.OutputCount}× {outputLabel})" );

		UpdateFrySound();
	}

	private void StopCooking( Ingredient ing )
	{
		bool removed = _trackedMapping.Remove( ing ) | _trackedStartTimes.Remove( ing );
		if ( removed )
		{
			Log.Info( $"[Fryer] {ing.SourceType} retiré de la friteuse avant fin de cuisson" );
			UpdateFrySound();
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		// Friteuse éteinte : on stoppe les timers (les ingrédients restent présents
		// dans _trackedMapping pour redémarrer si le joueur rallume).
		if ( !IsLit )
		{
			if ( _trackedStartTimes.Count > 0 ) _trackedStartTimes.Clear();
			IsFrying = false;
			return;
		}

		if ( _trackedStartTimes.Count == 0 ) return;

		foreach ( var kv in _trackedStartTimes.ToList() )
		{
			var ing = kv.Key;
			if ( !ing.IsValid() )
			{
				_trackedMapping.Remove( ing );
				_trackedStartTimes.Remove( ing );
				continue;
			}

			if ( !_trackedMapping.TryGetValue( ing, out var mapping ) ) continue;

			float elapsed = Time.Now - kv.Value;
			float requiredDuration = CookDurationPerOutput * Math.Max( 1, mapping.OutputCount );
			if ( elapsed < requiredDuration ) continue;

			FryIngredient( ing, mapping );
			_trackedMapping.Remove( ing );
			_trackedStartTimes.Remove( ing );
		}

		UpdateFrySound();
	}

	private void FryIngredient( Ingredient ing, FryMapping mapping )
	{
		// Mode 1 : OutputPrefab null → on transforme l'état Raw→Cooked et on garde
		// l'ingrédient (le tint passera à CookedTint via Ingredient.SetState).
		// Idéal pour les frites : le joueur récupère une portion dorée dans le panier.
		if ( mapping.OutputPrefab == null )
		{
			if ( ing.IsValid() && ing.IsCookable && ing.State == CookState.Raw )
			{
				ing.SetState( CookState.Cooked );
				Log.Info( $"[Fryer] ✓ {ing.SourceType} passé à l'état Cooked (in-place)" );
			}
			BroadcastFryFinishedSound();
			return;
		}

		// Mode 2 : OutputPrefab renseigné → on détruit l'ingrédient source et on
		// spawn N copies du prefab output (ex: raw chicken nuggets → cooked nuggets).
		var basePos = ing.WorldPosition;
		var baseRot = ing.WorldRotation;

		// Capture le panier parent AVANT destroy pour pouvoir y replacer les outputs.
		var parentBasket = ing.Components.Get<FryerBasket>( FindMode.InAncestors );

		ing.GameObject.Destroy();

		int count = Math.Max( 1, mapping.OutputCount );
		for ( int i = 0; i < count; i++ )
		{
			float angle = ( i / (float)count ) * MathF.PI * 2f;
			var offset = new Vector3( MathF.Cos( angle ) * 6f, MathF.Sin( angle ) * 6f, 6f );
			var go = Spawnable.CreateWithReturnFromHost( mapping.OutputPrefab.ResourcePath, new Transform( basePos + offset, baseRot ) );
			if ( go == null ) continue;
			go.NetworkSpawn();

			if ( parentBasket.IsValid() )
				parentBasket.AddToBasket( go );
		}

		BroadcastFryFinishedSound();

		Log.Info( $"[Fryer] ✓ {mapping.FromType} cuit en {count}× {mapping.OutputPrefab.ResourceName}" );
	}

	private void UpdateFrySound()
	{
		if ( !Networking.IsHost ) return;
		_trackedStartTimes.Keys.ToList().ForEach( k => { if ( !k.IsValid() ) _trackedStartTimes.Remove( k ); } );
		// L'huile ne frémit que si la friteuse est allumée ET qu'au moins une portion cuit.
		IsFrying = IsLit && _trackedStartTimes.Count > 0;
	}

	protected override void OnUpdate()
	{
		// Reconcile chaque frame : tous les clients (host inclus) gèrent leur handle local
		ApplyEffectsState();

		if ( FrySound != null )
		{
			if ( IsFrying && _frySoundHandle == null )
				_frySoundHandle = Sound.Play( FrySound, WorldPosition );
			else if ( !IsFrying && _frySoundHandle != null )
			{
				_frySoundHandle.Stop();
				_frySoundHandle = null;
			}
		}
	}

	/// <summary>
	/// Active/désactive les GameObjects d'effets selon IsLit. Appelé chaque frame
	/// pour rester cohérent même si un client se connecte alors que la friteuse
	/// est déjà allumée.
	/// </summary>
	private void ApplyEffectsState()
	{
		if ( Effects == null ) return;
		foreach ( var fx in Effects )
		{
			if ( !fx.IsValid() ) continue;
			if ( fx.Enabled != IsLit ) fx.Enabled = IsLit;
		}
	}

	/// <summary>
	/// Bascule l'état allumé/éteint. Appelé côté host par Commands.RPC_ToggleFryer.
	/// À l'allumage, démarre les timers pour les ingrédients déjà posés.
	/// </summary>
	public void SetLit( bool on )
	{
		if ( !Networking.IsHost ) return;
		if ( IsLit == on ) return;

		IsLit = on;

		if ( on )
		{
			// Démarre les timers pour tous les ingrédients déjà mappés et présents
			foreach ( var ing in _trackedMapping.Keys.ToList() )
			{
				if ( !ing.IsValid() )
				{
					_trackedMapping.Remove( ing );
					continue;
				}
				if ( _trackedStartTimes.ContainsKey( ing ) ) continue;
				_trackedStartTimes[ing] = Time.Now;
				Log.Info( $"[Fryer] ✓ {ing.SourceType} démarre cuisson après allumage" );
			}
		}
		else
		{
			_trackedStartTimes.Clear();
		}

		UpdateFrySound();
		Log.Info( $"[Fryer] {(on ? "allumée" : "éteinte")}" );
	}

	[Button( "Preview : afficher les effets" )]
	public void PreviewLit()
	{
		if ( Effects == null ) return;
		foreach ( var fx in Effects )
		{
			if ( fx.IsValid() ) fx.Enabled = true;
		}
		Log.Info( $"[Fryer.Preview] {Effects.Count} effet(s) activés pour preview" );
	}

	[Button( "Preview : effacer les effets" )]
	public void PreviewUnlit()
	{
		if ( Effects == null ) return;
		foreach ( var fx in Effects )
		{
			if ( fx.IsValid() ) fx.Enabled = false;
		}
		Log.Info( "[Fryer.Preview] effets désactivés" );
	}

	[Rpc.Broadcast]
	private void BroadcastFryFinishedSound()
	{
		if ( FryFinishedSound != null )
			Sound.Play( FryFinishedSound, WorldPosition );
	}

	protected override void OnDestroy()
	{
		if ( _frySoundHandle != null )
		{
			_frySoundHandle.Stop();
			_frySoundHandle = null;
		}
	}

	public class FryMapping
	{
		[Property] public IngredientType FromType { get; set; }
		[Property] public PrefabFile OutputPrefab { get; set; }
		[Property, Range( 1, 5 )] public int OutputCount { get; set; } = 1;
	}
}
