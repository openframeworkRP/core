using Facepunch;
using Sandbox;
using Sandbox.Audio;
using OpenFramework;

public partial class VoiceControllerComponent : Component
{
	[Property] public PlayerVoiceComponent VoiceComp { get; set; }
	[Property] public RadioComponent RadioComp { get; set; }

	private RealTimeSince lastSwitch;
	private const float SwitchCooldown = 0.15f;

	private const float AgcTarget = 0.18f;
	private const float AgcMin = 0.3f;
	private const float AgcMax = 3.5f;

	protected override void OnUpdate()
	{
		//if ( !Networking.IsHost ) return;
		if ( IsProxy ) return;
		ApplyAgcToAll();
		CycleModeInput();
		UpdateRadioInput();
		if ( VoiceComp == null ) return;
		if ( VoiceComp.Pawn == null ) return;
		if ( !VoiceComp.Pawn.IsValid() ) return; // <- important
		if ( VoiceComp.Pawn.HealthComponent.State == LifeState.Dead ) return;

		/*
		// Exclusion dynamique selon distance
		foreach ( var client in GameUtils.AllPlayers )
		{
			if ( client.Pawn is not PlayerPawn otherPawn ) continue;
			if ( !otherPawn.IsValid() ) continue; // <- important
			if ( otherPawn == VoiceComp.Pawn ) continue;
			if ( otherPawn.HealthComponent.State == LifeState.Dead )
			{
				Input.Clear( "voiceup" );
			}

			float distance = (otherPawn.WorldPosition - VoiceComp.Pawn.WorldPosition).Length;
			float maxDistance = VoiceComp.GetMaxDistance();

			if ( distance > maxDistance )
			{
				VoiceComp.ExcludeClient( client.Connection );
			}
		}*/
	}


	private void ApplyAgcToAll()
	{
		foreach ( var voice in Scene.GetAll<PlayerVoiceComponent>() )
		{
			if ( !voice.IsProxy ) continue;

			voice.AgcAmplitude = voice.AgcAmplitude.LerpTo( voice.Amplitude, Time.Delta * 4f );

			if ( voice.AgcAmplitude > 0.015f )
			{
				float targetGain = (AgcTarget / voice.AgcAmplitude).Clamp( AgcMin, AgcMax );
				voice.AgcGain = voice.AgcGain.LerpTo( targetGain, Time.Delta * 1.5f );
				voice.Volume = voice.AgcGain;
			}
		}
	}

	[Rpc.Owner]
	private void UpdateRadioInput()
	{
		if ( RadioComp == null || VoiceComp == null ) return;
		if ( IsProxy ) return;

		bool canRecord = Input.Down( "Radio" ) && RadioComp.IsActivate;

		if ( canRecord != RadioComp.IsRecording )
		{
			RadioComp.SetRecording( canRecord );

			// On utilise la fonction de VoiceComp qui gère déjà le Mixer et le Worldspace
			VoiceComp.SetTransmissionType( canRecord );

			if ( canRecord )
			{
				Sound.Play( "sounds/radio/radio-on.sound" );
				VoiceComp.BroadcastRadioStart( RadioComp.Frequency );
				VoiceComp.TargetMixer = Mixer.FindMixerByName( "Voice.Radio" );
				VoiceComp.Mode = Sandbox.Voice.ActivateMode.AlwaysOn;
				VoiceComp.Distance = 1000000f;
				VoiceComp.Falloff = 1000000f;
			}
			else
			{
				Sound.Play( "sounds/radio/radio-off.sound" );
				VoiceComp.BroadcastRadioStop( RadioComp.Frequency );
				VoiceComp.Mode = Sandbox.Voice.ActivateMode.PushToTalk;
				VoiceComp.Distance = 15000f;
				VoiceComp.Falloff = 500f;
			}
		}
	}


	[Rpc.Owner]
	void CycleModeInput()
	{
		// Changer le mode de voix
		if ( Input.Pressed( "voiceup" ) && lastSwitch > SwitchCooldown )
		{

			VoiceComp.CycleVoiceMode();
			lastSwitch = 0f;
		}

	}

}
