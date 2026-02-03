using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Groups;

namespace ShikshaMitra.EntityFrameworkCore.Groups;

/// <summary>
/// EF Core configuration for GroupStudentRule entity
/// </summary>
public class GroupStudentRuleConfiguration : IEntityTypeConfiguration<GroupStudentRule>
{
    public void Configure(EntityTypeBuilder<GroupStudentRule> builder)
    {
        builder.ToTable($"{ShikshaMitraConsts.DbTablePrefix}GroupStudentRules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GroupId)
            .IsRequired();

        builder.Property(x => x.SelectionType)
            .IsRequired();

        builder.Property(x => x.Field)
            .IsRequired();

        builder.Property(x => x.Operator)
            .IsRequired();

        builder.Property(x => x.Value)
            .IsRequired()
            .HasMaxLength(500);

        // Index for faster lookups
        builder.HasIndex(x => new { x.GroupId, x.SelectionType })
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}GroupStudentRules_GroupId_SelectionType");
    }
}
