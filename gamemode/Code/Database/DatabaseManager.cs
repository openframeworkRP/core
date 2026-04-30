using OpenFramework.Database.DTO;
using OpenFramework.Database.Tables;

namespace OpenFramework.Database;
public partial class DatabaseManager : SingletonComponent<DatabaseManager>
{
	/// <summary>
	/// The base URL for the API used by the database system.
	/// </summary>
	public const string ApiUrl = "https://localhost:7322";

	/// <summary>
	/// Should the system use local system to save and to load data ? (.JSON)
	/// False = MySQL
	/// </summary>
	public const bool SaveLocal = true;

	/// <summary>
	/// Holds all the tables that have been loaded by the system.
	/// </summary>
	private IEnumerable<ITable> _tables { get; set; }

	/// <summary>
	/// Holds all the tables that have been loaded by the system.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )]
	public List<TableBase> LoadedTables { get; set; } = new List<TableBase>();

	[Sync]
	[Property]
	public IEnumerable<CommandDTO> AvailableCommands { get; private set; }
	protected override void OnAwake()
	{
		base.OnAwake();

		if ( !Enabled )
			return;

		if ( !Networking.IsHost )
		{
			Log.Warning( "[DatabaseManager] Initialization attempted on client — skipping." );
			return;
		}

		Load();
		LoadCommands();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if ( !Enabled )
			return;

		if ( !Networking.IsHost )
		{
			Log.Warning( "[DatabaseManager] Destroy called on client — skipping save." );
			return;
		}

		Save();
	}

	/// <summary>
	/// Scans the system for types that implement the <see cref="ITable"/> interface and
	/// loads them. These tables are stored in the <see cref="LoadedTables"/> list.
	/// </summary>
	private void Load()
	{
		List<ITable> tables = new();

		// Étape 1: Scanner toutes les classes qui implémentent une interface ITable<T>
		foreach ( var type in TypeLibrary.GetTypes().Where( x => !x.IsAbstract ) )
		{
			// On vérifie si elles implémentent une ITable<T>
			foreach ( var iface in type.Interfaces.Where( x => x.IsGenericType && x.Name.StartsWith( "ITable" ) ) )
			{
				Log.Info( $"Table found {type}" );

				// On instancie dynamiquement la table
				var tableInstance = (ITable)TypeLibrary.Create( type.Name, type.TargetType );

				if ( tableInstance != null )
				{
					tables.Add( tableInstance );

					// On lance le chargement de la table (fichier JSON ou API)
					_ = tableInstance.Load();
				}
			}
		}

		// Stocker dans les propriétés centrales
		_tables = tables;
		LoadedTables = tables.Cast<TableBase>().ToList();

		// 3. Résolution des ForeignKey après TOUT avoir chargé
		foreach ( var table in tables )
		{
			if ( table is TableBase baseTable && baseTable is IForeignKeyResolvable fkTable )
			{
				fkTable.ResolveForeignKeys();
			}
		}
	}

	/// <summary>
	/// Save all tables and their data on the system.
	/// Expensive call !! (Use at with your own precaution)
	/// Should be called only when the server shutdowns or each X minutes to avoid data loses.
	/// </summary>
	public void Save()
	{
		foreach ( var table in _tables )
		{
			// Call the Save method on each table
			_ = table.Save();
		}
	}

	public static T Get<T>() where T : ITable
	{
		if ( Instance == null ) return default;

		T tableInstance = default;

		// Check if the current context is the host
		if ( Networking.IsHost )
		{
			// If it's the host, use _tables and try to find the table of type T
			tableInstance = Instance._tables.OfType<T>().FirstOrDefault();
		}
		else
		{
			// If it's not the host, use LoadedTables and try to find the table of type T
			tableInstance = Instance.LoadedTables.OfType<T>().FirstOrDefault();
		}

		return tableInstance;
	}


	public static IEnumerable<ITable> GetAll()
	{
		if ( Instance == null ) return default;

		if ( Networking.IsHost )
		{
			// Ensure that _tables can be cast to IEnumerable<ITable>
			return Instance._tables.Cast<ITable>();
		}
		else
		{
			// Ensure that LoadedTables can be treated as IEnumerable<ITable>
			return Instance.LoadedTables.OfType<ITable>();
		}
	}

	public static ITable GetTableByType( string typename )
	{
		if ( Instance == null ) return null;
		return Instance._tables.FirstOrDefault( t => t.TypeName == typename );
	}
	private void LoadCommands()
	{
		var table = DatabaseManager.Get<CommandsTable>();

		if ( table == null )
			return;

		AvailableCommands = table.GetAllRows();
	}
}
