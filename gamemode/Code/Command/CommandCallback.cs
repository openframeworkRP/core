using Facepunch;
using OpenFramework.Database;
using OpenFramework.Database.DTO;
using OpenFramework.Database.Tables;
using System.Data;
using System.Reflection;

namespace OpenFramework.Command;

public static class CommandCallback
{
	/// <summary>Invoke / call a command by its targetable name.</summary>
	public static CommandCallBuilder Call( string command )
	{
		var match = TypeLibrary.GetMethodsWithAttribute<CommandAttribute>()
			.FirstOrDefault( x => x.Attribute.ChatCommand.Contains( command ) );

		if ( match.Method != null )
			return new CommandCallBuilder( match.Method );

		Log.Error( $"[Command] Call() : '{command}' not found." );
		throw new Exception( $"Command '{command}' not found." );
	}

	public record CommandFindContext
	{
		public MethodDescription Method { get; private set; }
		public ParameterInfo[] Args => Method.Parameters;
		public int ArgsCount => Method.Parameters.Count( x => !x.IsOptional );
		public CommandAttribute Attribute { get; private set; }
		public CommandDTO DTO { get; private set; }

		public CommandFindContext( MethodDescription _method )
		{
			Method = _method;
			Attribute = _method.GetCustomAttribute<CommandAttribute>();

			var table = DatabaseManager.Get<CommandsTable>();
			if ( table == null ) return;

			var row = table.FindRowByIndexValue( Attribute.Name );
			if ( row == null ) return;

			DTO = row;
		}
	}

	/// <summary>Find a command context by its chat alias. Returns null if not found.</summary>
	public static CommandFindContext Find( string command )
	{
		var match = TypeLibrary.GetMethodsWithAttribute<CommandAttribute>()
			.FirstOrDefault( x => x.Attribute.ChatCommand.Contains( command ) );

		return match.Method == null ? null : new CommandFindContext( match.Method );
	}
}
