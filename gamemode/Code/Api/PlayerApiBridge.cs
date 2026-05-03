using Sandbox;
using OpenFramework.Api;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Facepunch;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using OpenFramework.Systems.AtmSystem;
using OpenFramework.Systems.Jobs;
using OpenFramework.Systems.Pawn;

namespace OpenFramework;

public class PlayerApiBridge : Component
{
    public static PlayerApiBridge Local { get; set; }

    public static Action<List<CharacterApi>> OnCharactersReceived { get; set; }
    public static Action<CharacterApi>       OnCharacterCreated   { get; set; }
    public static Action<string> OnCharacterSelected { get; set; }
    public static Action                     OnAuthFailed         { get; set; }

    // ResourcePath des modeles citizen male/female references dans player.prefab
    // et le character creator. Centralises ici pour eviter toute divergence entre
    // hydratation host (HydrateAppearanceFromApi) et code client (CharacterManager).
    public const string ModelPathMale   = "models/citizen_human/citizen_human_male.vmdl";
    public const string ModelPathFemale = "models/citizen_human/citizen_human_female.vmdl";

    /// <summary>
    /// Remplit les champs [Sync] d'apparence du Client a partir des donnees du
    /// CharacterApi. A appeler cote HOST AVANT SpawnPawn lors d'une selection ou
    /// d'une creation de personnage. Garantit que le pawn est spawne avec la
    /// bonne couleur, le bon sexe, le bon modele et les bons morphs des le
    /// premier frame, sans dependre d'un round-trip client (Manager.ApplyToPlayer).
    ///
    /// Sans cet hydrate, le pawn spawn avec les valeurs par defaut
    /// (SavedSkinGroup="default", SavedIsFemale=false, SavedModelPath="",
    /// HasCustomizedAppearance=false) et le seul rattrapage repose sur le
    /// callback OnCharacterSelected cote client, qui peut echouer en serveur
    /// dedie ou si le CharacterManager n'est pas pret => perso revient en
    /// "femme noire" / sans vetements au respawn.
    ///
    /// Ne touche pas a SavedClothingJson : les vetements equipes sont restaures
    /// par InventoryApiSystem.LoadPlayerInventoryAsync apres le spawn.
    /// </summary>
    public static void HydrateAppearanceFromApi( Client client, CharacterApi character )
    {
        if ( !Networking.IsHost )
        {
            Log.Warning( "[AppearanceSync][HOST] HydrateAppearanceFromApi appele HORS HOST — ignore." );
            return;
        }
        if ( client == null || character == null )
        {
            Log.Warning( $"[AppearanceSync][HOST] HydrateAppearanceFromApi: client={client != null}, character={character != null} — abort." );
            return;
        }

        var isFemale = character.Gender == Gender.Female;
        // Mapping ColorBody -> SkinTone partage avec MainMenuPanel.ApplyCharacterToPreview.
        // ColorBody.Dark : MaterialGroup "skin_dark" + head body group 0
        // ColorBody.Light : MaterialGroup "skin_light" + head body group 1
        var skinGroup = character.ColorBody == ColorBody.Dark ? "skin_dark" : "skin_light";
        var headIndex = character.ColorBody == ColorBody.Dark ? 0 : 1;

        client.SavedIsFemale  = isFemale;
        client.SavedSkinGroup = skinGroup;
        client.SavedHeadIndex = headIndex;
        client.SavedModelPath = isFemale ? ModelPathFemale : ModelPathMale;
        client.SavedMorphsJson = SerializeMorphsForBody( character );

        if ( OpenFramework.Systems.Pawn.PlayerBody.DebugMorphLogs )
        {
            // Resume des morphs bruts cote API : utile pour savoir si l'API renvoie
            // bien des valeurs non-nulles ou si le Push n'a pas persiste correctement.
            var apiSummary = $"Brow={character.BrowDown:F2}/{character.BrowInnerUp:F2}/{character.BrowOuterUp:F2}, " +
                             $"Eye=LD{character.EyesLookDown:F2}/LI{character.EyesLookIn:F2}/LO{character.EyesLookOut:F2}/LU{character.EyesLookUp:F2}/Sq{character.EyesSquint:F2}/Wd{character.EyesWide:F2}, " +
                             $"Cheek={character.CheekPuff:F2}/{character.CheekSquint:F2}, Nose={character.NoseSneer:F2}, " +
                             $"Jaw={character.JawForward:F2}/{character.JawLeft:F2}/{character.JawRight:F2}, " +
                             $"Mouth=D{character.MouthDimple:F2}/RU{character.MouthRollUpper:F2}/St{character.MouthStretch:F2}";
            Log.Info( $"[Morphs][HYDRATE] {client.DisplayName} (charId={character.Id}) API morphs: {apiSummary}" );
            Log.Info( $"[Morphs][HYDRATE] {client.DisplayName} -> SavedMorphsJson length={client.SavedMorphsJson?.Length ?? 0}, json={client.SavedMorphsJson}" );
        }
        // Active la branche de Spawn() qui broadcast skin + clothing : sans ca,
        // les clients voient le pawn avec le MaterialGroup et le modele par
        // defaut du prefab (toujours masculin).
        client.HasCustomizedAppearance = true;

        // Apparence "coiffure" — fallback aux defauts si l'API retourne du vide
        // (anciens characters avant la migration AddHairAndBeardAppearance).
        client.SavedHairColor  = string.IsNullOrWhiteSpace( character.HairColor ) ? "#3a2a1c" : character.HairColor;
        client.SavedBeardColor = string.IsNullOrWhiteSpace( character.BeardColor ) ? "#3a2a1c" : character.BeardColor;
        client.SavedHairStyle  = character.HairStyle ?? "";
        client.SavedBeardStyle = character.BeardStyle ?? "";

        Log.Info( $"[AppearanceSync][HOST] HydrateAppearanceFromApi {client.DisplayName} (steam={client.SteamId}, charId={character.Id}): apiGender={character.Gender}, apiColor={character.ColorBody} -> SavedIsFemale={isFemale}, SavedSkinGroup={skinGroup}, SavedHeadIndex={headIndex}, SavedModelPath={client.SavedModelPath}, HasCustomizedAppearance=true, hair={client.SavedHairColor}, beard={client.SavedBeardColor}" );
    }

