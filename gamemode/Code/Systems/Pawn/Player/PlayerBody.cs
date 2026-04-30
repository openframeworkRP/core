using System.Text.Json;

namespace OpenFramework.Systems.Pawn;

public partial class PlayerBody : Component
{
    [ConVar( "core_debug_appearance", Help = "Active les logs de debug RestoreAppearance/ApplyAppearance" )]
    public static bool DebugAppearanceLogs { get; set; } = false;

    [ConVar( "core_debug_morphs", Help = "Active les logs de debug pour la chaine de morphs faciaux (hydratation, sync, application)" )]
    public static bool DebugMorphLogs { get; set; } = true;

    /// <summary>
    /// Outil de diagnostic temporaire : imprime les noms reels des morphs exposes
    /// par le modele du Renderer du pawn local. A executer une fois en jeu, sortie
    /// a coller au dev pour figer le MorphCatalog (plan de refonte appearance).
    /// Sans ca, on ne peut pas savoir si "BrowDown_L" / "BrowDown" / autre chose
    /// est le vrai nom attendu par le citizen, et Renderer.Morphs.Set() echoue
    /// silencieusement comme on l'a vu dans les logs (unknownNames=30).
    /// </summary>
    [ConCmd( "core_dump_morph_names" )]
    public static void DumpMorphNames()
    {
        var localPawn = Client.Local?.PlayerPawn;
        if ( localPawn == null )
        {
            Log.Warning( "[MorphDump] Pas de PlayerPawn local — t'es spawn ?" );
            return;
        }
        var body = localPawn.Components.Get<PlayerBody>( FindMode.EverythingInSelfAndChildren );
        if ( body?.Renderer == null )
        {
            Log.Warning( "[MorphDump] Pas de PlayerBody / Renderer sur le pawn local." );
            return;
        }
        var model = body.Renderer.Model;
        var morphs = body.Renderer.Morphs;
        Log.Info( $"[MorphDump] === DEBUT ===" );
        Log.Info( $"[MorphDump] Model: {model?.ResourcePath ?? "<null>"}" );
        Log.Info( $"[MorphDump] Renderer.Morphs == null ? {morphs == null}" );
        if ( morphs == null )
        {
            Log.Info( $"[MorphDump] === FIN (Morphs accessor null) ===" );
            return;
        }
        var names = morphs.Names?.ToList() ?? new List<string>();
        Log.Info( $"[MorphDump] Total morph names exposed: {names.Count}" );
        foreach ( var n in names )
            Log.Info( $"[MorphDump]   - {n}" );
        Log.Info( $"[MorphDump] === FIN ===" );
    }

    [ConVar( "core_debug_hair", Help = "Active les logs de debug du systeme coiffeur (RPC, sync, apply tint)" )]
    public static bool DebugHairLogs { get; set; } = false;

    [Property] public SkinnedModelRenderer Renderer { get; set; }
    [Property,Sync] public SkinnedModelRenderer HandcuffModel { get; set; }
	[Property] public ModelPhysics Physics { get; set; }
    [Property] public PlayerPawn Player { get; set; }
    [Property] public GameObject FirstPersonBody { get; set; }
    [Property] public List<AnimationHelper> AnimationHelpers { get; set; } = new();

    public Vector3 DamageTakenPosition { get; set; }
    public Vector3 DamageTakenForce { get; set; }

    private bool IsFirstPerson;
    public bool IsRagdoll => Physics.Enabled;
	
	internal void SetRagdoll( bool ragdoll )
    {
        Physics.Enabled = ragdoll;
        Renderer.UseAnimGraph = !ragdoll;

        GameObject.Tags.Set( "ragdoll", ragdoll );

        if ( !ragdoll )
        {
            GameObject.LocalPosition = Vector3.Zero;
            GameObject.LocalRotation = Rotation.Identity;
			RestoreAppearance();
		}

        SetFirstPersonView( !ragdoll );

        if ( ragdoll && DamageTakenForce.LengthSquared > 0f )
            ApplyRagdollImpulses( DamageTakenPosition, DamageTakenForce );

        Transform.ClearInterpolation();
    }

    internal void ApplyRagdollImpulses( Vector3 position, Vector3 force )
    {
        if ( !Physics.IsValid() ) return;

        var bodies = GameObject.Components.GetAll<PhysicsBody>( FindMode.EverythingInSelf );
        foreach ( var body in bodies )
            body.ApplyImpulseAt( position, force );

        DamageTakenForce = Vector3.Zero;
    }

	public void ApplyAppearance( int headIndex, string skinTone, string morphsJson )
	{
		if ( !Renderer.IsValid() ) return;

		if ( DebugAppearanceLogs )
			Log.Info( $"[PlayerBody] ApplyAppearance → skin: {skinTone} | head: {headIndex}" );

		Renderer.SetBodyGroup( "head", headIndex );
		Renderer.MaterialGroup = skinTone;
		ApplyMorphs( morphsJson );
		SyncFirstPersonBodyAppearance();
	}

