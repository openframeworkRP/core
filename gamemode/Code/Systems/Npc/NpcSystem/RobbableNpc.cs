using Facepunch;
using Sandbox.Citizen;
using OpenFramework;
using OpenFramework.Inventory;
using OpenFramework.Systems.Jobs;

public class RobbableNpc : Component, IFocusedByWeapon
{
    /// <summary>Montant en cash droppé lors du braquage.</summary>
    [Property] public int MoneyAmount { get; set; } = 50;
    
    [Property] public GameObject MoneyItem { get; set; }
 
    /// <summary>Cooldown en secondes entre deux braquages.</summary>
    [Property] public float RobberyCooldown { get; set; } = 60f;
 
    private SkinnedModelRenderer _skinnedModelRenderer;
    private bool _isBeingRobbed   = false;
    private bool _hasBeenRobbed   = false;
    private RealTimeSince _lastRobbery;
 
    protected override void OnAwake()
    {
	    _skinnedModelRenderer = GameObject.Components.GetInChildren<SkinnedModelRenderer>();
    }
 
    // ──────────────────────────────────────────────────────────────
    //  IFocusedByWeapon
    // ──────────────────────────────────────────────────────────────
 
    public void OnFocusedByWeapon( PlayerPawn player )
    {
        if ( _isBeingRobbed ) return;
        if ( player.Client.Data.Job == "police" )
        {
	        player.Client.Notify( NotificationSystem.NotificationType.Info, "Rangez votre arme, vous faites peur...");
	        return;
        }
        
        var policeJob = JobSystem.GetJob( "police" );
        if ( policeJob == null || policeJob.Employees == null || policeJob.Employees.Count < 2 )
        {
	        player.Client.Notify( NotificationSystem.NotificationType.Error , "Il n'y a pas assez d'agent de Police pour braquer l'épicier." );
	        return;
        }
        
        
        if ( _hasBeenRobbed && _lastRobbery < RobberyCooldown )
        {
	        player.Client.Notify( NotificationSystem.NotificationType.Error , $"Attendez encore {_lastRobbery - RobberyCooldown} avant de pouvoir rebraquer ce pauvre homme." );
	        return;
        }
 
        _isBeingRobbed = true;
        _hasBeenRobbed = true;
        _lastRobbery   = 0f;
 
        Log.Info( $"[RobbableNpc] {GameObject.Name} se fait braquer par {player.Client?.DisplayName}" );
        
        // Reuse policeJob du early-check ci-dessus (non-null garanti puisqu'on n'a pas return)
        foreach ( var policeMan in policeJob.Employees )
        {
	        policeMan.Notify( NotificationSystem.NotificationType.Info, "Il y a un epicier qui se fait braquer !" );
        }
 
        // Animation mains levées sur tous les clients
        PlayHandsUpAnimation();
 
        // Drop l'argent côté host
        DropMoney();
    }
 
    public void OnFocusLost( PlayerPawn player )
    {
        if ( !_isBeingRobbed ) return;
        _isBeingRobbed = false;
 
        // Reprend l'animation normale
        PlayIdleAnimation();
    }
 
    // ──────────────────────────────────────────────────────────────
    //  ANIMATIONS (broadcast → tous les clients voient)
    // ──────────────────────────────────────────────────────────────
 
    [Rpc.Broadcast]
    private void PlayHandsUpAnimation()
    {
        if ( _skinnedModelRenderer == null ) return;
 
        // Citizen Animation Helper : HoldType.HoldItem lève les bras
        _skinnedModelRenderer.Set("thrill", 1);
    }
 
    [Rpc.Broadcast]
    private void PlayIdleAnimation()
    {
	    if ( _skinnedModelRenderer == null ) return;
	    _skinnedModelRenderer.Set("thrill", 0);
    }
 
    // ──────────────────────────────────────────────────────────────
    //  DROP ARGENT (host uniquement)
    // ──────────────────────────────────────────────────────────────
 
    private async void DropMoney()
    {
	    if ( !Networking.IsHost ) return;

	    var forward = WorldRotation.Forward;
	    var right   = WorldRotation.Right;
	    var count   = Game.Random.Next( 12, 17 );

	    for ( var t = 0; t < count; t++ )
	    {
		    var offset = forward * Game.Random.Float( 30f, 80f )
		                 + right   * Game.Random.Float( -40f, 40f )
		                 + Vector3.Up * 70f;

		    var spawnTransform = new Transform( WorldPosition + offset );
		    var money          = MoneyItem.Clone( spawnTransform );

		    // ✅ NetworkSpawn pour que les clients voient l'objet
		    money.NetworkSpawn();

		    var item = money.GetComponent<InventoryItem>(  );
		    if ( item != null )
			    item.Quantity = MoneyAmount;
		    else
			    Log.Warning( "[RobbableNpc] InventoryItem non trouvé sur MoneyItem prefab !" );

		    await Task.DelayRealtimeSeconds( 0.15f );
	    }
    }
}
