using Sandbox.Audio;
using OpenFramework;
using OpenFramework.GameLoop;
using System;
using System.Collections.Generic;
using System.Linq;
using static Facepunch.NotificationSystem;

namespace Facepunch
{
	public partial class PlayerVoiceComponent : Voice
	{
		[Property] public PlayerPawn Pawn { get; set; }
		[Property] public RadioComponent RadioComp { get; set; }
		IVoiceFilter Filter { get; set; }

		// On garde uniquement le Mixer par défaut pour la voix spatiale
		private Mixer Mixer { get; set; }

		[Sync] public bool IsTransmittingRadio { get; set; } = false;

		public enum VoiceMode
		{
			Chuchoter,
			Normal,
			Crie
		}

		[Property] public VoiceMode VoiceType { get; set; } = VoiceMode.Normal;

		// Gain AGC appliqué depuis VoiceControllerComponent
		public float AgcAmplitude { get; set; } = 0f;
		public float AgcGain { get; set; } = 1f;

		protected override void OnStart()
		{
			Filter = Scene.GetAllComponents<IVoiceFilter>().FirstOrDefault();
			Mixer = Mixer.FindMixerByName( "Voice" );

			// On utilise toujours le même mixer
			TargetMixer = Mixer;
		}

		public void SetTransmissionType( bool radioActive )
		{
			IsTransmittingRadio = radioActive;

			if ( radioActive )
			{
				TargetMixer = Mixer.FindMixerByName( "Voice.Radio" );
				WorldspacePlayback = false; // Force le mode 2D (ignore la géométrie)

				if ( TargetMixer != null )
				{
					TargetMixer.Occlusion = 0f;
					TargetMixer.Spacializing = 0f;
				}
			}
			else
			{
				TargetMixer = Mixer.FindMixerByName( "Voice" );
				WorldspacePlayback = true;
			}
		}

		public float GetVoiceVolume( PlayerPawn listener )
		{
			// Utilise ta propriété RadioComp si tu l'as assignée, sinon Get
			var myRadio = RadioComp ?? GameObject.Components.Get<RadioComponent>();
			var listenerRadio = listener.Components.Get<RadioComponent>( true );

			// 1. Logique Radio (Prioritaire)
			if ( IsTransmittingRadio && myRadio is { IsActivate: true } && listenerRadio is { IsActivate: true } )
			{
				if ( myRadio.Frequency == listenerRadio.Frequency )
				{
					return 1.2f; // On ignore l'atténuation spatiale
				}
			}

			// 2. Logique Proximité
			float distance = listener.WorldPosition.Distance( Pawn.WorldPosition );
			float maxDistance = GetMaxDistance();
			if ( distance > maxDistance ) return 0f;

			float t = distance / maxDistance;
			float volume = 1f - t;
			/*
			var tr = Scene.Trace.Ray( Pawn.WorldPosition + Vector3.Up * 60, listener.WorldPosition + Vector3.Up * 60 )
				.WithAnyTags( "map", "window" )
				.Run();

			if ( tr.Hit )
			{
				if ( tr.GameObject.Tags.Has( "window" ) )
				{
					return volume * 0.80f;
				}

				float diffZ = Math.Abs( Pawn.WorldPosition.z - listener.WorldPosition.z );
				if ( diffZ > 120f ) return 0f;

				return volume * 0.10f;
			}*/

			return volume;
		}

		public float GetMaxDistance()
		{
			var constants = Constants.Instance;
			return VoiceType switch
			{
				VoiceMode.Chuchoter => constants.VoiceWhisperDistance,
				VoiceMode.Normal => constants.VoiceDistanceDefault,
				VoiceMode.Crie => constants.VoiceShoutDistance,
				_ => constants.VoiceDistanceDefault
			};
		}

		private float UnitsToMeters( float units ) => units * 0.0254f;

		public void CycleVoiceMode()
		{
			VoiceType = VoiceType switch
			{
				VoiceMode.Chuchoter => VoiceMode.Normal,
				VoiceMode.Normal => VoiceMode.Crie,
				VoiceMode.Crie => VoiceMode.Chuchoter,
				_ => VoiceMode.Normal
			};

			float roundedMeters = MathF.Round( UnitsToMeters( GetMaxDistance() ) );

			Client.Local.Notify( NotificationType.Info, $"{VoiceType} : {roundedMeters} m" );
		}

