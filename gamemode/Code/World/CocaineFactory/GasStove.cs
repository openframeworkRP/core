using OpenFramework.GameLoop;

namespace OpenFramework.World.CocaineFactory
{
	public sealed class GasStoveButton : Component
	{
		/// <summary>Optional name to show in the HUD.</summary>
		[Property] public string DisplayName { get; set; }
	}

	/// <summary>
	/// Gas stove for cocaine cooking, with two gas bottle slots and four stove plates.
	/// </summary>
	public sealed class GasStove : Component, Component.ICollisionListener
	{
		/// <summary>Optional name to show in the HUD.</summary>
		[Property] public string DisplayName { get; set; }

		/// <summary>The two gas bottle slots.</summary>
		[Property, ReadOnly, Sync( SyncFlags.FromHost ), InlineEditor] 
		public GasBottle[] Bottles { get; private set; }

		/// <summary>Returns true if there are two gas bottles installed.</summary>
		[Property, ReadOnly]
		public bool HasTwoBottle => Bottles != null && Bottles.Length == 2 && Bottles[0] != null && Bottles[1] != null;

		[Property, ReadOnly, Sync(SyncFlags.FromHost), InlineEditor]
		public StovePlate[] Plates { get; private set; }

		[Property, Sync( SyncFlags.FromHost )] public int GasAmount { get; set; }

		#region Visuals & Tags
		[Property, Group( "Gas Visuals" )] public ModelRenderer ModelRenderer => GameObject.GetComponent<ModelRenderer>();
		[Property, Group( "Gas Visuals" )] public Model GasCylinderModel {get; set; }
		[Property, Group( "Gas Visuals" )] public Transform Gas1Anchor { get; set; }
		[Property, Group( "Gas Visuals" )] public Transform Gas2Anchor { get; set; }
		[Property, Group( "Gas Visuals" )] public GameObject GazGaugeBone { get; set; }
		[Property, Group( "Gas Visuals" )] public Vector3 GazGaugeMoveTo { get; set; } = new Vector3( 0, -2.078f, 3.620f );
		[Property, Group( "Gas Visuals" ), Range( 0f, 1f ), Change] public float GazLevel { get; set; }
		private Vector3 _baseLocalPos;

		[Property, Group( "Tags" )] public string PotTag { get; set; } = "cooking_pot";
		[Property, Group( "Tags" )] public string PlateTag { get; set; } = "plate_package";
		[Property, Group( "Tags" )] public string GasTag { get; set; } = "gas";
		#endregion

		#region Actions
		[Property, Group( "Actions" )] public Action OnGasAdded { get; set; }
		[Property, Group( "Actions" )] public Action OnPlateAdded { get; set; }
		[Property, Group( "Actions" )] public Action OnPotAdded { get; set; } 
		[Property, Group( "Actions" )] public Action OnCookingFinish { get; set; }
		#endregion

		public void OnGazLevelChanged( float oldValue, float newValue )
		{
			if ( GazGaugeBone == null ) return;

			var t = Math.Clamp( newValue, 0f, 1f );

			// Interpolation entre la position de base et la position finale
			//GazGaugeBone.LocalPosition = Vector3.Lerp( _baseLocalPos, _baseLocalPos + GazGaugeMoveTo, t );
			GazGaugeBone.LocalPosition = _baseLocalPos + (GazGaugeMoveTo * t);
		}


		private TimeUntil PartAttachTry = 0;

		protected override void OnAwake()
		{
			Bottles = new GasBottle[2];
			Plates = new StovePlate[4];

			if ( GazGaugeBone != null )
				_baseLocalPos = GazGaugeBone.LocalPosition;
		}

