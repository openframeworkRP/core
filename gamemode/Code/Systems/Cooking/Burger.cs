using System.Collections.Generic;

namespace OpenFramework.Systems.Cooking;

/// <summary>
/// Burger en cours d'assemblage ou fini. Accumule la liste des condiments dans
/// l'ordre où ils ont été posés, et empile dynamiquement les modèles 3D.
///
/// État synchronisé :
///   - CondimentsCsv : liste des IngredientType posés, séparés par virgules
///   - IsFinalized   : true quand le BunTop a été posé (le burger se détache de la planche)
///
/// Limite hard : MaxCondiments empêche les burgers infinis (anti-griefing).
/// </summary>
public sealed class Burger : Component
{
	public const int MaxCondiments = 12;

	/// <summary>Espacement par défaut si une entrée VisualLibrary a StackHeight = 0.</summary>
	[Property, Range( 0.5f, 8f )] public float DefaultStackHeight { get; set; } = 2.5f;

	/// <summary>
	/// Bouton éditeur : affiche toutes les entrées de la VisualLibrary empilées
	/// (teintes blanches) pour visualiser l'ordre + StackHeight de chaque couche.
	/// À cliquer après chaque modif, en jeu c'est le CondimentsCsv synced qui prime.
	/// </summary>
	[Button( "Preview : afficher la pile" ), Group( "Preview" )]
	public void EditorBuildPreview()
	{
		BuildPreviewStack();
	}

	[Button( "Preview : effacer" ), Group( "Preview" )]
	public void EditorClearPreview()
	{
		ClearStackVisuals();
	}

	/// <summary>
	/// Mapping IngredientType → Model. Configuré dans le prefab burger pour que
	/// chaque ingrédient ait son visuel propre quand empilé.
	/// </summary>
	[Property] public List<IngredientVisual> VisualLibrary { get; set; } = new();

	[Sync( SyncFlags.FromHost )]
	public string CondimentsCsv { get; set; } = "";

	/// <summary>
	/// Teintes par couche — synced en parallèle de CondimentsCsv. Format : "r,g,b,a;r,g,b,a;...".
	/// Une teinte par condiment, dans le même ordre. Permet à chaque couche du burger
	/// d'hériter de la teinte qu'avait l'ingrédient au moment du placement (ex: steak brun).
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public string TintsCsv { get; set; } = "";

	[Sync( SyncFlags.FromHost )]
	public bool IsFinalized { get; set; } = false;

	/// <summary>
	/// Calories totales du burger — somme des EffectiveCalories de chaque ingrédient
	/// au moment où il a été posé sur la planche. Utilisé pour la nutrition / score
	/// quand le burger est mangé ou livré.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public int TotalCalories { get; set; } = 0;

	private readonly List<GameObject> _stackedVisuals = new();
	private string _lastObservedCsv = "";

	protected override void OnUpdate()
	{
		// Resync depuis CondimentsCsv synced FromHost (runtime). En éditeur de prefab,
		// l'utilisateur clique "Preview : afficher la pile" pour visualiser.
		if ( CondimentsCsv != _lastObservedCsv )
		{
			_lastObservedCsv = CondimentsCsv;
			RebuildVisualStack();
		}
	}

	/// <summary>Construit la pile de preview depuis VisualLibrary (toutes les entrées, teintes blanches).</summary>
	private void BuildPreviewStack()
	{
		ClearStackVisuals();
		if ( VisualLibrary == null ) return;

		float yOffset = 0f;
		foreach ( var visual in VisualLibrary )
		{
			if ( visual?.Model == null ) continue;

			var go = new GameObject( true, $"preview_{visual.Type}" );
			go.SetParent( GameObject );
			go.LocalPosition = Vector3.Up * yOffset;
			go.LocalRotation = Rotation.Identity;

			var renderer = go.Components.Create<ModelRenderer>();
			renderer.Model = visual.Model;
			// Teinte blanche pour voir les modèles purs en preview

			_stackedVisuals.Add( go );

			float layerHeight = visual.StackHeight != 0f ? visual.StackHeight : DefaultStackHeight;
			yOffset += layerHeight;
		}
	}

	private void ClearStackVisuals()
	{
		foreach ( var go in _stackedVisuals )
		{
			if ( go.IsValid() ) go.Destroy();
		}
		_stackedVisuals.Clear();
	}


	public List<IngredientType> GetCondiments()
	{
		if ( string.IsNullOrEmpty( CondimentsCsv ) ) return new List<IngredientType>();
		var result = new List<IngredientType>();
		foreach ( var part in CondimentsCsv.Split( ',' ) )
		{
			if ( System.Enum.TryParse<IngredientType>( part, out var t ) )
				result.Add( t );
		}
		return result;
	}

