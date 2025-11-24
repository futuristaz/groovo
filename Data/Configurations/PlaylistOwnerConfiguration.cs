using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Groovo.Models;

namespace Groovo.Data.Configurations;

public class PlaylistOwnerConfiguration : IEntityTypeConfiguration<PlaylistOwner>
{
    public void Configure(EntityTypeBuilder<PlaylistOwner> builder)
    {
        builder.HasOne(po => po.Playlist)
               .WithMany(p => p.PlaylistOwners)
               .HasForeignKey(po => po.PlaylistId)
               .OnDelete(DeleteBehavior.Cascade);
               
        builder.HasOne(po => po.User)
               .WithMany(u => u.PlaylistOwners)
               .HasForeignKey(po => po.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}