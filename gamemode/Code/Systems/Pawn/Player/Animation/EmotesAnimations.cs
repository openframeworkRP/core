using OpenFramework.Command;

namespace OpenFramework.Systems.Pawn;

public class EmotesAnimations : Component
{
	public enum EmotesStyles
	{
		Anim1,
		Anim2,
		pouce_lever,
		stopanim
	}

	public static EmotesAnimations Instance => Game.ActiveScene.GetComponentInChildren<EmotesAnimations>();

	[Property]
	public SkinnedModelRenderer ModelRenderer { get; set; }
	public EmotesStyles EmoteStyle { get; set; }



	[Rpc.Broadcast]
	public void PlayAnimation( EmotesStyles style )
	{
		//if ( !Networking.IsHost ) return;

		var pl = Client.Local.PlayerPawn;

		Commands.Thirdperson();

		if ( !ModelRenderer.IsValid() )
			return;
	
		if ( pl.IsProxy || ModelRenderer.IsValid() )
		{
			Log.Info( $"Lecture animation: {(int)EmoteStyle}" );
			ModelRenderer.Set( IsAnimationLooped( style ), true );
		}
		else
		{
			Log.Warning( $"Aucune animation trouvée pour {style}" );
		}
	}

	[Rpc.Broadcast]
	public void StopAnimation( EmotesStyles style )
	{
		//if ( !Networking.IsHost ) return;

		var pl = Client.Local.PlayerPawn;

		Commands.Thirdperson();

		if ( !ModelRenderer.IsValid() )
			return;

		if ( pl.IsProxy || ModelRenderer.IsValid() )
		{
			
			Log.Info( $"Lecture animation: {(int)EmoteStyle}" );
			ModelRenderer.Set( IsAnimationLooped( style ), false );
		}
		else
		{
			Log.Warning( $"Aucune animation trouvée pour {style}" );
		}
	}


	private string IsAnimationLooped( EmotesStyles style )
	{
		return style switch
		{
			EmotesStyles.Anim1 => "b_anim1",
			EmotesStyles.pouce_lever => "b_emote",
			EmotesStyles.Anim2 => "b_emote",
			EmotesStyles.stopanim => "b_emote",
			_ => null
		};
	}

	protected override void OnUpdate()
	{
		//if ( IsProxy ) return;
	}
}