	/// <summary>
	/// Ajoute un condiment au burger. <paramref name="calories"/> est ajouté
	/// au TotalCalories. <paramref name="tint"/> sera utilisé pour la couche
	/// visuelle correspondante (récupéré depuis Ingredient.EffectiveTint).
	/// </summary>
	public bool TryAddCondiment( IngredientType type, int calories = 0, Color? tint = null )
	{
		if ( !Networking.IsHost ) return false;
		if ( IsFinalized ) return false;

		var condiments = GetCondiments();
		if ( condiments.Count >= MaxCondiments ) return false;

		// Premier ingrédient : doit être BunBottom
		if ( condiments.Count == 0 && type != IngredientType.BunBottom ) return false;

		// BunBottom ne peut pas être posé deux fois (sauf pour le tout 1er)
		if ( type == IngredientType.BunBottom && condiments.Contains( IngredientType.BunBottom ) ) return false;

		// BunTop ferme le burger → finalize
		bool isFinalizing = ( type == IngredientType.BunTop );

		condiments.Add( type );
		CondimentsCsv = string.Join( ",", condiments );

		// Ajout teinte parallèle (blanche par défaut)
		var t = tint ?? Color.White;
		TintsCsv = string.IsNullOrEmpty( TintsCsv )
			? FormatColor( t )
			: $"{TintsCsv};{FormatColor( t )}";

		TotalCalories += calories;

		if ( isFinalizing )
			IsFinalized = true;

		return true;
	}

	private static string FormatColor( Color c ) => $"{c.r:F3},{c.g:F3},{c.b:F3},{c.a:F3}";

	private static Color ParseColor( string s )
	{
		if ( string.IsNullOrEmpty( s ) ) return Color.White;
		var parts = s.Split( ',' );
		if ( parts.Length < 3 ) return Color.White;
		float r = float.Parse( parts[0], System.Globalization.CultureInfo.InvariantCulture );
		float gC = float.Parse( parts[1], System.Globalization.CultureInfo.InvariantCulture );
		float b = float.Parse( parts[2], System.Globalization.CultureInfo.InvariantCulture );
		float a = parts.Length >= 4 ? float.Parse( parts[3], System.Globalization.CultureInfo.InvariantCulture ) : 1f;
		return new Color( r, gC, b, a );
	}

	private List<Color> GetTints()
	{
		if ( string.IsNullOrEmpty( TintsCsv ) ) return new List<Color>();
		return TintsCsv.Split( ';' ).Select( ParseColor ).ToList();
	}

	private void RebuildVisualStack()
	{
		ClearStackVisuals();

		var condiments = GetCondiments();
		var tints = GetTints();
		float yOffset = 0f;
		int missing = 0;

		Log.Info( $"[Burger] VisualLibrary contient {VisualLibrary?.Count ?? 0} entrées : {string.Join( ", ", VisualLibrary?.Select( v => $"{v.Type}={(v.Model != null ? "OK" : "NULL")}" ) ?? new List<string>() )}" );

		for ( int i = 0; i < condiments.Count; i++ )
		{
			var type = condiments[i];

			// Lookup direct par SourceType (RawBeef, Cheese, Tomato, etc.).
			// L'état (Raw/Cooked/Burned) est porté par la tint, pas par le type.
			var visual = VisualLibrary?.FirstOrDefault( v => v.Type == type );

			if ( visual == null )
			{
				Log.Warning( $"[Burger] {type} : AUCUNE entrée dans VisualLibrary" );
				missing++;
				continue;
			}
			if ( visual.Model == null )
			{
				Log.Warning( $"[Burger] {type} : entrée présente mais Model = NULL" );
				missing++;
				continue;
			}

			var go = new GameObject( true, $"burger_layer_{type}" );
			go.SetParent( GameObject );
			go.LocalPosition = Vector3.Up * yOffset;
			go.LocalRotation = Rotation.Identity;

			var renderer = go.Components.Create<ModelRenderer>();
			renderer.Model = visual.Model;

			// Applique la teinte transmise depuis l'Ingredient au moment du placement
			if ( i < tints.Count )
				renderer.Tint = tints[i];

			_stackedVisuals.Add( go );

			// Espacement par couche : visual.StackHeight si != 0, sinon DefaultStackHeight global.
			// StackHeight peut être négatif pour corriger des modèles au pivot mal placé.
			float layerHeight = visual.StackHeight != 0f ? visual.StackHeight : DefaultStackHeight;
			yOffset += layerHeight;
		}

		Log.Info( $"[Burger] RebuildVisualStack : {_stackedVisuals.Count} couches affichées, {missing} sans modèle (csv='{CondimentsCsv}')" );
	}

	public class IngredientVisual
	{
		[Property] public IngredientType Type { get; set; }
		[Property] public Model Model { get; set; }

		/// <summary>
		/// Épaisseur de cette couche dans la pile du burger (en unités locales).
		/// 0 → utilise DefaultStackHeight du Burger.
		/// > 0 → décale la couche suivante vers le haut.
		/// < 0 → la couche suivante chevauche cette couche (utile pour corriger
		///        la hauteur visuelle d'un modèle dont le pivot est trop haut).
		/// </summary>
		[Property, Range( -5f, 10f )] public float StackHeight { get; set; } = 0f;
	}
}
