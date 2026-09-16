using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.SeatAllocation;

public class ExamSeatingPlanConfiguration : IEntityTypeConfiguration<ExamSeatingPlan>
{
    public void Configure(EntityTypeBuilder<ExamSeatingPlan> builder)
    {
        builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "ExamSeatingPlans", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ExamId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ScopeType).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.SessionKey).HasMaxLength(80).IsRequired(false);
        builder.Property(x => x.RoomIdsCsv).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.AllocationRulesJson).HasMaxLength(2000).IsRequired(false);
        builder.Property(x => x.ExcludedStudentIdsCsv).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.SpecialRequirementStudentIdsCsv).HasMaxLength(4000).IsRequired(false);
        builder.Property(x => x.IsLocked).IsRequired();

        builder.HasIndex(x => x.ExamId);
        builder.HasIndex(x => new { x.TenantId, x.ExamId, x.SessionKey }).IsUnique();
    }
}
