using System.Threading.Tasks;

namespace OpenFramework.Database;

public abstract class TableBase
{
	public abstract string TypeName { get; }
}

public abstract class Table<T> : TableBase, ITable<T>, IForeignKeyResolvable where T : ITableDTO
{
	public string Name { get; }
	public string EndPoint { get; }
	private List<T> Rows { get; set; }
	public int Count => Rows?.Count ?? 0;
	public bool SingleEntry { get; }
	public override string TypeName => typeof( T ).Name;

	private BaseFileSystem _fileSystem = FileSystem.Data;

	// Dictionary to store indexable properties
	private Dictionary<string, Func<T, object>> indexableProperties;

	// Dictionary to store unique properties
	private Dictionary<string, Func<T, object>> uniqueProperties;

	protected Table( string _name, string _endpoint, bool _singleEntry = false )
	{
		Name = _name;
		EndPoint = _endpoint;
		Rows = new List<T>();

		if ( _singleEntry )
			Rows.Capacity = 1;

		SingleEntry = _singleEntry;
	}

	public virtual async Task Load()
	{
		Log.Info( $"Table {Name} is being loaded." );
		//Server.AddLog( $"[{Name}] Loading table..", Logs.LogType.Database, Logs.LogLevel.Debug );

		if ( !DatabaseManager.SaveLocal )
		{
			//await Http.RequestJsonAsync<List<T>>( $"{DatabaseManager.ApiUrl}/{EndPoint}" );
		}
		else
		{
			string filePath = $"tables/{Name}.json";

			if ( !_fileSystem.DirectoryExists( "tables" ) )
				_fileSystem.CreateDirectory( "tables" );

			if ( !_fileSystem.FileExists( filePath ) )
				_fileSystem.WriteJson( filePath, Rows );
			else
				Rows = _fileSystem.ReadJsonOrDefault<List<T>>( filePath );
		}

		// Populate the indexable properties
		PopulateProperties();
		OnFinishLoad();
	}

	/// <summary>
	/// Populates the indexable and unique properties from the table rows using the <see cref="TablePropertyAttribute"/>.
	/// </summary>
	private void PopulateProperties()
	{
		Log.Info( "PopulateProperties" );

		indexableProperties = new Dictionary<string, Func<T, object>>();
		uniqueProperties = new Dictionary<string, Func<T, object>>();

		// Get the type of T using TypeLibrary
		var typeInfo = TypeLibrary.GetType( typeof( T ).Name );

		// Iterate over the properties and find indexable and unique ones
		foreach ( var member in typeInfo.Properties )
		{
			var attribute = member.GetCustomAttribute<TablePropertyAttribute>();
			if ( attribute != null )
			{
				// Store indexable properties
				if ( attribute.IsIndex )
					indexableProperties[member.Name] = ( instance ) => member.GetValue( instance );

				// Store unique properties
				if ( attribute.IsUnique )
					uniqueProperties[member.Name] = ( instance ) => member.GetValue( instance );
			}
		}
	}

	public void ResolveForeignKeys()
	{
		CheckForForeignReferences();
	}

	/// <summary>
	/// Vérifie toutes les propriétés marquées comme ForeignKey dans les entrées de cette table
	/// et lie automatiquement les objets référencés à partir des autres tables chargées.
	/// </summary>
	private void CheckForForeignReferences()
	{
		Log.Info( $"[FK] Vérification des références étrangères dans {TypeName}" );

		// Type runtime du DTO (ex: UserDTO, CharacterDTO)
		var typeInfo = TypeLibrary.GetType( typeof( T ).Name );

		// Parcourir chaque ligne de la table
		foreach ( var row in GetAllRows() )
		{
			// On parcourt les propriétés du DTO
			foreach ( var property in typeInfo.Properties )
			{
				// Vérifie si la propriété est décorée avec [ForeignKey]
				var attribute = property.GetCustomAttribute<ForeignKeyAttribute>();
				if ( attribute == null )
					continue;

				// Cherche la propriété "clé étrangère" associée, ex: CharacterId
				var foreignProperty = typeInfo.Properties.FirstOrDefault( p => p.Name == attribute.ForeignKeyName );
				if ( foreignProperty == null )
				{
					Log.Warning( $"[FK] Clé étrangère '{attribute.ForeignKeyName}' non trouvée pour {property.Name}" );
					continue;
				}

				// Récupère la valeur de la clé étrangère (ex: le GUID)
				var foreignValue = foreignProperty.GetValue( row );
				if ( foreignValue is not Guid guid || guid == Guid.Empty )
					continue;

				Log.Info( $"[FK] Clé étrangère trouvée: ({property.PropertyType.Name}){property.Name} -> {attribute.ForeignKeyName}(Id: {foreignValue})" );

				// {property.PropertyType.Name} est le type de la propriété foreign a chercher. (ex: CharacterDTO)

				var distantTable = DatabaseManager.GetTableByType( property.PropertyType.Name );
				distantTable.FindRowByIndexValue( foreignValue, out object distantRow );
				property.SetValue(row, distantRow );
				Log.Info( $"DistantTable {distantTable.Name} | Row {distantRow}" );

				Log.Info( row );
			}
		}
	}


