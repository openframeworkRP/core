using System.Collections.Generic;

namespace OpenFramework.Systems.Cooking;

/// <summary>
/// Panier à friteuse — conteneur physique qui accueille des ingrédients.
///
/// Workflow :
///   1. Le joueur dépose des raw_fries dans le panier (le trigger les aspire)
///   2. Une fois aspirés, leurs Colliders sont désactivés pour qu'ils ne fassent
///      plus de physique avec le panier ni entre eux
///   3. Le joueur attrape le panier et le plonge dans la friteuse
///   4. C'est LE PANIER (pas chaque ingrédient) qui déclenche le trigger fryer ;
///      le fryer voit le FryerBasket entrer et démarre la cuisson de tout son
///      contenu en lot
///   5. Les frites cuites restent parentées au panier
///   6. Quand le joueur sort le panier de l'huile, le fryer arrête le suivi
///   7. Quand le panier est posé sur la planche d'assemblage, il vide ses
///      contenus (DumpContents) pour que la planche les détecte normalement
///
/// Multi-safe : tout passe par le host (Networking.IsHost).
/// </summary>
public sealed class FryerBasket : Component, Component.ITriggerListener
{
	[Property] public string DisplayName { get; set; } = "Panier à friteuse";

	/// <summary>
	/// Position locale (relative au panier) où ranger les ingrédients quand ils
	/// entrent. On ajoute un petit décalage aléatoire dessus pour que les
	/// ingrédients ne s'empilent pas exactement au même endroit.
	/// </summary>
	[Property] public Vector3 ContentLocalOffset { get; set; } = new Vector3( 0f, 0f, 2f );

	/// <summary>
	/// Liste blanche : seuls les ingrédients dont le SourceType est dans cette liste
	/// peuvent être aspirés par le panier. Bloque les pochettes, gobelets et autres
	/// items qu'on ne veut pas voir tomber dedans par erreur. Si la liste est vide,
	/// le panier accepte tout (rétrocompatible).
	/// </summary>
	[Property] public List<IngredientType> AcceptedTypes { get; set; } = new() { IngredientType.RawFries };

	private readonly HashSet<GameObject> _contents = new();

	public void OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost ) return;
		if ( other?.GameObject == null ) return;

		var ing = other.GameObject.Components.Get<Ingredient>( FindMode.EverythingInSelfAndAncestors );
		if ( ing == null ) return;

		// Filtre par whitelist : on rejette tout ce qui n'est pas dans AcceptedTypes
		if ( AcceptedTypes != null && AcceptedTypes.Count > 0 && !AcceptedTypes.Contains( ing.SourceType ) )
		{
			Log.Info( $"[Basket] {ing.SourceType} non accepté (whitelist : {string.Join( ",", AcceptedTypes )})" );
			return;
		}

		// Évite de ré-aspirer un ingrédient déjà dans ce panier ou dans un autre
		var existingBasket = ing.Components.Get<FryerBasket>( FindMode.InAncestors );
		if ( existingBasket != null ) return;

		AddToBasket( ing.GameObject );
	}

	/// <summary>
	/// Range un GameObject dans le panier : désactive sa physique ET ses colliders
	/// pour qu'il n'interfère plus avec rien (panier, autres frites, etc.) puis le
	/// parente au transform du panier. La détection au fryer/planche se fait
	/// désormais via le panier lui-même, pas via les ingrédients individuels.
	/// </summary>
	public void AddToBasket( GameObject go )
	{
		if ( !Networking.IsHost ) return;
		if ( go == null || !go.IsValid() ) return;
		if ( _contents.Contains( go ) ) return;

		var rb = go.Components.Get<Rigidbody>();
		if ( rb.IsValid() ) rb.MotionEnabled = false;

		// Désactive tous les colliders : plus aucune interaction physique ni
		// trigger pendant que l'ingrédient est dans le panier.
		foreach ( var col in go.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			col.Enabled = false;
		}

		go.SetParent( GameObject );

		// Petite dispersion pour ne pas empiler à la même position locale
		float jitterX = ( (float)System.Random.Shared.NextDouble() - 0.5f ) * 4f;
		float jitterY = ( (float)System.Random.Shared.NextDouble() - 0.5f ) * 4f;
		float jitterZ = (float)System.Random.Shared.NextDouble() * 2f;
		go.LocalPosition = ContentLocalOffset + new Vector3( jitterX, jitterY, jitterZ );
		go.LocalRotation = Rotation.Identity;

		_contents.Add( go );
		Log.Info( $"[Basket] {go.Name} ajouté au panier ({_contents.Count} item(s) dedans)" );
	}

	/// <summary>
	/// Vide le panier : détache tous les enfants, réactive leurs colliders et
	/// leur physique. Utilisé par AssemblyPlank quand on y dépose le panier
	/// (les ingrédients tombent et sont détectés par le trigger de la planche).
	/// </summary>
	public void DumpContents()
	{
		if ( !Networking.IsHost ) return;
		if ( _contents.Count == 0 ) return;

		Log.Info( $"[Basket] Vidage du panier ({_contents.Count} item(s))" );

		foreach ( var go in _contents.ToList() )
		{
			if ( !go.IsValid() ) continue;
			ReleaseFromBasket( go );
		}
		_contents.Clear();
	}

	/// <summary>
	/// Détache un seul élément du panier et restaure ses colliders / physique.
	/// Public pour permettre à AssemblyPlank de retirer une frite cuite tout en
	/// laissant le reste dans le panier.
	/// </summary>
	public void ReleaseFromBasket( GameObject go )
	{
		if ( !Networking.IsHost ) return;
		if ( go == null || !go.IsValid() ) return;

		go.SetParent( null );

		foreach ( var col in go.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			col.Enabled = true;
		}

		var rb = go.Components.Get<Rigidbody>();
		if ( rb.IsValid() ) rb.MotionEnabled = true;

		_contents.Remove( go );
	}

	/// <summary>Liste filtrée du contenu valide (utile pour le debug ou des intégrations).</summary>
	public IEnumerable<GameObject> GetContents()
	{
		_contents.RemoveWhere( g => !g.IsValid() );
		return _contents;
	}
}
