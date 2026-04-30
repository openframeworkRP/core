using Facepunch;

namespace OpenFramework.Utility;

public static class ObjectEffects
{
	public static void BuyEffect(Client client, GameObject ontarget = null, bool network = false)
	{
		var moneyEffect = GameObject.GetPrefab( "particles/gameplay/money/money_burst.prefab" );
		moneyEffect.Parent = ontarget;

		if ( network )
			moneyEffect.NetworkSpawn( client.Connection );

		client.PlaySound( "sounds/ui/buy.sound" );
	}
}
