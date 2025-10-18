using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Groovo.Models;

namespace Groovo.Data.Configurations;

public class PlaylistSongConfiguration : IEntityTypeConfiguration<PlaylistSong>
{
    public void Configure(EntityTypeBuilder<PlaylistSong> builder)
    {
        // Configure index for ordering
        builder.HasIndex(ps => ps.Order);

        // Configure relationships
        builder.HasOne(ps => ps.Playlist)
               .WithMany(p => p.PlaylistSongs)
               .HasForeignKey(ps => ps.PlaylistId)
               .OnDelete(DeleteBehavior.Cascade);
               
        builder.HasOne(ps => ps.Song)
               .WithMany(s => s.PlaylistSongs)
               .HasForeignKey(ps => ps.SongId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}