using Facepunch;
using Sandbox;
using OpenFramework.Systems.Pawn;

namespace OpenFramework.Systems.Gps;

/// <summary>
/// Système GPS purement client-side.
/// Chaque client gère son propre waypoint — pas de réseau.
/// Le composant existe sur un GameObject local créé à la demande,
/// implémente <see cref="IMinimapLabel"/> pour s'afficher sur la
/// minimap, et est lu par le HUD (<c>GpsHud</c>) pour la direction.
/// </summary>
public sealed class GpsSystem : Component, IMinimapLabel
{
	public static GpsSystem Instance { get; private set; }

	/// <summary>True si un waypoint est actif (sinon le marqueur est masqué).</summary>
	public bool IsActive { get; set; }

	/// <summary>Position monde du waypoint.</summary>
	public Vector3 Target { get; set; }

	/// <summary>Texte (emoji + libellé court) affiché sur la minimap.</summary>
	public string Label { get; set; } = "🚩";

	/// <summary>Couleur du label sur la minimap et de l'arrow GPS.</summary>
	public Color LabelColor { get; set; } = new Color( 1f, 0.25f, 0.25f );

	/// <summary>
	/// Identifiant éventuel de l'appel dispatch lié — permet de clear
	/// automatiquement le waypoint quand l'appel est clôturé.
	/// </summary>
	public int CallId { get; set; } = -1;

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	// ─── IMinimapElement ─────────────────────────────
	public new Vector3 WorldPosition => Target;
	public bool IsVisible( Pawn.Pawn viewer ) => IsActive && viewer is PlayerPawn;

	// ─── Helpers statiques ────────────────────────────

	private static void Ensure()
	{
		if ( Instance != null && Instance.IsValid() ) return;

		var scene = Game.ActiveScene;
		if ( scene == null ) return;

		var go = new GameObject( true, "GpsSystem" );
		go.Components.Create<GpsSystem>();
	}

	/// <summary>Pose / remplace le waypoint courant.</summary>
	public static void SetWaypoint( Vector3 position, string label, Color color, int callId = -1 )
	{
		Ensure();
		if ( Instance == null ) return;

		Instance.Target     = position;
		Instance.Label      = string.IsNullOrWhiteSpace( label ) ? "🚩" : label;
		Instance.LabelColor = color;
		Instance.CallId     = callId;
		Instance.IsActive     = true;
	}

	/// <summary>Efface le waypoint courant.</summary>
	public static void Clear()
	{
		if ( Instance == null ) return;
		Instance.IsActive = false;
		Instance.CallId = -1;
	}

	/// <summary>Efface uniquement si le waypoint courant correspond à cet appel.</summary>
	public static void ClearIfCall( int callId )
	{
		if ( Instance == null || !Instance.IsActive ) return;
		if ( Instance.CallId == callId ) Clear();
	}

	public static bool    HasWaypoint     => Instance != null && Instance.IsActive;
	public static Vector3 Position        => Instance?.Target ?? Vector3.Zero;
	public static string  Text            => Instance?.Label ?? "";
	public static Color   WaypointTint    => Instance?.LabelColor ?? Color.White;
}
