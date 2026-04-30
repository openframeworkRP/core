using Sandbox;
using OpenFramework.UI;
using System.Collections.Generic;
using System.Linq;

namespace OpenFramework.World;

/// <summary>
/// Systeme de culling local : a attacher sur le root d'un mega-prefab (props deco,
/// trees, decorations...) qui contient N objets enfants statiques, identiques sur
/// toutes les machines (NetworkMode = Never recommande pour eviter le sync inutile).
///
/// Au Start, scanne tous les descendants qui ont un ModelRenderer et les enregistre.
/// A intervalles reguliers (CheckIntervalSeconds), check la distance au viewer local
/// et toggle Enabled sur le Renderer + Colliders. Pour 1000-5000 enfants ca tient
/// largement (~2k-10k distance checks/sec, negligeable).
///
/// Pas de network — tout est purement local par machine. Pas de RPC, pas de sync.
/// Donc anti-duplication / multi-dedie : aucun risque, on ne touche aucune autorite.
///
/// Limites :
///  - Les enfants ajoutes APRES le Start ne sont pas pris en compte (a vrai pour des
///    decos statiques). Pour des props dynamiques (poses par les joueurs), utiliser
///    un autre mecanisme (LocalCullable a venir, ou re-scan periodique).
///  - La distance est calculee au WorldPosition du root du sous-objet (pas la BBox).
///    Suffit pour des props petits/moyens. Pour des gros batiments, ajuster la
///    distance ou raffiner avec ModelRenderer.Bounds.
/// </summary>
[Title( "Local Culling Zone" )]
[Category( "Networking" )]
[Icon( "blur_on" )]
public sealed class LocalCullingZone : Component
{
	/// <summary>
	/// Active les logs de debug du systeme de culling (settings au start, build entries,
	/// dumps periodiques, transitions show/hide). Pilotable via la console : `culling_debug 1`.
	/// Desactive par defaut pour eviter le spam en prod.
	/// </summary>
	[ConVar( "culling_debug" )]
	public static bool DebugLogs { get; set; } = false;

	/// <summary>Distance maximale (units = inches) sous laquelle un enfant est rendu.</summary>
	[Property, Range( 500f, 30000f )] public float MaxVisibilityDistance { get; set; } = 3000f;

	/// <summary>Intervalle entre 2 checks (secondes). 0.5s = 2Hz est un bon defaut.</summary>
	[Property, Range( 0.1f, 5f )] public float CheckIntervalSeconds { get; set; } = 0.5f;

	/// <summary>Si true, toggle aussi les Collider. Si false, juste les ModelRenderer.</summary>
	[Property] public bool ToggleColliders { get; set; } = true;

	/// <summary>
	/// Marge d'hysteresis (units) pour eviter le flicker quand un objet est pile a la limite.
	/// Un objet visible reste visible jusqu'a MaxVisibilityDistance + Hysteresis.
	/// </summary>
	[Property, Range( 0f, 2000f )] public float HysteresisMargin { get; set; } = 200f;

	/// <summary>
	/// Logs verbose : trace chaque transition visible/invisible avec le nom du GameObject
	/// et la distance. Utile pour diagnostiquer pourquoi tel batiment disparait trop tot.
	/// A laisser OFF en prod (spam si bcp d'objets bougent au-dela du seuil).
	/// </summary>
	[Property] public bool Verbose { get; set; } = false;

	/// <summary>
	/// Killswitch : si true, le component ne fait absolument rien (tout reste visible
	/// en permanence). Utile pour desactiver vite sans avoir a retirer le component.
	/// </summary>
	[Property] public bool DisableCulling { get; set; } = false;

	/// <summary>
	/// Si true, scanne tous les ModelRenderer de la scene entiere au lieu des descendants.
	/// A activer si tu poses le component sur un GameObject qui n'est pas parent des
	/// objets a culler — typiquement le PlayerPawn (les arbres ne sont pas ses enfants).
	/// </summary>
	[Property] public bool ScanWholeScene { get; set; } = false;

	/// <summary>
	/// Si true, le component ne s'active que si SON GameObject appartient au Client local
	/// (i.e. c'est le PlayerPawn du viewer). Les autres instances (sur les pawns des
	/// autres joueurs) restent passives. Indispensable si tu attaches le component sur
	/// un prefab PlayerPawn — sinon tu auras N scans en parallele pour rien.
	/// </summary>
	[Property] public bool OnlyForLocalViewer { get; set; } = false;

	private RealTimeUntil _nextDebugDump;

	private readonly List<Entry> _entries = new();
	private RealTimeUntil _nextCheck;