		protected override void OnStart()
		{
			// Créer/Assigner les 4 composants Plate
			for ( int i = 0; i < Plates.Length; i++ )
			{
				if ( Plates[i].IsValid() ) continue;
				var node = new GameObject( false, $"Plate_{i+1}" );
				node.SetParent( GameObject );
				var collider = node.Components.Create<BoxCollider>();
				collider.IsTrigger = true;
				collider.Center = new Vector3( 0, 0, 0 );
				collider.Scale = new Vector3( 13, 13, 1 );
				var plate = node.Components.Create<StovePlate>();
				plate.Index = i+1;
				plate.BodyGroupName = $"plate_{i + 1}";
				plate.Stove = this;
				Plates[i] = plate;
			}
		}

		// Add these properties to tune the effect from the inspector
		[Property, Group( "Gizmo Pulse" )] public Color PulseColorA { get; set; } = Color.Red;
		[Property, Group( "Gizmo Pulse" )] public Color PulseColorB { get; set; } = Color.Red;
		[Property, Group( "Gizmo Pulse" )] public float PulseSpeed { get; set; } = 2.0f;  // Hz-ish
		[Property, Group( "Gizmo Pulse" )] public float MinAlpha { get; set; } = 0.15f;
		[Property, Group( "Gizmo Pulse" )] public float MaxAlpha { get; set; } = 0.55f;

		private static float Ping01( float x ) => 0.5f * (MathF.Sin( x ) + 1f);

		protected override void DrawGizmos()
		{
			if ( GasCylinderModel == null ) return;

			var stoveWorld = (Game.IsPlaying ? GameObject.LocalTransform : GameObject.WorldTransform);

			// Compute the shared pulse once
			float s = Ping01( Time.Now * PulseSpeed );             // 0..1
			float a = MathX.Lerp( MinAlpha, MaxAlpha, s );         // alpha pulse
			var c = Color.Lerp( PulseColorA, PulseColorB, s );
			c.a = a;

			// Draw both anchors using the same fading color
			DrawAnchorGizmo( Gas1Anchor, c );
			DrawAnchorGizmo( Gas2Anchor, c );

			void DrawAnchorGizmo( Transform anchor, Color color )
			{
				if ( !anchor.IsValid ) return;

				var world = new Transform
				{
					Position = stoveWorld.Position + stoveWorld.Rotation * anchor.Position,
					Rotation = stoveWorld.Rotation * anchor.Rotation,
					Scale = stoveWorld.Scale * anchor.Scale
				};

				Gizmo.Draw.Color = color;
				Gizmo.Draw.Model( GasCylinderModel, world );

				// Optional: add a soft sphere halo
				// Gizmo.Draw.Color = new Color(color.r, color.g, color.b, color.a * 0.5f);
				// Gizmo.Draw.Sphere(world.Position, 3f);
			}
		}

		// ─────────────────────────────────────────────────────────────────────────
		// RPC entry points (static void) — delegate to instance helpers, then
		// notify clients with a broadcast for VFX/UI (slot index, etc.)
		// ─────────────────────────────────────────────────────────────────────────

		private void TryMountPlatePart( GameObject obj )
		{
			if(Constants.Instance.Debug) 
				Log.Info( $"TryMountPlatePart: {obj}" );

			int freeSlot = Array.FindIndex( Plates, p => p != null && !p.Draw);

			// Pas de slot libre → consommer la boîte (ton comportement demandé)
			if ( freeSlot == -1 )
				return;

			Plates[freeSlot].Draw = true;
			OnPlateAdded?.Invoke();
			obj.Destroy();
		}

		private void TryMountGasBottle( GameObject obj )
		{
            int freeSlot = Array.FindIndex(Bottles, p => p == null);

			if( freeSlot == -1 ) return;

			Log.Info( freeSlot );

			if( freeSlot == 0 )
				ModelRenderer?.SetBodyGroup( "gas_lines", 1);
			else
				ModelRenderer?.SetBodyGroup( "gas_lines", 2 );

			GazLevel += .5f;

			var bottle = obj.GetComponent<GasBottle>();
			if ( bottle == null ) return;

			Bottles[freeSlot] = bottle;
			OnGasAdded?.Invoke();
			obj.SetParent( GameObject, false );
			obj.LocalTransform = (freeSlot == 0 ? Gas1Anchor : Gas2Anchor);

			//obj.Destroy();
			var rb = obj.Components.Get<Rigidbody>();
			if ( rb != null ) rb.MotionEnabled = false;
		}

