using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendancePolicyOverrideConfiguration : IEntityTypeConfiguration<AttendancePolicyOverride>
{
	public void Configure(EntityTypeBuilder<AttendancePolicyOverride> builder)
	{
		builder.ToTable("AppAttendancePolicyOverrides");
		builder.ConfigureByConvention();

		builder.Property(x => x.AttendanceConfigurationId).IsRequired();
		builder.Property(x => x.TargetType).IsRequired();
		builder.Property(x => x.TargetId).IsRequired().HasMaxLength(128);
		builder.Property(x => x.TargetName).IsRequired().HasMaxLength(160);
		builder.Property(x => x.IsActive).IsRequired();
		builder.Property(x => x.Priority).IsRequired();
		builder.Property(x => x.HoursBasedPayPercentage);
		builder.Property(x => x.GraceTime);
		builder.Property(x => x.MaxLateAllowedPerMonth);
		builder.Property(x => x.PenaltyType);
		builder.Property(x => x.OvertimeEnabled);
		builder.Property(x => x.OvertimeRate);
		builder.Property(x => x.MinOvertimeMinutes);

		builder.HasIndex(x => new { x.TenantId, x.AttendanceConfigurationId, x.TargetType, x.TargetId })
			.IsUnique()
			.HasFilter("\"IsActive\" = TRUE")
			.HasDatabaseName("IX_AttendancePolicyOverrides_Target");
	}
}
