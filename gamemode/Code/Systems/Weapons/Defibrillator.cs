using Facepunch;
using OpenFramework.Command;
using OpenFramework.Extension;
using OpenFramework.Systems.Jobs;
using OpenFramework.Systems.Pawn;
using OpenFramework.Utility;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Weapons;

[Title( "Defibrillator" ), Icon( "monitor_heart" ), Group( "Weapon Components" )]
public sealed class Defibrillator : WeaponInputAction
{
	[Property, Category( "Config" ), EquipmentResourceProperty] public float MaxRange { get; set; } = 100f;
	[Property, Category( "Config" ), EquipmentResourceProperty] public float ReviveDuration { get; set; } = 5f;

	[Property, Category( "Sounds" )] public SoundEvent ChargeSound { get; set; }
	[Property, Category( "Sounds" )] public SoundEvent ReviveSound { get; set; }

	private bool _isCharging;
	private RagdollOwner _currentTarget;
	private RealTimeSince _holdStart;

	// Côté host : token par cible pour annuler une task en cours
	private static readonly Dictionary<PlayerPawn, int> _reviveTokens = new();
	private static int _tokenCounter;

	protected override void OnInputDown()
	{
		if ( _isCharging ) return;
		if ( !HasReviveAuthorisation( Client.Local ) )
		{
			Client.Local?.Notify( NotificationType.Error, "Seuls les médecins peuvent utiliser le défibrillateur." );
			return;
		}
		if ( !TryFindRagdoll( out var owner ) ) return;

		_currentTarget = owner;
		_holdStart = 0;
		_isCharging = true;

		if ( ChargeSound is not null )
			Sound.Play( ChargeSound, Equipment.WorldPosition );

		StartRevive( owner.OwnerPawn, ReviveDuration, ReviveSound?.ResourcePath );
	}

	protected override void OnInputUp()
	{
		if ( _isCharging )
		{
			Client.Local?.Notify( NotificationType.Warning, "Réanimation interrompue." );
			if ( _currentTarget != null )
				CancelRevive( _currentTarget.OwnerPawn );
		}

		_isCharging = false;
		_currentTarget = null;
		Equipment.Owner.PickupProgress = 0f;
	}

	protected override void OnInputUpdate()
	{
		if ( !_isCharging )
		{
			Equipment.Owner.PickupProgress = 0f;
			return;
		}

		if ( !TryFindRagdoll( out var owner ) || owner != _currentTarget )
		{
			if ( _currentTarget != null )
				CancelRevive( _currentTarget.OwnerPawn );

			_isCharging = false;
			_currentTarget = null;
			Equipment.Owner.PickupProgress = 0f;
			Client.Local?.Notify( NotificationType.Warning, "Réanimation interrompue." );
			return;
		}

		Equipment.Owner.PickupProgress = Math.Min( 1f, (float)_holdStart / ReviveDuration );

		// Charge complète — on coupe le tracking pour que OnInputUp ne montre pas "interrompue"
		if ( Equipment.Owner.PickupProgress >= 1f )
		{
			_isCharging = false;
			_currentTarget = null;
		}
	}

	private bool TryFindRagdoll( out RagdollOwner owner )
	{
		owner = null;

		if ( !Equipment.Owner.IsValid() ) return false;

		var ray = Equipment.Owner.AimRay;
		var trace = Scene.Trace.Ray( ray.Position, ray.Position + ray.Forward * MaxRange )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "movement" )
			.Run();

		if ( !trace.Hit ) return false;

		owner = trace.GameObject.Components.GetInAncestorsOrSelf<RagdollOwner>();
		if ( owner == null ) return false;

		return owner.OwnerPawn.IsValid() && owner.OwnerPawn.HealthComponent.State == LifeState.Dead;
	}

	// Statique pour éviter le routage via l'ID du GameObject de l'arme (qui échoue sur dédié)
	[Rpc.Host]
	private static async void StartRevive( PlayerPawn target, float reviveDuration, string reviveSoundPath )
	{
		if ( !Networking.IsHost || !target.IsValid() ) return;
		if ( target.HealthComponent.State != LifeState.Dead ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		// Anti-triche : seul un job avec CanRevive peut déclencher la réa, même si le client a forcé l'input.
		if ( !HasReviveAuthorisation( caller ) )
		{
			caller.Notify( NotificationType.Error, "Seuls les médecins peuvent utiliser le défibrillateur." );
			return;
		}

		var token = ++_tokenCounter;
		_reviveTokens[target] = token;

		await GameTask.DelaySeconds( reviveDuration );

		// Si le token a changé (cancel ou nouvelle tentative), on abandonne
		if ( !_reviveTokens.TryGetValue( target, out var current ) || current != token ) return;
		_reviveTokens.Remove( target );

		if ( !target.IsValid() || target.HealthComponent.State != LifeState.Dead ) return;

		Commands.RPC_RespawnInPlace( target.Client );

		if ( !string.IsNullOrEmpty( reviveSoundPath ) )
			BroadcastReviveSoundAt( target.WorldPosition, reviveSoundPath );

		caller?.Notify( NotificationType.Success, $"Réanimation réussie sur {target.DisplayName}." );
		target.Client?.Notify( NotificationType.Warning, "Vous revenez d'entre les morts..." );
	}

	[Rpc.Host]
	private static void CancelRevive( PlayerPawn target )
	{
		if ( !Networking.IsHost || !target.IsValid() ) return;
		_reviveTokens.Remove( target );
	}

	[Rpc.Broadcast]
	private static void BroadcastReviveSoundAt( Vector3 pos, string soundPath )
	{
		var resource = ResourceLibrary.Get<SoundEvent>( soundPath );
		if ( resource != null ) Sound.Play( resource, pos );
	}

	private static bool HasReviveAuthorisation( Client client )
	{
		if ( client?.Data == null ) return false;
		var job = JobSystem.GetJob( client.Data.Job );
		return job != null && job.HasPermission( JobPerms.CanRevive );
	}
}
