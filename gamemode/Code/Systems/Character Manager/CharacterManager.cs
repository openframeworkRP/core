using Sandbox;
using System.Threading.Tasks;
using static Sandbox.Clothing;

namespace OpenFramework;

public sealed class CharacterManager : Component
{
	[Property] public SkinnedModelRenderer MaleBody { get; set; }
	[Property] public Dresser MaleDresser { get; set; }
	[Property] public SkinnedModelRenderer FemaleBody { get; set; }
	[Property] public Dresser FemaleDresser { get; set; }

	[Property, Group( "Catalogues" )]
	public ClothingCatalogResource MaleCatalog { get; set; }

	[Property, Group( "Catalogues" )]
	public ClothingCatalogResource FemaleCatalog { get; set; }


	/// <summary>
	///  Camera
	/// </summary>
	[Property] public float CharacterRotation { get; set; } = 180f;
	[Property] public CameraComponent PreviewCamera { get; set; } 
	[Property] public float ZoomDistance { get; set; } = 150f;
	private float _currentRotation = 180f;

	[Property] public float RotationSpeed { get; set; } = 100f; // Vitesse de rotation
	public int RotationDirection { get; set; } = 0; //

	[Property] public float CameraHeight { get; set; } = 50f;

	public int ZoomDir { get; set; } = 0; // -1 zoom+, 1 zoom-
	public int HeightDir { get; set; } = 0; // 1 monte, -1 descend

	public Dictionary<string, float> FaceMorphs { get; set; } = new();
	public Dictionary<string, Color> ClothingTints { get; set; } = new();

	// Retourne automatiquement le catalogue correspondant au genre actuel
	public ClothingCatalogResource ActiveCatalog => IsFemale ? FemaleCatalog : MaleCatalog;

	private int _currentHeadIndex = 0;
	private string _currentSkinGroup = "default";

	[Property, Change( nameof( UpdateGenderFromInspector ) )]
	public bool IsFemale { get; set; } = false;

	public SkinnedModelRenderer ActiveBody => IsFemale ? FemaleBody : MaleBody;

	public PlayerPawn pawn => Client.Local.PlayerPawn;

	private Vector3 _cameraOrigin;
	private float _originZoom;
	private float _originHeight;


	protected override void OnStart()
	{
		if(MaleBody.IsValid() && FemaleBody.IsValid())
		{
			

			var model = ActiveBody.Model;
			for ( int i = 0; i < model.Parts.All.Count; i++ )
			{
				var part = model.Parts.All[i];
				Log.Info( $"Part {i}: {part.Name} = {ActiveBody.GetBodyGroup( part.Name )}" );
				ActiveBody.SetBodyGroup( "legs", 0 );
			}
		}
		


		if ( PreviewCamera.IsValid() )
		{
			PreviewCamera.Enabled = true;
			PreviewCamera.Priority = 100; // Prend le dessus sur la caméra du joueur
			_cameraOrigin = PreviewCamera.WorldPosition;
			_originZoom = ZoomDistance;
			_originHeight = CameraHeight;
		}
			

		string[] morphList = { 
        // Sourcils
        "BrowDown_L", "BrowDown_R", "BrowInnerUp", "BrowOuterUp_L", "BrowOuterUp_R", 
        // Yeux
        "EyeLookDown_L", "EyeLookDown_R", "EyeLookIn_L", "EyeLookIn_R",
		"EyeLookOut_L", "EyeLookOut_R", "EyeLookUp_L", "EyeLookUp_R",
		"EyeSquint_L", "EyeSquint_R", "EyeWide_L", "EyeWide_R",
        // Joues & Nez
        "CheekPuff", "CheekSquint_L", "CheekSquint_R", "NoseSneer_L", "NoseSneer_R",
        // M�choire & Bouche
        "JawForward", "JawLeft", "JawRight", "MouthDimple_L", "MouthDimple_R",
		"MouthRollUpper", "MouthStretch_L", "MouthStretch_R"
		};

		foreach ( var name in morphList )
		{
			FaceMorphs[name] = 0f;
		}
		//UpdateGender( IsFemale );

		if(MaleDresser.IsValid() && FemaleDresser.IsValid())
		{
			MaleDresser.Clear();
			FemaleDresser.Clear();
		}
	}