	protected override void OnStart()
	{
		// Dump les settings effectifs au start pour eviter les ambiguites
		// "j'ai coche dans l'inspector mais peut-etre pas applique au prefab"
		if ( DebugLogs )
			Log.Info( $"[LocalCullingZone] '{GameObject.Name}' SETTINGS : ScanWholeScene={ScanWholeScene} OnlyForLocalViewer={OnlyForLocalViewer} MaxDist={MaxVisibilityDistance:F0}u Hyst={HysteresisMargin:F0}u Toggle Colliders={ToggleColliders} Disable={DisableCulling}" );

		BuildEntries();
		if ( DebugLogs )
			Log.Info( $"[LocalCullingZone] '{GameObject.Name}' : {_entries.Count} enfants enregistres (max {MaxVisibilityDistance:F0}u, hysteresis {HysteresisMargin:F0}u)" );

		if ( Verbose && DebugLogs )
		{
			// Liste les 10 plus gros pour voir ce qu'on cull (utile si on suspecte
			// que des batiments massifs se font cull alors qu'ils sont encore visibles)
			var top = _entries
				.Where( e => e.LocalBounds.HasValue )
				.OrderByDescending( e => e.LocalBounds.Value.Size.Length )
				.Take( 10 )
				.ToList();
			Log.Info( $"[LocalCullingZone] Top 10 plus gros enfants enregistres :" );
			foreach ( var e in top )
			{
				var size = e.LocalBounds.Value.Size;
				Log.Info( $"  {e.GameObject.Name}  bounds=({size.x:F0}x{size.y:F0}x{size.z:F0})u" );
			}
		}
	}

	/// <summary>
	/// Scan recursif de tous les descendants pour trouver ceux qui ont un ModelRenderer.
	/// Appele au Start. Si tu spawns des enfants en runtime, appelle Rebuild() apres.
	/// </summary>
	public void Rebuild()
	{
		BuildEntries();
	}

	private void BuildEntries()
	{
		_entries.Clear();

		// Source des renderers : descendants du component OU scene entiere
		IEnumerable<ModelRenderer> source;
		int rawCount = 0;
		if ( ScanWholeScene )
		{
			var all = Scene?.GetAllComponents<ModelRenderer>().ToList() ?? new List<ModelRenderer>();
			rawCount = all.Count;
			source = all;
			if ( DebugLogs )
				Log.Info( $"[LocalCullingZone] '{GameObject.Name}' BuildEntries : ScanWholeScene found {rawCount} ModelRenderer dans la scene" );
		}
		else
		{
			var all = Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ).ToList();
			rawCount = all.Count;
			source = all;
			if ( DebugLogs )
				Log.Info( $"[LocalCullingZone] '{GameObject.Name}' BuildEntries : Descendants found {rawCount} ModelRenderer sous ce GameObject" );
		}

		int skippedSelf = 0;
		int skippedDoor = 0;
		int kept = 0;

		foreach ( var renderer in source )
		{
			if ( renderer == null || !renderer.IsValid() ) continue;

			// On ignore le renderer si il est sur le GameObject racine du LocalCullingZone
			// (typiquement le root est juste un conteneur sans modele)
			if ( renderer.GameObject == GameObject ) { skippedSelf++; continue; }

			var go = renderer.GameObject;

			// Skip les portes : elles ont leur propre systeme de visibility
			// (DistanceNetworkVisibility + DoorAnimationSystem). Si on les inclut ici,
			// notre toggle Renderer/Collider entre en conflit avec le leur.
			if ( go.Components.Get<Door>().IsValid() || go.Components.Get<RollingDoor>().IsValid() )
			{ skippedDoor++; continue; }

			Collider[] colliders = System.Array.Empty<Collider>();
			if ( ToggleColliders )
			{
				colliders = go.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ).ToArray();
			}

			// Bounds locales du model (en model-space). Utile pour calculer la distance
			// au plus proche point de la BBox au lieu du centre — un gros batiment
			// dont le centre est a 80m peut avoir un mur a 30m du joueur.
			BBox? localBounds = null;
			if ( renderer.Model != null )
				localBounds = renderer.Model.Bounds;

