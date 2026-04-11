using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Groups;

namespace ShikshaMitra.EntityFrameworkCore.Groups;

/// <summary>
/// EF Core configuration for GroupEmployeeRule entity
/// </summary>
public class GroupEmployeeRuleConfiguration : IEntityTypeConfiguration<GroupEmployeeRule>
{
    public void Configure(EntityTypeBuilder<GroupEmployeeRule> builder)
    {
        builder.ToTable($"{ShikshaMitraConsts.DbTablePrefix}GroupEmployeeRules");

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
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}GroupEmployeeRules_GroupId_SelectionType");
    }
}
