using Facepunch;
using System.Reflection;
using System.Text.Json;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Command;

public class CommandCallBuilder
{
	private readonly MethodDescription _method;
	private readonly Dictionary<string, object> _arguments = new();

	public CommandCallBuilder( MethodDescription method )
	{
		_method = method ?? throw new ArgumentNullException( nameof( method ) );
	}

	public CommandCallBuilder Set( string name, object value )
	{
		_arguments[name] = value;
		return this;
	}

	// -------------------------------------------------------------------------
	// Execute
	// -------------------------------------------------------------------------

	public void Execute()
	{
		// Permission check avant tout
		var attribute = _method.GetCustomAttribute<CommandAttribute>();
		if ( attribute != null && !HasPermission( attribute.RequiredPermission ) )
		{
			Client.Local.Notify( NotificationType.Error, "Vous n'avez pas accès à cette commande !" );
			Log.Warning( $"[Command] Permission denied: {Client.Local.DisplayName} tried to run {_method.Name} (requires {attribute.RequiredPermission})" );
			return;
		}

		var parameters = _method.Parameters; // ParameterInfo[]

		// Pre-pass: détecte @all sur un paramètre AutoResolve Client/Connection → fan-out
		for ( int i = 0; i < parameters.Length; i++ )
		{
			var parameter = parameters[i];
			var argAttr = parameter.GetCustomAttribute<CommandArgAttribute>();

			if ( argAttr?.AutoResolve != true ) continue;
			if ( !_arguments.TryGetValue( parameter.Name, out var rawValue ) ) continue;

			var input = rawValue?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
			if ( input != "@all" ) continue;

			var isClient = typeof( Client ).IsAssignableFrom( parameter.ParameterType );
			var isConnection = typeof( Connection ).IsAssignableFrom( parameter.ParameterType );

			if ( !isClient && !isConnection ) continue;

			IEnumerable<object> targets = isClient
				? GameUtils.AllPlayers.Cast<object>()
				: Connection.All.Cast<object>();

			int count = 0;
			foreach ( var target in targets )
			{
				ExecuteWith( parameters, fanOutIndex: i, fanOutValue: target );
				count++;
			}

			Log.Info( $"[Command] @all fan-out: invoked {_method.Name} {count}x." );
			return;
		}

		// Exécution normale (cible unique)
		ExecuteWith( parameters, fanOutIndex: -1, fanOutValue: null );
	}

	/// <summary>
	/// Construit le tableau d'arguments et invoque la méthode.
	/// Si fanOutIndex >= 0, ce slot est pré-rempli avec fanOutValue (déjà résolu).
	/// </summary>
	private void ExecuteWith( ParameterInfo[] parameters, int fanOutIndex, object fanOutValue )
	{
		var args = new object[parameters.Length];

		for ( int i = 0; i < parameters.Length; i++ )
		{
			// Slot fan-out : déjà résolu en amont
			if ( i == fanOutIndex )
			{
				args[i] = fanOutValue;
				continue;
			}

			var parameter = parameters[i];
			var argAttr = parameter.GetCustomAttribute<CommandArgAttribute>();

			if ( argAttr?.AutoResolve == true )
			{
				if ( _arguments.TryGetValue( parameter.Name, out var rawValue ) )
				{
					var (success, resolved) = TryResolveArgument( parameter.ParameterType, rawValue );
					if ( !success ) return;
					args[i] = resolved;
					Log.Info( $"[Command] AutoResolved {parameter.Name} => {args[i]}" );
				}
				else if ( parameter.IsOptional )
				{
					args[i] = parameter.DefaultValue;
				}
				else
				{
					Log.Error( $"[Command] Missing required auto-resolve argument: {parameter.Name}" );
					Client.Local.Notify( NotificationType.Warning, $"Argument manquant : {parameter.Name}" );
					return;
				}
			}
			else if ( _arguments.TryGetValue( parameter.Name, out var raw ) )
			{
				// Déjà le bon type, pas besoin de conversion
				if ( raw != null && parameter.ParameterType.IsAssignableFrom( raw.GetType() ) )
				{
					args[i] = raw;
					continue;
				}

				try
				{
					args[i] = Convert.ChangeType( raw, parameter.ParameterType );
					Log.Info( $"[Command] Converted {parameter.Name} => {args[i]}" );
				}
				catch ( Exception ex )
				{
					Log.Error( $"[Command] Failed to convert '{raw}' to {parameter.ParameterType.Name}: {ex.Message}" );
					Client.Local.Notify( NotificationType.Warning,
						$"Valeur invalide pour '{parameter.Name}' : attendu {parameter.ParameterType.Name}." );
					return;
				}
			}
			else if ( parameter.IsOptional )
			{
				args[i] = parameter.DefaultValue;
			}
			else
			{
				Log.Error( $"[Command] Missing required argument '{parameter.Name}' for {_method.Name}." );
				Client.Local.Notify( NotificationType.Warning, $"Argument manquant : {parameter.Name}" );
				return;
			}
		}

		_method.Invoke( null, args );

		// AUDIT : trace l'invocation côté serveur (le RPC re-vérifie IsAdmin pour
		// éviter qu'un client trafiqué injecte de faux logs). On loggue uniquement
		// les commandes admin — les commandes Everyone (chat, whisper...) ne nous
		// intéressent pas pour l'audit modération.
		try
		{
			var attribute = _method.GetCustomAttribute<CommandAttribute>();
			if ( attribute?.RequiredPermission == CommandPermission.Admin )
			{
				ulong targetSteamId = 0;
				var argsForLog = new Dictionary<string, object>();
				for ( int i = 0; i < parameters.Length; i++ )
				{
					var raw = args[i];
					if ( raw is Client cl )
					{
						targetSteamId = cl.SteamId;
						argsForLog[parameters[i].Name] = cl.SteamId.ToString();
					}
					else if ( raw is Connection co )
					{
						targetSteamId = (ulong)co.SteamId.Value;
						argsForLog[parameters[i].Name] = co.SteamId.Value.ToString();
					}
					else
					{
						argsForLog[parameters[i].Name] = raw?.ToString() ?? "";
					}
				}

				var argsJson = JsonSerializer.Serialize( argsForLog );
				Commands.RPC_LogAdminCommand( _method.Name, targetSteamId, argsJson );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Audit] cmd log failed: {e.Message}" );
		}
	}

