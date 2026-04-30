using Facepunch;
using OpenFramework.Extension;
using OpenFramework.Systems.Pawn;
using OpenFramework.Systems.Vehicles.UI;
using OpenFramework.Utility;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Type de composant mécanique inspectable.
/// </summary>
public enum InspectableComponent
{
	Engine,
	Gearbox,
	Tires,
	Turbo
}

/// <summary>
/// Point d'inspection placé sur un GameObject enfant du véhicule.
/// L'inspection est déclenchée via le menu radial du véhicule (Vehicle.RequestInspectNearest).
/// Pour les pneus : un point par roue, lié via la propriété Wheel.
/// </summary>
[Category( "Vehicles" )]
[Title( "Vehicle Inspection Point" )]
[Icon( "search" )]
public sealed class VehicleInspectionPoint : Component
{
	/// <summary>Référence vers le véhicule parent.</summary>
	[Property] public Vehicle Vehicle { get; set; }

	/// <summary>Quel composant ce point permet d'inspecter.</summary>
	[Property] public InspectableComponent ComponentType { get; set; } = InspectableComponent.Engine;

	/// <summary>Rayon du gizmo de visualisation dans l'éditeur.</summary>
	[Property] public float DetectionRadius { get; set; } = 60f;

	/// <summary>Roue associée (uniquement pour les points Tires).</summary>
	[Property] public Wheel Wheel { get; set; }

	// ── Inspection (appelé depuis Vehicle.RequestInspectNearest) ───────────────

	/// <summary>Exécute l'inspection pour un joueur donné (appelé côté Host).</summary>
	public void DoInspection( PlayerPawn pawn )
	{
		var internals = Vehicle?.Components.Get<VehicleInternals>();
		if ( !Vehicle.IsValid() || internals == null ) return;

		float healthPct;
		float km;
		float lifespanKm;
		string label;
		bool isFlat = false;

		switch ( ComponentType )
		{
			case InspectableComponent.Engine:
				healthPct = internals.EngineHealthPct;
				km = internals.EngineKm;
				lifespanKm = internals.EngineLifespanKm;
				label = "Moteur";
				break;
			case InspectableComponent.Gearbox:
				healthPct = internals.GearboxHealthPct;
				km = internals.GearboxKm;
				lifespanKm = internals.GearboxLifespanKm;
				label = "Boîte de vitesse";
				break;
			case InspectableComponent.Tires:
				var tire = internals.GetTireForWheel( Wheel );
				if ( tire == null ) return;
				healthPct = tire.WearPct;
				km = tire.Km;
				lifespanKm = internals.TireLifespanKm;
				label = $"Pneu ({Wheel.GameObject.Name})";
				isFlat = tire.IsFlat;
				break;
			case InspectableComponent.Turbo:
				healthPct = internals.TurboHealthPct;
				km = internals.TurboKm;
				lifespanKm = internals.TurboLifespanKm;
				label = "Turbo";
				break;
			default:
				return;
		}

		var connection = pawn.Network.Owner;
		if ( connection == null ) return;

		using ( Rpc.FilterInclude( connection ) )
		{
			ShowInspectionPanel( label, healthPct, km, lifespanKm, isFlat );
		}
	}

	[Rpc.Broadcast]
	private static void ShowInspectionPanel( string label, float healthPct, float km, float lifespanKm, bool isFlat )
	{
		Log.Info( $"[InspectionPoint] ShowInspectionPanel RPC received — label={label} health={healthPct:F0}%" );
		VehicleInspectionPanel.Show( label, healthPct, km, lifespanKm, isFlat );
		Log.Info( $"[InspectionPoint] VehicleInspectionPanel.Show() done — Instance={VehicleInspectionPanel.Instance != null} Visible={VehicleInspectionPanel.Instance?.IsVisible}" );
	}

	// ── Gizmo ─────────────────────────────────────────────────────────────────

	protected override void DrawGizmos()
	{
		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.LineThickness = 1.5f;

		Gizmo.Draw.Color = ComponentType switch
		{
			InspectableComponent.Engine => new Color( 1f, 0.5f, 0f, 0.6f ),
			InspectableComponent.Gearbox => new Color( 0.3f, 0.6f, 1f, 0.6f ),
			InspectableComponent.Tires => new Color( 0.4f, 1f, 0.4f, 0.6f ),
			InspectableComponent.Turbo => new Color( 1f, 0.3f, 0.3f, 0.6f ),
			_ => new Color( 1f, 1f, 1f, 0.6f )
		};

		Gizmo.Draw.LineSphere( new Sphere( Vector3.Zero, DetectionRadius ), 16 );

		Gizmo.Draw.Color = Color.White;
		string label = ComponentType switch
		{
			InspectableComponent.Engine => "ENGINE",
			InspectableComponent.Gearbox => "GEARBOX",
			InspectableComponent.Tires => "TIRE",
			InspectableComponent.Turbo => "TURBO",
			_ => "INSPECT"
		};
		Gizmo.Draw.Text( label, Transform.World, size: 14 );
	}
}
