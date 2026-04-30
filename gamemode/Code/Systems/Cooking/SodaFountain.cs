using System.Collections.Generic;
using OpenFramework.Inventory;

namespace OpenFramework.Systems.Cooking;

/// <summary>
/// Fontaine à soda — détecte un gobelet vide (EmptyCup) posé dans son trigger
/// (le bac sous le robinet), le remplit pendant FillDuration secondes, puis le
/// remplace par le prefab de la boisson finale (typiquement
/// prefabs/props/soda.prefab) avec son attribut 'calories'.
///
/// Multi-safe : tout passe par Networking.IsHost. Le gobelet source est détruit
/// AVANT le spawn de la boisson finale (anti-duplication).
///
/// Mécanique identique au FryerStation mais sans état Raw/Cooked (le verre n'a
/// pas d'état intermédiaire — soit vide, soit plein) et sans système de panier.
/// </summary>
public sealed class SodaFountain : Component, Component.ITriggerListener
{
	[Property] public string DisplayName { get; set; } = "Fontaine à soda";

	/// <summary>
	/// Mappages source → boisson finale. En général une seule entrée :
	/// EmptyCup → prefabs/props/soda.prefab (cola). Étendable si on veut
	/// plusieurs parfums (un EmptyCup peut donner cola, fanta, sprite, etc.).
	/// Pour V1 on prend toujours la première qui matche.
	/// </summary>
	[Property] public List<DrinkMapping> DrinkMappings { get; set; } = new();

	/// <summary>
	/// GameObjects d'effets visuels (jet de soda, ParticleEffect, etc.) à activer
	/// pendant qu'un gobelet se remplit. Toggled selon IsFilling chaque frame.
	/// </summary>
	[Property] public List<GameObject> Effects { get; set; } = new();

	/// <summary>Durée de remplissage du verre (s).</summary>
	[Property, Range( 0.5f, 15f )] public float FillDuration { get; set; } = 4f;

	[Property] public SoundEvent FillSound { get; set; }
	[Property] public SoundEvent FillFinishedSound { get; set; }

	private readonly Dictionary<Ingredient, DrinkMapping> _trackedMapping = new();
	private readonly Dictionary<Ingredient, float> _trackedStartTimes = new();
	private SoundHandle _fillSoundHandle;

	[Sync( SyncFlags.FromHost )] public bool IsFilling { get; set; }

