using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Groovo.Models;

namespace Groovo.Data.Configurations;

public class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> builder)
    {
        builder.Property(sl => sl.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(sl => sl.IsRevoked)
            .HasDefaultValue(false);

        builder.HasOne(sl => sl.CreatedByUser)
            .WithMany(u => u.ShareLinks)
            .HasForeignKey(sl => sl.CreatedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
