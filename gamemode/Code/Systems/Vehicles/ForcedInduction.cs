using Sandbox.Audio;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Simulates turbocharger or supercharger forced induction.
/// Turbo: boost builds with RPM + throttle, has spool lag and blow-off valve.
/// Supercharger: boost is instant and proportional to RPM, has a whine sound.
/// Multiplies the engine's torque output by (1 + CurrentBoost * BoostMultiplier).
/// </summary>
[Category( "Vehicles" )]
[Title( "Forced Induction" )]
[Icon( "speed" )]
public sealed class ForcedInduction : Component
{
	public enum InductionType
	{
		Turbo,
		Supercharger
	}

	[Property, Group( "References" )] public Vehicle Vehicle { get; set; }

	// ── Configuration ─────────────────────────────────────────────────────────

	/// <summary>Type of forced induction.</summary>
	[Property, Group( "Config" )] public InductionType Type { get; set; } = InductionType.Turbo;

	/// <summary>Maximum boost pressure in bar (0.5 = small turbo, 1.5 = big turbo).</summary>
	[Property, Group( "Config" )] public float MaxBoost { get; set; } = 0.8f;

	/// <summary>Torque multiplier at full boost. Final torque = engineTorque * (1 + CurrentBoost * BoostMultiplier).</summary>
	[Property, Group( "Config" )] public float BoostMultiplier { get; set; } = 0.4f;

	/// <summary>RPM below which the turbo/supercharger produces no boost.</summary>
	[Property, Group( "Config" )] public float MinActiveRpm { get; set; } = 3000f;

	/// <summary>RPM at which the turbo reaches full potential. Below this, boost is partial.</summary>
	[Property, Group( "Config" )] public float FullBoostRpm { get; set; } = 5000f;

	// ── Turbo-specific ────────────────────────────────────────────────────────

	/// <summary>How fast the turbo spools up (higher = less lag). Typical: 1–3.</summary>
	[Property, Group( "Turbo" )] public float SpoolRate { get; set; } = 1.5f;

	/// <summary>How fast the turbo spools down when off throttle. Typical: 3–6.</summary>
	[Property, Group( "Turbo" )] public float SpoolDownRate { get; set; } = 4f;

	/// <summary>Minimum throttle position for the turbo to spool (0-1).</summary>
	[Property, Group( "Turbo" )] public float MinThrottle { get; set; } = 0.3f;

	/// <summary>Blow-off valve triggers when boost drops faster than this per second.</summary>
	[Property, Group( "Turbo" )] public float BovThreshold { get; set; } = 0.3f;

	// ── Sounds ────────────────────────────────────────────────────────────────

	/// <summary>Turbo spool/whistle loop (volume + pitch scale with boost). Or supercharger whine loop.</summary>
	[Property, Group( "Sound" )] public SoundEvent SpoolLoop { get; set; }

	/// <summary>Blow-off valve sound (turbo only, plays once when throttle is released at high boost).</summary>
	[Property, Group( "Sound" )] public SoundEvent BlowOffValve { get; set; }

	// ── Sound tuning ──────────────────────────────────────────────────────────

	/// <summary>Minimum pitch of the spool/whine loop.</summary>
	[Property, Group( "Sound Tuning" )] public float SpoolMinPitch { get; set; } = 0.5f;

	/// <summary>Maximum pitch of the spool/whine loop at full boost.</summary>
	[Property, Group( "Sound Tuning" )] public float SpoolMaxPitch { get; set; } = 1.2f;

	/// <summary>Maximum volume of the spool/whine loop.</summary>
	[Property, Group( "Sound Tuning" )] public float SpoolMaxVolume { get; set; } = 0.5f;

	/// <summary>Boost must reach this % before the spool sound starts (0-1). Prevents sound at very low boost.</summary>
	[Property, Group( "Sound Tuning" )] public float SoundBoostThreshold { get; set; } = 0.25f;

	// ── Runtime state ─────────────────────────────────────────────────────────

	/// <summary>Current boost level (0 to MaxBoost).</summary>
	[Sync( SyncFlags.FromHost )] public float CurrentBoost { get; private set; }

	/// <summary>Normalized boost (0-1) for UI/HUD.</summary>
	public float BoostNormalized => MaxBoost > 0f ? CurrentBoost / MaxBoost : 0f;

	private float _previousBoost;
	private SoundHandle _spoolHandle;
	private float _smoothSpoolVolume;
	private int _lastGear;
	private TimeSince _timeSinceBov = 10f;
	private TimeSince _timeSinceSpoolRestart = 0f;