	public void RestoreAppearance()
	{
		// Cherche le client owner du pawn
		var client = Player?.Client;

		// Fallback : cherche via le réseau
		if ( client == null )
			client = Game.ActiveScene.GetComponents<Client>()
				.FirstOrDefault( c => c.PlayerPawn == Player );

		RestoreAppearance( client?.SavedSkinGroup, client?.SavedHeadIndex, client?.SavedMorphsJson, client != null );
	}

	/// <summary>
	/// Surcharge qui accepte les valeurs explicites au lieu de dépendre de la sync
	/// du Client sur le pawn. Evite la race condition au respawn ou sur les clients
	/// qui recoivent le RPC Dresser avant que pawn.Client (Sync FromHost) ne soit
	/// propagé : dans ce cas, l'ancienne version lisait "default" et écrasait le
	/// bon MaterialGroup deja applique.
	/// </summary>
	public void RestoreAppearance( string skinOverride, int? headOverride, string morphsOverride, bool clientFound = true )
	{
		if ( !Renderer.IsValid() ) return;

		var skin = skinOverride ?? "default";
		var head = headOverride ?? 0;
		var morphs = string.IsNullOrEmpty( morphsOverride ) ? "{}" : morphsOverride;

		// === DEBUG CLOTHING BUG ===
		if ( DebugAppearanceLogs )
		{
			Log.Info( $"[RestoreAppearance] BEFORE → MaterialGroup: {Renderer.MaterialGroup} | BodyGroups: {Renderer.BodyGroups}" );
			Log.Info( $"[RestoreAppearance] Restoring → skin: {skin} | head: {head} | clientFound: {clientFound}" );
		}

		// Restaure le skin et la tête (les body groups sont gérés par Dresser.Apply via HideBody)
		Renderer.SetBodyGroup( "Head", head );
		Renderer.SetBodyGroup( "legs", 0 );
		Renderer.MaterialGroup = skin;

		// Cache le sous-vêtement par défaut
		foreach ( var child in Renderer.GameObject.Children.ToList() )
			if ( child.Name == "Clothing - y_front_pants_white" )
				child.Enabled = false;

		if ( DebugAppearanceLogs )
			Log.Info( $"[RestoreAppearance] AFTER → MaterialGroup: {Renderer.MaterialGroup} | BodyGroups: {Renderer.BodyGroups}" );

		ApplyMorphs( morphs );
		SyncFirstPersonBodyAppearance();
	}

	/// <summary>
	/// Recopie l'apparence visuelle du Renderer principal vers le SkinnedModelRenderer
	/// du FirstPersonBody (modele male/female, MaterialGroup = couleur de peau, BodyGroups).
	/// Sans ça, le FirstPersonBody garde son modele/material par defaut du prefab et son
	/// ombre (visible en 1ere personne car le Renderer principal est exclu via le tag
	/// "viewer") ne correspond pas au personnage du joueur.
	/// </summary>
	private void SyncFirstPersonBodyAppearance()
	{
		if ( !Renderer.IsValid() || FirstPersonBody == null ) return;

		var fpRenderer = FirstPersonBody.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndChildren );
		if ( fpRenderer == null ) return;

		if ( Renderer.Model != null && fpRenderer.Model != Renderer.Model )
			fpRenderer.Model = Renderer.Model;

