using Facepunch;
using Sandbox.Events;
using OpenFramework.Inventory;
using OpenFramework.Systems.Weapons;

namespace OpenFramework.Systems.Weapons;

[Title( "Reload" ), Group( "Weapon Components" )]
public partial class Reloadable : WeaponInputAction,
	IGameEventHandler<EquipmentHolsteredEvent>
{
	/// <summary>
	/// How long does it take to reload?
	/// </summary>
	[Property] public float ReloadTime { get; set; } = 1.0f;

	/// <summary>
	/// How long does it take to reload while empty?
	/// </summary>
	[Property] public float EmptyReloadTime { get; set; } = 2.0f;

	[Property] public bool SingleReload { get; set; } = false;

	/// <summary>
	/// Duree d'appui long sur Reload pour ejecter le chargeur dans l'inventaire.
	/// </summary>
	[Property] public float EjectHoldTime { get; set; } = 0.5f;

	/// <summary>
	/// This is really just the magazine for the weapon.
	/// </summary>
	[Property] public WeaponAmmo AmmoComponent { get; set; }

	private TimeUntil TimeUntilReload { get; set; }
	[Sync] public bool IsReloading { get; set; }

	private TimeUntil _timeUntilEject;
	[Sync] public bool IsEjecting { get; set; }

	private RealTimeSince _reloadHeldTime;
	private bool _suppressReload = false;
	private bool _ejectTriggered = false;

	/// <summary>
	/// Le chargeur qui sera inséré à la fin du rechargement (choisi au début du reload).
	/// </summary>
	private InventoryItem _pendingMagazine;

	protected override void OnEnabled()
	{
		BindTag( "reloading", () => IsReloading );
	}

	protected override void OnInputDown()
	{
		_reloadHeldTime = 0;
		_ejectTriggered = false;
	}

	protected override void OnInputUp()
	{
		_suppressReload = false;
		_ejectTriggered = false;
	}

	protected override void OnInput()
	{
		if ( _suppressReload ) return;

		// Shift + Reload : affiche le nombre de balles dans le chargeur
		if ( Input.Down( "Run" ) )
		{
			ShowAmmoCountNotification();
			_suppressReload = true;
			return;
		}

		// Appui long sur Reload : ejecte le chargeur dans l'inventaire.
		// IsDown() == false au tout premier frame du press (le base class appelle OnInput AVANT OnInputDown) :
		// on attend que _reloadHeldTime ait ete remis a 0, sinon sa valeur par defaut (time-since-game-start)
		// declenche l'ejection sur un simple tap.
		if ( IsDown() && !_ejectTriggered && _reloadHeldTime > EjectHoldTime && AmmoComponent.IsValid() )
		{
			if ( AmmoComponent.HasMagazine )
			{
				if ( IsReloading ) CancelReload();
				EjectMagazine();
			}
			else
			{
				var container = NotificationContainer.Instance;
				if ( container != null )
				{
					container.AddChild( new NotificationItem( NotificationSystem.NotificationType.Error, "Pas de chargeur a ejecter" ) );
				}
			}
			_ejectTriggered = true;
			_suppressReload = true;
			return;
		}

		if ( CanReload() )
		{
			StartReload();
		}
	}

	private void ShowAmmoCountNotification()
	{
		if ( !AmmoComponent.IsValid() ) return;

		string msg = AmmoComponent.HasMagazine
			? $"Chargeur : {AmmoComponent.Ammo}/{AmmoComponent.MaxAmmo}"
			: "Pas de chargeur insere";

		var container = NotificationContainer.Instance;
		if ( container != null )
		{
			container.AddChild( new NotificationItem( NotificationSystem.NotificationType.Info, msg ) );
		}
	}

	[Rpc.Owner]
	public void EjectMagazine()
	{
		Log.Info( $"[Reload:Eject] EjectMagazine called (IsProxy={IsProxy}, IsHost={Networking.IsHost}, HasMag={AmmoComponent?.HasMagazine}, MagPresent={AmmoComponent?.MagPresent})" );

		if ( !AmmoComponent.IsValid() || !AmmoComponent.HasMagazine )
		{
			Log.Warning( "[Reload:Eject] Aborting: no ammo component or no magazine" );
			return;
		}

		if ( !IsProxy )
		{
			IsReloading = false;
			IsEjecting = true;
			_timeUntilEject = EmptyReloadTime;
			Log.Info( $"[Reload:Eject] Owner: set IsEjecting=true, timer={EmptyReloadTime}s" );
		}

		var container = NotificationContainer.Instance;
		if ( container != null )
		{
			container.AddChild( new NotificationItem( NotificationSystem.NotificationType.Info, "Chargeur ejecte" ) );
		}

		// Pas d'animation dediee a l'ejection, on reutilise celle de rechargement
		Equipment.ViewModel?.ModelRenderer?.Set( "b_reload", true );
		Equipment.Owner?.BodyRenderer?.Set( "b_reload", true );
		Log.Info( $"[Reload:Eject] Set b_reload=true (ViewModel={Equipment.ViewModel?.ModelRenderer != null}, BodyRenderer={Equipment.Owner?.BodyRenderer != null})" );

		foreach ( var kv in EmptyReloadSounds )
		{
			PlayAsyncSound( kv.Key, kv.Value, () => IsEjecting );
		}
	}

	void EndEject()
	{
		Log.Info( $"[Reload:Eject] EndEject called (IsProxy={IsProxy})" );

		if ( !IsProxy )
		{
			IsEjecting = false;
			RPC_HostEjectMagazine();
		}

		Equipment.ViewModel?.ModelRenderer?.Set( "b_reload", false );
		Equipment.Owner?.BodyRenderer?.Set( "b_reload", false );
	}

	[Rpc.Host]
	private void RPC_HostEjectMagazine()
	{
		Log.Info( $"[Reload:Eject] RPC_HostEjectMagazine (IsHost={Networking.IsHost}, HasMag={AmmoComponent?.HasMagazine})" );

		if ( !AmmoComponent.IsValid() ) return;
		if ( !AmmoComponent.HasMagazine ) return;

		var unloaded = AmmoComponent.UnloadMagazine();
		Log.Info( $"[Reload:Eject] Host: ejection manuelle du chargeur — result={(unloaded != null ? $"created item slot={unloaded.SlotIndex}" : "FAILED (null)")}" );
	}

	protected override void OnUpdate()
	{
		if ( !Player.IsValid() )
			return;

		if ( !Player.IsLocallyControlled )
		{
			if ( Player.IsNpc )
			{
				if ( IsReloading && TimeUntilReload )
				{
					EndReload();
				}
				return;
			}
			return;
		}

		if ( SingleReload && IsReloading && Input.Pressed( "Attack1" ) )
		{
			_queueCancel = true;
		}

		if ( IsReloading && TimeUntilReload )
		{
			EndReload();
		}

		if ( IsEjecting && _timeUntilEject )
		{
			EndEject();
		}
	}

	void IGameEventHandler<EquipmentHolsteredEvent>.OnGameEvent( EquipmentHolsteredEvent eventArgs )
	{
		if ( !IsProxy && IsReloading )
		{
			CancelReload();
		}
	}

	/// <summary>
	/// Cherche un chargeur compatible dans l'inventaire du joueur.
	/// Compatible = même MagAmmoType que l'AmmoType de l'arme, et contient des balles.
	/// Le chargeur actuellement inséré dans l'arme n'est plus dans l'inventaire,
	/// donc il est naturellement exclu de la recherche.
	/// </summary>
	public InventoryItem FindBestMagazine()
	{
		if ( !AmmoComponent.IsValid() || Equipment?.LinkedItem?.Metadata == null )
			return null;

		var weaponAmmoType = Equipment.LinkedItem.Metadata.AmmoType;
		if ( weaponAmmoType == null ) return null;

		var playerContainer = GetPlayerInventoryContainer();
		if ( playerContainer == null ) return null;

		return playerContainer.Items
			.Where( x =>
				x.Metadata?.IsMagazine == true &&
				x.Metadata.MagAmmoType == weaponAmmoType &&
				x.MagAmmo > 0
			)
			.OrderByDescending( x => x.MagAmmo )
			.FirstOrDefault();
	}

	/// <summary>
	/// Récupère l'InventoryContainer du joueur.
	/// </summary>
	private InventoryContainer GetPlayerInventoryContainer()
	{
		return Player?.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
	}

	bool CanReload()
	{
		if ( Input.Down( "Use" ) ) return false;
		if ( IsReloading ) return false;
		if ( !AmmoComponent.IsValid() ) return false;

		// Si le chargeur actuel est plein, pas besoin de recharger
		if ( AmmoComponent.IsFull ) return false;

		// Sur le host on peut vérifier le chargeur compatible directement.
		// Sur le client (serveur dédié), LinkedItem n'est pas sync donc
		// FindBestMagazine() ne peut pas fonctionner — on laisse le host valider.
		if ( Networking.IsHost )
			return FindBestMagazine() != null;

		return true;
	}

	float GetReloadTime()
	{
		if ( !AmmoComponent.HasAmmo ) return EmptyReloadTime;
		return ReloadTime;
	}

	Dictionary<float, SoundEvent> GetReloadSounds()
	{
		if ( !AmmoComponent.HasAmmo ) return EmptyReloadSounds;
		return TimedReloadSounds;
	}

	bool _queueCancel = false;

	[Rpc.Owner]
	public void StartReload()
	{
		_queueCancel = false;

		// Demander au host de pré-sélectionner le chargeur
		RPC_HostSelectMagazine();

		if ( !IsProxy )
		{
			IsReloading = true;
			TimeUntilReload = GetReloadTime();
		}

		if ( SingleReload )
		{
			Equipment.ViewModel?.ModelRenderer?.Set( "b_reloading", true );
			bool hasAmmo = AmmoComponent.HasAmmo;
			Equipment.ViewModel?.ModelRenderer.Set( !hasAmmo ? "b_reloading_first_shell" : "b_reloading_shell", true );
		}
		else
		{
			Equipment.ViewModel?.ModelRenderer?.Set( "b_reload", true );
		}

		foreach ( var kv in GetReloadSounds() )
		{
			PlayAsyncSound( kv.Key, kv.Value, () => IsReloading );
		}

		Equipment.Owner?.BodyRenderer?.Set( "b_reload", true );
	}

	/// <summary>
	/// Appelé sur le host pour trouver le meilleur chargeur à insérer.
	/// Si aucun chargeur trouvé, annule le reload.
	/// </summary>
	[Rpc.Host]
	private void RPC_HostSelectMagazine()
	{
		_pendingMagazine = FindBestMagazine();

		if ( _pendingMagazine == null )
		{
			Log.Warning( $"[Reload] Host: no compatible magazine found — cancelling reload" );
			CancelReload();
			return;
		}

		Log.Info( $"[Reload] Host: selected magazine = {_pendingMagazine.Metadata?.ResourceName} (bullets: {_pendingMagazine.MagAmmo})" );
	}

	/// <summary>
	/// Declenche un rechargement avec un chargeur specifique choisi par le joueur
	/// (via le menu contextuel de l'inventaire). Joue l'animation normale de reload.
	/// </summary>
	[Rpc.Owner]
	public void StartReloadWithMagazine( InventoryItem mag )
	{
		Log.Info( $"[Reload:Equip] StartReloadWithMagazine called (IsProxy={IsProxy}, mag={mag?.Metadata?.ResourceName}, bullets={mag?.MagAmmo})" );

		if ( mag == null || !mag.IsValid )
		{
			Log.Warning( "[Reload:Equip] Aborting: magazine invalid" );
			return;
		}

		_queueCancel = false;

		// Le host pre-selectionne CE chargeur (pas FindBestMagazine)
		RPC_HostSetPendingMagazine( mag );

		if ( !IsProxy )
		{
			IsReloading = true;
			TimeUntilReload = GetReloadTime();
			Log.Info( $"[Reload:Equip] Owner: set IsReloading=true, timer={GetReloadTime()}s" );
		}

		if ( SingleReload )
		{
			Equipment.ViewModel?.ModelRenderer?.Set( "b_reloading", true );
			bool hasAmmo = AmmoComponent.HasAmmo;
			Equipment.ViewModel?.ModelRenderer.Set( !hasAmmo ? "b_reloading_first_shell" : "b_reloading_shell", true );
		}
		else
		{
			Equipment.ViewModel?.ModelRenderer?.Set( "b_reload", true );
		}

		foreach ( var kv in GetReloadSounds() )
		{
			PlayAsyncSound( kv.Key, kv.Value, () => IsReloading );
		}

		Equipment.Owner?.BodyRenderer?.Set( "b_reload", true );
	}

	/// <summary>
	/// Le host valide et stocke le chargeur choisi par le joueur pour le reload en cours.
	/// </summary>
	[Rpc.Host]
	private void RPC_HostSetPendingMagazine( InventoryItem mag )
	{
		Log.Info( $"[Reload:Equip] RPC_HostSetPendingMagazine (IsHost={Networking.IsHost}, mag={mag?.Metadata?.ResourceName})" );

		if ( mag == null || !mag.IsValid || mag.Metadata == null || !mag.Metadata.IsMagazine )
		{
			Log.Warning( "[Reload:Equip] Host: invalid magazine item" );
			CancelReload();
			return;
		}

		if ( !AmmoComponent.IsValid() || AmmoComponent.LinkedItem?.Metadata?.AmmoType == null )
		{
			Log.Warning( "[Reload:Equip] Host: no ammo component or weapon has no ammo type" );
			CancelReload();
			return;
		}

		if ( mag.Metadata.MagAmmoType != AmmoComponent.LinkedItem.Metadata.AmmoType )
		{
			Log.Warning( $"[Reload:Equip] Host: incompatible ammo type (mag={mag.Metadata.MagAmmoType?.ResourceName}, weapon={AmmoComponent.LinkedItem.Metadata.AmmoType?.ResourceName})" );
			CancelReload();
			return;
		}

		_pendingMagazine = mag;
		Log.Info( $"[Reload:Equip] Host: pending magazine set = {mag.Metadata.ResourceName} (bullets: {mag.MagAmmo})" );
	}

	[Rpc.Owner]
	void CancelReload()
	{
		if ( !IsProxy )
		{
			IsReloading = false;
			_pendingMagazine = null;
		}

		Equipment.ViewModel?.ModelRenderer?.Set( "b_reload", false );
		Equipment.Owner?.BodyRenderer?.Set( "b_reload", false );
		Equipment.ViewModel?.ModelRenderer?.Set( "b_reloading", false );
	}

	[Rpc.Owner]
	void EndReload()
	{
		if ( !IsProxy )
		{
			// Demander au host de faire le swap de chargeur
			RPC_HostSwapMagazine();

			if ( SingleReload )
			{
				// Pour SingleReload, on pourrait vouloir continuer à recharger
				if ( !_queueCancel && !AmmoComponent.IsFull && FindBestMagazine() != null )
					StartReload();
				else
				{
					Equipment.ViewModel?.ModelRenderer?.Set( "b_reloading", false );
					IsReloading = false;
				}
			}
			else
			{
				IsReloading = false;
			}
		}

		Equipment.ViewModel?.ModelRenderer.Set( "b_reload", false );
	}

	/// <summary>
	/// Appelé sur le host pour effectuer le swap de chargeur.
	/// Swap atomique : on capture l'état du nouveau chargeur, on détruit son GameObject pour
	/// libérer son slot, puis on décharge l'ancien dans ce slot libéré avant d'écrire le nouveau
	/// dans les attributs de l'arme.
	/// </summary>
	[Rpc.Host]
	private void RPC_HostSwapMagazine()
	{
		if ( _pendingMagazine == null || !_pendingMagazine.IsValid )
		{
			Log.Warning( $"[Reload] Host: EndReload mais _pendingMagazine est null ou invalide" );
			return;
		}

		// Capture de l'état du chargeur entrant avant sa destruction
		var newMeta = _pendingMagazine.Metadata;
		int newBullets = _pendingMagazine.MagAmmo;
		int newSlot = _pendingMagazine.SlotIndex;
		var oldName = AmmoComponent.LinkedItem?.Attributes.GetValueOrDefault( "loaded_mag_type", "" );

		// Détruit le GameObject du chargeur entrant — son état vit maintenant dans les variables locales
		_pendingMagazine.GameObject.Destroy();
		_pendingMagazine = null;

		// Décharge l'ancien chargeur dans le slot libéré (crée un nouvel InventoryItem de mag)
		if ( AmmoComponent.HasMagazine )
		{
			AmmoComponent.UnloadMagazine( newSlot );
		}

		// Écrit l'état du nouveau chargeur dans les attributs de l'arme
		AmmoComponent.SetLoadedMag( newMeta, newBullets );

		Log.Info( $"[Reload] Host: swap chargeur (old: {oldName ?? "none"}) -> (new: {newMeta?.ResourceName}, ammo: {AmmoComponent.Ammo}/{AmmoComponent.MaxAmmo})" );
	}

	[Property] public Dictionary<float, SoundEvent> TimedReloadSounds { get; set; } = new();
	[Property] public Dictionary<float, SoundEvent> EmptyReloadSounds { get; set; } = new();

	async void PlayAsyncSound( float delay, SoundEvent snd, Func<bool> playCondition = null )
	{
		await GameTask.DelaySeconds( delay );

		if ( playCondition != null && !playCondition.Invoke() )
			return;

		if ( !GameObject.IsValid() )
			return;

		GameObject.PlaySound( snd );
	}
}