	protected override void OnUpdate()
	{
		if ( ActiveBody == null )
			return;

		if ( RotationDirection != 0 )
			CharacterRotation += RotationDirection * 100f * RealTime.Delta;

		if ( ZoomDir != 0 )
			ZoomDistance = Math.Clamp( ZoomDistance + (ZoomDir * 150f * RealTime.Delta), 40f, 250f );

		if ( HeightDir != 0 )
			CameraHeight = Math.Clamp( CameraHeight + (HeightDir * 100f * RealTime.Delta), 10f, 100f );

		_currentRotation = MathX.Lerp( _currentRotation, CharacterRotation, RealTime.Delta * 5f );
		ActiveBody.WorldRotation = Rotation.FromYaw( _currentRotation );

		if ( PreviewCamera.IsValid() )
		{
			PreviewCamera.WorldPosition = new Vector3(
				_cameraOrigin.x,
				_cameraOrigin.y + (ZoomDistance - _originZoom),  // delta depuis l'origine
				_cameraOrigin.z + (CameraHeight - _originHeight) // delta depuis l'origine
			);
		}

		if ( MaleDresser.IsValid() && FemaleDresser.IsValid() )
		{
			foreach ( var child in MaleBody.GameObject.Children.ToList() )
				if ( child.Name == "Clothing - y_front_pants_white" )
					child.Enabled = false;
			foreach ( var child in FemaleBody.GameObject.Children.ToList() )
				if ( child.Name == "Clothing - y_front_pants_white" )
					child.Enabled = false;

			var maleRenderer = MaleBody.GameObject.GetComponent<SkinnedModelRenderer>();
			var femaleRenderer = FemaleBody.GameObject.GetComponent<SkinnedModelRenderer>();

			if ( maleRenderer.IsValid() )
				maleRenderer.SetBodyGroup( "legs", 0 );
			if ( femaleRenderer.IsValid() )
				femaleRenderer.SetBodyGroup( "legs", 0 );
		}
	}


	/*
	protected override void OnUpdate()
	{
		// 1. Rotation continue (d�j� faite)
		if ( RotationDirection != 0 )
			CharacterRotation += RotationDirection * 100f * RealTime.Delta;

		// 2. Zoom continu
		if ( ZoomDir != 0 )
			ZoomDistance = Math.Clamp( ZoomDistance + (ZoomDir * 150f * RealTime.Delta), 40f, 250f );

		// 3. Hauteur continue
		if ( HeightDir != 0 )
			CameraHeight = Math.Clamp( CameraHeight + (HeightDir * 100f * RealTime.Delta), 10f, 100f );

		// Application des lissages et positions
		_currentRotation = MathX.Lerp( _currentRotation, CharacterRotation, RealTime.Delta * 5f );
		ActiveBody.WorldRotation = Rotation.FromYaw( _currentRotation );
		
		if ( PreviewCamera.IsValid() )
		{
			PreviewCamera.WorldPosition = Vector3.Forward * -ZoomDistance + Vector3.Up * CameraHeight;
		}


	}*/

	public void UpdateHeadAndSkin( int headIndex, string skinName )
	{
		// On m�morise pour plus tard
		_currentHeadIndex = headIndex;
		_currentSkinGroup = skinName;

		// IMPORTANT : refleter la couleur dans CharacterCreationState pour que le DTO
		// envoye a l'API (BuildDto.Color) corresponde au choix du joueur. Sans ca, la
		// valeur par defaut ColorBody.Dark est persistee meme si le joueur a clique
		// sur "skin_light" -> au reload le perso revient en peau noire.
		CharacterCreationState.ColorBody =
			(skinName == "skin_light") ? OpenFramework.Api.ColorBody.Light : OpenFramework.Api.ColorBody.Dark;

		// On applique imm�diatement au corps actif
		if ( ActiveBody.IsValid() )
		{
			ActiveBody.SetBodyGroup( "head", headIndex );
			ActiveBody.MaterialGroup = skinName;
		}
	}

	// Change la m�thode en "public async Task"
	public async System.Threading.Tasks.Task UpdateClothing( ClothingContainer container )
	{
		var activeDresser = IsFemale ? FemaleDresser : MaleDresser;
		if ( activeDresser == null || !ActiveBody.IsValid() ) return;

		activeDresser.Clothing = container.Clothing.ToList();

		// On applique les vêtements
		await activeDresser.Apply();

		// === DEBUG CLOTHING BUG ===
		LogBodyGroups( "AFTER Apply", ActiveBody );

		// Petit délai de sécurité pour s'assurer que le moteur a fini
		await Task.Frame();

		foreach ( var child in activeDresser.GameObject.Children )
		{
			var match = ClothingTints.FirstOrDefault( x => child.Name.Contains( x.Key, StringComparison.OrdinalIgnoreCase ) );

			if ( match.Key != null )
			{
				var renderer = child.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndChildren );
				if ( renderer.IsValid() )
				{
					renderer.Tint = match.Value;
				}
			}
		}
		// Restaure le skin et la tête (les body groups sont gérés par Dresser.Apply via HideBody)
		ActiveBody.SetBodyGroup( "legs", 0 );
		ActiveBody.MaterialGroup = _currentSkinGroup;
		ActiveBody.SetBodyGroup( "Head", _currentHeadIndex );

