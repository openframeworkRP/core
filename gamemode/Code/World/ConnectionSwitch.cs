namespace OpenFramework.World;

public sealed class ConnectionSwitch : Component, Component.ICollisionListener
{
	[Header( "Bornes de Départ (Machine/Batterie)" )]
	[Property] public GameObject SourcePos { get; set; }
	[Property] public GameObject SourceNeg { get; set; }

	[Header( "Bornes Cibles (Connectées)" )]
	[Property, ReadOnly] public GameObject ConnectedPos { get; set; }
	[Property, ReadOnly] public GameObject ConnectedNeg { get; set; }

	[Header( "Paramètres des Câbles" )]
	[Property] public float MaxWireLength { get; set; } = 200f;
	[Property] public float WireThickness { get; set; } = 0.2f;

	private GameObject _currentDraggingObj;
	private bool _isDraggingPositive;

	private LineRenderer _linePos;
	private LineRenderer _lineNeg;

	[Property, ReadOnly]
	public bool IsFullyConnected => ConnectedPos.IsValid() && ConnectedNeg.IsValid();

	protected override void OnUpdate()
	{
		// --- LOGIQUE DE DRAGGING ---
		if ( _currentDraggingObj.IsValid() )
		{
			GameObject startPoint = _isDraggingPositive ? SourcePos : SourceNeg;

			// 1. Définir la position "tenue en main" (80 unités devant les yeux)
			float holdDistance = 80f;
			Vector3 eyePos = Scene.Camera.WorldPosition;
			Vector3 eyeDir = Scene.Camera.WorldRotation.Forward;

			// Position cible idéale devant le joueur
			Vector3 targetPos = eyePos + (eyeDir * holdDistance);

			// 2. Vérification de la longueur du câble par rapport à la machine
			Vector3 fromSource = targetPos - startPoint.WorldPosition;

			if ( fromSource.Length > MaxWireLength )
			{
				// Si on s'éloigne trop, le câble reste tendu à sa limite max
				_currentDraggingObj.WorldPosition = startPoint.WorldPosition + (fromSource.Normal * MaxWireLength);
				CancelDragging();
			}
			else
			{
				// Sinon, il suit nos mains (nos yeux)
				_currentDraggingObj.WorldPosition = targetPos;
			}

			// 3. Annulation si on lâche la touche (si tu décides de réactiver le Down)
			if ( !Input.Down( "Use" ) )
			{
				CancelDragging();
			}
			return;
		}

		// --- LOGIQUE D'INTERACTION ---
		if ( Input.Pressed( "Use" ) )
		{
			var tr = Scene.Trace.Ray( Scene.Camera.ScreenNormalToRay( 0.5f ), 100f )
				.IgnoreGameObjectHierarchy(Client.Local.PlayerPawn.GameObject)
				.WithoutTags("zone")
				//.WithAllTags( "terminal_pos", "terminal_neg" )
				.HitTriggersOnly()
				.Run();

			if ( tr.Hit && tr.Collider.GameObject.IsValid() )
			{
				if ( tr.Collider.GameObject == SourcePos )
				{
					Log.Info( SourcePos );
					StartLineDragging( true );
				}
				else if ( tr.Collider.GameObject == SourceNeg )
				{
					Log.Info( SourcePos );
					StartLineDragging( false );
				}
			}
		}
	}

	public void StartLineDragging( bool fromPositive = true )
	{
		GameObject startPoint = fromPositive ? SourcePos : SourceNeg;
		if ( !startPoint.IsValid() ) return;

		// On réinitialise la connexion si on recommence à tirer le fil
		if ( fromPositive ) ConnectedPos = null; else ConnectedNeg = null;

		// Création de l'entité temporaire pour le bout du fil
		_currentDraggingObj = new GameObject((fromPositive ? SourcePos : SourceNeg), true, fromPositive ? "Cable_End_Pos" : "Cable_End_Neg" );
		_currentDraggingObj.WorldPosition = startPoint.WorldPosition;
		_isDraggingPositive = fromPositive;

		// Trigger de collision
		var col = _currentDraggingObj.Components.Create<BoxCollider>();
		//col.IsTrigger = true;
		col.Scale = new Vector3( 3, 3, 3 );

		// Tag pour identifier le pôle lors de la collision
		_currentDraggingObj.Tags.Add( fromPositive ? "cable_pos" : "cable_neg" );

		// Setup du LineRenderer
		var renderer = fromPositive ? _linePos : _lineNeg;
		if ( !renderer.IsValid() )
		{
			renderer = Components.Create<LineRenderer>();
			if ( fromPositive ) _linePos = renderer; else _lineNeg = renderer;
		}

		renderer.Enabled = true;
		renderer.UseVectorPoints = false;
		renderer.Points = new List<GameObject> { startPoint, _currentDraggingObj };
		renderer.Width = WireThickness;
		renderer.Color = fromPositive ? Color.Red : Color.Black;
		renderer.Face = SceneLineObject.FaceMode.Cylinder;
	}

