using Facepunch;
using OpenFramework.UI.RadialMenu;
using OpenFramework.World;

namespace OpenFramework.Systems.RadialMenu;

/// <summary>
/// Menu radial affiché quand un joueur interagit avec une porte.
/// Remplace DoorActionMenu (IQuickMenuInterface).
/// À placer sur le même GameObject que la Door.
/// </summary>
public class DoorRadialMenu : RadialMenuBase
{
	[Property] public Door Door { get; set; }

	protected override void OnStart()
	{
		Door ??= Components.Get<Door>( FindMode.EnabledInSelfAndChildren );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
	}
	
	public override List<RadialMenuItem> BuildItems()
	{
		var list = new List<RadialMenuItem>();
		var local = Client.Local;
		var playerJob = local?.Data?.Job ?? "";

		bool isOwner = Door.Owner == local;
		bool isCoOwner = Door.CoOwners != null && Door.CoOwners.Contains( local );
		bool hasJob = Door.CanBeAllowedJob
			&& !string.IsNullOrEmpty( Door.JobName )
			&& string.Equals( Door.JobName, playerJob, StringComparison.OrdinalIgnoreCase );

		// ── ACHAT / PROPRIÉTÉ ─────────────────────
		if ( Door.CanBePurchased )
		{
			if ( Door.Owner == null )
			{
				list.Add( new RadialMenuItem
				{
					Label = "Acheter",
					Icon = "ui/icons/money.svg",
					Color = "#5ac864",
					OnSelected = () => Door.TryToBuy( Door ),
				} );
			}

			if ( isOwner )
			{
				list.Add( new RadialMenuItem
				{
					Label = "Vendre",
					Icon = "ui/icons/sell.svg",
					Color = "#e25050",
					OnSelected = () => Door.TryToSell( Door ),
				} );

				list.Add( new RadialMenuItem
				{
					Label = "Partager",
					Icon = "ui/icons/keys.svg",
					Color = "#007AFF",
					IsEnabled = GameUtils.AllPlayers.Any( p => p != local && !Door.CoOwners.Contains( p ) ),
					OnSelected = () => OpenShareMenu(),
				} );

				if ( Door.CoOwners != null && Door.CoOwners.Count > 0 )
				{
					list.Add( new RadialMenuItem
					{
						Label = "Retirer clé",
						Icon = "ui/icons/unlock.svg",
						Color = "#fdea60",
						OnSelected = () => OpenUnshareMenu(),
					} );
				}
			}
			else if ( isCoOwner )
			{
				list.Add( new RadialMenuItem
				{
					Label = "Rendre la clé",
					Icon = "ui/icons/keys.svg",
					Color = "#fdea60",
					OnSelected = () => Door.RemoveShareDoor( Door, local ),
				} );
			}
		}

		// ── VERROUILLAGE ──────────────────────────
		if ( isOwner || isCoOwner || hasJob )
		{
			if ( Door.IsLocked )
			{
				list.Add( new RadialMenuItem
				{
					Label = "Déverrouiller",
					Icon = "ui/icons/unlock.svg",
					Color = "#5ac864",
					OnSelected = () => Door.Unlock( Door ),
				} );
			}
			else
			{
				list.Add( new RadialMenuItem
				{
					Label = "Verrouiller",
					Icon = "ui/icons/lock.svg",
					Color = "#e25050",
					OnSelected = () => Door.Lock( Door ),
				} );
			}
		}

		// ── Annuler ───────────────────────────────
		list.Add( new RadialMenuItem
		{
			Label = "Annuler",
			Icon = "ui/icons/close.svg",
			Color = "#ffffff",
			OnSelected = () => { },
		} );

		return list;
	}

	// ── Sous-menus (ouvrent un nouveau RadialMenu) ─
	private void OpenShareMenu()
	{
		var targets = GameUtils.AllPlayers
			.Where( p => p != Client.Local && !Door.CoOwners.Contains( p ) )
			.ToList();

		if ( targets.Count == 0 ) return;

		var items = targets.Select( p => new RadialMenuItem
		{
			Label = p.DisplayName,
			Icon = "ui/icons/contacts.svg",
			Color = "#007AFF",
			OnSelected = () => Door.ShareDoor( Door, p ),
		} ).ToList();

		items.Add( new RadialMenuItem { Label = "Annuler", Icon = "ui/icons/close.svg", Color = "#ffffff", OnSelected = () => { } } );

		//RadialMenuSystem.Instance?.OpenInternal( this, items );
	}

	private void OpenUnshareMenu()
	{
		if ( Door.CoOwners == null || Door.CoOwners.Count == 0 ) return;

		var items = Door.CoOwners.Select( id =>
		{
			var p = GameUtils.AllPlayers.FirstOrDefault( x => x == id );
			var name = p != null ? p.DisplayName : id.ToString();
			return new RadialMenuItem
			{
				Label = name,
				Icon = "ui/icons/contacts.svg",
				Color = "#e25050",
				OnSelected = () => Door.RemoveShareDoor( Door, id ),
			};
		} ).ToList();

		items.Add( new RadialMenuItem { Label = "Annuler", Icon = "ui/icons/close.svg", Color = "#ffffff", OnSelected = () => { } } );

		//RadialMenuSystem.Instance?.OpenInternal( this, items );
	}
}
