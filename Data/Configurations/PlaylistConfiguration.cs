using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Groovo.Models;

namespace Groovo.Data.Configurations;

public class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        // Configure default values
        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);
            
        builder.Property(p => p.IsPublic)
            .HasDefaultValue(false);
            
        builder.Property(p => p.IsAlbum)
            .HasDefaultValue(false);
    }
}