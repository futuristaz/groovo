using Microsoft.EntityFrameworkCore;
using Groovo.Models;

namespace Groovo.Data.Contexts;

public sealed class ApplicationDbContext : DbContext
{

    public DbSet<Song> Songs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<SongAuthor> SongAuthors { get; set; }
    public DbSet<PlaylistSong> PlaylistSongs { get; set; }
    public DbSet<PlaylistOwner> PlaylistOwners { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatically apply all entity configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            UpdateTimestamps();
        }
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            UpdateTimestamps();
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType();
            
            if (entityType.GetProperty("CreatedAt") != null && 
                entityType.GetProperty("UpdatedAt") != null)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
                }
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
            
            if (entry.Entity is PlaylistSong && entry.State == EntityState.Added)
            {
                entry.Property("AddedAt").CurrentValue = DateTime.UtcNow;
            }
        }
    }
}