    /// <summary>
    /// Construit le JSON des morphs faciaux au format attendu par PlayerBody.RestoreAppearance
    /// (clefs "BrowDown_L" etc, pas les clefs API agregees comme "BrowDown"). Symetrise les
    /// morphs L/R que l'API ne stocke qu'en valeur unique : c'est le meme contrat que
    /// MainMenuPanel.ApplyCharacterToPreview.
    /// </summary>
    private static string SerializeMorphsForBody( CharacterApi c )
    {
        var morphs = new Dictionary<string, float>
        {
            [ "BrowDown_L" ]    = c.BrowDown,
            [ "BrowDown_R" ]    = c.BrowDown,
            [ "BrowInnerUp" ]   = c.BrowInnerUp,
            [ "BrowOuterUp_L" ] = c.BrowOuterUp,
            [ "BrowOuterUp_R" ] = c.BrowOuterUp,
            [ "EyeLookDown_L" ] = c.EyesLookDown,
            [ "EyeLookDown_R" ] = c.EyesLookDown,
            [ "EyeLookIn_L" ]   = c.EyesLookIn,
            [ "EyeLookIn_R" ]   = c.EyesLookIn,
            [ "EyeLookOut_L" ]  = c.EyesLookOut,
            [ "EyeLookOut_R" ]  = c.EyesLookOut,
            [ "EyeLookUp_L" ]   = c.EyesLookUp,
            [ "EyeLookUp_R" ]   = c.EyesLookUp,
            [ "EyeSquint_L" ]   = c.EyesSquint,
            [ "EyeSquint_R" ]   = c.EyesSquint,
            [ "EyeWide_L" ]     = c.EyesWide,
            [ "EyeWide_R" ]     = c.EyesWide,
            [ "CheekPuff" ]     = c.CheekPuff,
            [ "CheekSquint_L" ] = c.CheekSquint,
            [ "CheekSquint_R" ] = c.CheekSquint,
            [ "NoseSneer_L" ]   = c.NoseSneer,
            [ "NoseSneer_R" ]   = c.NoseSneer,
            [ "JawForward" ]    = c.JawForward,
            [ "JawLeft" ]       = c.JawLeft,
            [ "JawRight" ]      = c.JawRight,
            [ "MouthDimple_L" ] = c.MouthDimple,
            [ "MouthDimple_R" ] = c.MouthDimple,
            [ "MouthRollUpper" ]= c.MouthRollUpper,
            [ "MouthStretch_L" ]= c.MouthStretch,
            [ "MouthStretch_R" ]= c.MouthStretch,
        };
        return JsonSerializer.Serialize( morphs );
    }