	private bool IsSupportedSimpleType( TypeDescription type )
	{
		return type.TargetType == typeof( string )
			|| type.TargetType == typeof( Guid )
			|| type.TargetType.IsEnum
			|| type.TargetType == typeof( int )
			|| type.TargetType == typeof( float )
			|| type.TargetType == typeof( bool )
			|| type.TargetType == typeof( double );
	}


	public virtual async Task Save()
	{
		string filePath = $"tables/{Name}.json";

		if ( !_fileSystem.DirectoryExists( "tables" ) )
			_fileSystem.CreateDirectory( "tables" );

		// Assuming GetAllRows() is available
		if ( Rows != null )
		{
			_fileSystem.WriteJson( filePath, Rows );
			Log.Info( $"Table {Name} has been saved." );
		}
	}

	public IEnumerable<T> GetAllRows() => Rows; // Return rows as IEnumerable<T>
	IEnumerable<object> ITable.GetAllRows() => Rows.Cast<object>(); // Non-generic version for ITable

	public Dictionary<string, object> GetRowProperties( object row )
	{
		var propertyValues = new Dictionary<string, object>();

		if ( row is T typedRow ) // Ensure the row is of type T
		{
			var typeInfo = TypeLibrary.GetType( typeof( T ).Name );

			foreach ( var member in typeInfo.Properties )
			{
				var attribute = member.GetCustomAttribute<TablePropertyAttribute>();
				if ( attribute != null )
				{
					var propertyValue = member.GetValue( typedRow );
					propertyValues[member.Name] = propertyValue;
				}
			}
		}

		return propertyValues;
	}

	public virtual void InsertRow( T row )
	{
		// Check for unique constraints before adding
		if ( uniqueProperties != null )
		{
			foreach ( var uniquePropertyAccessor in uniqueProperties.Values )
			{
				var newValue = uniquePropertyAccessor( row );
				foreach ( var existingRow in Rows )
				{
					var existingValue = uniquePropertyAccessor( existingRow );

					if ( existingValue?.Equals( newValue ) == true )
					{
						Log.Error( $"Cannot insert row. A row with the same unique value already exists: {newValue}" );
						return;
					}
				}
			}
		}

		if ( SingleEntry && Rows.Count == 1 )
		{
			//Server.AddLog( $"[{Name}] Row {typeof( T ).Name} has been added", Logs.LogType.Database, Logs.LogLevel.Error );
			return;
		}

		Rows.Add( row );
		//Server.AddLog( $"[{Name}] Row {typeof( T ).Name} has been added", Logs.LogType.Database, Logs.LogLevel.Debug );
	}

	public virtual void DeleteRow( T row )
	{
		//Server.AddLog( $"[{Name}] Row {typeof( T ).Name} has been deleted", Logs.LogType.Database, Logs.LogLevel.Debug );
		Rows.Remove( row );

		//var result = await Http.RequestAsync( $"{Database.ApiUrl}/{EndPoint}", method: "DELETE" );
	}

	bool ITable.FindRowByIndexValue( object value, out object row )
	{
		var typedRow = FindRowByIndexValue( value );
		Log.Info( $"[FindRowByIndexValue] Searching row by {value}" );
		row = typedRow;
		return typedRow != null;
	}

	public virtual T FindRowByIndexValue( object value )
	{
		return Rows.FirstOrDefault( r =>
		{
			foreach ( var propertyAccessor in indexableProperties.Values )
			{
				try
				{
					var propertyValue = propertyAccessor( r );
					if ( propertyValue?.Equals( value ) == true )
						return true;
				}
				catch ( Exception ex )
				{
					Log.Error( $"[FindRowByIndexValue] Error accessing index property: {ex.Message}" );
					if ( ex.InnerException != null )
						Log.Error( $"[Inner Exception] {ex.InnerException.Message}" );
				}
			}
			return false;
		} );
	}

	public virtual bool FindRowByIndexValue( object value, out T row )
	{
		row = FindRowByIndexValue( value );
		return row != null;
	}

	public virtual bool ContainsRow( T row )
	{
		return Rows.Contains( row );
	}

	public virtual T FindById( Guid id )
	{
		return Rows.FirstOrDefault( x => x.Id == id );
	}

	protected virtual void OnFinishLoad()
	{
	}
}