		protected override IEnumerable<Connection> ExcludeFilter()
		{
			var excluded = new List<Connection>();
			if ( !Pawn.IsValid() ) return excluded;

			// Un joueur mort ne peut pas parler
			if ( Pawn.HealthComponent?.State == LifeState.Dead )
				return GameUtils.AllPlayers.Select( c => c.Connection ).ToList();

			var myRadio = RadioComp ?? GameObject.Components.Get<RadioComponent>();
			float maxDistance = GetMaxDistance();

			foreach ( var client in GameUtils.AllPlayers )
			{
				if ( client.Pawn is not PlayerPawn otherPawn || !otherPawn.IsValid() || otherPawn == Pawn ) continue;

				var otherRadio = otherPawn.Components.Get<RadioComponent>();
				var otherVoice = otherPawn.Components.Get<PlayerVoiceComponent>();

				// --- CORRECTION CRITIQUE ---
				// On vérifie si l'un des deux émet à la radio sur la bonne fréquence
				bool radioActive = (IsTransmittingRadio || (otherVoice != null && otherVoice.IsTransmittingRadio))
								   && myRadio is { IsActivate: true }
								   && otherRadio is { IsActivate: true }
								   && myRadio.Frequency == otherRadio.Frequency;

				if ( radioActive ) continue; // On laisse passer le son sans tester la géométrie

				// --- LOGIQUE PROXIMITÉ ---
				float dist = Pawn.WorldPosition.Distance( otherPawn.WorldPosition );
				if ( dist > maxDistance ) { excluded.Add( client.Connection ); continue; }

				var tr = Scene.Trace.Ray( Pawn.WorldPosition + Vector3.Up * 60, otherPawn.WorldPosition + Vector3.Up * 60 )
					.WithAnyTags( "map", "window" )
					.Run();

				float diffZ = Math.Abs( Pawn.WorldPosition.z - otherPawn.WorldPosition.z );

				if ( tr.Hit )
				{
					if ( tr.GameObject.Tags.Has( "window" ) )
					{
						if ( VoiceType != VoiceMode.Crie ) excluded.Add( client.Connection );
					}
					else
					{
						if ( VoiceType != VoiceMode.Crie || diffZ > 120f ) excluded.Add( client.Connection );
					}
				}
				else if ( diffZ > 120f ) excluded.Add( client.Connection );
			}
			return excluded;
		}

		protected override bool ShouldHearVoice( Connection connection )
		{
			var senderPawn = GameUtils.AllPlayers
								.FirstOrDefault( x => x.Connection == connection )?.Pawn as PlayerPawn;

			if ( senderPawn == null ) return false;

			// On n'entend pas les joueurs morts
			if ( senderPawn.HealthComponent?.State == LifeState.Dead ) return false;

			var senderRadio = senderPawn.Components.Get<RadioComponent>( true );
			var senderVoice = senderPawn.Components.Get<PlayerVoiceComponent>( true );

			if ( senderVoice is { IsTransmittingRadio: true } && senderRadio is { IsActivate: true } && RadioComp is { IsActivate: true } )
			{
				if ( senderRadio.Frequency == RadioComp.Frequency )
				{
					TargetMixer = Mixer.FindMixerByName( "Voice.Radio" );
					WorldspacePlayback = false;
					return true;
				}
			}

			TargetMixer = Mixer.FindMixerByName( "Voice" );
			WorldspacePlayback = true;
			float dist = WorldPosition.Distance( senderPawn.WorldPosition );
			float maxDist = GetMaxDistance();
			return dist <= maxDist;
		}

		[Rpc.Broadcast]
		public void BroadcastRadioStart( float frequency )
		{
			if ( IsProxy )
			{
				var localPawn = GameUtils.AllPlayers
					.FirstOrDefault( x => x.Connection == Connection.Local )?.Pawn as PlayerPawn;
				if ( localPawn == null ) return;

				var myRadio = localPawn.Components.Get<RadioComponent>( true );
				if ( myRadio is { IsActivate: true } && myRadio.Frequency == frequency )
				{
					Sound.Play( "sounds/radio/radio-on.sound" );
				}
			}
		}

		[Rpc.Broadcast]
		public void BroadcastRadioStop( float frequency )
		{
			if ( IsProxy )
			{
				var localPawn = GameUtils.AllPlayers
					.FirstOrDefault( x => x.Connection == Connection.Local )?.Pawn as PlayerPawn;
				if ( localPawn == null ) return;

				var myRadio = localPawn.Components.Get<RadioComponent>( true );
				if ( myRadio is { IsActivate: true } && myRadio.Frequency == frequency )
				{
					Sound.Play( "sounds/radio/radio-off.sound" );
				}
			}
		}
	}
}
