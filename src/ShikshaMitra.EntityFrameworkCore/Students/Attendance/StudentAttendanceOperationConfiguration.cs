using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Attendance;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.Attendance;

public class StudentAttendanceOperationConfiguration : IEntityTypeConfiguration<StudentAttendanceOperation>
{
	public void Configure(EntityTypeBuilder<StudentAttendanceOperation> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAttendanceOperations"); b.ConfigureByConvention();
		b.Property(x => x.RequestHash).HasMaxLength(64);
		b.HasOne<AttendanceDay>().WithMany().HasForeignKey(x => x.AttendanceDayId).OnDelete(DeleteBehavior.Restrict);
	}
}