	// -------------------------------------------------------------------------
	// Permission
	// -------------------------------------------------------------------------

	private static bool HasPermission( CommandPermission required )
	{
		return required switch
		{
			CommandPermission.Everyone   => true,
			CommandPermission.Admin      => Client.Local.IsAdmin,
			_                            => false,
		};
	}

	// -------------------------------------------------------------------------
	// Résolution des arguments
	// -------------------------------------------------------------------------

	/// <summary>
	/// Essaie de résoudre une valeur brute dans le type attendu.
	/// Retourne (true, valeur) en succès, (false, null) en échec (l'utilisateur est déjà notifié).
	/// </summary>
	private (bool Success, object Value) TryResolveArgument( Type type, object value )
	{
		// Déjà le bon type
		if ( value != null && type.IsAssignableFrom( value.GetType() ) )
			return (true, value);

		var input = value?.ToString()?.Trim() ?? string.Empty;

		if ( typeof( Client ).IsAssignableFrom( type ) )
		{
			var result = ResolveClient( input );
			return result != null ? (true, result) : (false, null);
		}

		if ( typeof( Connection ).IsAssignableFrom( type ) )
		{
			var result = ResolveConnection( input );
			return result != null ? (true, result) : (false, null);
		}

		// Fallback primitif
		try
		{
			return (true, Convert.ChangeType( input, type ));
		}
		catch
		{
			Log.Error( $"[Command] Cannot auto-resolve '{input}' as {type.Name}" );
			Client.Local.Notify( NotificationType.Warning, $"Impossible de résoudre '{input}' en {type.Name}." );
			return (false, null);
		}
	}

	private static Client ResolveClient( string input )
	{
		var lower = input.ToLowerInvariant();

		if ( lower == "@me" )
			return Client.Local;

		// @all est géré en amont dans Execute(), ne devrait jamais arriver ici
		if ( lower == "@all" )
		{
			Log.Warning( "[Command] ResolveClient reached @all — should have been fan-out." );
			return null;
		}

		if ( lower == "@random" )
		{
			var all = GameUtils.AllPlayers.ToList();
			if ( all.Count == 0 )
			{
				Client.Local.Notify( NotificationType.Warning, "Aucun joueur en ligne." );
				return null;
			}
			return all[Random.Shared.Next( all.Count )];
		}

		// Exact → commence par → contient
		var client =
			GameUtils.AllPlayers.FirstOrDefault( x => x.DisplayName.Equals( input, StringComparison.OrdinalIgnoreCase ) )
			?? GameUtils.AllPlayers.FirstOrDefault( x => x.DisplayName.StartsWith( input, StringComparison.OrdinalIgnoreCase ) )
			?? GameUtils.AllPlayers.FirstOrDefault( x => x.DisplayName.Contains( input, StringComparison.OrdinalIgnoreCase ) );

		// GUID
		if ( client == null && Guid.TryParse( input, out var guid ) )
			client = GameUtils.AllPlayers.FirstOrDefault( x => x.Id == guid );

		// SteamID
		if ( client == null && ulong.TryParse( input, out var steamId ) )
			client = GameUtils.AllPlayers.FirstOrDefault( x => x.SteamId == steamId );

		if ( client == null )
			Client.Local.Notify( NotificationType.Warning, $"Aucun joueur trouvé pour '{input}'." );

		return client;
	}

	private static Connection ResolveConnection( string input )
	{
		var lower = input.ToLowerInvariant();

		if ( lower == "@me" )
			return Connection.Local;

		if ( lower == "@all" )
		{
			Log.Warning( "[Command] ResolveConnection reached @all — should have been fan-out." );
			return null;
		}

		if ( lower == "@random" )
		{
			var all = Connection.All.ToList();
			if ( all.Count == 0 )
			{
				Client.Local.Notify( NotificationType.Warning, "Aucune connexion en ligne." );
				return null;
			}
			return all[Random.Shared.Next( all.Count )];
		}

		// Exact → commence par → contient
		var conn =
			Connection.All.FirstOrDefault( x => x.DisplayName.Equals( input, StringComparison.OrdinalIgnoreCase ) )
			?? Connection.All.FirstOrDefault( x => x.DisplayName.StartsWith( input, StringComparison.OrdinalIgnoreCase ) )
			?? Connection.All.FirstOrDefault( x => x.DisplayName.Contains( input, StringComparison.OrdinalIgnoreCase ) );

		// GUID
		if ( conn == null && Guid.TryParse( input, out var guid ) )
			conn = Connection.All.FirstOrDefault( x => x.Id == guid );

		// SteamID
		if ( conn == null && ulong.TryParse( input, out var steamId ) )
			conn = Connection.All.FirstOrDefault( x => x.SteamId == steamId );

		if ( conn == null )
			Client.Local.Notify( NotificationType.Warning, $"Aucune connexion trouvée pour '{input}'." );

		return conn;
	}
}