    protected override void OnAwake()   { TrySetLocal(); }
    protected override void OnStart()   { TrySetLocal(); }
    protected override void OnEnabled() { TrySetLocal(); }
    protected override void OnDestroy() { if ( Local == this ) Local = null; }

    private void TrySetLocal()
    {
        if ( Network.IsOwner && Local != this )
        {
            Log.Info( $"[Bridge] TrySetLocal → Local set (IsOwner: {Network.IsOwner}, was: {Local != null})" );
            Local = this;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  AUTH
    // ══════════════════════════════════════════════════════════════

    public void StartAuthentication()
    {
        if ( !Networking.IsHost ) return;
        Log.Info( $"[Bridge] StartAuthentication called (IsHost: {Networking.IsHost})" );
        AskClientForSteamToken();
    }
    
    
    private static readonly Dictionary<ulong, string> _activeCharacters = new();
 
    public static string GetActiveCharacter( ulong steamId )
	    => _activeCharacters.TryGetValue( steamId, out var id ) ? id : null;
 
// Dans RequestSelectCharacter(), après SetActiveCharacter, ajoute :
//   _activeCharacters[caller.SteamId] = characterId;
 
// La méthode complète devient :

    
    [Rpc.Owner]
    private async void AskClientForSteamToken()
    {
        Log.Info( $"[Bridge] AskClientForSteamToken → requesting token..." );

        // Steam throttle parfois la creation de ticket juste apres le launch du jeu.
        // On retry avec backoff exponentiel: 1s, 2s, 4s (total ~7s).
        string token = null;
        var delays = new[] { 1f, 2f, 4f };
        for ( int attempt = 0; attempt <= delays.Length; attempt++ )
        {
            token = await Sandbox.Services.Auth.GetToken( "OpenFramework" );
            if ( !string.IsNullOrEmpty( token ) ) break;

            if ( attempt < delays.Length )
            {
                Log.Warning( $"[Bridge] Token vide (tentative {attempt + 1}/{delays.Length + 1}), retry dans {delays[attempt]}s..." );
                await Task.DelayRealtimeSeconds( delays[attempt] );
            }
        }

        if ( string.IsNullOrEmpty( token ) )
        {
            if ( ApiComponent.DevBypass )
            {
                Log.Warning( "[Bridge] Token null mais DevBypass actif — auth sans token Facepunch." );
                token = "";
            }
            else
            {
                Log.Error( "[Bridge] Token null apres tous les retry - abandon auth" );
                return;
            }
        }
        Log.Info( $"[Bridge] AskClientForSteamToken → token received, sending to server" );
        SendSteamTokenToServer( token );
    }

    [Rpc.Host]
    private async void SendSteamTokenToServer( string steamToken )
    {
        var steamId = Rpc.Caller.SteamId;
        var caller = Rpc.Caller;
        Log.Info( $"[Bridge] SendSteamTokenToServer → authenticating {steamId}..." );
        var ok      = await ApiComponent.Instance.AuthenticateWithSteamToken( steamId, steamToken );
        if ( !ok ) { Log.Error( $"[Bridge] Auth FAILED for {steamId}" ); NotifyAuthFailed(); return; }
        Log.Info( $"[Bridge] Auth OK for {steamId}, fetching inventory..." );
        var items = await ApiComponent.Instance.GetInventory( steamId ) ?? new List<InventoryItemDto>();

        if ( items.Count > 0 )
        {
	        var client = Scene.GetAllComponents<Client>().FirstOrDefault( c => c.Connection == caller );
	        if ( client?.IsValid() == true && client.PlayerPawn != null )
	        {
		        var container = client.PlayerPawn.GameObject.Components
			        .Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );

		        if ( container != null )
		        {
			        foreach ( var item in items )
				        InventoryContainer.Add( container, item.ItemGameId, item.Count );

			        Log.Info( $"[Bridge] {items.Count} item(s) restauré(s) pour {steamId} ✓" );
		        }
		        else
		        {
			        Log.Info( "pas cool" );
		        }
	        }
        }
        Log.Info( $"[Bridge] Fetching characters for {steamId}..." );
        var characters = await ApiComponent.Instance.GetCharacters( steamId ) ?? new List<CharacterApi>();
        Log.Info( $"[Bridge] Got {characters.Count} character(s) for {steamId} from API" );
        ReceiveCharacters( JsonSerializer.Serialize( characters ) );
    }

    public static void FetchInventory( ) => Local?.RequestInventory(  );

    [Rpc.Host]
    private async void RequestInventory()
    {
	    var steamId = Rpc.Caller.SteamId;
	    var items   = await ApiComponent.Instance.GetInventory( steamId ) 
	                  ?? new List<InventoryItemDto>();
	    var json    = JsonSerializer.Serialize( items );

	    using ( Rpc.FilterInclude( Rpc.Caller ) )
		    ReceiveInventory( json );
    }

    [Rpc.Owner]
    private void ReceiveInventory( string json )
    {
	    var items = JsonSerializer.Deserialize<List<InventoryItemDto>>(
		                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true } )
	                ?? new();
	    OnInventoryReceived?.Invoke( items );
    }

