using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Groovo.Models;

namespace Groovo.Data.Configurations;

public class PlaylistSongConfiguration : IEntityTypeConfiguration<PlaylistSong>
{
    public void Configure(EntityTypeBuilder<PlaylistSong> builder)
    {
        builder.HasIndex(ps => ps.Order);

        builder.Property(ps => ps.AddedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");


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