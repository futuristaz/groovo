using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Groovo.Models;

namespace Groovo.Data.Configurations;

public class SongAuthorConfiguration : IEntityTypeConfiguration<SongAuthor>
{
    public void Configure(EntityTypeBuilder<SongAuthor> builder)
    {
        builder.HasOne(sa => sa.Song)
               .WithMany(s => s.SongAuthors)
               .HasForeignKey(sa => sa.SongId)
               .OnDelete(DeleteBehavior.Cascade);
               
        builder.HasOne(sa => sa.User)
               .WithMany(u => u.SongAuthors)
               .HasForeignKey(sa => sa.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}