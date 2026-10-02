using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Attendance;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.Attendance;

public class AttendanceEntryConfiguration : IEntityTypeConfiguration<AttendanceEntry>
{
	public void Configure(EntityTypeBuilder<AttendanceEntry> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AttendanceEntries", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasKey(ae => ae.Id);

		// Properties
		builder.Property(ae => ae.AttendanceDayId).IsRequired();
		builder.Property(ae => ae.StudentId).IsRequired();
		builder.Property(ae => ae.SchoolDate).HasColumnType("date");
		builder.Property(ae => ae.DenominatorCredit).HasDefaultValue(1d);
		builder.HasIndex(ae => new { ae.EnrollmentId, ae.SchoolDate }).IsUnique()
			.HasFilter("\"EnrollmentId\" IS NOT NULL AND \"IsDeleted\" = false")
			.HasDatabaseName("UX_AttendanceEntry_EnrollmentDate");
		builder.Property(ae => ae.MarkedByUserId).IsRequired();

		builder.Property(ae => ae.Status)
			.IsRequired()
			.HasConversion<string>()
			.HasMaxLength(20);

		builder.Property(ae => ae.Remarks)
			.HasMaxLength(500)
			.IsRequired(false);

		// Indexes
		builder.HasIndex(ae => ae.AttendanceDayId);
		builder.HasIndex(ae => ae.StudentId)
			.HasDatabaseName("IX_AttendanceEntry_Student");

		// Unique constraint: One entry per AttendanceDay per Student
		builder.HasIndex(ae => new { ae.AttendanceDayId, ae.StudentId })
			.IsUnique()
			.HasDatabaseName("UX_AttendanceEntry_Unique");

		// Relationships
		builder.HasOne(ae => ae.Student)
			.WithMany()
			.HasForeignKey(ae => ae.StudentId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<ShikshaMitra.Students.StudentEnrollments.StudentEnrollment>()
			.WithMany().HasForeignKey(ae => ae.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
	}
}