	public void OnCollisionStart( Collision collision )
	{
		Log.Info( collision.Other.GameObject );

		if ( !Networking.IsHost || IsProxy ) return;

		var triggerObj = collision.Self.GameObject; // Le bout du fil
		var targetObj = collision.Other.GameObject;  // La borne cible (ex: sur la batterie)

		if ( !triggerObj.IsValid() || !targetObj.IsValid() ) return;

		// Validation Positive
		if ( triggerObj.Tags.Has( "cable_pos" ) && targetObj.Tags.Has( "terminal_pos" ) )
		{
			ConnectedPos = targetObj;
			FinalizeConnection( true, targetObj, triggerObj );
		}
		// Validation Négative
		else if ( triggerObj.Tags.Has( "cable_neg" ) && targetObj.Tags.Has( "terminal_neg" ) )
		{
			ConnectedNeg = targetObj;
			FinalizeConnection( false, targetObj, triggerObj );
		}
	}

	private void FinalizeConnection( bool isPositive, GameObject targetTerminal, GameObject dragObj )
	{
		var renderer = isPositive ? _linePos : _lineNeg;
		var startPoint = isPositive ? SourcePos : SourceNeg;

		if ( renderer.IsValid() )
		{
			// On fixe définitivement le rendu entre la source et la borne cible
			renderer.Points = new List<GameObject> { startPoint, targetTerminal };
		}

		dragObj.Destroy();
		_currentDraggingObj = null;

		Log.Info( $"🔌 Câble {(isPositive ? "POSITIF" : "NÉGATIF")} connecté à {targetTerminal.Parent.Name}" );

		if ( IsFullyConnected )
		{
			Log.Info( "⚡ CIRCUIT ÉLECTRIQUE FERMÉ !" );
		}
	}

	private void CancelDragging()
	{
		if ( _currentDraggingObj.IsValid() ) _currentDraggingObj.Destroy();
		_currentDraggingObj = null;

		// Si on annule, on cache les lignes qui ne sont pas connectées
		if ( !ConnectedPos.IsValid() && _linePos.IsValid() ) _linePos.Enabled = false;
		if ( !ConnectedNeg.IsValid() && _lineNeg.IsValid() ) _lineNeg.Enabled = false;
	}

	protected override void DrawGizmos()
	{
		// On dessine la borne Positive
		if ( SourcePos.IsValid() )
		{
			DrawColliderGizmo( SourcePos, Color.Red, "+" );
		}

		// On dessine la borne Négative
		if ( SourceNeg.IsValid() )
		{
			DrawColliderGizmo( SourceNeg, Color.Black, "-" );
		}

		// On dessine le trigger de Drag
		if ( _currentDraggingObj.IsValid() )
		{
			DrawColliderGizmo( _currentDraggingObj, _isDraggingPositive ? Color.Red : Color.Black, "DRAG" );
		}
	}

	/// <summary>
	/// Dessine une BBox parfaitement alignée sur les bounds réels de l'objet
	/// </summary>
	private void DrawColliderGizmo( GameObject obj, Color color, string label )
	{
		// On récupère le BoxCollider pour être fidèle à la zone de collision réelle
		var col = obj.Components.Get<BoxCollider>( FindMode.EverythingInSelf );
		if ( !col.IsValid() ) return;

		using ( Gizmo.Scope() )
		{
			// On applique la transformation du GameObject
			Gizmo.Transform = obj.WorldTransform;
			Gizmo.Draw.Color = color;

			// On reproduit exactement la logique du code source que tu as fourni :
			// BBox.FromPositionAndSize utilise le Center et le Scale du composant
			BBox box = BBox.FromPositionAndSize( col.Center, col.Scale );

			// On dessine la boîte filaire
			Gizmo.Draw.LineThickness = 1f;
			Gizmo.Draw.LineBBox( in box );

			// On place le texte au centre de cette boîte
			Gizmo.Draw.Text( label, obj.WorldTransform.WithPosition(col.Center), size:56 );
		}
	}
}
