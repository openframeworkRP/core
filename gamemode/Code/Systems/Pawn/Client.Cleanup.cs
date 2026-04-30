using OpenFramework.Inventory;
using OpenFramework.Systems;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Pawn;

public partial class Client : Component.INetworkListener
{
	/// <summary>
	/// How long has it been since this player has d/c'd
	/// </summary>
	RealTimeSince TimeSinceDisconnected { get; set; }

	/// <summary>
	/// How long does it take to clean up a player once they disconnect?
	/// </summary>
	public static float DisconnectCleanupTime { get; set; } = 120f;

	/// <summary>
	/// Sauvegarde le snapshot d'inventaire puis détruit le PlayerPawn.
	/// La position est sauvegardee per-character par PlayerPawn.OnDestroy
	/// via ApiComponent.UpdatePosition — on ne la stocke plus en memoire
	/// (sinon on spawnait un autre character sur la position du precedent).
	/// Appelé par ServerManager à la déconnexion.
	/// </summary>
	public void SavePositionAndDestroyPawn()
	{
		Log.Info( $"[Reco:Deco] SavePositionAndDestroyPawn appele pour {DisplayName} (SteamId={SteamId}, PlayerPawn valid={PlayerPawn.IsValid()})" );
		if ( PlayerPawn.IsValid() )
		{
			// Collecter le snapshot d'inventaire AVANT de détruire le pawn
			// pour éviter la duplication d'items (race condition avec ForceSaveAsync)
			if ( Networking.IsHost && InventoryApiSystem.Instance != null )
			{
				var mainContainer = PlayerPawn.InventoryContainer;
				var clothingEquip = PlayerPawn.Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
				if ( mainContainer != null )
				{
					var snapshot = InventoryApiSystem.Instance.CollectPlayerSnapshot( mainContainer, clothingEquip?.Container );
					Log.Info( $"[Reco:Deco] Snapshot collecte pour {DisplayName}: {snapshot.Count} items (main + clothing). Lancement SaveSnapshotAsync..." );
					_ = InventoryApiSystem.Instance.SaveSnapshotAsync( this, snapshot );
					mainContainer.ClearDirty();
					clothingEquip?.Container?.ClearDirty();
				}
				else
				{
					Log.Warning( $"[Reco:Deco] mainContainer NULL pour {DisplayName}, snapshot NON sauvegarde !" );
				}
			}
			else
			{
				Log.Warning( $"[Reco:Deco] Pas de sauvegarde inventaire pour {DisplayName} (IsHost={Networking.IsHost}, ApiSystem={InventoryApiSystem.Instance != null})" );
			}

			PlayerPawn.GameObject.Destroy();
			PlayerPawn = null;
		}
	}

	void INetworkListener.OnDisconnected( Connection channel )
	{
		if ( Connection == channel )
		{
			TimeSinceDisconnected = 0;
			Log.Info( $"[Reco:Deco] INetworkListener.OnDisconnected sur Client {DisplayName} (SteamId={SteamId}). Countdown {DisconnectCleanupTime}s avant destruction Client." );
			// La sauvegarde d'inventaire est maintenant gérée dans SavePositionAndDestroyPawn()
			// pour garantir que le snapshot est collecté AVANT la destruction du pawn.
		}
	}

	protected override void OnUpdate()
	{
		//if ( !Networking.IsHost ) return;
		//VerifyRespawn();

		if ( IsConnected ) return;
		if ( IsProxy ) return;

		if ( TimeSinceDisconnected > DisconnectCleanupTime )
		{
			Log.Info( $"[Reco:Deco] Destruction Client {DisplayName} (SteamId={SteamId}) apres {DisconnectCleanupTime}s deconnecte." );
			GameObject.Destroy();
		}

		if ( IsGlobalVocalMuted && !MuteIndefinite && GetRemaining( UntilUnmuteEndTime ) <= 0 )
		{
			IsGlobalVocalMuted = false;
			Notify( NotificationType.Success, "Vous n'etes plus mute !" );
		}

	}
}
