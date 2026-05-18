using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DayCalculationSettingConfiguration : IEntityTypeConfiguration<DayCalculationSetting>
{
	public void Configure(EntityTypeBuilder<DayCalculationSetting> builder)
	{
		builder.ToTable("AppDayCalculationSettings");

		builder.ConfigureByConvention();

		builder.Property(x => x.DayCalculationType)
			.IsRequired();

		builder.Property(x => x.AcademicYearId)
			.IsRequired(false);

		builder.Property(x => x.ManualDays)
			.IsRequired(false);

		builder.Property(x => x.ManualMonthDaysJson)
			.HasColumnType("text")
			.IsRequired(false);

		builder.Property(x => x.SaturdayWorkPolicy)
			.IsRequired()
			.HasConversion<int>();

		builder.Property(x => x.HolidaySyncMode)
			.IsRequired()
			.HasConversion<int>();

		builder.Property(x => x.HolidayOverridesJson)
			.HasColumnType("text")
			.IsRequired(false);

		builder.Property(x => x.LockedMonthSettingsJson)
			.HasColumnType("text")
			.IsRequired(false);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId })
			.IsUnique()
			.HasFilter("\"AcademicYearId\" IS NOT NULL")
			.HasDatabaseName("IX_DayCalculationSettings_Tenant_AcademicYear");

		builder.HasIndex(x => x.TenantId)
			.IsUnique()
			.HasFilter("\"AcademicYearId\" IS NULL")
			.HasDatabaseName("IX_DayCalculationSettings_Tenant_Fallback");
	}
}
