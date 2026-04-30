using Sandbox;
using Sandbox.Events;
using OpenFramework.Api;
using OpenFramework.Systems.Pawn;
using OpenFramework.Utility;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenFramework.Systems.AtmSystem;

/// <summary>
/// Composant ATM — un par machine dans la scène.
/// Pas de lock exclusif : plusieurs joueurs peuvent utiliser le même ATM en même temps,
/// l'UI est client-local et l'API backend serialise les opérations (UPDLOCK par compte).
/// </summary>
public class AtmComponent : Component, IGameEventHandler<PlayerDisconnectedEvent>
{
    [Property] public string AtmId { get; set; } = "";
    [Property] public string Label { get; set; } = "ATM";

    /// <summary>Distance max (units) serveur pour autoriser une opération.</summary>
    [Property] public float MaxUseDistance { get; set; } = 120f;

    /// <summary>Prefab du billet à faire sortir/entrer lors d'un dépôt/retrait (cosmétique, client-side).</summary>
    [Property] public GameObject MoneyPrefab { get; set; }

    /// <summary>Position locale du slot à billets par rapport à l'ATM (où le billet apparaît).</summary>
    [Property] public Vector3 CashSlotLocalOffset { get; set; } = new Vector3( 8f, 0f, 30f );

    /// <summary>Durée avant destruction auto du billet spawné.</summary>
    [Property] public float CashEffectDuration { get; set; } = 3f;

    /// <summary>Délai minimum (s) entre deux opérations dépôt/retrait/virement pour un même joueur (anti-spam / anti-dupe).</summary>
    [Property] public float MinOperationInterval { get; set; } = 1.5f;

    // Callbacks d'INSTANCE — chaque ATM a les siens
    public Action<BankAccountDto, List<TransactionDto>> OnAtmDataReceived { get; set; }
    public Action<bool, string>                          OnOperationResult { get; set; }
    public Action                                        OnOpenRequested   { get; set; }

    // Rate-limit serveur : dernière opération dépôt/retrait/virement par joueur
    private readonly Dictionary<Guid, RealTimeSince> _lastOpByCaller = new();
    // Garde-fou anti-réentrance : opération en cours par joueur (évite spam pendant l'await API)
    private readonly HashSet<Guid> _opInFlight = new();

    // ══════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════

    protected override void OnStart()
    {
        base.OnStart();
        if ( Networking.IsHost && string.IsNullOrEmpty( AtmId ) )
        {
            AtmId = GameObject.Id.ToString();
            Log.Info( $"[ATM] AtmId auto-provisionné: {AtmId}" );
        }
    }

    /*protected override void DrawGizmos()
    {
        // Dessine en espace local (Gizmo respecte la transform courante)
        var pos = CashSlotLocalOffset;

        Gizmo.Draw.Color = Color.Yellow;
        Gizmo.Draw.LineSphere( pos, 2f );

        // Flèche indiquant la direction d'éjection du billet (forward local = X+)
        Gizmo.Draw.Color = new Color( 1f, 0.42f, 0f );
        Gizmo.Draw.Arrow( pos, pos + Vector3.Forward * 12f, 2f, 1f );

        Gizmo.Draw.Color = Color.White;
        Gizmo.Draw.Text( "Cash slot", new Transform( pos + Vector3.Up * 4f ), size: 14 );
    }*/

    public void OnGameEvent( PlayerDisconnectedEvent eventArgs )
    {
        if ( !Networking.IsHost ) return;
        if ( eventArgs.Player?.Connection == null ) return;

        var id = eventArgs.Player.Connection.Id;
        _opInFlight.Remove( id );
        _lastOpByCaller.Remove( id );
    }

    // ══════════════════════════════════════════════════════════════
    //  OUVRIR L'ATM
    // ══════════════════════════════════════════════════════════════

