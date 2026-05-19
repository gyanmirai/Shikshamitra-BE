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
		builder.Property(x => x.PolicyName).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.EffectiveFrom);
		builder.Property(x => x.EffectiveTo);
		builder.Property(x => x.WeeklyOffPattern).IsRequired();
		builder.Property(x => x.CustomWeeklyOffs).HasMaxLength(64);
		builder.Property(x => x.HoursBasedPayPercentage).IsRequired();
		builder.Property(x => x.HalfDayPayPercentage).IsRequired();
		builder.Property(x => x.AbsentPayPercentage).IsRequired();
		builder.Property(x => x.MissingPunchBehavior).IsRequired();
		builder.Property(x => x.ManualCorrectionAllowed).IsRequired();
		builder.Property(x => x.GraceTime).IsRequired();
		builder.Property(x => x.MaxLateAllowedPerMonth).IsRequired();
		builder.Property(x => x.PenaltyType).IsRequired();
		builder.Property(x => x.PenaltyCountThreshold).IsRequired();
		builder.Property(x => x.EarlyExitEnabled).IsRequired();
		builder.Property(x => x.EarlyExitGraceTime).IsRequired();
		builder.Property(x => x.OvertimeEnabled).IsRequired();
		builder.Property(x => x.OvertimeRate);
		builder.Property(x => x.MinOvertimeMinutes);
		builder.Property(x => x.HolidayWorkBehavior).IsRequired();
		builder.Property(x => x.SandwichRuleEnabled).IsRequired();
		builder.Property(x => x.PayrollPreviewEnabled).IsRequired();
		builder.Property(x => x.PayrollIntegrationMode).IsRequired();
		builder.Property(x => x.RequirePublishedAttendanceForPayrollLock).IsRequired();

		builder.HasMany(x => x.Overrides)
			.WithOne()
			.HasForeignKey(x => x.AttendanceConfigurationId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId })
			.IsUnique()
			.HasDatabaseName("IX_AttendanceConfigurations_TenantId_AcademicYearId");
	}
}
