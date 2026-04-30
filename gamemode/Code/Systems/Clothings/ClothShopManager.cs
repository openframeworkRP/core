using OpenFramework;
using OpenFramework.Inventory;
using System.Threading.Tasks;

public class ClothingShopManager : Component
{

	// La liste des objets que le joueur a choisi d'essayer
	public List<SelectedEntry> MaSelection { get; set; } = new();

	public List<ItemMetadata> HiddenItems { get; set; } = new();

	public ItemMetadata SelectedItem { get; set; }

	public class SelectedEntry
	{
		public ItemMetadata Item { get; set; }
		public Color Tint { get; set; } = Color.White;
	}

	public void UpdateStoredColor( ItemMetadata item, Color newColor )
	{
		var entry = MaSelection.FirstOrDefault( x => x.Item == item );
		if ( entry != null )
		{
			entry.Tint = newColor;
		}
	}

	public void ToggleHideSingle( ItemMetadata item )
	{
		if ( item == null ) return;
		SelectedItem = (SelectedItem == item) ? null : item;

		RefreshPreview( Color.White ); // On envoie blanc par défaut
	}

	public void AddToPreview( ItemMetadata newItem )
	{
		if ( newItem == null || newItem.ClothingResource == null ) return;

		// 1. Correction : x est un SelectedEntry, on doit accéder à x.Item.ClothingsResource
		MaSelection.RemoveAll( x => x.Item != null &&
									x.Item.ClothingResource != null &&
									x.Item.ClothingResource.SlotsOver == newItem.ClothingResource.SlotsOver );

		// 2. Correction : On doit ajouter un nouveau SelectedEntry, pas juste l'item
		MaSelection.Add( new SelectedEntry
		{
			Item = newItem,
			Tint = Color.White
		} );

		RefreshPreview( Color.White );
	}

	public void ShowSingleItem( ItemMetadata item )
	{
		if ( item == null || item.ClothingResource == null ) return;

		var player = Scene.GetAllComponents<PlayerPawn>().FirstOrDefault( x => !x.IsProxy );
		var dresser = player?.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		if ( dresser == null ) return;

		// 1. On part de ce qu'il porte VRAIMENT (pas des essais)
		var finalClothing = dresser.Clothing.ToList();

		// 2. On remplace le slot par l'item cliqué
		finalClothing.RemoveAll( x => x.Clothing.SlotsOver == item.ClothingResource.SlotsOver );
		finalClothing.Add( new ClothingContainer.ClothingEntry { Clothing = item.ClothingResource } );

		// 3. On applique
		dresser.Clothing = finalClothing;
		dresser.Apply();
	}



	public async void RefreshPreview( Color currentTint )
	{
		var player = Scene.GetAllComponents<PlayerPawn>().FirstOrDefault( x => !x.IsProxy );
		var dresser = player?.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		var dressingRoom = Scene.GetAllComponents<ShopDressingRoom>().FirstOrDefault();

		if ( dresser == null || dressingRoom?.OriginalClothing == null ) return;

		// 1. On repart de la tenue d'origine
		var finalClothing = new List<ClothingContainer.ClothingEntry>( dressingRoom.OriginalClothing );

		// 2. On boucle sur MaSelection (qui contient des SelectedEntry)
		foreach ( var entry in MaSelection )
		{
			// ERREUR ICI : Il faut faire entry.Item.ClothingsResource
			if ( entry.Item == null || entry.Item.ClothingResource == null ) continue;

			var resource = entry.Item.ClothingResource;

			finalClothing.RemoveAll( x => x.Clothing.SlotsOver == resource.SlotsOver );
			finalClothing.Add( new ClothingContainer.ClothingEntry { Clothing = resource } );
		}

		dresser.Clothing = finalClothing;
		_ = dresser.Apply();

		// Attendre que s&box crée les nouveaux GameObjects 3D
		await Task.Delay( 100 );

		// 3. Réappliquer les couleurs stockées
		foreach ( var entry in MaSelection )
		{
			if ( entry.Item == null ) continue;

			var obj = FindClothingObject( entry.Item.ClothingResource.ResourceName, dresser );
			if ( obj.IsValid() )
			{
				var renderer = obj.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndChildren );
				if ( renderer.IsValid() )
				{
					// On applique la couleur sauvegardée dans l'entrée
					renderer.Tint = entry.Tint;
				}
			}
		}
	}

	public void RefreshColorsOnly( Color tintColor )
	{
		var player = Scene.GetAllComponents<PlayerPawn>().FirstOrDefault( x => !x.IsProxy );
		var dresser = player?.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		if ( dresser == null || SelectedItem == null ) return;

		// On récupère le nom de l'item en cours d'essai
		var resourceName = SelectedItem.ClothingResource?.ResourceName;
		var clothingObj = FindClothingObject( resourceName, dresser );

		if ( clothingObj.IsValid() )
		{
			var renderers = clothingObj.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren );
			foreach ( var r in renderers )
			{
				r.Tint = tintColor;
			}
		}
	}

	[Rpc.Broadcast]
	public void BroadcastEquip( GameObject playerObj, string path, Color tint )
	{
		if ( !playerObj.IsValid() ) return;

		var dresser = playerObj.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		var clothingResource = ResourceLibrary.Get<Clothing>( path );

		if ( dresser != null && clothingResource != null )
		{
			// 1. Logique de remplacement
			var newCategory = clothingResource.Category;
			dresser.Clothing.RemoveAll( x => x.Clothing != null && x.Clothing.Category == newCategory );

			// 2. Ajout du vêtement
			dresser.Clothing.Add( new ClothingContainer.ClothingEntry { Clothing = clothingResource } );

			// 3. Application visuelle différée pour la couleur
			_ = ApplyWithColor( dresser, clothingResource.ResourceName, tint );
		}
	}


	private async Task ApplyWithColor( Dresser dresser, string resourceName, Color tint )
	{
		_ = dresser.Apply();

		// On attend que s&box crée le GameObject du vêtement (important !)
		await Task.Delay( 150 );

		// On cherche l'objet pour changer son Tint
		var clothingObj = FindClothingObject( resourceName, dresser );
		if ( clothingObj.IsValid() )
		{
			var renderer = clothingObj.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndChildren );
			if ( renderer.IsValid() )
			{
				renderer.Tint = tint;
			}
		}
	}

	private GameObject FindClothingObject( string resourceName, Dresser dresser )
	{
		if ( string.IsNullOrEmpty( resourceName ) ) return null;

		// On cherche dans les enfants l'objet qui contient le nom de la ressource
		return dresser.GameObject.GetAllObjects( true )
			.FirstOrDefault( x => x.Name.Contains( resourceName, StringComparison.OrdinalIgnoreCase ) );
	}

	
}