    [Rpc.Host]
    private async void Server_RequestOpen()
    {
        var caller = Rpc.Caller;
        Log.Info( $"[ATM][{AtmId}] Open demandé par {caller.DisplayName} ({caller.SteamId})" );

        try
        {
            if ( !IsCallerInRange( caller, out var distErr ) )
            {
                using ( Rpc.FilterInclude( caller ) )
                    Client_OperationResult( false, distErr );
                return;
            }

            var active      = await ApiComponent.Instance.GetActiveCharacter( caller.SteamId );
            var characterId = active?.Id;

            if ( string.IsNullOrEmpty( characterId ) )
            {
                Log.Warning( $"[ATM][{AtmId}] Refusé : aucun personnage actif pour {caller.DisplayName}" );
                using ( Rpc.FilterInclude( caller ) )
                    Client_OperationResult( false, "Aucun personnage actif." );
                return;
            }

            if ( !ApiComponent.Instance.IsServerAuthenticated )
            {
                Log.Warning( $"[ATM][{AtmId}] Serveur non auth — attente 1s..." );
                await Task.DelayRealtimeSeconds( 1f );
                if ( !ApiComponent.Instance.IsServerAuthenticated )
                {
                    Log.Error( $"[ATM][{AtmId}] Refusé : serveur toujours non authentifié" );
                    using ( Rpc.FilterInclude( caller ) )
                        Client_OperationResult( false, "Serveur non authentifié." );
                    return;
                }
            }

            await Server_RefreshAtmData( caller, characterId );
        }
        catch ( Exception ex )
        {
            Log.Error( $"[ATM][{AtmId}] Exception dans Server_RequestOpen pour {caller.DisplayName}: {ex.Message}" );
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, "Erreur serveur lors de l'ouverture." );
        }
    }

    /// <summary>Vérifie côté serveur que le caller est bien à portée de l'ATM.</summary>
    private bool IsCallerInRange( Connection caller, out string error )
    {
        var pawn = Scene.GetAllComponents<PlayerPawn>().FirstOrDefault( p => p.Network.Owner == caller );
        if ( pawn == null ) { error = ""; return true; }

        var dist = pawn.WorldPosition.Distance( WorldPosition );
        if ( dist > MaxUseDistance )
        {
            Log.Info( $"[ATM][{AtmId}] Refusé : {caller.DisplayName} trop loin ({dist:F0}u > {MaxUseDistance}u)" );
            error = "Trop loin de l'ATM.";
            return false;
        }
        error = "";
        return true;
    }

    private async Task Server_RefreshAtmData( Connection caller, string characterId )
    {
        var api     = ApiComponent.Instance;
        var account = await api.GetBankAccount( characterId );

        if ( account == null )
        {
            Log.Error( $"[ATM][{AtmId}] Compte bancaire introuvable (char: {characterId})" );
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, "Compte bancaire introuvable." );
            return;
        }

        var transactions = await api.GetTransactions( account.Id ) ?? new List<TransactionDto>();

        Log.Info( $"[ATM][{AtmId}] Data → {caller.DisplayName} : compte {account.AccountNumber} (balance API={account.Balance:F2}$), {transactions.Count} dernières transactions" );

        var json1 = JsonSerializer.Serialize( account );
        var json2 = JsonSerializer.Serialize( transactions );

        using ( Rpc.FilterInclude( caller ) )
            Client_ReceiveAtmData( json1, json2 );
    }


    [Rpc.Broadcast]
    private void Client_ReceiveAtmData( string accountJson, string transactionsJson )
    {
	    var opts    = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
	    var account = JsonSerializer.Deserialize<BankAccountDto>( accountJson, opts );
	    var txs     = JsonSerializer.Deserialize<List<TransactionDto>>( transactionsJson, opts )
	                  ?? new List<TransactionDto>();

	    OnAtmDataReceived?.Invoke( account, txs );
    }

    // ══════════════════════════════════════════════════════════════
    //  MÉTHODES PUBLIQUES APPELÉES PAR AtmUI
    // ══════════════════════════════════════════════════════════════

    /// <summary>Demande l'ouverture de l'ATM côté serveur.</summary>
    public void Open()
    {
        OnOpenRequested?.Invoke();
        Server_RequestOpen();
    }

    /// <summary>Ferme l'ATM côté client (pas de lock serveur — pur UI).</summary>
    public void Close() { /* no-op serveur : plus de lock */ }

    /// <summary>Dépôt cash → compte</summary>
    public void Deposit( string accountId, string characterId, decimal amount )
        => Server_Deposit( accountId, characterId, (float)amount );

    /// <summary>Retrait compte → cash</summary>
    public void Withdraw( string accountId, string characterId, decimal amount )
        => Server_Withdraw( accountId, characterId, (float)amount );

    /// <summary>Virement compte → compte (toAccountNumber = numéro affiché, pas l'UUID)</summary>
    public void Transfer( string fromAccountId, string toAccountNumber, string characterId, decimal amount, string comment )
        => Server_Transfer( fromAccountId, toAccountNumber, characterId, (float)amount, comment ?? "" );

    // ══════════════════════════════════════════════════════════════
    //  DÉPÔT
    // ══════════════════════════════════════════════════════════════

    [Rpc.Host]
    private async void Server_Deposit( string accountId, string characterId, float amount )
    {
        var caller = Rpc.Caller;
        Log.Info( $"[ATM][{AtmId}] Dépôt demandé par {caller.DisplayName} : {amount}$ (compte: {accountId})" );

        if ( !IsCallerInRange( caller, out var distErr ) )
        {
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, distErr );
            return;
        }

        if ( amount <= 0 )
        {
            Log.Warning( $"[ATM][{AtmId}] Dépôt refusé : montant invalide ({amount})" );
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, "Montant invalide." );
            return;
        }

        if ( !TryStartOperation( caller, "Dépôt", out var rateError ) )
        {
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, rateError );
            return;
        }

        try
        {
            var client = Scene.GetAllComponents<Client>().FirstOrDefault( c => c.Connection == caller );

            if ( client == null ) { Log.Error( $"[ATM][{AtmId}] Client introuvable pour {caller.DisplayName} !" ); return; }

            if ( !MoneySystem.CanAfford( client, (int)amount ) )
            {
                Log.Info( $"[ATM][{AtmId}] Dépôt refusé : pas assez de cash sur {caller.DisplayName}" );
                using ( Rpc.FilterInclude( caller ) )
                    Client_OperationResult( false, "Pas assez d'argent sur vous." );
                return;
            }

            var result = await ApiComponent.Instance.AtmDeposit( AtmId, accountId, characterId, (decimal)amount );
            var ok     = result?.Success ?? false;
            var error  = result?.Error ?? "Erreur inconnue";

            if ( ok )
            {
                MoneySystem.Remove( client, (int)amount );
                Client_PlayCashEffect( true );
                Log.Info( $"[ATM][{AtmId}] Dépôt OK : {caller.DisplayName} -{amount}$ cash → compte {accountId}" );
            }
            else
            {
                Log.Warning( $"[ATM][{AtmId}] Dépôt API KO : {error}" );
            }

            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( ok, ok ? "" : error );

            if ( ok )
            {
                try { await Server_RefreshAtmData( caller, characterId ); }
                catch ( Exception ex ) { Log.Warning( $"[ATM][{AtmId}] Refresh post-dépôt KO: {ex.Message}" ); }
            }
        }
        finally
        {
            EndOperation( caller );
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  RETRAIT
    // ══════════════════════════════════════════════════════════════

    [Rpc.Host]
    private async void Server_Withdraw( string accountId, string characterId, float amount )
    {
        var caller = Rpc.Caller;
        Log.Info( $"[ATM][{AtmId}] Retrait demandé par {caller.DisplayName} : {amount}$ (compte: {accountId})" );

        if ( !IsCallerInRange( caller, out var distErr ) )
        {
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, distErr );
            return;
        }

        if ( amount <= 0 )
        {
            Log.Warning( $"[ATM][{AtmId}] Retrait refusé : montant invalide ({amount})" );
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, "Montant invalide." );
            return;
        }

        if ( !TryStartOperation( caller, "Retrait", out var rateError ) )
        {
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, rateError );
            return;
        }

        try
        {
            var client = Scene.GetAllComponents<Client>().FirstOrDefault( c => c.Connection == caller );

            if ( client == null ) { Log.Error( $"[ATM][{AtmId}] Client introuvable pour {caller.DisplayName} !" ); return; }

            var account = await ApiComponent.Instance.GetBankAccount( characterId );
            if ( account == null || account.Id != accountId )
            {
                Log.Warning( $"[ATM][{AtmId}] Retrait refusé : compte introuvable ou non détenu (account={accountId}, char={characterId})" );
                using ( Rpc.FilterInclude( caller ) )
                    Client_OperationResult( false, "Compte introuvable." );
                return;
            }

            // Pré-check cote game : l'API est de toute facon autoritaire (UPDLOCK + vérif solde)
            if ( account.Balance < (decimal)amount )
            {
                Log.Info( $"[ATM][{AtmId}] Retrait refusé : solde insuffisant (balance={account.Balance:F2}$ < {amount}$)" );
                using ( Rpc.FilterInclude( caller ) )
                    Client_OperationResult( false, "Solde insuffisant." );
                return;
            }

            var result = await ApiComponent.Instance.AtmWithdrawal( AtmId, accountId, characterId, (decimal)amount );
            var ok     = result?.Success ?? false;
            var error  = result?.Error ?? "Erreur inconnue";

            if ( ok )
            {
                MoneySystem.Add( client, (int)amount );
                Client_PlayCashEffect( false );
                Log.Info( $"[ATM][{AtmId}] Retrait OK : compte {accountId} → {caller.DisplayName} +{amount}$ cash" );
            }
            else
            {
                Log.Warning( $"[ATM][{AtmId}] Retrait API KO : {error}" );
            }

            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( ok, ok ? "" : error );

            if ( ok )
            {
                try { await Server_RefreshAtmData( caller, characterId ); }
                catch ( Exception ex ) { Log.Warning( $"[ATM][{AtmId}] Refresh post-retrait KO: {ex.Message}" ); }
            }
        }
        finally
        {
            EndOperation( caller );
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  VIREMENT
    // ══════════════════════════════════════════════════════════════

    [Rpc.Host]
    private async void Server_Transfer( string fromAccountId, string toAccountNumber, string characterId, float amount, string comment )
    {
        var caller = Rpc.Caller;
        Log.Info( $"[ATM][{AtmId}] Virement demandé par {caller.DisplayName} : {amount}$ (de {fromAccountId} vers #{toAccountNumber})" );

        if ( !IsCallerInRange( caller, out var distErr ) )
        {
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, distErr );
            return;
        }

        if ( amount <= 0 )
        {
            Log.Warning( $"[ATM][{AtmId}] Virement refusé : montant invalide ({amount})" );
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, "Montant invalide." );
            return;
        }

        if ( string.IsNullOrWhiteSpace( toAccountNumber ) )
        {
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, "Numéro de compte destinataire requis." );
            return;
        }

        if ( !TryStartOperation( caller, "Virement", out var rateError ) )
        {
            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( false, rateError );
            return;
        }

        try
        {
            var account = await ApiComponent.Instance.GetBankAccount( characterId );
            if ( account == null || account.Id != fromAccountId )
            {
                Log.Warning( $"[ATM][{AtmId}] Virement refusé : compte source non détenu (from={fromAccountId}, char={characterId})" );
                using ( Rpc.FilterInclude( caller ) )
                    Client_OperationResult( false, "Compte source introuvable." );
                return;
            }

            if ( account.AccountNumber == toAccountNumber )
            {
                Log.Info( $"[ATM][{AtmId}] Virement refusé : self-transfer ({toAccountNumber})" );
                using ( Rpc.FilterInclude( caller ) )
                    Client_OperationResult( false, "Virement vers soi-même impossible." );
                return;
            }

            if ( account.Balance < (decimal)amount )
            {
                Log.Info( $"[ATM][{AtmId}] Virement refusé : solde insuffisant (balance={account.Balance:F2}$ < {amount}$)" );
                using ( Rpc.FilterInclude( caller ) )
                    Client_OperationResult( false, "Solde insuffisant." );
                return;
            }

            var result = await ApiComponent.Instance.AtmTransfer( AtmId, fromAccountId, toAccountNumber, characterId, (decimal)amount, comment );
            var ok     = result?.Success ?? false;
            var error  = result?.Error ?? "Erreur inconnue";

            if ( ok )
            {
                Log.Info( $"[ATM][{AtmId}] Virement OK : {caller.DisplayName} {fromAccountId} → #{toAccountNumber} : {amount}$" );
            }
            else
            {
                Log.Warning( $"[ATM][{AtmId}] Virement API KO : {error}" );
            }

            using ( Rpc.FilterInclude( caller ) )
                Client_OperationResult( ok, ok ? "" : error );

            if ( ok )
            {
                try { await Server_RefreshAtmData( caller, characterId ); }
                catch ( Exception ex ) { Log.Warning( $"[ATM][{AtmId}] Refresh post-virement KO: {ex.Message}" ); }
            }
        }
        finally
        {
            EndOperation( caller );
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  RATE-LIMIT — anti-spam serveur
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Vérifie qu'aucune opération n'est en cours pour ce caller (anti-réentrance pendant l'await API),
    /// et que le délai mini entre deux opérations est respecté. Marque l'opération comme en vol si OK.
    /// </summary>
    private bool TryStartOperation( Connection caller, string label, out string error )
    {
        if ( _opInFlight.Contains( caller.Id ) )
        {
            Log.Warning( $"[ATM][{AtmId}] {label} refusé : opération déjà en cours pour {caller.DisplayName}" );
            error = "Opération déjà en cours, attendez...";
            return false;
        }

        if ( _lastOpByCaller.TryGetValue( caller.Id, out var since ) && since < MinOperationInterval )
        {
            var wait = MinOperationInterval - (float)since;
            Log.Info( $"[ATM][{AtmId}] {label} rate-limited pour {caller.DisplayName} (réessaye dans {wait:F2}s)" );
            error = "Trop rapide, patientez un instant.";
            return false;
        }

        _opInFlight.Add( caller.Id );
        error = "";
        return true;
    }

    private void EndOperation( Connection caller )
    {
        _opInFlight.Remove( caller.Id );
        _lastOpByCaller[caller.Id] = 0f;
    }

    // ══════════════════════════════════════════════════════════════
    //  RÉSULTAT → CLIENT
    // ══════════════════════════════════════════════════════════════

    [Rpc.Broadcast]
    private void Client_OperationResult( bool success, string error )
    {
        OnOperationResult?.Invoke( success, error );
    }

    // ══════════════════════════════════════════════════════════════
    //  EFFET COSMÉTIQUE — billet qui sort/rentre
    // ══════════════════════════════════════════════════════════════

    [Rpc.Broadcast]
    private void Client_PlayCashEffect( bool isDeposit )
    {
        if ( MoneyPrefab == null )
        {
            Log.Warning( $"[ATM] MoneyPrefab non assigné sur '{GameObject.Name}' → pas d'effet visuel. Assigne Assets/prefabs/props/money.prefab dans l'éditeur." );
            return;
        }

        var slotWorldPos = WorldTransform.PointToWorld( CashSlotLocalOffset );
        var forward      = WorldRotation.Forward;

        var money = MoneyPrefab.Clone( slotWorldPos, WorldRotation );
        if ( money == null ) return;

        // Pas de physique : on neutralise gravité + collisions pour faire une translation pure
        var rb = money.Components.Get<Rigidbody>();
        if ( rb != null ) rb.Enabled = false;

        foreach ( var col in money.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
            col.Enabled = false;

        var dir    = isDeposit ? -forward : forward;
        var mover  = money.Components.Create<AtmCashMover>();
        mover.Velocity = dir * 60f;

        money.DestroyAsync( CashEffectDuration );
    }
}

/// <summary>
/// Composant cosmétique : déplace le GameObject en ligne droite à vélocité constante,
/// sans physique ni gravité. Utilisé pour l'anim de billet ATM (dépôt/retrait).
/// </summary>
public sealed class AtmCashMover : Component
{
    [Property] public Vector3 Velocity { get; set; }

    protected override void OnUpdate()
    {
        WorldPosition += Velocity * Time.Delta;
    }
}
