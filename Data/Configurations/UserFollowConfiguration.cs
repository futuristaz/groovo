using Groovo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Groovo.Data.Configurations;

public sealed class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> entity)
    {
        entity.HasKey(e => new { e.FollowerId, e.FollowedId });

        entity.HasOne(e => e.Follower)
            .WithMany()
            .HasForeignKey(e => e.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Followed)
            .WithMany()
            .HasForeignKey(e => e.FollowedId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.FollowerId);

        entity.HasIndex(e => e.FollowedId);
    }
}