		/// <summary>Host-only: remove by slot index.</summary>
		[Rpc.Host]
		public static void HostRemoveGas( GasStove stove, int index )
		{
			if ( stove == null ) return;

			var removed = stove.RemoveGasInternal( index, null );
			if ( removed != null )
			{
				ClientOnBottleRemoved( stove, removed, index );
			}
		}

		/// <summary>Host-only: remove by bottle reference.</summary>
		[Rpc.Host]
		public static void HostRemoveGas( GasStove stove, GasBottle bottle )
		{
			if ( stove == null || bottle == null ) return;

			// We’ll find the index inside the helper for the broadcast payload.
			int idx = stove.IndexOfBottle( bottle );
			var removed = stove.RemoveGasInternal( -1, bottle );
			if ( removed != null )
			{
				ClientOnBottleRemoved( stove, removed, idx );
			}
		}

		// ─────────────────────────────────────────────────────────────────────────
		// Client notifications (fire-and-forget). Use for UI/VFX/audio.
		// The authoritative state is still on the host via Bottles[].
		// ─────────────────────────────────────────────────────────────────────────

		[Rpc.Broadcast]
		private static void ClientOnBottleAdded( GasStove stove, GasBottle bottle, int slot )
		{
			if ( stove == null || bottle == null ) return;

			// Example: play a sound, show HUD blip, spark FX, etc.
			// (Client-side code only; no state changes needed here.)
			// Sound.Play("stove.bottle.insert", stove.Transform.Position);
		}

		[Rpc.Broadcast]
		private static void ClientOnBottleRemoved( GasStove stove, GasBottle bottle, int slot )
		{
			if ( stove == null || bottle == null ) return;

			// Example: play removal sound / UI update
			// Sound.Play("stove.bottle.remove", stove.Transform.Position);
		}

		// ─────────────────────────────────────────────────────────────────────────
		// Instance helpers (host-only writers)
		// ─────────────────────────────────────────────────────────────────────────

		/// <summary>Finds the index of a bottle in the array, or -1 if not found.</summary>
		private int IndexOfBottle( GasBottle bottle )
		{
			for ( int i = 0; i < Bottles.Length; i++ )
			{
				if ( Bottles[i] == bottle ) return i;
			}
			return -1;
		}

		/// <summary>
		/// Shared logic for removing a bottle by index or reference. Returns removed bottle or null.
		/// </summary>
		private GasBottle RemoveGasInternal( int index, GasBottle targetBottle )
		{
			// Remove by index
			if ( targetBottle == null )
			{
				if ( index < 0 || index >= Bottles.Length || Bottles[index] == null )
					return null;

				var bottle = Bottles[index];
				Bottles[index] = null;
				return bottle;
			}

			// Remove by reference
			for ( int i = 0; i < Bottles.Length; i++ )
			{
				if ( Bottles[i] == targetBottle )
				{
					Bottles[i] = null;
					return targetBottle;
				}
			}

			return null;
		}

		public void OnCollisionStart( Collision collision )
		{
			// Host-only authority for attachment
			if ( !Networking.IsHost ) return;
			if ( IsProxy ) return;

			var other = collision.Other.GameObject;
			if ( other == null ) return;

			if(Constants.Instance.Debug )
				Log.Info( $"GasStove: OnCollisionStart with {other}" );

			if( !PartAttachTry )
			{
				return;
			}
			
			PartAttachTry = 0.5f; // debounce

			if ( other.Tags.Has( PlateTag ) )
			{
				TryMountPlatePart( other );
				return;
			}

            if ( other.Tags.Has( GasTag ) )
            {
				TryMountGasBottle( other );
				return;
			}
			else
			{
				Log.Info( "wait !" );
			}

			// Autres cas : gaz, pot, etc
		}
	}
}
