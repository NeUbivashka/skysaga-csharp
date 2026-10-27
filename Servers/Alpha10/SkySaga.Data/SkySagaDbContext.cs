using Microsoft.EntityFrameworkCore;
using SkySaga.Data.Entities;

namespace SkySaga.Data;

public class SkySagaDbContext(DbContextOptions<SkySagaDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<CharacterCurrency> CharacterCurrencies => Set<CharacterCurrency>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<CharacterJob> CharacterJobs => Set<CharacterJob>();
    public DbSet<ActiveChallenge> ActiveChallenges => Set<ActiveChallenge>();
    public DbSet<World> Worlds => Set<World>();
    public DbSet<VoxelEdit> VoxelEdits => Set<VoxelEdit>();
    public DbSet<PlacedDevice> PlacedDevices => Set<PlacedDevice>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<GameEvent> GameEvents => Set<GameEvent>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Account>(account =>
        {
            account.HasIndex(a => a.Username).IsUnique();
            account.Property(a => a.Username).HasMaxLength(64);
            account.Property(a => a.Role).HasConversion<string>().HasMaxLength(16);
            account.Property(a => a.PasswordHash).HasMaxLength(256);
            account.HasIndex(a => a.EmailIndex).IsUnique();
            account.Property(a => a.EmailHint).HasMaxLength(128);
        });

        model.Entity<AuthSession>(session =>
        {
            session.HasIndex(s => s.TokenHash).IsUnique();
            session.HasIndex(s => s.RefreshHash).IsUnique();
            session.HasIndex(s => s.AccountId);
            session.Property(s => s.Kind).HasConversion<string>().HasMaxLength(16);
            session.HasOne(s => s.Account).WithMany().HasForeignKey(s => s.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PasswordReset>(reset =>
        {
            reset.HasIndex(r => r.CodeHash).IsUnique();
            reset.HasOne(r => r.Account).WithMany().HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Character>(character =>
        {
            character.HasIndex(c => c.Name).IsUnique();
            character.Property(c => c.Name).HasMaxLength(64);
            character.HasOne(c => c.Account).WithMany(a => a.Characters).HasForeignKey(c => c.AccountId);
            character.OwnsOne(c => c.Settings, settings =>
            {
                settings.ToJson();
                settings.OwnsMany(s => s.Hotbar);
            });
            character.HasMany(c => c.Currencies).WithOne().HasForeignKey(c => c.CharacterId).OnDelete(DeleteBehavior.Cascade);
            character.HasMany(c => c.Inventory).WithOne().HasForeignKey(i => i.CharacterId).OnDelete(DeleteBehavior.Cascade);
            character.HasMany(c => c.Jobs).WithOne().HasForeignKey(j => j.CharacterId).OnDelete(DeleteBehavior.Cascade);
            character.HasMany(c => c.ActiveChallenges).WithOne().HasForeignKey(a => a.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<CharacterCurrency>().HasKey(c => new { c.CharacterId, c.NameHash });
        model.Entity<InventoryItem>().HasKey(i => new { i.CharacterId, i.Slot });
        model.Entity<CharacterJob>().HasKey(j => new { j.CharacterId, j.Name });
        model.Entity<ActiveChallenge>().HasKey(a => new { a.CharacterId, a.Name });
        model.Entity<ActiveChallenge>().Property(a => a.TodoTimestamp).HasPrecision(20, 0);

        model.Entity<World>(world =>
        {
            world.HasIndex(w => w.MapName);
            world.Property(w => w.MapName).HasMaxLength(128);
        });

        model.Entity<VoxelEdit>(edit =>
        {
            edit.HasKey(e => new { e.WorldId, e.X, e.Y, e.Z });
            edit.HasOne<World>().WithMany().HasForeignKey(e => e.WorldId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PlacedDevice>(device =>
        {
            device.HasIndex(d => d.WorldId);
            device.Property(d => d.Items).HasColumnType("jsonb");
            device.HasOne<World>().WithMany().HasForeignKey(d => d.WorldId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Checkpoint>(checkpoint =>
        {
            checkpoint.HasKey(c => new { c.WorldId, c.CharacterId });
            checkpoint.HasOne<World>().WithMany().HasForeignKey(c => c.WorldId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<GameEvent>(gameEvent =>
        {
            gameEvent.HasIndex(e => e.Time);
            gameEvent.HasIndex(e => new { e.CharacterId, e.Time });
            gameEvent.Property(e => e.Kind).HasMaxLength(64);
            gameEvent.Property(e => e.Data).HasColumnType("jsonb");
        });
    }
}