		fpRenderer.MaterialGroup = Renderer.MaterialGroup;
		fpRenderer.BodyGroups = Renderer.BodyGroups;
	}

	private void ApplyMorphs( string morphsJson )
    {
        var who = Player?.DisplayName ?? GameObject?.Name ?? "?";

        if ( string.IsNullOrEmpty( morphsJson ) || morphsJson == "{}" )
        {
            if ( DebugMorphLogs )
                Log.Info( $"[Morphs] {who} ApplyMorphs SKIP: jsonEmpty (json='{morphsJson ?? "<null>"}')" );
            return;
        }
        if ( Renderer == null )
        {
            if ( DebugMorphLogs )
                Log.Warning( $"[Morphs] {who} ApplyMorphs SKIP: Renderer null" );
            return;
        }
        if ( Renderer.Morphs == null )
        {
            if ( DebugMorphLogs )
                Log.Warning( $"[Morphs] {who} ApplyMorphs SKIP: Renderer.Morphs null (model={Renderer.Model?.ResourcePath ?? "<null>"}). Probablement le model n'a pas encore charge ses morphs." );
            return;
        }

        try
        {
            var morphs = JsonSerializer.Deserialize<Dictionary<string, float>>( morphsJson );
            if ( morphs == null )
            {
                if ( DebugMorphLogs )
                    Log.Warning( $"[Morphs] {who} ApplyMorphs deserialize -> null (json={morphsJson})" );
                return;
            }

            int applied = 0;
            int unknown = 0;
            int nonZero = 0;
            foreach ( var (name, value) in morphs )
            {
                var clamped = Math.Clamp( value, 0f, 1f );
                Renderer.Morphs.Set( name, clamped );
                applied++;
                if ( clamped > 0.0001f ) nonZero++;
                if ( DebugMorphLogs )
                {
                    var available = Renderer.Morphs.Names?.Contains( name ) ?? false;
                    if ( !available ) unknown++;
                }
            }

            if ( DebugMorphLogs )
                Log.Info( $"[Morphs] {who} ApplyMorphs OK: applied={applied}, nonZero={nonZero}, unknownNames={unknown}, model={Renderer.Model?.ResourcePath ?? "<null>"}" );
        }
        catch ( Exception e )
        {
            Log.Error( $"[Morphs] {who} ApplyMorphs ERREUR parsing: {e.Message} | json={morphsJson}" );
        }
    }

    /// <summary>
    /// Applique une teinte sur les renderers cheveux et barbe equipes via le Dresser.
    /// Tourne sur tous les clients (proxies + owner) — chacun parse la liste de
    /// vetements equipes (Dresser.Clothing) pour identifier les pieces de
    /// categorie Hair/Facial, puis tinte leurs renderers.
    /// Reapplique aussi apres un Apply() du dresser : voir Client.ApplyDresserFromSyncAsync.
    /// </summary>
    public void ApplyHairColor( string hairHex, string beardHex )
    {
        var dresser = GameObject.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
        if ( dresser == null )
        {
            if ( DebugHairLogs ) Log.Warning( "[HairSystem] ApplyHairColor: Dresser introuvable sur le pawn — skip" );
            return;
        }

        if ( !TryParseHexColor( hairHex, out var hairColor ) ) hairColor = Color.White;
        if ( !TryParseHexColor( beardHex, out var beardColor ) ) beardColor = Color.White;

        var side = Networking.IsHost ? "HOST" : "CLIENT";
        if ( DebugHairLogs )
            Log.Info( $"[HairSystem][{side}] ApplyHairColor: hair={hairHex} beard={beardHex} (clothingCount={dresser.Clothing.Count})" );

        foreach ( var entry in dresser.Clothing )
        {
            if ( entry?.Clothing == null ) continue;

            var category = entry.Clothing.Category;
            // Sandbox.Clothing.ClothingCategory.Facial = barbe / moustache.
            var isHair = category == Sandbox.Clothing.ClothingCategory.Hair;
            var isBeard = category == Sandbox.Clothing.ClothingCategory.Facial;
            if ( !isHair && !isBeard ) continue;

            var resourceName = entry.Clothing.ResourceName;
            var clothingObj = FindClothingObjectByName( dresser, resourceName );
            if ( !clothingObj.IsValid() )
            {
                if ( DebugHairLogs ) Log.Warning( $"[HairSystem][{side}] GameObject introuvable pour {resourceName} (Apply pas encore fini ?)" );
                continue;
            }

            var renderers = clothingObj.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren );
            var color = isHair ? hairColor : beardColor;
            foreach ( var r in renderers )
            {
                if ( !r.IsValid() ) continue;
                r.Tint = color;
            }

            if ( DebugHairLogs )
                Log.Info( $"[HairSystem][{side}]   tinted {resourceName} ({(isHair ? "hair" : "beard")}) -> {color}" );
        }
    }

    private static GameObject FindClothingObjectByName( Dresser dresser, string resourceName )
    {
        if ( string.IsNullOrEmpty( resourceName ) ) return null;
        return dresser.GameObject.GetAllObjects( true )
            .FirstOrDefault( x => x.Name.Contains( resourceName, StringComparison.OrdinalIgnoreCase ) );
    }

    /// <summary>
    /// Parse "#RRGGBB" ou "#RRGGBBAA" en Color. Tolerant a l'absence de '#'.
    /// </summary>
    public static bool TryParseHexColor( string hex, out Color color )
    {
        color = Color.White;
        if ( string.IsNullOrWhiteSpace( hex ) ) return false;
        try
        {
            color = Color.Parse( hex ) ?? Color.White;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Refresh() => SetFirstPersonView( IsFirstPerson );

    public void SetFirstPersonView( bool firstPerson )
    {
        IsFirstPerson = firstPerson;

        if ( Player.CurrentEquipment.IsValid() )
            Player.CurrentEquipment.UpdateRenderMode();

        FirstPersonBody.Enabled = IsFirstPerson;

        if ( IsFirstPerson ) SyncFirstPersonBodyAppearance();
    }

	[Rpc.Broadcast]
	public void ShowHandcuffModel( bool show )
	{
		if ( !HandcuffModel.IsValid() ) return;
		HandcuffModel.Enabled = show;
	}

	protected override void OnUpdate()
    {
        if ( !Player.IsValid() || !Player.CameraController.IsValid() ) return;

        // When in a vehicle, VehicleCameraController handles the viewer tag
        if ( Player.CurrentCar.IsValid() ) return;

        var isWatchingThisPlayer = Client.Viewer.IsValid() && Client.Viewer.Pawn == Player;
        Tags.Set( "viewer", isWatchingThisPlayer && Player.CameraController.Mode == CameraMode.FirstPerson );
    }

    internal void UpdateRotation( Rotation rotation )
    {
        WorldRotation = rotation;
        FirstPersonBody?.WorldRotation = rotation;
    }
}
