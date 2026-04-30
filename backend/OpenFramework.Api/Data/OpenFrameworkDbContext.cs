using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Models;
using OpenFramework.Api.Models.Administration;
using OpenFramework.Api.Models.Administration.Logs;
using OpenFramework.Api.Models.Mdt;

namespace OpenFramework.Api.Data;

public class OpenFrameworkDbContext : DbContext
{
    public OpenFrameworkDbContext(DbContextOptions<OpenFrameworkDbContext> options) : base(options)
    {
        
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasOne(t => t.FromAccount)
                .WithMany(a => a.OutgoingTransactions)
                .HasForeignKey(t => t.FromAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
 
            entity.HasOne(t => t.ToAccount)
                .WithMany(a => a.IncomingTransactions)
                .HasForeignKey(t => t.ToAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
 
            entity.HasOne(t => t.Atm)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AtmId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });
 
        modelBuilder.Entity<AccountAccess>(entity =>
        {
            entity.HasOne(a => a.Account)
                .WithMany(b => b.Accesses)
                .HasForeignKey(a => a.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<InventoryItem>()
            .Property(e => e.Metadata)
            .HasConversion(
                v => JsonSerializer.Serialize(v , JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions.Default)
            );
    }
    public DbSet<Server> Servers { get; set; }
    public DbSet<Character> Characters { get; set; }
    public DbSet<CharacterPosition> CharacterPositions { get; set; }
    public DbSet<InventoryItem> Items { get; set; }
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Cloth> Cloths { get; set; }
    public DbSet<AccountAccess> AccountAccesses { get; set; }
    public DbSet<BankAccount> BankAccounts { get; set; }
    public DbSet<AtmMachine> AtmMachines { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<UserBan> Bans { get; set; }
    public DbSet<UserWarn> Warns { get; set; }
    public DbSet<UserWhitelist> Whitelists { get; set; }
    public DbSet<AdminActionLog> AdminActionLogs { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<ChatLog> ChatLogs { get; set; }
    public DbSet<InventoryLog> InventoryLogs { get; set; }
    public DbSet<CriminalRecord> CriminalRecords { get; set; }
    public DbSet<PendingAdminCommand> PendingAdminCommands { get; set; }
    
}