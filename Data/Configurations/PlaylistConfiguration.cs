using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Groovo.Models;

namespace Groovo.Data.Configurations;

public class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        builder.Property(p => p.IsActive)
            .HasDefaultValue(false);
            
        builder.Property(p => p.IsPublic)
            .HasDefaultValue(false);
            
        builder.Property(p => p.IsAlbum)
            .HasDefaultValue(false);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}