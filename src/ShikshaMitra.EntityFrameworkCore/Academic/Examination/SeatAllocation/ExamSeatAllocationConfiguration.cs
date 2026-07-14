using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.SeatAllocation;

public class ExamSeatAllocationConfiguration : IEntityTypeConfiguration<ExamSeatAllocation>
{
    public void Configure(EntityTypeBuilder<ExamSeatAllocation> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamSeatAllocations", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.SeatingPlanId).IsRequired();
        builder.Property(x => x.ExamId).IsRequired();
        builder.Property(x => x.StudentId).IsRequired();
        builder.Property(x => x.RoomId).IsRequired();
        builder.Property(x => x.SeatNumber).IsRequired();
        builder.Property(x => x.BenchId).IsRequired(false);
        builder.Property(x => x.IsRoomAssign).IsRequired();

        builder.HasIndex(x => new { x.SeatingPlanId, x.StudentId }).IsUnique();
        builder.HasIndex(x => new { x.SeatingPlanId, x.RoomId, x.SeatNumber }).IsUnique();
        builder.HasIndex(x => x.ExamId);
    }
}