		// === DEBUG CLOTHING BUG ===
		LogBodyGroups( "AFTER Restore", ActiveBody );
		// Log dresser children
		foreach ( var child in (IsFemale ? FemaleDresser : MaleDresser).GameObject.Children )
		{
			var renderer = child.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndChildren );
			if ( renderer.IsValid() )
				Log.Info( $"  Child: {child.Name} | Model: {renderer.Model?.ResourcePath} | MaterialGroup: {(renderer is SkinnedModelRenderer smr ? smr.MaterialGroup : "N/A")} | Tint: {renderer.Tint}" );
		}

		ReapplyAllMorphs();
	}

	private void LogBodyGroups( string label, SkinnedModelRenderer body )
	{
		if ( body?.Model?.Parts?.All == null ) return;
		var parts = new System.Text.StringBuilder();
		parts.Append( $"[CharMgr] {label} → MaterialGroup: {body.MaterialGroup} | " );
		foreach ( var part in body.Model.Parts.All )
		{
			var val = body.GetBodyGroup( part.Name );
			parts.Append( $"{part.Name}={val}/{part.Choices?.Count ?? 0} " );
		}
		Log.Info( parts.ToString() );
	}

	public void RefreshPhysicalLook()
	{
		if ( !ActiveBody.IsValid() ) return;

		// On r�utilise tes variables m�moris�es
		ActiveBody.SetBodyGroup( "Head", _currentHeadIndex );
		ActiveBody.MaterialGroup = _currentSkinGroup;

		ReapplyAllMorphs();
	}
	private void UpdateGenderFromInspector()
	{
		UpdateGender( IsFemale );
	}
	public async void SetGender( bool female )
	{
		IsFemale = female;
		UpdateGender( IsFemale );

		// Apr�s avoir chang� de sexe, on force le rafra�chissement des v�tements
		// pour que les teintes soient appliqu�es sur le nouveau mod�le
		var activeDresser = IsFemale ? FemaleDresser : MaleDresser;
		var container = new ClothingContainer();
		foreach ( var entry in activeDresser.Clothing ) container.Clothing.Add( entry );

		await UpdateClothing( container );
	}

	public void UpdateGender( bool female )
	{
		IsFemale = female;

		// On active/d�sactive les GameObjects parents
		if ( MaleBody.IsValid() ) MaleBody.GameObject.Enabled = !IsFemale;
		if ( FemaleBody.IsValid() ) FemaleBody.GameObject.Enabled = IsFemale;

	}

	public void ShowBody() // utiliser pour le character creator uniqument !!!
	{
		if ( MaleBody.IsValid() ) MaleBody.GameObject.Enabled = !IsFemale;
	}

	public List<string> GetBodyGroupNames()
	{
		var model = ActiveBody?.Model;
		// On acc�de � Parts.All qui est la IReadOnlyList
		if ( model?.Parts?.All == null ) return new List<string>();

		return model.Parts.All.Select( x => x.Name ).ToList();
	}

	public int GetBodyGroupChoiceCount( string name )
	{
		var model = ActiveBody?.Model;
		if ( model?.Parts?.All == null ) return 0;

		// On utilise la m�thode Get(name) fournie dans ton code source
		var part = model.Parts.Get( name );

		// Choices est une liste de Model.BodyPart.Choice
		return part?.Choices?.Count ?? 0;
	}


	// --- Gestion des Morphs (Bas�e sur ton code source fourni) ---
	public string[] GetMorphNames()
	{
		// On v�rifie si l'objet est actif avant de lire les Morphs
		if ( ActiveBody == null || !ActiveBody.GameObject.Enabled )
			return System.Array.Empty<string>();

		return ActiveBody.Morphs?.Names.ToArray() ?? System.Array.Empty<string>();
	}

	public float GetMorphValue( string name )
	{
		// On utilise la m�thode .Get(name) du MorphAccessor
		return ActiveBody?.Morphs?.Get( name ) ?? 0f;
	}

	public float GetMorph( string name )
	{
		if ( ActiveBody == null || ActiveBody.Morphs == null ) return 0f;

		// On utilise l'indexeur [] ou la m�thode Get
		return ActiveBody.Morphs.Get( name );
	}

	// Pour appliquer une nouvelle valeur
	public void SetMorph( string name, float val )
	{
		if ( !ActiveBody.IsValid() || ActiveBody.Morphs == null ) return;

		// 1. On contraint la valeur
		float clampedVal = Math.Clamp( val, 0f, 1f );

		// 2. On m�morise dans le dictionnaire pour le bug des v�tements
		FaceMorphs[name] = clampedVal;

		// 3. LA CORRECTION : On utilise l'objet Morphs du renderer
		ActiveBody.Morphs.Set( name, clampedVal );
	}

	public void ReapplyAllMorphs()
	{
		if ( !ActiveBody.IsValid() || ActiveBody.Morphs == null ) return;

		foreach ( var entry in FaceMorphs )
		{
			// On r�-applique chaque valeur m�moris�e
			ActiveBody.Morphs.Set( entry.Key, entry.Value );
		}
	}

	// R�cup�rer le nom du groupe actuel
	public string GetCurrentMaterialGroup()
	{
		return ActiveBody?.MaterialGroup ?? "default";
	}

	// Appliquer un nouveau groupe (ex: "skin_dark")
	public void SetSkin( string groupName )
	{
		if ( ActiveBody == null ) return;
		_currentSkinGroup = groupName; // On m�morise

		MaleBody.MaterialGroup = groupName;
		FemaleBody.MaterialGroup = groupName;
	}


	[ConCmd( "cam_info" )]
	public static void CmdCamInfo()
	{
		var mgr = Game.ActiveScene?.GetComponentInChildren<CharacterManager>();
		if ( mgr == null ) { Log.Warning( "CharacterManager introuvable" ); return; }
		Log.Info( $"[CAM] Zoom: {mgr.ZoomDistance:F1} | Height: {mgr.CameraHeight:F1}" );
	}

	public void SetCameraPreset( float zoom, float height )
	{
		ZoomDistance = Math.Clamp( zoom, 40f, 250f );
		CameraHeight = Math.Clamp( height, 10f, 100f );
	}

	public void SetBodyGroup( string name, int value )
	{
		if ( name == "head" ) _currentHeadIndex = value; // On m�morise la t�te
		ActiveBody?.SetBodyGroup( name, value );
	}

	/// Apply To Player.Prefab
	public void ApplyToPlayer()
	{
		var player = Client.Local?.PlayerPawn;
		CreatorDebug.Info( $"[Creator] ApplyToPlayer: Client.Local={Client.Local != null}, PlayerPawn={player != null}" );
		if ( player == null ) return;

		var playerBody = player.Components.Get<PlayerBody>( FindMode.EverythingInSelfAndChildren );
		var playerDresser = player.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );

		if ( playerBody == null || playerDresser == null ) return;

		var sourceRenderer = ActiveBody;
		var activeDresser = IsFemale ? FemaleDresser : MaleDresser;

		var clothingPaths = activeDresser?.Clothing
			.Where( x => x.Clothing != null )
			.Select( x => x.Clothing.ResourcePath )
			.ToList() ?? new List<string>();

		// IMPORTANT : on n'ecrit PAS Client.Local.Saved* directement ici. Ces
		// proprietes sont [Sync(SyncFlags.FromHost)] — seul le host peut les
		// ecrire et faire repliquer la valeur aux autres joueurs. Si on les
		// ecrivait cote client, ca echouerait silencieusement et les autres
		// joueurs verraient ce pawn nu (bug "je me vois habille mais les autres
		// me voient nu"). Le call BroadcastAppearance ci-dessous fait le travail
		// correctement : cote host, il met a jour tous les Saved* (cf
		// BroadcastAppearance dans Client.RPC.cs), et la replication FromHost
		// pousse ensuite ces valeurs a tous les clients (incluant les late-joiners).
		Client.BroadcastAppearance(
			player.GameObject,
			IsFemale,
			sourceRenderer?.Model?.ResourcePath ?? "",
			_currentSkinGroup,
			_currentHeadIndex,
			System.Text.Json.JsonSerializer.Serialize( clothingPaths ),
			System.Text.Json.JsonSerializer.Serialize( ClothingTints ),
			System.Text.Json.JsonSerializer.Serialize( FaceMorphs )
		);
	}
}
