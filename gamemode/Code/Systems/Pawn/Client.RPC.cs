using Facepunch;
using Facepunch.UI;
using Sandbox;
using Sandbox.World;
using OpenFramework.Extension;
using OpenFramework.Systems.Weapons;
using System.Threading.Tasks;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Pawn;

public partial class Client : Component
{
	// ---------------------------
	//  FAIM
	// ---------------------------

	[Rpc.Host]
	public static void AddHunger( float amount )
	{
		var client = Rpc.Caller.GetClient();
		if ( client == null || client.Data == null ) return;

		client.Data.Hunger = Math.Clamp( client.Data.Hunger + amount, 0f, 100f );
		//client.Notify( NotificationType.Info, $"🍗 Faim : {client.Data.Hunger:0}% (+{amount})" );
	}

	[Rpc.Host]
	public static void RemoveHunger( float amount )
	{
		var client = Rpc.Caller.GetClient();
		if ( client == null || client.Data == null ) return;

		client.Data.Hunger = Math.Clamp( client.Data.Hunger - amount, 0f, 100f );
		//client.Notify( NotificationType.Warning, $"🍗 Faim : {client.Data.Hunger:0}% (-{amount})" );
	}

	// ---------------------------
	//  SOIF
	// ---------------------------

	[Rpc.Host]
	public static void AddThirst( float amount )
	{
		var client = Rpc.Caller.GetClient();
		if ( client == null || client.Data == null ) return;

		client.Data.Thirst = Math.Clamp( client.Data.Thirst + amount, 0f, 100f );
		//client.Notify( NotificationType.Info, $"🥤 Soif : {client.Data.Thirst:0}% (+{amount})" );
	}

	[Rpc.Host]
	public static void RemoveThirst( float amount )
	{
		var client = Rpc.Caller.GetClient();
		if ( client == null || client.Data == null ) return;

		client.Data.Thirst = Math.Clamp( client.Data.Thirst - amount, 0f, 100f );
		//client.Notify( NotificationType.Warning, $"🥤 Soif : {client.Data.Thirst:0}% (-{amount})" );
	}

	// ---------------------------
	//  BANQUE
	// ---------------------------

	[Rpc.Host]
	public static void AddBank( int amount )
	{
		if ( amount <= 0 ) return;

		var client = Rpc.Caller.GetClient();
		if ( client == null || client.Data == null ) return;

		client.Data.Bank += amount;
		client.Notify( NotificationType.Success, $"🏦 +{amount}$ en banque" );
	}


	[Rpc.Host]
	public static void RemoveBank( int amount )
	{
		if ( amount <= 0 ) return;

		var client = Rpc.Caller.GetClient();
		if ( client == null || client.Data == null ) return;

		client.Data.Bank -= amount;

		// Notification dynamique
		if ( client.Data.Bank < 0 )
		{
			client.Notify( NotificationType.Warning, $"Payé ! Nouveau solde (Découvert) : {client.Data.Bank}$)" );
		}
		else
		{
			client.Notify( NotificationType.Warning, $"🏦 -{amount}$ en banque" );
		}
	}

	[Rpc.Host]
	public static void DepositMoney( int amount )
	{
		if ( amount <= 0 ) return;
		var cl = Rpc.Caller.GetClient();
		if ( cl == null || cl.Data == null ) return;

		// Vérification de sécurité côté SERVEUR (obligatoire avec FromHost)
		if ( MoneySystem.Get(cl) < amount ) return;

		// On fait tout d'un coup
		MoneySystem.Remove( cl, amount );
		cl.Data.Bank += amount;
	}

