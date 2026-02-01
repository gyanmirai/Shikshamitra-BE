using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Groups;

namespace ShikshaMitra.EntityFrameworkCore.Groups;

/// <summary>
/// EF Core configuration for GroupMember entity
/// </summary>
public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable($"{ShikshaMitraConsts.DbTablePrefix}GroupMembers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GroupId)
            .IsRequired();

        builder.Property(x => x.MemberType)
            .IsRequired();

        builder.Property(x => x.MemberId)
            .IsRequired();

        builder.Property(x => x.UserId);

        // Indices for faster lookups
        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}GroupMembers_GroupId");

        builder.HasIndex(x => new { x.GroupId, x.MemberType, x.MemberId })
            .IsUnique()
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}GroupMembers_Unique");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}GroupMembers_UserId");
    }
}
