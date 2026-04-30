namespace OpenFramework.Command;

public enum CommandPermission
{
	/// <summary>Tout le monde peut exécuter cette commande.</summary>
	Everyone,
	/// <summary>Réservé aux admins.</summary>
	Admin,
}

/// <summary>
/// Attribute to mark methods as commands within the OpenFramework game context.
/// </summary>
[AttributeUsage( AttributeTargets.Method )]
public class CommandAttribute : Attribute
{
	/// <summary>Gets the name of the command.</summary>
	public string Name { get; }

	/// <summary>Gets the chat executable command aliases (e.g., "kick").</summary>
	public string[] ChatCommand { get; }

	/// <summary>Gets the description of the command.</summary>
	public string Description { get; }

	/// <summary>Icon path of the command.</summary>
	public string Icon { get; }

	/// <summary>Required permission level to execute this command.</summary>
	public CommandPermission RequiredPermission { get; }

	public CommandAttribute( string name, string[] chatCommand, string description = "", string icon = "images/icons/default.svg", CommandPermission permission = CommandPermission.Everyone )
	{
		Name = name;
		ChatCommand = chatCommand;
		Description = description;
		Icon = icon;
		RequiredPermission = permission;
	}
}

/// <summary>
/// Attribute to mark command parameter as auto-resolvable.
/// The CommandCallBuilder will try to resolve the value automatically (e.g. Client from name/id/steamid).
/// </summary>
[AttributeUsage( AttributeTargets.Parameter )]
public class CommandArgAttribute : Attribute
{
	public bool AutoResolve { get; set; } = false;

	/// <summary>Optional hint shown in the command input UI (e.g. "player name or ID").</summary>
	public string Hint { get; set; } = "";
}
