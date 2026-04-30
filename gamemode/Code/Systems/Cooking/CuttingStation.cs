using System.Collections.Generic;

namespace OpenFramework.Systems.Cooking;

/// <summary>
/// Station de découpe — détecte les ingrédients "entiers" posés sur sa surface et
/// les transforme en N tranches après un délai (CutDuration).
///
/// Configuration via <see cref="SliceMappings"/> : chaque mapping associe un
/// IngredientType source (ex: WholeTomato) à un prefab de tranche (ex:
/// rfs_tomato.prefab) et un nombre de tranches produites (ex: 4).
///
/// Multi-safe :
///   - Toutes les transformations passent par le host (Networking.IsHost guard)
///   - L'ingrédient source est consommé (Destroy) AVANT le spawn des tranches
///   - Les tranches sont NetworkSpawn pour que tous les clients les voient
/// </summary>
public sealed class CuttingStation : Component, Component.ITriggerListener
{
	[Property] public string DisplayName { get; set; } = "Planche à Découper";

	/// <summary>
	/// Durée de découpe par tranche, en secondes. La durée totale pour un ingrédient
	/// = CutDurationPerSlice × mapping.OutputCount. Ex : 2s × 4 tranches = 8s pour
	/// une tomate entière, 2s × 5 = 10s pour une salade.
	/// </summary>
	[Property, Range( 0.2f, 10f )] public float CutDurationPerSlice { get; set; } = 2f;

	/// <summary>
	/// Mappages source → tranches. Si un ingrédient touche le trigger et que son
	/// SourceType matche un FromType, le timer de découpe démarre.
	/// </summary>
	[Property] public List<CuttingMapping> SliceMappings { get; set; } = new();

	/// <summary>Son joué en boucle pendant la découpe (couteau qui tape).</summary>
	[Property] public SoundEvent CutSound { get; set; }

	/// <summary>Son one-shot quand la découpe se termine.</summary>
	[Property] public SoundEvent CutFinishedSound { get; set; }

	private readonly Dictionary<Ingredient, CuttingMapping> _trackedMapping = new();
	private readonly Dictionary<Ingredient, float> _trackedStartTimes = new();
	private SoundHandle _cutSoundHandle;

	/// <summary>
	/// Synced du host vers tous les clients : true tant qu'au moins une découpe est
	/// en cours. Sur dédié, le host n'a pas de joueur ; chaque client doit gérer son
	/// propre sound handle local en lisant cet état dans OnUpdate.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool IsCutting { get; set; }

	public void OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost ) return;
		if ( other?.GameObject == null ) return;

		var ing = other.GameObject.Components.Get<Ingredient>( FindMode.EverythingInSelfAndAncestors );
		if ( ing == null ) return;

		var mapping = SliceMappings?.FirstOrDefault( m => m != null && m.FromType == ing.SourceType );
		if ( mapping == null )
		{
			Log.Info( $"[Cutting] {ing.SourceType} : pas de mapping de découpe pour ce type" );
			return;
		}
		if ( mapping.OutputPrefab == null )
		{
			Log.Warning( $"[Cutting] Mapping {mapping.FromType} → OutputPrefab null, découpe impossible" );
			return;
		}
		if ( _trackedStartTimes.ContainsKey( ing ) ) return;

		_trackedMapping[ing] = mapping;
		_trackedStartTimes[ing] = Time.Now;
		float totalDuration = CutDurationPerSlice * Math.Max( 1, mapping.OutputCount );
		Log.Info( $"[Cutting] ✓ {ing.SourceType} commence la découpe ({totalDuration:F1}s pour {mapping.OutputCount}× {mapping.OutputPrefab.ResourceName})" );

		UpdateCutSound();
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
			Log.Info( $"[Cutting] {ing.SourceType} retiré de la planche avant fin de découpe" );
			UpdateCutSound();
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
			float requiredDuration = CutDurationPerSlice * Math.Max( 1, mapping.OutputCount );
			if ( elapsed < requiredDuration ) continue;

			CutIngredient( ing, mapping );

			_trackedMapping.Remove( ing );
			_trackedStartTimes.Remove( ing );
		}

		UpdateCutSound();
	}

	private void CutIngredient( Ingredient ing, CuttingMapping mapping )
	{
		var basePos = ing.WorldPosition;
		var baseRot = ing.WorldRotation;

		// Détruit l'ingrédient source AVANT de spawner les tranches (anti-duplication)
		ing.GameObject.Destroy();

		int count = Math.Max( 1, mapping.OutputCount );
		for ( int i = 0; i < count; i++ )
		{
			// Espace les tranches autour du point d'origine pour qu'elles ne s'empilent pas
			float angle = ( i / (float)count ) * MathF.PI * 2f;
			var offset = new Vector3( MathF.Cos( angle ) * 6f, MathF.Sin( angle ) * 6f, 4f );

			var go = Spawnable.CreateWithReturnFromHost( mapping.OutputPrefab.ResourcePath, new Transform( basePos + offset, baseRot ) );
			if ( go == null ) continue;
			go.NetworkSpawn();
		}

		BroadcastCutFinishedSound();

		Log.Info( $"[Cutting] ✓ {mapping.FromType} découpé en {count}× {mapping.OutputPrefab.ResourceName}" );
	}

	private void UpdateCutSound()
	{
		if ( !Networking.IsHost ) return;
		_trackedStartTimes.Keys.ToList().ForEach( k => { if ( !k.IsValid() ) _trackedStartTimes.Remove( k ); } );
		IsCutting = _trackedStartTimes.Count > 0;
	}

	protected override void OnUpdate()
	{
		// Reconcile chaque frame : tous les clients (host inclus) gèrent leur handle local
		if ( CutSound == null ) return;

		if ( IsCutting && _cutSoundHandle == null )
			_cutSoundHandle = Sound.Play( CutSound, WorldPosition );
		else if ( !IsCutting && _cutSoundHandle != null )
		{
			_cutSoundHandle.Stop();
			_cutSoundHandle = null;
		}
	}

	[Rpc.Broadcast]
	private void BroadcastCutFinishedSound()
	{
		if ( CutFinishedSound != null )
			Sound.Play( CutFinishedSound, WorldPosition );
	}

	protected override void OnDestroy()
	{
		if ( _cutSoundHandle != null )
		{
			_cutSoundHandle.Stop();
			_cutSoundHandle = null;
		}
	}

	public class CuttingMapping
	{
		[Property] public IngredientType FromType { get; set; }
		[Property] public PrefabFile OutputPrefab { get; set; }
		[Property, Range( 1, 10 )] public int OutputCount { get; set; } = 4;
	}
}
