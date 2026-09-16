using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.SeatAllocation;

public class SeatingPlanAuditLogConfiguration : IEntityTypeConfiguration<SeatingPlanAuditLog>
{
    public void Configure(EntityTypeBuilder<SeatingPlanAuditLog> builder)
    {
        builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "SeatingPlanAuditLogs", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ExamId).IsRequired();
        builder.Property(x => x.SeatingPlanId).IsRequired();
        builder.Property(x => x.Action).IsRequired();
        builder.Property(x => x.ActorUserId).IsRequired();
        builder.Property(x => x.ActorNameSnapshot).HasMaxLength(256).IsRequired(false);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired(false);
        builder.Property(x => x.AssignmentsSnapshotJson).HasColumnType("text").IsRequired(false);

        builder.HasIndex(x => x.ExamId);
        builder.HasIndex(x => new { x.ExamId, x.SeatingPlanId });
    }
}
