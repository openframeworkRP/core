using OpenFramework.Command;
using OpenFramework.Database;
using OpenFramework.Database.DTO;

namespace OpenFramework.Database.Tables;

public class CommandsTable : Table<CommandDTO>
{
	public IEnumerable<(MethodDescription Method, CommandAttribute Attribute)> LoadedMethods;

	public CommandsTable() : base( "table_commands", "" )
	{}

	protected override void OnFinishLoad()
	{
		LoadedMethods = TypeLibrary.GetMethodsWithAttribute<CommandAttribute>();
		var commandsInCode = new HashSet<string>( LoadedMethods.Select( x => x.Attribute.Name ) );

		foreach ( var commandAttr in LoadedMethods )
		{
			var name = commandAttr.Attribute.Name;
			var icon = commandAttr.Attribute.Icon;

			var row = FindRowByIndexValue( name );
			var finalIcon = FileSystem.Mounted.FileExists( icon ) ? icon : "ui/icons/default.svg";

			// Extract aliases
			var aliases = commandAttr.Attribute.ChatCommand ?? Array.Empty<string>();

			// Extract argument names
			var arguments = commandAttr.Method.Parameters
				.ToDictionary( param => param.Name, param => param.IsOptional );

			if ( row == null )
			{
				InsertRow( new CommandDTO
				{
					Id = Guid.NewGuid(),
					Name = name,
					Description = commandAttr.Attribute.Description,
					Icon = finalIcon,
					Enabled = true
				} );
			}
			else
			{
				row.Icon = finalIcon;
				row.Aliases = aliases;
				row.Arguments = arguments;
			}

			Log.Info( arguments );
		}


		// Cleanup: remove commands from DB that no longer exist in code
		foreach ( var row in GetAllRows().ToList() )
		{
			if ( !commandsInCode.Contains( row.Name ) )
			{
				DeleteRow( row );
			}
		}
	}
}