	/// <summary>
	/// Méthode centrale pour appliquer les vêtements et restaurer l'apparence.
	/// Les overrides skin/head/morphs court-circuitent la lecture de Client.SavedSkinGroup
	/// dans RestoreAppearance — indispensable au respawn ou sur un client quand le RPC
	/// arrive avant la sync de pawn.Client (Sync FromHost), sinon RestoreAppearance
	/// retombe sur "default" et écrase le bon MaterialGroup.
	/// </summary>
	private static async Task ApplyDresser( Dresser dresser, PlayerBody playerBody, string tintsJson = "{}", string resourceName = "", Color tint = default, string skinOverride = null, int? headOverride = null, string morphsOverride = null, string modelOverride = null )
	{
		try
		{
			bool hasOverride = skinOverride != null || headOverride.HasValue || morphsOverride != null;

			// Swap le modèle AVANT tout : Dresser.Apply doit dresser le bon body (female/male).
			if ( !string.IsNullOrEmpty( modelOverride ) && playerBody?.Renderer.IsValid() == true )
			{
				var model = ResourceLibrary.Get<Model>( modelOverride );
				if ( model != null && playerBody.Renderer.Model != model )
					playerBody.Renderer.Model = model;
			}

			// Pré-configure le skin AVANT Apply pour que les meshes créés héritent du bon MaterialGroup
			if ( hasOverride )
				playerBody?.RestoreAppearance( skinOverride, headOverride, morphsOverride );
			else
				playerBody?.RestoreAppearance();

			await dresser.Apply();

			// Attend un frame complet pour que le moteur finalise les body groups et meshes
			await GameTask.DelayRealtime( 1 );

			// Re-applique le skin APRÈS Apply (Dresser.Apply peut réinitialiser le MaterialGroup)
			if ( hasOverride )
				playerBody?.RestoreAppearance( skinOverride, headOverride, morphsOverride );
			else
				playerBody?.RestoreAppearance();

			// Force le MaterialGroup sur le BodyTarget du Dresser et ses enfants skin
			OpenFramework.Inventory.ClothingEquipment.RestoreSkinOnDresser( dresser, playerBody );

			// Teintes spécifiques
			if ( !string.IsNullOrEmpty( tintsJson ) && tintsJson != "{}" )
			{
				var tints = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Color>>( tintsJson );
				if ( tints != null )
				{
					foreach ( var child in dresser.GameObject.Children )
					{
						var match = tints.FirstOrDefault( x => child.Name.Contains( x.Key, StringComparison.OrdinalIgnoreCase ) );
						if ( match.Key != null )
						{
							var renderer = child.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndChildren );
							if ( renderer.IsValid() )
								renderer.Tint = match.Value;
						}
					}
				}
			}

			// Teinte sur un vêtement spécifique
			if ( !string.IsNullOrEmpty( resourceName ) )
			{
				var renderer = dresser.GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren )
					.FirstOrDefault( x => x.Model.ResourcePath.Contains( resourceName, StringComparison.OrdinalIgnoreCase ) );
				if ( renderer.IsValid() )
					renderer.Tint = tint;
			}
		}
		catch ( Exception e )
		{
			Log.Error( $"[ApplyDresser] Erreur: {e.Message}" );
		}
	}


	[Rpc.Broadcast]
	public static async void BroadcastAppearance( GameObject playerObj, bool isFemale, string modelPath, string skinGroup, int headIndex, string clothingJson, string tintsJson, string morphsJson )
	{
		if ( !playerObj.IsValid() ) return;

		// Le host recoit ce broadcast et doit persister TOUS les champs Saved*
		// pour les respawns futurs et la propagation aux late-joiners. Ces champs
		// sont [Sync(SyncFlags.FromHost)] : seul le host peut les ecrire, et ses
		// ecritures sont repliquees a tous les proxies (declenchant le [Change]
		// callback chez chaque client). Sans ca, les autres joueurs verraient le
		// pawn nu meme si BroadcastAppearance lui-meme s'execute (le RPC s'execute
		// localement chez chaque client mais ne survit pas aux respawns / arrivees
		// de nouveaux joueurs).
		if ( Networking.IsHost )
		{
			var pawn = playerObj.Components.Get<PlayerPawn>();
			if ( pawn?.Client != null )
			{
				pawn.Client.SavedIsFemale = isFemale;
				pawn.Client.SavedMorphsJson = morphsJson;
				pawn.Client.SavedHeadIndex = headIndex;
				pawn.Client.SavedSkinGroup = skinGroup;
				pawn.Client.SavedModelPath = modelPath ?? "";
				pawn.Client.SavedClothingJson = clothingJson ?? "[]";
				pawn.Client.HasCustomizedAppearance = true;

				// Persiste color/head/morphs sur l'API. Sans ca, a la reconnexion
				// HydrateAppearanceFromApi relit l'ancienne valeur (Dark par defaut)
				// et le perso revient en peau noire / autre tete. Le createur de
				// perso fait deja un POST /create avec ces champs, mais ce push est
				// indispensable quand on edite un perso existant (futur PNJ chir
				// esthetique / changement de morphs en RP).
				_ = PushBodyAppearanceToApi( pawn.Client, skinGroup, headIndex, morphsJson );
			}
			var appearance = playerObj.Components.Get<PlayerAppearance>( FindMode.EverythingInSelfAndChildren );
			appearance?.SetAppearanceFromServer( isFemale, morphsJson, headIndex, skinGroup );
		}

		var playerBody = playerObj.Components.Get<PlayerBody>( FindMode.EverythingInSelfAndChildren );
		var dresser = playerObj.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		if ( playerBody == null || dresser == null ) return;

		if ( !string.IsNullOrEmpty( modelPath ) )
		{
			var model = ResourceLibrary.Get<Model>( modelPath );
			if ( model != null && playerBody.Renderer.IsValid() )
				playerBody.Renderer.Model = model;
		
		}

		if ( !string.IsNullOrEmpty( clothingJson ) )
		{
			var paths = System.Text.Json.JsonSerializer.Deserialize<List<string>>( clothingJson );
			dresser.Clothing.Clear();
			foreach ( var path in paths )
			{
				var clothing = ResourceLibrary.Get<Clothing>( path );
				if ( clothing != null )
					dresser.Clothing.Add( new ClothingContainer.ClothingEntry { Clothing = clothing } );
			}
		}

		if ( playerBody?.Renderer.IsValid() == true )
		{
			playerBody.Renderer.SetBodyGroup( "legs", 0 );
		}

		await ApplyDresser( dresser, playerBody, tintsJson );
	}

	[Rpc.Broadcast]
	public static void BroadcastEquip( GameObject playerObj, string path, Color tint )
	{
		if ( !playerObj.IsValid() ) return;

		var dresser = playerObj.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		var clothing = ResourceLibrary.Get<Clothing>( path );
		if ( dresser == null || clothing == null ) return;

		dresser.Clothing.RemoveAll( x => x.Clothing?.Category == clothing.Category );
		dresser.Clothing.Add( new ClothingContainer.ClothingEntry { Clothing = clothing } );

		var playerBody = playerObj.Components.Get<PlayerBody>( FindMode.EverythingInSelfAndChildren );
		_ = ApplyDresser( dresser, playerBody, resourceName: clothing.ResourceName, tint: tint );
	}

	[Rpc.Broadcast]
	public static void BroadcastEquipList( GameObject playerObj, List<string> paths, bool keepHairAndBeard = true )
	{
		BroadcastEquipListInternal( playerObj, paths, keepHairAndBeard, null, null, null );
	}

	/// <summary>
	/// Variante utilisée au respawn : passe explicitement skin/head/morphs/modelPath
	/// pour que ApplyDresser n'ait pas à dépendre de pawn.Client.SavedSkinGroup, qui
	/// peut ne pas être encore synchronisé côté client quand le RPC arrive, et pour
	/// restaurer le modèle (female) effacé par le clone du prefab.
	/// </summary>
	[Rpc.Broadcast]
	public static void BroadcastEquipListWithAppearance( GameObject playerObj, List<string> paths, bool keepHairAndBeard, string skinGroup, int headIndex, string morphsJson, string modelPath )
	{
		BroadcastEquipListInternal( playerObj, paths, keepHairAndBeard, skinGroup, headIndex, morphsJson, modelPath );
	}

	private static void BroadcastEquipListInternal( GameObject playerObj, List<string> paths, bool keepHairAndBeard, string skinOverride, int? headOverride, string morphsOverride, string modelOverride = null )
	{
		if ( !playerObj.IsValid() )
		{
			Log.Warning( "[AppearanceSync][BCAST] BroadcastEquipList recu mais playerObj invalide" );
			return;
		}

		var side = Networking.IsHost ? "HOST" : "CLIENT";
		Log.Info( $"[AppearanceSync][{side}] BroadcastEquipList recu: pawn={playerObj.Name}, paths={paths?.Count ?? 0}, keepHairBeard={keepHairAndBeard}, skinOverride={skinOverride}, modelOverride={modelOverride}" );

		var dresser = playerObj.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		var playerBody = playerObj.Components.Get<PlayerBody>( FindMode.EverythingInSelfAndChildren );
		if ( dresser == null )
		{
			Log.Warning( $"[AppearanceSync][{side}] BroadcastEquipList: Dresser introuvable sur {playerObj.Name} (race de replication ?)" );
			return;
		}

		if ( keepHairAndBeard )
		{
			dresser.Clothing.RemoveAll( x =>
				x.Clothing != null &&
				x.Clothing.Category != Clothing.ClothingCategory.Hair &&
				x.Clothing.Category != Clothing.ClothingCategory.Facial &&
				x.Clothing.Category != Clothing.ClothingCategory.Skin
			);
		}
		else
		{
			dresser.Clothing.Clear();
		}

		foreach ( var path in paths )
		{
			var clothing = ResourceLibrary.Get<Clothing>( path );
			if ( clothing == null ) continue;
			dresser.Clothing.RemoveAll( x => x.Clothing?.Category == clothing.Category );
			dresser.Clothing.Add( new ClothingContainer.ClothingEntry { Clothing = clothing } );
		}

		// Met a jour SavedClothingJson avec le dresser complet — host-only car
		// le champ est [Sync(SyncFlags.FromHost)]. Sans ce guard, les clients
		// tenteraient l'ecriture en vain et le state synced ne refleterait
		// pas le dresser final (les late-joiners verraient un dresser obsolete).
		if ( Networking.IsHost )
		{
			var client = playerObj.Components.Get<PlayerPawn>( FindMode.EverythingInSelfAndChildren )?.Client;
			if ( client != null )
			{
				var allPaths = dresser.Clothing
					.Where( x => x.Clothing != null )
					.Select( x => x.Clothing.ResourcePath )
					.ToList();
				client.SavedClothingJson = System.Text.Json.JsonSerializer.Serialize( allPaths );
			}
		}

		_ = ApplyDresser( dresser, playerBody, skinOverride: skinOverride, headOverride: headOverride, morphsOverride: morphsOverride, modelOverride: modelOverride );
	}

	/// <summary>
	/// Force le MaterialGroup, le head body group et les morphs directement sur le
	/// PlayerBody, sans passer par RestoreAppearance qui lit Client.SavedSkinGroup.
	/// Nécessaire pour le respawn : sur les clients proxies, pawn.Client peut ne pas
	/// être encore synchronisé au moment où on tente de restaurer le skin, et
	/// RestoreAppearance retombe alors sur "default" et écrase la bonne valeur.
	///
	/// Si modelPath est renseigné, le modèle du Renderer est aussi remplacé — sans ça,
	/// le clone de prefab au respawn garde le modèle masculin par défaut et un perso
	/// féminin respawn en homme.
	/// </summary>
	[Rpc.Broadcast]
	public static void BroadcastApplySkin( GameObject playerObj, string skinGroup, int headIndex, string morphsJson, string modelPath = "" )
	{
		if ( !playerObj.IsValid() )
		{
			Log.Warning( "[AppearanceSync][BCAST] BroadcastApplySkin recu mais playerObj invalide" );
			return;
		}

		var side = Networking.IsHost ? "HOST" : "CLIENT";
		Log.Info( $"[AppearanceSync][{side}] BroadcastApplySkin recu: pawn={playerObj.Name}, skin={skinGroup}, head={headIndex}, model={modelPath}" );

		var playerBody = playerObj.Components.Get<PlayerBody>( FindMode.EverythingInSelfAndChildren );
		if ( playerBody == null )
		{
			Log.Warning( $"[AppearanceSync][{side}] BroadcastApplySkin: PlayerBody introuvable sur {playerObj.Name}" );
			return;
		}

		// Restaure le modèle (male/female) avant le skin : sinon le MaterialGroup
		// serait appliqué sur le mauvais mesh et les morphs seraient perdus au swap.
		if ( !string.IsNullOrEmpty( modelPath ) && playerBody.Renderer.IsValid() )
		{
			var model = ResourceLibrary.Get<Model>( modelPath );
			if ( model != null && playerBody.Renderer.Model != model )
				playerBody.Renderer.Model = model;
		}

		playerBody.RestoreAppearance( skinGroup, headIndex, morphsJson );

		// Propage aussi sur le BodyTarget du Dresser et ses enfants skin (underwear, etc.)
		var dresser = playerObj.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		if ( dresser != null )
			OpenFramework.Inventory.ClothingEquipment.RestoreSkinOnDresser( dresser, playerBody );
	}

	[Rpc.Broadcast]
	public static void RestorePersonalClothing( GameObject playerObj )
	{
		if ( !playerObj.IsValid() ) return;

		var pawn = playerObj.Components.Get<PlayerPawn>( FindMode.EverythingInSelfAndChildren );
		var client = pawn?.Client;
		if ( client == null ) return;

		if ( string.IsNullOrEmpty( client.SavedPersonalClothingJson ) || client.SavedPersonalClothingJson == "[]" ) return;

		// Mutation des champs [Sync(SyncFlags.FromHost)] reservee au host : sans
		// ce guard, l'ecriture echouerait silencieusement sur les clients (sans
		// FromHost l'autorite n'est pas chez le client) et le state synced ne
		// se mettrait jamais a jour pour les late-joiners.
		if ( Networking.IsHost )
			client.SavedClothingJson = client.SavedPersonalClothingJson;

		var paths = System.Text.Json.JsonSerializer.Deserialize<List<string>>( client.SavedPersonalClothingJson );
		BroadcastEquipList( playerObj, paths, false );
	}

	////

	// ---------------------------
	//  NOTIFICATIONS / FX
	// ---------------------------

	// This runs on CLIENT (no #server here)
	public void Notify( NotificationType type, string message )
	{
		using ( Rpc.FilterInclude( Connection ) )
		{
			NotificationSystem.Notify( type, message );
		}
	}

	//[Rpc.Broadcast] public static void AttachATMMenu( Atm atm ) { var panel = new AtmMenu { ParentAtm = atm }; var rootpanel = Game.ActiveScene.GetComponentInChildren<PanelComponent>()?.Panel; if ( rootpanel != null ) rootpanel.AddChild( panel ); }
	[Rpc.Owner] public void PlaySound( string soundName ) { var resource = ResourceLibrary.Get<SoundEvent>( soundName ); if ( resource == null ) return; if ( Local?.PlayerPawn == null ) return; var handle = Sound.Play( resource, Local.PlayerPawn.WorldPosition ); }
	[Rpc.Owner] public void PlaySound( SoundEvent resource ) { if ( Local?.PlayerPawn == null ) return; var handle = Sound.Play( resource, Local.PlayerPawn.WorldPosition ); }
	[Rpc.Owner] public void ScreenEffect( GameObject effect ) { if ( effect == null || PlayerPawn == null ) return; var obj = effect.Clone(); obj.Parent = PlayerPawn.GameObject; }

	// ══════════════════════════════════════════════════════════════
	//  COIFFEUR — apparence cheveux/barbe (couleur + style memorise)
	// ══════════════════════════════════════════════════════════════

	/// <summary>
	/// RPC envoyee depuis le CoiffeurPanelComponent quand le joueur valide.
	/// - Cote HOST : ecrit les Saved* (replication automatique aux autres clients via SyncFlags.FromHost)
	///               + persiste en API via UpdateCharacterAppearance.
	/// - Cote TOUS LES CLIENTS : applique immediatement la teinte sur les renderers cheveux/barbe.
	///
	/// Le re-equipement effectif d'une nouvelle coupe / barbe passe par le systeme
	/// d'inventaire normal (Loadout / ClothingEquipment) AVANT cet appel — ici on ne
	/// touche qu'a la couleur et a la "memoire" du style cote API.
	/// </summary>
	[Rpc.Broadcast]
	public static async void BroadcastHairColor( GameObject playerObj, string hairHex, string beardHex, string hairStylePath, string beardStylePath )
	{
		if ( !playerObj.IsValid() ) return;

		var side = Networking.IsHost ? "HOST" : "CLIENT";
		if ( PlayerBody.DebugHairLogs )
			Log.Info( $"[HairSystem][{side}] BroadcastHairColor recu: pawn={playerObj.Name} hair={hairHex} beard={beardHex} hairStyle={hairStylePath} beardStyle={beardStylePath}" );

		// HOST : persiste sur le Client synced (replication aux proxies)
		// + appelle l'API pour persister cote backend.
		if ( Networking.IsHost )
		{
			var pawn = playerObj.Components.Get<PlayerPawn>();
			var client = pawn?.Client;
			if ( client != null )
			{
				client.SavedHairColor = string.IsNullOrWhiteSpace( hairHex ) ? "#3a2a1c" : hairHex;
				client.SavedBeardColor = string.IsNullOrWhiteSpace( beardHex ) ? "#3a2a1c" : beardHex;
				client.SavedHairStyle = hairStylePath ?? "";
				client.SavedBeardStyle = beardStylePath ?? "";

				if ( PlayerBody.DebugHairLogs )
					Log.Info( $"[HairSystem][HOST] Saved* mis a jour pour {client.DisplayName}, persistance API en cours..." );

				// Persiste en API. Le character actif est connu cote bridge — on
				// l'utilise pour cibler le bon character du joueur.
				var characterId = OpenFramework.PlayerApiBridge.GetActiveCharacter( client.SteamId );
				if ( !string.IsNullOrEmpty( characterId ) && OpenFramework.Api.ApiComponent.Instance != null )
				{
					try
					{
						await OpenFramework.Api.ApiComponent.Instance.UpdateCharacterAppearance(
							client.SteamId, characterId,
							new OpenFramework.Api.CharacterAppearanceUpdateDto
							{
								HairColor = client.SavedHairColor,
								BeardColor = client.SavedBeardColor,
								HairStyle = client.SavedHairStyle,
								BeardStyle = client.SavedBeardStyle,
							} );
						if ( PlayerBody.DebugHairLogs )
							Log.Info( $"[HairSystem][HOST] API update OK pour character {characterId}" );
					}
					catch ( Exception e )
					{
						Log.Warning( $"[HairSystem][HOST] API update FAIL pour character {characterId}: {e.Message}" );
					}
				}
				else if ( PlayerBody.DebugHairLogs )
				{
					Log.Warning( $"[HairSystem][HOST] characterId actif introuvable pour steam={client.SteamId} — skip API persistance" );
				}
			}
		}

		// Tous les clients : applique la teinte tout de suite (avant que la sync
		// ne propage les Saved* — la sync rejouera ApplyHairColor une seconde
		// fois via ApplyAppearanceFromSync, c'est idempotent).
		var body = playerObj.Components.Get<PlayerBody>( FindMode.EverythingInSelfAndChildren );
		body?.ApplyHairColor( hairHex, beardHex );
	}

	/// <summary>
	/// Pousse couleur de peau / tete / morphs vers l'API pour le character actif
	/// du joueur. Idempotent et silencieux : echec API = log warning, pas d'exception
	/// remontee au caller (sinon ca casserait le RPC).
	/// </summary>
	private static async System.Threading.Tasks.Task PushBodyAppearanceToApi( Client client, string skinGroup, int headIndex, string morphsJson )
	{
		if ( client == null || !Networking.IsHost ) return;

		var characterId = OpenFramework.PlayerApiBridge.GetActiveCharacter( client.SteamId );
		if ( string.IsNullOrEmpty( characterId ) || OpenFramework.Api.ApiComponent.Instance == null )
		{
			Log.Warning( $"[AppearanceSync][HOST] PushBodyAppearanceToApi {client.DisplayName}: characterId/API indisponible — skip" );
			return;
		}

		var color = (skinGroup == "skin_light") ? OpenFramework.Api.ColorBody.Light : OpenFramework.Api.ColorBody.Dark;
		var dto = new OpenFramework.Api.CharacterAppearanceUpdateDto
		{
			HairColor = client.SavedHairColor,
			BeardColor = client.SavedBeardColor,
			HairStyle = client.SavedHairStyle,
			BeardStyle = client.SavedBeardStyle,
			ColorBody = color,
			HeadIndex = headIndex,
		};

		// Decoupe les morphs JSON en champs nullable du DTO. Cle absente => null
		// (l'API ne touchera pas la valeur existante).
		if ( !string.IsNullOrEmpty( morphsJson ) && morphsJson != "{}" )
		{
			try
			{
				var morphs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, float>>( morphsJson );
				if ( morphs != null )
				{
					float? Avg( string l, string r )
					{
						bool hasL = morphs.TryGetValue( l, out var vl );
						bool hasR = morphs.TryGetValue( r, out var vr );
						if ( !hasL && !hasR ) return null;
						if ( hasL && hasR ) return (vl + vr) / 2f;
						return hasL ? vl : vr;
					}
					float? Get( string k ) => morphs.TryGetValue( k, out var v ) ? v : (float?)null;

					dto.BrowDown = Avg( "BrowDown_L", "BrowDown_R" );
					dto.BrowInnerUp = Get( "BrowInnerUp" );
					dto.BrowOuterUp = Avg( "BrowOuterUp_L", "BrowOuterUp_R" );
					dto.EyesLookDown = Avg( "EyeLookDown_L", "EyeLookDown_R" );
					dto.EyesLookIn = Avg( "EyeLookIn_L", "EyeLookIn_R" );
					dto.EyesLookOut = Avg( "EyeLookOut_L", "EyeLookOut_R" );
					dto.EyesLookUp = Avg( "EyeLookUp_L", "EyeLookUp_R" );
					dto.EyesSquint = Avg( "EyeSquint_L", "EyeSquint_R" );
					dto.EyesWide = Avg( "EyeWide_L", "EyeWide_R" );
					dto.CheekPuff = Get( "CheekPuff" );
					dto.CheekSquint = Avg( "CheekSquint_L", "CheekSquint_R" );
					dto.NoseSneer = Avg( "NoseSneer_L", "NoseSneer_R" );
					dto.JawForward = Get( "JawForward" );
					dto.JawLeft = Get( "JawLeft" );
					dto.JawRight = Get( "JawRight" );
					dto.MouthDimple = Avg( "MouthDimple_L", "MouthDimple_R" );
					dto.MouthRollUpper = Get( "MouthRollUpper" );
					dto.MouthStretch = Avg( "MouthStretch_L", "MouthStretch_R" );
				}
			}
			catch ( Exception e )
			{
				Log.Warning( $"[AppearanceSync][HOST] PushBodyAppearanceToApi {client.DisplayName}: parse morphs FAIL — {e.Message}" );
			}
		}

		if ( OpenFramework.Systems.Pawn.PlayerBody.DebugMorphLogs )
		{
			var summary = $"BD={dto.BrowDown}, BIU={dto.BrowInnerUp}, BOU={dto.BrowOuterUp}, " +
			              $"ELD={dto.EyesLookDown}, ELI={dto.EyesLookIn}, ELO={dto.EyesLookOut}, ELU={dto.EyesLookUp}, " +
			              $"ESq={dto.EyesSquint}, EWd={dto.EyesWide}, CkP={dto.CheekPuff}, CkSq={dto.CheekSquint}, " +
			              $"Nose={dto.NoseSneer}, JF={dto.JawForward}, JL={dto.JawLeft}, JR={dto.JawRight}, " +
			              $"MD={dto.MouthDimple}, MRU={dto.MouthRollUpper}, MSt={dto.MouthStretch}";
			Log.Info( $"[Morphs][PUSH] {client.DisplayName} (char={characterId}) DTO morphs nullable: {summary}" );
			Log.Info( $"[Morphs][PUSH] {client.DisplayName} morphsJson source = {morphsJson}" );
		}

		try
		{
			await OpenFramework.Api.ApiComponent.Instance.UpdateCharacterAppearance( client.SteamId, characterId, dto );
			Log.Info( $"[AppearanceSync][HOST] PushBodyAppearanceToApi OK pour {client.DisplayName} (char={characterId}, color={color}, head={headIndex})" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[AppearanceSync][HOST] PushBodyAppearanceToApi FAIL pour {client.DisplayName} (char={characterId}): {e.Message}" );
		}
	}
}