    public static Action<List<InventoryItemDto>> OnInventoryReceived { get; set; }

// SAVE INVENTORY
    public static void SaveInventory( List<InventoryItemDto> items )
	    => Local?.RequestSaveInventory( JsonSerializer.Serialize( items ) );

    // ══════════════════════════════════════════════════════════════
    //  AMENDES
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Charge les amendes du personnage depuis l'API et les injecte dans Client.Data.Fines.
    /// Appele cote HOST apres le spawn du pawn.
    /// </summary>
    private static async Task LoadFinesAsync( Client client, string characterId )
    {
        var dtos = await ApiComponent.Instance.GetFines( characterId );
        if ( dtos == null || client?.Data == null ) return;

        client.Data.Fines.Clear();
        foreach ( var dto in dtos )
        {
            client.Data.Fines.Add( new OpenFramework.Models.Fine
            {
                Id       = dto.Id,
                IssuedAt = dto.IssuedAt,
                DueAt    = dto.DueAt,
                Amount   = dto.Amount,
                Reason   = dto.Reason ?? "",
                Paid     = dto.Paid,
                PaidAt   = dto.PaidAt ?? default,
            } );
        }
        Log.Info( $"[Fines] {dtos.Count} amende(s) chargee(s) pour {client.DisplayName} (charId={characterId})" );
    }

    /// <summary>
    /// Demande au serveur de marquer une amende comme payee.
    /// Appele depuis le client (bouton Payer).
    /// </summary>
    public static void PayFine( string fineId ) => Local?.RequestPayFine( fineId );

    [Rpc.Host]
    private async void RequestPayFine( string fineId )
    {
        var caller = Rpc.Caller.GetClient();
        if ( caller?.Data == null ) return;

        var idx = -1;
        for ( int i = 0; i < caller.Data.Fines.Count; i++ )
        {
            if ( caller.Data.Fines[i].Id == fineId ) { idx = i; break; }
        }
        if ( idx < 0 ) return;

        var fine = caller.Data.Fines[idx];
        if ( fine.Paid ) return;

        if ( !MoneySystem.CanAfford( caller, fine.Amount ) )
        {
            caller.Notify( Facepunch.NotificationSystem.NotificationType.Error,
                $"Vous n'avez pas assez d'argent liquide pour payer cette amende ({fine.Amount}$)." );
            return;
        }

        MoneySystem.Remove( caller, fine.Amount );
        fine.Paid  = true;
        fine.PaidAt = DateTime.Now;
        caller.Data.Fines[idx] = fine;

        caller.Notify( Facepunch.NotificationSystem.NotificationType.Success,
            $"Vous avez payé une amende de {fine.Amount}$ pour \"{fine.Reason}\"." );

        var characterId = GetActiveCharacter( caller.SteamId );
        if ( !string.IsNullOrEmpty( characterId ) )
            await ApiComponent.Instance.PayFine( characterId, fineId );
    }

    [Rpc.Host]
    private async void RequestSaveInventory(  string itemsJson )
    {
	    var steamId = Rpc.Caller.SteamId;
	    var items   = JsonSerializer.Deserialize<List<InventoryItemDto>>(
		    itemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );

	    // Clear puis re-add chaque item
	    await ApiComponent.Instance.ClearInventory( steamId);
	    foreach ( var item in items ?? new() )
		    await ApiComponent.Instance.AddInventoryItem( steamId, item );
	    
    }

