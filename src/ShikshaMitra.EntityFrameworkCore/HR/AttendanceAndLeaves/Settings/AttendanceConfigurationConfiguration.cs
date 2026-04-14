using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendanceConfigurationConfiguration : IEntityTypeConfiguration<AttendanceConfiguration>
{
	public void Configure(EntityTypeBuilder<AttendanceConfiguration> builder)
	{
		builder.ToTable("AppAttendanceConfigurations");
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.HoursBasedPayPercentage).IsRequired();
		builder.Property(x => x.GraceTime).IsRequired();
		builder.Property(x => x.MaxLateAllowedPerMonth).IsRequired();
		builder.Property(x => x.PenaltyType).IsRequired();
		builder.Property(x => x.OvertimeEnabled).IsRequired();
		builder.Property(x => x.OvertimeRate);
		builder.Property(x => x.MinOvertimeMinutes);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId })
			.IsUnique()
			.HasDatabaseName("IX_AttendanceConfigurations_TenantId_AcademicYearId");
	}
}
