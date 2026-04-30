using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Editor;
using Sandbox;
using OpenFramework.Inventory;
using OpenFramework.Inventory.UI;

namespace OpenFramework.EditorTools;

/// <summary>
/// Outil editor : itere tous les ItemMetadata du projet, genere un PNG
/// d'icone pour chacun (via InventoryIconRenderer), et patche directement
/// le fichier .item JSON pour assigner le PNG au champ "Icon".
///
/// Apres execution : recharger le projet (ou redemarrer l'editeur) pour
/// que les .item rechargent leur Icon.
///
/// Lance via le menu Editor : OpenFramework > Generate Item Icons.
/// </summary>
public static class InventoryIconBatchGenerator
{
	private const string OutputSubPath = "ui/item_icons";
	private const int IconSize = 256;

	[Menu( "Editor", "OpenFramework/Generate Item Icons" )]
	public static void GenerateAll()
	{
		var assetsPath = Project.Current?.GetAssetsPath();
		if ( string.IsNullOrEmpty( assetsPath ) )
		{
			Log.Warning( "[IconBatch] Pas de Project.Current ou pas de dossier Assets." );
			return;
		}

		var outputDir = Path.Combine( assetsPath, OutputSubPath );
		Directory.CreateDirectory( outputDir );

		var items = ResourceLibrary.GetAll<ItemMetadata>().ToList();
		Log.Info( $"[IconBatch] {items.Count} items trouves dans le projet." );

		int generated = 0, skipped = 0, failed = 0, patched = 0;

		foreach ( var item in items )
		{
			if ( item == null ) { skipped++; continue; }

			if ( item.PreviewObject == null || !item.PreviewObject.IsValid )
			{
				Log.Info( $"[IconBatch] Skip '{item.ResourceName}' : pas de PreviewObject." );
				skipped++;
				continue;
			}

			var pngBytes = InventoryIconRenderer.RenderToPngBytes( item.PreviewObject, IconSize );
			if ( pngBytes == null || pngBytes.Length == 0 )
			{
				Log.Warning( $"[IconBatch] Render echoue pour '{item.ResourceName}'." );
				failed++;
				continue;
			}

			var fileName = SanitizeFileName( item.ResourceName ) + ".png";
			var pngFullPath = Path.Combine( outputDir, fileName );
			var pngRelPath = $"{OutputSubPath}/{fileName}"; // chemin attendu par s&box (relatif a Assets/)

			try
			{
				File.WriteAllBytes( pngFullPath, pngBytes );
				generated++;
			}
			catch ( System.Exception ex )
			{
				Log.Warning( $"[IconBatch] Ecriture PNG echouee pour '{item.ResourceName}' : {ex.Message}" );
				failed++;
				continue;
			}

			// Patch du .item JSON pour y inscrire le champ "Icon".
			if ( TryPatchItemFile( item, assetsPath, pngRelPath ) )
				patched++;
		}

		Log.Info( $"[IconBatch] Termine. PNG generes: {generated}  .item patches: {patched}  Skip: {skipped}  Echec: {failed}" );
		Log.Info( $"[IconBatch] Dossier de sortie : {outputDir}" );
		Log.Info( "[IconBatch] Recharge le projet (ou redemarre l'editeur) pour que les Icon prennent effet." );
	}

	/// <summary>
	/// Modifie le fichier .item sur disque pour y inscrire le champ "Icon".
	/// On parse le JSON existant, on injecte/remplace la valeur, on reecrit
	/// en preservant les autres champs et l'ordre approximatif.
	/// </summary>
	private static bool TryPatchItemFile( ItemMetadata item, string assetsPath, string iconRelPath )
	{
		try
		{
			var itemFullPath = Path.Combine( assetsPath, item.ResourcePath );
			if ( !File.Exists( itemFullPath ) )
			{
				Log.Warning( $"[IconBatch] .item introuvable sur disque : {itemFullPath}" );
				return false;
			}

			var jsonText = File.ReadAllText( itemFullPath );
			var node = JsonNode.Parse( jsonText );
			if ( node is not JsonObject obj )
			{
				Log.Warning( $"[IconBatch] JSON invalide : {item.ResourcePath}" );
				return false;
			}

			obj["Icon"] = iconRelPath;

			var output = obj.ToJsonString( new JsonSerializerOptions { WriteIndented = true } );
			File.WriteAllText( itemFullPath, output );
			return true;
		}
		catch ( System.Exception ex )
		{
			Log.Warning( $"[IconBatch] Patch .item echoue pour '{item?.ResourceName}' : {ex.Message}" );
			return false;
		}
	}

	private static string SanitizeFileName( string name )
	{
		if ( string.IsNullOrEmpty( name ) ) return "unnamed";
		foreach ( var c in Path.GetInvalidFileNameChars() )
			name = name.Replace( c, '_' );
		return name;
	}
}
