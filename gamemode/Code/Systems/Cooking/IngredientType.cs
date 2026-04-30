namespace OpenFramework.Systems.Cooking;

/// <summary>
/// Type d'ingrédient — utilisé par les stations pour décider des transformations
/// et par la planche d'assemblage pour valider l'ordre de l'empilement.
/// V1 burger : 5 types essentiels.
/// </summary>
public enum IngredientType
{
	// Indices verrouillés explicitement : ne JAMAIS modifier les valeurs existantes,
	// uniquement ajouter de nouveaux types à la fin (8, 9, 10, ...).
	// Sinon les .item / .prefab sérialisés se retrouvent avec des valeurs orphelines.
	None         = 0,
	BunBottom    = 1,
	BunTop       = 2,
	RawBeef      = 3,
	Cheese       = 4,
	Tomato       = 5,    // tranche de tomate
	Lettuce      = 6,    // feuille de salade
	Onion        = 7,
	WholeTomato  = 8,    // tomate entière → se découpe en N×Tomato
	WholeLettuce = 9,    // salade entière → se découpe en N×Lettuce
	WholeCheese  = 10,   // bloc de cheddar → se découpe en N×Cheese
	WholePotato  = 11,   // pomme de terre entière → se découpe en N×RawFries
	RawFries     = 12,   // portion de frites → cuit dans le fryer → state Cooked
	FriesPouch   = 13,   // pochette en carton vide → reçoit des frites cuites sur la planche
	EmptyCup     = 14    // gobelet vide → posé sous la fontaine à soda → devient un soda
	// Note : un ingrédient a une seule entrée par type, son état (Raw/Cooked/Burned)
	// est porté par CookState et transmis au burger via la tint et les calories.
}

/// <summary>
/// État de cuisson pour les ingrédients qui passent par une station de cuisson.
/// </summary>
public enum CookState
{
	Raw,
	Cooked,
	Burned
}
