using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.Attendance;

public class AttendancePolicyConfiguration : IEntityTypeConfiguration<StudentAttendanceConfiguration>, IEntityTypeConfiguration<StudentAttendancePolicyVersion>, IEntityTypeConfiguration<StudentAttendanceSchedule>
{
	public void Configure(EntityTypeBuilder<StudentAttendanceConfiguration> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAttendanceConfigurations", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention();
		b.Property(x => x.DraftPolicyJson).IsRequired().HasColumnType("text");
		b.HasIndex(x => new { x.TenantId, x.AcademicYearId }).IsUnique().HasFilter("\"AcademicYearId\" IS NOT NULL AND \"IsDeleted\" = false");
		b.HasIndex(x => x.TenantId).IsUnique().HasFilter("\"AcademicYearId\" IS NULL AND \"IsDeleted\" = false");
	}
	public void Configure(EntityTypeBuilder<StudentAttendancePolicyVersion> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAttendancePolicyVersions", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention();
		b.Property(x => x.PolicyJson).IsRequired().HasColumnType("text"); b.Property(x => x.EffectiveFrom).HasColumnType("date");
		b.HasIndex(x => new { x.TenantId, x.ConfigurationId, x.VersionNumber }).IsUnique();
		b.HasOne<StudentAttendanceConfiguration>().WithMany().HasForeignKey(x => x.ConfigurationId).OnDelete(DeleteBehavior.Restrict);
	}
	public void Configure(EntityTypeBuilder<StudentAttendanceSchedule> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAttendanceSchedules", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention();
		b.Property(x => x.Name).IsRequired().HasMaxLength(150); b.Property(x => x.Kind).IsRequired().HasMaxLength(20);
		b.Property(x => x.WeekDaysJson).IsRequired().HasColumnType("text"); b.Property(x => x.StartDate).HasColumnType("date"); b.Property(x => x.EndDate).HasColumnType("date");
		b.Property(x => x.StartTime).HasMaxLength(5); b.Property(x => x.EndTime).HasMaxLength(5);
		b.Property(x => x.RetiredFrom).HasColumnType("date");
		b.HasIndex(x => new { x.TenantId, x.EnrollmentId, x.StartDate });
		b.HasOne<StudentEnrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
	}
}
