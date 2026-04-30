using Sandbox;
using System.Collections.Generic;
using System.Linq;
public sealed class DresserGeneratorNpc : Component
{
	[Property]
	public List<Clothing> Clothing { get; set; } = new List<Clothing>();
	/*
	[Property, Title( "Dresser Component" )] public Dresser DresserPlayer { get; set; }
	public void AddClothingList()
	{
		if ( DresserPlayer != null )
		{
			foreach ( var item in Clothing )
			{
				Log.Info( "Listes vetement " + item );
				DresserPlayer.Clothing.AddRange( item );
			}

		}


	}*/
}
