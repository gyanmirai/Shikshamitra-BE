using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Attendance;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.Attendance;

public class StudentAttendanceCorrectionConfiguration : IEntityTypeConfiguration<StudentAttendanceCorrection>
{
	public void Configure(EntityTypeBuilder<StudentAttendanceCorrection> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAttendanceCorrections"); b.ConfigureByConvention();
		b.Property(x => x.Reason).HasMaxLength(2000); b.Property(x => x.ReviewReason).HasMaxLength(2000);
		b.Property(x => x.Status).HasMaxLength(20);
		b.HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
		b.HasIndex(x => new { x.TenantId, x.AttendanceDayId, x.StudentId }).IsUnique()
			.HasFilter("\"Status\" = 'Pending' AND \"IsDeleted\" = false");
		b.HasOne<AttendanceDay>().WithMany().HasForeignKey(x => x.AttendanceDayId).OnDelete(DeleteBehavior.Restrict);
	}
}
