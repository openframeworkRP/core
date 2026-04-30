namespace OpenFramework.Systems.Cooking;

/// <summary>
/// Component à attacher sur chaque prefab d'ingrédient physique (steak, bun, fromage…).
/// L'ingrédient est une entité 3D du monde — pas un item d'inventaire.
/// La détection des stations passe par les triggers de la station (qui appellent
/// nos handlers via OnTriggerEnter/Exit côté host).
///
/// Synced FromHost :
///   - State : Raw / Cooked / Burned (pour les ingrédients cuisinables)
///   - SourceType : type de base (RawBeef devient CookedBeef quand State=Cooked, etc.)
/// </summary>
public sealed class Ingredient : Component
{
	[Property] public IngredientType SourceType { get; set; } = IngredientType.None;

	/// <summary>
	/// Modèles alternatifs selon l'état de cuisson — assignés dans le prefab.
	/// Si null, le modèle de l'état Raw est conservé pour les autres états (utile si
	/// tu changes uniquement la couleur via les Tint).
	/// </summary>
	[Property] public Model RawModel { get; set; }
	[Property] public Model CookedModel { get; set; }
	[Property] public Model BurnedModel { get; set; }

	/// <summary>
	/// Teintes appliquées au ModelRenderer selon l'état. Permet de réutiliser le
	/// même modèle 3D et juste changer la couleur (ex: steak rouge → brun → noir).
	/// Si une teinte est laissée à blanc (1,1,1,1), elle ne modifie pas le rendu.
	/// </summary>
	[Property] public Color RawTint { get; set; } = Color.White;
	[Property] public Color CookedTint { get; set; } = Color.White;
	[Property] public Color BurnedTint { get; set; } = Color.White;

	/// <summary>
	/// Calories apportées au burger selon l'état de cuisson.
	/// Pour les ingrédients sans cuisson (fromage, légumes, pains), seul RawCalories est utilisé.
	/// Convention :
	///   - Cru = peu nutritif (mauvais)
	///   - Cuit = optimal
	///   - Brûlé = quasi-nul (mauvais aussi)
	/// </summary>
	[Property] public int RawCalories { get; set; } = 30;
	[Property] public int CookedCalories { get; set; } = 80;
	[Property] public int BurnedCalories { get; set; } = 5;

	/// <summary>Calories effectives selon l'état actuel.</summary>
	public int EffectiveCalories => State switch
	{
		CookState.Cooked => CookedCalories,
		CookState.Burned => BurnedCalories,
		_ => RawCalories
	};

	/// <summary>Teinte effective selon l'état actuel — lue par AssemblyPlank pour la transmettre au burger.</summary>
	public Color EffectiveTint => State switch
	{
		CookState.Cooked => CookedTint,
		CookState.Burned => BurnedTint,
		_ => RawTint
	};

	/// <summary>
	/// Si true, cet ingrédient peut être cuisiné (passe par le GrillStation et change d'état).
	/// Si false, il reste toujours à State=Raw (pour les ingrédients qui ne se cuisent pas
	/// dans la recette : tomate fraîche, salade…).
	/// </summary>
	[Property] public bool IsCookable { get; set; } = false;

	[Sync( SyncFlags.FromHost )]
	public CookState State { get; set; } = CookState.Raw;

	private CookState _lastObservedState = CookState.Raw;
	private ModelRenderer _renderer;

	protected override void OnStart()
	{
		_renderer = Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		_lastObservedState = State;
		ApplyModelForState();
	}

	protected override void OnUpdate()
	{
		if ( _lastObservedState != State )
		{
			_lastObservedState = State;
			ApplyModelForState();
		}
	}

	private void ApplyModelForState()
	{
		if ( !_renderer.IsValid() ) return;

		var targetModel = State switch
		{
			CookState.Cooked => CookedModel ?? RawModel,
			CookState.Burned => BurnedModel ?? CookedModel ?? RawModel,
			_ => RawModel
		};

		if ( targetModel != null )
			_renderer.Model = targetModel;

		var targetTint = State switch
		{
			CookState.Cooked => CookedTint,
			CookState.Burned => BurnedTint,
			_ => RawTint
		};

		_renderer.Tint = targetTint;
	}

	/// <summary>
	/// Appelé côté host par une station de cuisson pour faire avancer l'état.
	/// </summary>
	public void SetState( CookState newState )
	{
		if ( !Networking.IsHost ) return;
		if ( State == newState ) return;
		Log.Info( $"[Ingredient] {SourceType} : {State} → {newState}" );
		State = newState;
	}
}
