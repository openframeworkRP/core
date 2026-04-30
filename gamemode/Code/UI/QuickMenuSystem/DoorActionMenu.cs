using Facepunch;
using OpenFramework.World;

namespace OpenFramework.UI.QuickMenuSystem;

public record DoorActionMenu( Door door ) : IQuickMenuInterface
{
	public string Title => "Door Menu";
	public string SubTitle { get; set; }

	public QuickMenuStyle Style => new();

	public List<MenuItem> BuildMenu()
	{
		var list = new List<MenuItem>();

		// 1. Récupération du job du joueur pour la comparaison
		string playerJob = Client.Local.Data.Job ?? "";

		// 2. Vérification si le joueur a le métier autorisé sur cette porte
		bool hasJobAccess = door.CanBeAllowedJob &&
							!string.IsNullOrEmpty( door.JobName ) &&
							string.Equals( door.JobName, playerJob, StringComparison.OrdinalIgnoreCase );

		//
		// 🔹 PARTIE "ACHAT / PROPRIÉTÉ"
		// On n'affiche cette section que si la porte est achetable
		//
		if ( door.CanBePurchased )
		{
			if ( door.Owner == null )
			{
				list.Add( new MenuItem( "Acheter", () => Door.TryToBuy( door ), CloseMenuOnSelect: true ) );
			}

			if ( door.Owner == Client.Local )
			{
				list.Add( new MenuItem( "Vendre", () => Door.TryToSell( door ), CloseMenuOnSelect: true ) );

				// Sous-menu partager une clé
				var shareSubMenu = GameUtils.AllPlayers
					.Where( p => p != door.Owner && !door.CoOwners.Contains( p ) )
					.Select( p => new MenuItem( p.DisplayName, () => Door.ShareDoor( door, p )) )
					.ToList();

				if ( shareSubMenu != null && shareSubMenu.Count > 0 )
					list.Add( new MenuItem( "Partager", null, shareSubMenu ) );

				// Sous-menu retirer une clé
				var unshareChildren = door.CoOwners?
					.Select( id =>
					{
						var p = GameUtils.AllPlayers.FirstOrDefault( x => x == id );
						var name = p != null ? p.DisplayName : id.ToString();
						return new MenuItem( $"Retirer {name}", () => Door.RemoveShareDoor( door, id ));
					} )
					.ToList();

				if ( unshareChildren != null && unshareChildren.Count > 0 )
					list.Add( new MenuItem( "Retirer clé", null, unshareChildren ) );

				if ( door.CoOwners != null && door.CoOwners.Count > 0 )
					SubTitle = $"Propriétaire: {door.Owner.DisplayName} | Clés partagées: {door.CoOwners.Count}";
				else
					SubTitle = $"Propriétaire: {door.Owner.DisplayName}";
			}
			else if ( door.CoOwners != null && door.CoOwners.Contains( Client.Local ) )
			{
				list.Add( new MenuItem( "Rendre la clé", () => Door.RemoveShareDoor( door, Client.Local ), CloseMenuOnSelect: true ) );
			}
		}

		//
		// 🔹 PARTIE "COMMUNE / MÉTIER" (Verrouiller / Déverrouiller)
		// On affiche si : Je suis proprio OR Je suis co-proprio OR J'ai le bon métier
		//
		if ( door.Owner == Client.Local || (door.CoOwners != null && door.CoOwners.Contains( Client.Local )) || hasJobAccess )
		{
			// Options de verrouillage selon l'état de la porte
			if ( door.IsLocked )
			{
				list.Add( new MenuItem( "🔓 Déverrouiller", () => Door.Unlock( door ), CloseMenuOnSelect: true ) );
			}
			else
			{
				list.Add( new MenuItem( "🔒 Verrouiller", () => Door.Lock( door ), CloseMenuOnSelect: true ) );
			}
		}

		return list;
	}
}