	public void OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost ) return;
		if ( other?.GameObject == null ) return;

		var ing = other.GameObject.Components.Get<Ingredient>( FindMode.EverythingInSelfAndAncestors );
		if ( ing == null ) return;

		var mapping = DrinkMappings?.FirstOrDefault( m => m != null && m.FromType == ing.SourceType );
		if ( mapping == null )
		{
			Log.Info( $"[Soda] {ing.SourceType} : pas de mapping de remplissage" );
			return;
		}
		if ( mapping.OutputPrefab == null )
		{
			Log.Warning( $"[Soda] Mapping {mapping.FromType} → OutputPrefab null, remplissage impossible" );
			return;
		}
		if ( _trackedStartTimes.ContainsKey( ing ) ) return;

		_trackedMapping[ing] = mapping;
		_trackedStartTimes[ing] = Time.Now;
		Log.Info( $"[Soda] ✓ {ing.SourceType} commence le remplissage ({FillDuration:F1}s → {mapping.OutputPrefab.ResourceName})" );

		UpdateFillSound();
	}

	public void OnTriggerExit( Collider other )
	{
		if ( !Networking.IsHost ) return;
		if ( other?.GameObject == null ) return;
		var ing = other.GameObject.Components.Get<Ingredient>( FindMode.EverythingInSelfAndAncestors );
		if ( ing == null ) return;

		bool removed = _trackedMapping.Remove( ing ) | _trackedStartTimes.Remove( ing );
		if ( removed )
		{
			Log.Info( $"[Soda] {ing.SourceType} retiré avant fin du remplissage" );
			UpdateFillSound();
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
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
			if ( elapsed < FillDuration ) continue;

			FillCup( ing, mapping );
			_trackedMapping.Remove( ing );
			_trackedStartTimes.Remove( ing );
		}

		UpdateFillSound();
	}

	private void FillCup( Ingredient ing, DrinkMapping mapping )
	{
		var basePos = ing.WorldPosition;
		var baseRot = ing.WorldRotation;

		// Détruit le gobelet vide AVANT le spawn de la boisson finale (anti-duplication)
		ing.GameObject.Destroy();

		var go = Spawnable.CreateWithReturnFromHost( mapping.OutputPrefab.ResourcePath, new Transform( basePos, baseRot ) );
		if ( go == null )
		{
			Log.Warning( $"[Soda] Spawn de {mapping.OutputPrefab.ResourceName} a échoué" );
			return;
		}

		// Inscrit les calories si renseignées dans le mapping et que l'output a un InventoryItem
		if ( mapping.Calories > 0 )
		{
			var item = go.Components.Get<InventoryItem>( FindMode.EverythingInSelfAndDescendants );
			if ( item != null )
				item.Attributes["calories"] = mapping.Calories.ToString();
		}

		go.NetworkSpawn();

		BroadcastFillFinishedSound();

		Log.Info( $"[Soda] ✓ {mapping.FromType} rempli en {mapping.OutputPrefab.ResourceName}" );
	}

	private void UpdateFillSound()
	{
		if ( !Networking.IsHost ) return;
		_trackedStartTimes.Keys.ToList().ForEach( k => { if ( !k.IsValid() ) _trackedStartTimes.Remove( k ); } );
		IsFilling = _trackedStartTimes.Count > 0;
	}

	protected override void OnUpdate()
	{
		// Tous les clients reconcilient l'état des effets visuels et du son local
		ApplyEffectsState();

		if ( FillSound != null )
		{
			if ( IsFilling && _fillSoundHandle == null )
				_fillSoundHandle = Sound.Play( FillSound, WorldPosition );
			else if ( !IsFilling && _fillSoundHandle != null )
			{
				_fillSoundHandle.Stop();
				_fillSoundHandle = null;
			}
		}
	}

	/// <summary>
	/// Active/désactive les GameObjects d'effets selon IsFilling. Appelé chaque
	/// frame (idempotent) pour rester cohérent même si un client se connecte
	/// pendant qu'un verre est en train de se remplir.
	/// </summary>
	private void ApplyEffectsState()
	{
		if ( Effects == null ) return;
		foreach ( var fx in Effects )
		{
			if ( !fx.IsValid() ) continue;
			if ( fx.Enabled != IsFilling ) fx.Enabled = IsFilling;
		}
	}

	[Button( "Preview : afficher les effets" )]
	public void PreviewFlow()
	{
		if ( Effects == null ) return;
		foreach ( var fx in Effects )
		{
			if ( fx.IsValid() ) fx.Enabled = true;
		}
		Log.Info( $"[Soda.Preview] {Effects.Count} effet(s) activés" );
	}

	[Button( "Preview : effacer les effets" )]
	public void PreviewClear()
	{
		if ( Effects == null ) return;
		foreach ( var fx in Effects )
		{
			if ( fx.IsValid() ) fx.Enabled = false;
		}
		Log.Info( "[Soda.Preview] effets désactivés" );
	}

	[Rpc.Broadcast]
	private void BroadcastFillFinishedSound()
	{
		if ( FillFinishedSound != null )
			Sound.Play( FillFinishedSound, WorldPosition );
	}

	protected override void OnDestroy()
	{
		if ( _fillSoundHandle != null )
		{
			_fillSoundHandle.Stop();
			_fillSoundHandle = null;
		}
	}

	public class DrinkMapping
	{
		[Property] public IngredientType FromType { get; set; }
		[Property] public PrefabFile OutputPrefab { get; set; }
		[Property, Range( 0, 500 )] public int Calories { get; set; } = 120;
	}
}