    // ══════════════════════════════════════════════════════════════
    //  FETCH CHARACTERS
    // ══════════════════════════════════════════════════════════════

    public static void FetchCharacters()
    {
        Log.Info( $"[Bridge] FetchCharacters called (Local: {Local != null})" );
        Local?.RequestCharacters();
    }

    [Rpc.Host]
    private async void RequestCharacters()
    {
        var steamId    = Rpc.Caller.SteamId;
        Log.Info( $"[Bridge] RequestCharacters → fetching for {steamId}..." );
        var characters = await ApiComponent.Instance.GetCharacters( steamId ) ?? new List<CharacterApi>();
        Log.Info( $"[Bridge] RequestCharacters → got {characters.Count} character(s) for {steamId}" );
        ReceiveCharacters( JsonSerializer.Serialize( characters ) );
    }
    
    
    [Rpc.Owner]
    private void ReceiveCharacters( string json )
    {
        var list = JsonSerializer.Deserialize<List<CharacterApi>>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true } )
            ?? new List<CharacterApi>();
        Log.Info( $"[Bridge] ReceiveCharacters → {list.Count} character(s) received on client (callback set: {OnCharactersReceived != null})" );
        foreach ( var c in list )
            Log.Info( $"[Bridge]   - {c.Id} : {c.FirstName} {c.LastName}" );
        OnCharactersReceived?.Invoke( list );
    }

    // ══════════════════════════════════════════════════════════════
    //  CREATE CHARACTER
    // ══════════════════════════════════════════════════════════════

    public static void CreateCharacter( CharacterCreationDto dto )
    {
        Log.Info( $"[Bridge] CreateCharacter called (Local: {Local != null})" );
        Local?.RequestCreateCharacter( JsonSerializer.Serialize( dto ) );
    }

    [Rpc.Host]
    private async void RequestCreateCharacter( string dtoJson )
    {
        var steamId = Rpc.Caller.SteamId;
        var caller = Rpc.Caller;
        Log.Info( $"[Bridge] RequestCreateCharacter for {steamId}" );
        var dto     = JsonSerializer.Deserialize<CharacterCreationDto>(
            dtoJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );

        if ( dto == null ) { Log.Error( "[Bridge] DTO invalide !" ); return; }

        var created = await ApiComponent.Instance.CreateCharacter( steamId, dto );
        if ( created == null ) { Log.Error( $"[Bridge] Création échouée pour {steamId} !" ); return; }

        Log.Info( $"[Bridge] Personnage {created.Id} ({created.FirstName} {created.LastName}) créé pour {steamId} ✓" );

        // Selectionne le nouveau character cote API AVANT le spawn.
        // Sans ca, /characters/actual/inventory/get renvoie l'inventaire de
        // l'ancien character actif => le nouveau character spawn avec les
        // items du precedent.
        await ApiComponent.Instance.SetActiveCharacter( steamId, created.Id );
        Log.Info( $"[Bridge] SetActiveCharacter API pour {steamId} -> {created.Id} (nouveau character, inventaire vide attendu)" );

        // Spawn le pawn si il n'existe pas encore + met à jour le nom RP
        var client = Scene.GetAllComponents<Client>().FirstOrDefault( c => c.Connection == caller );
        if ( client != null )
        {
            if ( client.Data != null )
            {
                client.Data.FirstName = created.FirstName;
                client.Data.LastName = created.LastName;
            }
            // Hydrate l'apparence host-side AVANT SpawnPawn pour que Spawn() broadcast
            // immediatement la bonne couleur/sexe/modele a tous les clients (cf doc
            // de HydrateAppearanceFromApi). Sans ca, le pawn apparait en homme/skin
            // par defaut tant que le client n'a pas appele Manager.ApplyToPlayer().
            HydrateAppearanceFromApi( client, created );
            client.SpawnPawn();
            _activeCharacters[steamId] = created.Id;
            Log.Info( $"[Bridge] Pawn ready after create for {steamId}, active character: {created.Id} ({created.FirstName} {created.LastName})" );
        }

        // Charge les amendes (nouveau perso = liste vide, mais cohérence du flux)
        _ = LoadFinesAsync( client, created.Id );

        // Notifie le client + renvoie la liste fraîche
        NotifyCharacterCreated( JsonSerializer.Serialize( created ) );
        var characters = await ApiComponent.Instance.GetCharacters( steamId ) ?? new List<CharacterApi>();
        Log.Info( $"[Bridge] After create → {characters.Count} character(s) total for {steamId}" );
        ReceiveCharacters( JsonSerializer.Serialize( characters ) );
    }

    [Rpc.Owner]
    private void NotifyCharacterCreated( string json )
    {
        var character = JsonSerializer.Deserialize<CharacterApi>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );
        OnCharacterCreated?.Invoke( character );
    }

    // ══════════════════════════════════════════════════════════════
    //  SELECT CHARACTER + SPAWN À LA DERNIÈRE POSITION
    // ══════════════════════════════════════════════════════════════

    public static void SelectCharacter( string characterId )
    {
        Log.Info( $"[Bridge] SelectCharacter called → {characterId} (Local: {Local != null})" );
        Local?.RequestSelectCharacter( characterId );
    }

    [Rpc.Host]
    private async void RequestSelectCharacter( string characterId )
    {
	    var caller = Rpc.Caller;
	    Log.Info( $"[Bridge] RequestSelectCharacter → {caller.SteamId} selecting {characterId}..." );
	    await ApiComponent.Instance.SetActiveCharacter( caller.SteamId, characterId );

	    // On mémorise le perso actif pour pouvoir sauvegarder la position plus tard
	    _activeCharacters[caller.SteamId] = characterId;

	    // Récupère les infos du perso pour mettre à jour le ClientData
	    var client = Scene.GetAllComponents<Client>().FirstOrDefault( c => c.Connection == caller );
	    if ( client != null )
	    {
		    // Met à jour le nom RP depuis l'API
		    var characters = await ApiComponent.Instance.GetCharacters( caller.SteamId ) ?? new List<CharacterApi>();
		    var selected = characters.FirstOrDefault( c => c.Id == characterId );
		    if ( selected != null && client.Data != null )
		    {
			    client.Data.FirstName = selected.FirstName;
			    client.Data.LastName = selected.LastName;
			    Log.Info( $"[Bridge] ClientData updated: {selected.FirstName} {selected.LastName}" );
		    }

		    // Hydrate l'apparence host-side AVANT SpawnPawn pour que Spawn() broadcast
		    // immediatement la bonne couleur/sexe/modele a tous les clients (cf doc
		    // de HydrateAppearanceFromApi). C'est ce qui corrige le bug "femme blanche
		    // devient femme noire apres reconnexion" : le host a maintenant l'etat
		    // d'apparence correct des le spawn, sans dependre du Manager du menu cote
		    // client qui pouvait echouer en serveur dedie.
		    if ( selected != null )
			    HydrateAppearanceFromApi( client, selected );
		    else
			    Log.Warning( $"[Bridge] Character {characterId} introuvable apres SetActiveCharacter pour {caller.SteamId}, apparence non hydratee !" );

		    // Spawn le pawn si il n'existe pas encore
		    client.SpawnPawn();
		    Log.Info( $"[Bridge] Pawn ready for {caller.SteamId}, PlayerPawn={client.PlayerPawn != null}" );

		    // Charge les amendes persistees depuis l'API
		    _ = LoadFinesAsync( client, characterId );
	    }
	    else
	    {
		    Log.Warning( $"[Bridge] Client not found for {caller.SteamId} !" );
	    }

	    Log.Info( $"[Bridge] Character selected: {caller.SteamId} → {characterId} ✓" );
	    NotifyCharacterReady( characterId );
    }
    [Rpc.Owner]
    private void NotifyCharacterReady( string characterId )
        => OnCharacterSelected?.Invoke( characterId );

    // ══════════════════════════════════════════════════════════════
    //  DELETE CHARACTER (admin only)
    // ══════════════════════════════════════════════════════════════

    public static void DeleteCharacter( string characterId )
    {
        Log.Info( $"[Bridge] DeleteCharacter called → {characterId} (Local: {Local != null})" );
        Local?.RequestDeleteCharacter( characterId );
    }

    [Rpc.Host]
    private async void RequestDeleteCharacter( string characterId )
    {
        var caller = Rpc.Caller;
        // .ValueUnsigned évite les implicit casts via SteamId(int) (deprecated dans le SDK).
        ulong steamId = caller.SteamId.ValueUnsigned;

        // Validation d'autorité côté host: un client non-admin ne peut pas supprimer.
        if ( !Client.IsSteamIdAdmin( steamId ) )
        {
            Log.Warning( $"[Bridge] RequestDeleteCharacter REFUSED for {steamId} (not admin)" );
            return;
        }

        Log.Info( $"[Bridge] RequestDeleteCharacter → admin {steamId} deleting {characterId}..." );
        await ApiComponent.Instance.DeleteCharacter( steamId, characterId );

        // Si c'était le perso actif du caller, on le retire de la map des actifs
        if ( _activeCharacters.TryGetValue( steamId, out var active ) && active == characterId )
            _activeCharacters.Remove( steamId );

        // On renvoie la liste rafraîchie au caller
        var characters = await ApiComponent.Instance.GetCharacters( steamId ) ?? new List<CharacterApi>();
        Log.Info( $"[Bridge] After delete → {characters.Count} character(s) remaining for {steamId}" );
        ReceiveCharacters( JsonSerializer.Serialize( characters ) );
    }

    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Récupère la dernière position du personnage depuis l'API
    /// puis téléporte le pawn local à cette position.
    /// Si aucune position n'est stockée, laisse la position par défaut.
    /// </summary>
    public static void SpawnAtLastPosition( string characterId )
        => Local?.RequestLastPosition( characterId );


    [Rpc.Host]
    private async void RequestLastPosition( string characterId )
    {
        // IMPORTANT : capturer Rpc.Caller AVANT tout await — apres await, Rpc.Caller
        // n'est plus le bon contexte. La position et le character sont retrieve avant
        // d'appeler GetClient() pour que le filtrage par Connection (cf.
        // ConnectionExtension.GetClient) s'applique sur la bonne connexion.
        var caller    = Rpc.Caller;
        var position  = await ApiComponent.Instance.GetLastPosition( caller.SteamId, characterId );
        var character = (await ApiComponent.Instance.GetCharacters( caller.SteamId ))
            ?.FirstOrDefault( c => c.Id == characterId );

        var callerClient = caller.GetClient();

        // SetJob/Notify sont best-effort : meme si l'API n'a pas retourne le character ou
        // si le client n'est pas (encore) trouve, on doit IMPERATIVEMENT envoyer
        // ReceiveSpawnPosition pour debloquer l'UI cote owner.
        if ( callerClient != null && character != null && !string.IsNullOrEmpty( character.ActualJob ) )
        {
            JobSystem.SetJob( callerClient, character.ActualJob );
            callerClient.Notify( NotificationSystem.NotificationType.Info, "Votre métié vous a été réattribué." );
        }
        else
        {
            Log.Warning( $"[Bridge] RequestLastPosition: skip SetJob (callerClient={callerClient}, character={(character == null ? "null" : character.Id)}) — UI quand meme debloquee." );
        }

        // Sérialise la position (ou Vector3.Zero si pas de position stockée)
        var pos  = position ?? Vector3.Zero;
        var json = JsonSerializer.Serialize( new { X = pos.x, Y = pos.y, Z = pos.z } );

        Log.Info( $"[Bridge] Dernière position pour {characterId} : {pos}" );
        ReceiveSpawnPosition( characterId, json );
    }

    [Rpc.Owner]
    private void ReceiveSpawnPosition( string characterId, string posJson )
    {
        var dto = JsonSerializer.Deserialize<PositionDto>(
            posJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );

        var playerManager = Game.ActiveScene.GetComponentInChildren<PlayerManagerHudComponent>();
        if ( playerManager == null ) return;

        // Récupère le pawn du joueur local
        var client = Game.ActiveScene.GetAllComponents<Client>()
            .FirstOrDefault( c => c.Connection == Connection.Local );

        if ( client?.PlayerPawn != null && dto != null )
        {
            var spawnPos = new Vector3( dto.X, dto.Y, dto.Z );

            // Si position par défaut (nouveau perso), garde la position de spawn de la carte
            if ( spawnPos != Vector3.Zero )
                client.PlayerPawn.WorldPosition = spawnPos;
        }

        // Affiche le jeu et cache le menu
        playerManager.SwitchToGame();

        Log.Info( $"[Bridge] Joueur lancé en jeu avec le personnage {characterId} ✓" );
    }

    // ══════════════════════════════════════════════════════════════
    //  AUTH FAILED
    // ══════════════════════════════════════════════════════════════

    [Rpc.Owner]
    private void NotifyAuthFailed()
    {
        Log.Error( "[Bridge] Auth échouée." );
        OnAuthFailed?.Invoke();
    }

    
}
