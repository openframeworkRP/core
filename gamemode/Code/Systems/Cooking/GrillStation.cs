using System.Collections.Generic;

namespace OpenFramework.Systems.Cooking;

/// <summary>
/// Station de cuisson type "grill" — détecte les ingrédients qui touchent sa surface
/// (via un Collider en mode trigger) et lance un timer de cuisson auto.
///
/// Cycle d'un steak :
///   Raw  -- CookDuration --> Cooked  -- BurnDuration --> Burned
///
/// Le joueur peut récupérer le steak à tout moment (en le grabbant) — l'ingrédient
/// quittant le trigger arrête son timer en cours.
///
/// Multi-safe :
///   - Toutes les transitions d'état passent par le host (Networking.IsHost guard)
///   - Les ingrédients sont synced (Ingredient.State [Sync]FromHost)
///   - Le timer est annulé si l'ingrédient quitte le trigger ou est détruit
/// </summary>
public sealed class GrillStation : Component, Component.ITriggerListener
{
	[Property] public string DisplayName { get; set; } = "Grill";

	/// <summary>
	/// État allumé / éteint. Synced du host vers les clients via le radial menu
	/// (Commands.RPC_ToggleGrill). Quand le grill est éteint, aucune cuisson ne
	/// progresse, le grésillement s'arrête et les effets visuels sont coupés.
	///
	/// Note : modifier cette valeur dans l'éditeur (sur un prefab non-runtime) sert
	/// uniquement à prévisualiser les effets visuels — voir <see cref="PreviewLit"/>
	/// et <see cref="PreviewUnlit"/> pour les boutons dédiés.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )]
	public bool IsLit { get; set; } = false;

	/// <summary>
	/// GameObjects d'effets visuels (flammes, fumée, glow) à activer/désactiver selon
	/// IsLit. Idéalement référencer ici les children du prefab portant des
	/// ParticleEffect / PointLight de feu + fumée.
	/// </summary>
	[Property] public List<GameObject> Effects { get; set; } = new();

	/// <summary>Durée de cuisson Raw → Cooked (en secondes).</summary>
	[Property, Range( 1f, 60f )] public float CookDuration { get; set; } = 10f;

	/// <summary>Délai supplémentaire Cooked → Burned si l'ingrédient reste sur le grill.</summary>
	[Property, Range( 1f, 60f )] public float BurnDuration { get; set; } = 10f;

	/// <summary>
	/// Filtre optionnel : si non vide, seuls les SourceTypes listés sont acceptés.
	/// Si vide, tout ingrédient avec <c>IsCookable=true</c> est cuisinable (par défaut).
	/// </summary>
	[Property] public List<IngredientType> AcceptedTypes { get; set; } = new();

	/// <summary>Son joué en boucle tant qu'au moins un ingrédient cuit (grésillement).</summary>
	[Property] public SoundEvent SizzleSound { get; set; }

	/// <summary>Son one-shot quand un ingrédient passe de Raw à Cooked.</summary>
	[Property] public SoundEvent ReadySound { get; set; }

	/// <summary>Son one-shot quand un ingrédient passe de Cooked à Burned.</summary>
	[Property] public SoundEvent BurnedSound { get; set; }

	/// <summary>Items en cours de transition d'état (Raw → Cooked → Burned).</summary>
	private readonly Dictionary<Ingredient, float> _trackedStartTimes = new();

	/// <summary>
	/// Items physiquement présents dans la zone trigger du grill (tout état confondu,
	/// y compris Burned). Sert à savoir si le son de grésillement doit tourner.
	/// </summary>
	private readonly HashSet<Ingredient> _itemsOnGrill = new();

	private SoundHandle _sizzleHandle;

	/// <summary>
	/// Synced du host vers tous les clients : true tant qu'au moins un item est sur le
	/// grill. Chaque client lit cet état dans OnUpdate et gère son propre handle de
	/// son local (sinon sur dédié le host n'a pas de joueur, personne n'entend).
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool IsSizzling { get; set; }