	protected override void OnUpdate()
	{
		if ( !Vehicle.IsValid() || !Vehicle.Engine.IsValid() )
			return;

		// Boost simulation runs on host only — clients get CurrentBoost via [Sync]
		if ( Networking.IsHost )
		{
			var engine = Vehicle.Engine;
			float rpm = engine.RPM;
			float throttle = engine.Throttle;

			_previousBoost = CurrentBoost;

			// ── Detect gear shift → dump boost ───────────────────────────
			if ( Vehicle.Gearbox.IsValid() )
			{
				int currentGear = Vehicle.Gearbox.CurrentGear;
				if ( currentGear != _lastGear && _lastGear > 0 && currentGear > 0 )
				{
					// Gear shift: turbo loses pressure (exhaust flow interrupted by clutch)
					CurrentBoost *= 0.3f;
				}
				_lastGear = currentGear;
			}

			// ── Calculate target boost ───────────────────────────────────
			float targetBoost = 0f;

			if ( rpm > MinActiveRpm )
			{
				// RPM factor ramps from 0 at MinActiveRpm to 1 at FullBoostRpm
				float rpmFactor = ((rpm - MinActiveRpm) / (FullBoostRpm - MinActiveRpm)).Clamp( 0f, 1f );

				if ( Type == InductionType.Turbo )
				{
					// Turbo: needs throttle to build exhaust pressure
					float throttleFactor = ((throttle - MinThrottle) / (1f - MinThrottle)).Clamp( 0f, 1f );
					targetBoost = MaxBoost * rpmFactor * throttleFactor;
				}
				else
				{
					// Supercharger: driven by engine, instant response, doesn't need throttle
					targetBoost = MaxBoost * rpmFactor;
				}
			}

			// ── Apply spool dynamics ─────────────────────────────────────
			if ( Type == InductionType.Turbo )
			{
				// Turbo has lag — slow spool up, faster spool down
				float rate = targetBoost > CurrentBoost ? SpoolRate : SpoolDownRate;
				CurrentBoost = CurrentBoost.LerpTo( targetBoost, rate * Time.Delta );
			}
			else
			{
				// Supercharger is nearly instant
				CurrentBoost = CurrentBoost.LerpTo( targetBoost, 15f * Time.Delta );
			}

			CurrentBoost = CurrentBoost.Clamp( 0f, MaxBoost );

			// ── Blow-off valve (turbo only, with cooldown) ──────────────
			if ( Type == InductionType.Turbo && BlowOffValve != null && _timeSinceBov > 1f )
			{
				float boostDrop = (_previousBoost - CurrentBoost) / Time.Delta;
				if ( boostDrop > BovThreshold && _previousBoost > MaxBoost * 0.4f )
				{
					Sound.Play( BlowOffValve, WorldPosition );
					_timeSinceBov = 0;
				}
			}
		}

		// Sounds run on all clients using the synced CurrentBoost
		UpdateSound();
	}

	/// <summary>
	/// Returns the torque multiplier to apply to engine output.
	/// Call this from Vehicle when computing wheel torque.
	/// </summary>
	public float GetTorqueMultiplier()
	{
		return 1f + CurrentBoost * BoostMultiplier;
	}

	private void UpdateSound()
	{
		if ( SpoolLoop == null )
			return;

		// Exponential volume curve: very quiet at low boost, loud at high boost
		// t² makes it so at 50% boost you only hear ~25% volume
		float t = BoostNormalized.Clamp( 0f, 1f );
		float targetVolume = t * t * t * SpoolMaxVolume;

		_smoothSpoolVolume = _smoothSpoolVolume.LerpTo( targetVolume, 5f * Time.Delta );

		if ( _smoothSpoolVolume > 0.01f )
		{
			if ( (_spoolHandle == null || _spoolHandle.IsStopped) && _timeSinceSpoolRestart > 0.5f )
			{
				_spoolHandle = Sound.Play( SpoolLoop, WorldPosition );
				_timeSinceSpoolRestart = 0f;
			}

			_spoolHandle.Pitch = SpoolMinPitch + BoostNormalized * (SpoolMaxPitch - SpoolMinPitch);
			_spoolHandle.Volume = _smoothSpoolVolume;
			_spoolHandle.Position = WorldPosition;
		}
		else
		{
			_spoolHandle?.Stop();
			_spoolHandle = null;
		}
	}

	protected override void OnDestroy()
	{
		_spoolHandle?.Stop();
		_spoolHandle = null;
	}
}