			_entries.Add( new Entry
			{
				GameObject = go,
				Renderer = renderer,
				Colliders = colliders,
				LocalBounds = localBounds,
				LastVisible = true,
			} );
			kept++;
		}

		if ( DebugLogs )
			Log.Info( $"[LocalCullingZone] '{GameObject.Name}' BuildEntries done : raw={rawCount} skippedSelf={skippedSelf} skippedDoor={skippedDoor} kept={kept}" );
	}

	protected override void OnUpdate()
	{
		// Killswitch : inspector OU reglage joueur (menu HUD Performance)
		if ( DisableCulling || !HudSettingsUI.EnableCulling )
		{
			EnsureAllVisible();
			return;
		}

		// Si attache a un PlayerPawn (ou descendant), seul l'instance du viewer local
		// fait le scan. Les autres pawns ont aussi le component mais restent passifs.
		if ( OnlyForLocalViewer && !IsLocalViewerInstance() )
		{
			return;
		}

		if ( !_nextCheck ) return;
		_nextCheck = CheckIntervalSeconds;

		// Distance effective : reglage joueur prioritaire si > 0, sinon valeur inspector
		float effectiveMaxDist = HudSettingsUI.CullingDistance > 0
			? HudSettingsUI.CullingDistance
			: MaxVisibilityDistance;

		// Position du viewer local. Defensive : si on n'a pas de pawn local valide
		// (debut de session, respawn, dedie sans Client.Local, etc.), on FORCE TOUT
		// VISIBLE plutot que d'utiliser un fallback Camera qui peut etre a (0,0,0)
		// et culler toute la scene par erreur.
		Vector3 viewerPos;
		var localPawn = Client.Local?.PlayerPawn;
		if ( !localPawn.IsValid() || localPawn.WorldPosition == Vector3.Zero )
		{
			EnsureAllVisible();
			if ( Verbose && DebugLogs && _nextDebugDump )
			{
				_nextDebugDump = 5f;
				Log.Info( $"[LocalCullingZone] '{GameObject.Name}' : pawn local invalide ou a Zero — tout visible" );
			}
			return;
		}
		viewerPos = localPawn.WorldPosition;

		// Verbose dump periodique (toutes les 5s) : viewer + 1er entry sample
		if ( Verbose && DebugLogs && _nextDebugDump )
		{
			_nextDebugDump = 5f;
			int visibleCount = 0;
			int invisibleCount = 0;
			for ( int i = 0; i < _entries.Count; i++ )
			{
				if ( _entries[i].LastVisible ) visibleCount++; else invisibleCount++;
			}
			var sample = _entries.Count > 0 ? _entries[0] : null;
			float sampleDist = -1f;
			if ( sample != null && sample.GameObject.IsValid() )
				sampleDist = sample.GameObject.WorldPosition.Distance( viewerPos );
			Log.Info( $"[LocalCullingZone] '{GameObject.Name}' viewer={viewerPos} max={effectiveMaxDist:F0}u  visible={visibleCount} invisible={invisibleCount}  sample='{sample?.GameObject.Name}' dist={sampleDist:F0}u" );
		}

		var maxSqr = effectiveMaxDist * effectiveMaxDist;
		var maxSqrWithHyst = (effectiveMaxDist + HysteresisMargin) * (effectiveMaxDist + HysteresisMargin);

		for ( int i = 0; i < _entries.Count; i++ )
		{
			var e = _entries[i];
			if ( e.GameObject == null || !e.GameObject.IsValid() ) continue;

			// Distance simple centre-a-centre. Suffit pour des props petits/moyens et
			// evite les pieges de BBox.Transform si l'API ne fait pas exactement ce qu'on
			// attend. Pour les gros batiments on les exclut deja (ils ont leur Door/etc).
			float distSqr = (e.GameObject.WorldPosition - viewerPos).LengthSquared;

			// Hysteresis : seuil different selon l'etat actuel pour eviter le flicker
			bool shouldBeVisible;
			if ( e.LastVisible )
				shouldBeVisible = distSqr <= maxSqrWithHyst;
			else
				shouldBeVisible = distSqr <= maxSqr;

			if ( shouldBeVisible == e.LastVisible ) continue;

			e.LastVisible = shouldBeVisible;
			if ( e.Renderer.IsValid() ) e.Renderer.Enabled = shouldBeVisible;
			for ( int c = 0; c < e.Colliders.Length; c++ )
			{
				if ( e.Colliders[c].IsValid() ) e.Colliders[c].Enabled = shouldBeVisible;
			}

			if ( Verbose && DebugLogs )
			{
				var dist = MathF.Sqrt( distSqr );
				Log.Info( $"[LocalCullingZone] {(shouldBeVisible ? "SHOW" : "HIDE")} '{e.GameObject.Name}' dist={dist:F0}u" );
			}
		}
	}

	/// <summary>
	/// Vrai si l'instance courante du component appartient (en hierarchie) au PlayerPawn
	/// du Client local. Utilise quand le component est pose sur un prefab PlayerPawn :
	/// chaque client a N PlayerPawns mais seul le sien doit cull.
	/// </summary>
	private bool IsLocalViewerInstance()
	{
		var localPawn = Client.Local?.PlayerPawn;
		if ( localPawn == null || !localPawn.IsValid() ) return false;

		var localGo = localPawn.GameObject;
		var current = GameObject;
		int safety = 64; // garde-fou contre cycles improbables
		while ( current.IsValid() && safety-- > 0 )
		{
			if ( current == localGo ) return true;
			current = current.Parent;
		}
		return false;
	}

	/// <summary>
	/// Force tous les enfants enregistres a etre visibles. Utilise quand on perd le
	/// viewer local (debut de session, respawn) pour eviter de laisser des objets
	/// Disabled par erreur.
	/// </summary>
	private void EnsureAllVisible()
	{
		for ( int i = 0; i < _entries.Count; i++ )
		{
			var e = _entries[i];
			if ( e.LastVisible ) continue;
			e.LastVisible = true;
			if ( e.Renderer.IsValid() ) e.Renderer.Enabled = true;
			for ( int c = 0; c < e.Colliders.Length; c++ )
			{
				if ( e.Colliders[c].IsValid() ) e.Colliders[c].Enabled = true;
			}
		}
	}

	private sealed class Entry
	{
		public GameObject GameObject;
		public ModelRenderer Renderer;
		public Collider[] Colliders;
		public BBox? LocalBounds;
		public bool LastVisible;
	}
}