	public void OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost ) return;
		if ( other?.GameObject == null ) return;

		var ing = other.GameObject.Components.Get<Ingredient>( FindMode.EverythingInSelfAndAncestors );
		if ( ing == null ) return; // joueur, autre objet — ignoré silencieusement

		// Toujours marquer la présence physique (pour le son)
		_itemsOnGrill.Add( ing );
		UpdateSizzleSound();

		if ( !CanCook( ing ) )
		{
			Log.Info( $"[Grill] {ing.SourceType} (state={ing.State}) posé mais pas cuisinable" );
			return;
		}

		if ( _trackedStartTimes.ContainsKey( ing ) )
		{
			Log.Info( $"[Grill] {ing.SourceType} déjà en cours de cuisson, ignoré" );
			return;
		}

		// Grill éteint : on garde la présence (sizzle + état) mais on ne lance pas
		// le timer. Il sera démarré quand le joueur allumera le grill.
		if ( !IsLit )
		{
			Log.Info( $"[Grill] {ing.SourceType} posé sur grill éteint, en attente d'allumage" );
			return;
		}

		_trackedStartTimes[ing] = Time.Now;
		Log.Info( $"[Grill] ✓ {ing.SourceType} commence la cuisson (durée Raw→Cooked = {CookDuration}s)" );
	}

	public void OnTriggerExit( Collider other )
	{
		if ( !Networking.IsHost ) return;
		if ( other?.GameObject == null ) return;

		var ing = other.GameObject.Components.Get<Ingredient>( FindMode.EverythingInSelfAndAncestors );
		if ( ing == null ) return;

		bool wasOnGrill = _itemsOnGrill.Remove( ing );
		bool wasCooking = _trackedStartTimes.Remove( ing );

		if ( wasOnGrill || wasCooking )
		{
			Log.Info( $"[Grill] {ing.SourceType} retiré du grill (state={ing.State})" );
			UpdateSizzleSound();
		}
	}

	private float _lastLogTime;

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		// Cleanup safety : si un item a été destroy sans déclencher OnTriggerExit
		int removed = _itemsOnGrill.RemoveWhere( ing => !ing.IsValid() );
		if ( removed > 0 ) UpdateSizzleSound();

		// Grill éteint : on stoppe le grésillement et on n'avance aucun timer
		// (les items déjà présents attendent un nouvel allumage).
		if ( !IsLit )
		{
			IsSizzling = false;
			_trackedStartTimes.Clear();
			return;
		}

		if ( _trackedStartTimes.Count == 0 ) return;

		// Log de progression toutes les 2 secondes (sinon ça spam)
		bool shouldLog = ( Time.Now - _lastLogTime ) >= 2f;
		if ( shouldLog ) _lastLogTime = Time.Now;

		// Itère une copie pour pouvoir mut le dict
		foreach ( var kv in _trackedStartTimes.ToList() )
		{
			var ing = kv.Key;
			var startTime = kv.Value;

			if ( !ing.IsValid() )
			{
				Log.Info( "[Grill] Ingredient devenu invalide, retire du tracker" );
				_trackedStartTimes.Remove( ing );
				continue;
			}

			float elapsed = Time.Now - startTime;

			if ( shouldLog )
			{
				float target = ing.State == CookState.Raw ? CookDuration : BurnDuration;
				Log.Info( $"[Grill] {ing.SourceType} state={ing.State} progress={elapsed:F1}/{target}s" );
			}

			if ( ing.State == CookState.Raw && elapsed >= CookDuration )
			{
				ing.SetState( CookState.Cooked );
				_trackedStartTimes[ing] = Time.Now;
				Log.Info( $"[Grill] ✓ {ing.SourceType} → Cooked (timer Burned démarre, {BurnDuration}s)" );
				BroadcastReadySound();
			}
			else if ( ing.State == CookState.Cooked && elapsed >= BurnDuration )
			{
				ing.SetState( CookState.Burned );
				_trackedStartTimes.Remove( ing );
				Log.Info( $"[Grill] ⚠️ {ing.SourceType} → Burned (foutu)" );
				BroadcastBurnedSound();
				// Pas de UpdateSizzleSound ici : l'item reste physiquement sur le grill
				// donc le grésillement continue. Il s'arrêtera quand le joueur le retire.
			}
		}
	}

	private bool CanCook( Ingredient ing )
	{
		if ( ing.State != CookState.Raw ) return false;
		if ( !ing.IsCookable ) return false;
		if ( AcceptedTypes == null || AcceptedTypes.Count == 0 ) return true;
		return AcceptedTypes.Contains( ing.SourceType );
	}

	/// <summary>
	/// Côté host : met à jour l'état synced IsSizzling. Côté client (et host) :
	/// tous lisent IsSizzling dans OnUpdate pour démarrer/arrêter leur handle local.
	/// Un sound handle est local à la machine (sinon sur dédié, le host joue pour
	/// personne) — donc on synchronise l'état booléen, pas le handle lui-même.
	/// </summary>
	private void UpdateSizzleSound()
	{
		if ( !Networking.IsHost ) return;
		_itemsOnGrill.RemoveWhere( ing => !ing.IsValid() );
		// Le grésillement ne tourne que si le grill est allumé ET qu'un item est posé.
		IsSizzling = IsLit && _itemsOnGrill.Count > 0;
	}

	protected override void OnUpdate()
	{
		// Tous les clients (host inclus) reconcilient leur handle local sur l'état synced
		ApplyEffectsState();

		if ( SizzleSound != null )
		{
			if ( IsSizzling && _sizzleHandle == null )
			{
				_sizzleHandle = Sound.Play( SizzleSound, WorldPosition );
			}
			else if ( !IsSizzling && _sizzleHandle != null )
			{
				_sizzleHandle.Stop();
				_sizzleHandle = null;
			}
		}
	}

	/// <summary>
	/// Active/désactive les GameObjects d'effets (flammes, fumée, glow) selon IsLit.
	/// Appelé chaque frame pour rester cohérent même quand un client se connecte
	/// pendant que le grill est déjà allumé.
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
	/// Bouton inspector — active visuellement les effets (FireGlow, SmokeFx, etc.)
	/// sans démarrer la logique de cuisson. Sert uniquement à régler le rendu dans
	/// l'éditeur du prefab. Ne pas utiliser à runtime (préférer SetLit côté host).
	/// </summary>
	[Button( "Preview : afficher les effets" )]
	public void PreviewLit()
	{
		if ( Effects == null ) return;
		foreach ( var fx in Effects )
		{
			if ( fx.IsValid() ) fx.Enabled = true;
		}
		Log.Info( $"[Grill.Preview] {Effects.Count} effet(s) activés pour preview" );
	}

	/// <summary>
	/// Bouton inspector — réinitialise l'affichage des effets (tous éteints), pour
	/// retrouver l'état "grill froid" du prefab.
	/// </summary>
	[Button( "Preview : effacer les effets" )]
	public void PreviewUnlit()
	{
		if ( Effects == null ) return;
		foreach ( var fx in Effects )
		{
			if ( fx.IsValid() ) fx.Enabled = false;
		}
		Log.Info( "[Grill.Preview] effets désactivés" );
	}

	/// <summary>
	/// Bascule l'état allumé/éteint. Appelé côté host par Commands.RPC_ToggleGrill.
	/// Quand on allume, démarre les timers pour tout item Raw cuisinable déjà posé.
	/// </summary>
	public void SetLit( bool on )
	{
		if ( !Networking.IsHost ) return;
		if ( IsLit == on ) return;

		IsLit = on;

		if ( on )
		{
			// Démarre les timers pour les items déjà posés et cuisinables
			_itemsOnGrill.RemoveWhere( ing => !ing.IsValid() );
			foreach ( var ing in _itemsOnGrill )
			{
				if ( !CanCook( ing ) ) continue;
				if ( _trackedStartTimes.ContainsKey( ing ) ) continue;
				_trackedStartTimes[ing] = Time.Now;
				Log.Info( $"[Grill] ✓ {ing.SourceType} démarre cuisson après allumage" );
			}
		}
		else
		{
			_trackedStartTimes.Clear();
		}

		UpdateSizzleSound();
		Log.Info( $"[Grill] {(on ? "allumé" : "éteint")}" );
	}

	[Rpc.Broadcast]
	private void BroadcastReadySound()
	{
		if ( ReadySound != null )
			Sound.Play( ReadySound, WorldPosition );
	}

	[Rpc.Broadcast]
	private void BroadcastBurnedSound()
	{
		if ( BurnedSound != null )
			Sound.Play( BurnedSound, WorldPosition );
	}

	protected override void OnDestroy()
	{
		if ( _sizzleHandle != null )
		{
			_sizzleHandle.Stop();
			_sizzleHandle = null;
		}
	}
}
