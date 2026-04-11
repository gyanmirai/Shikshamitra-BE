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

		builder.Property(x => x.ManualDays)
			.IsRequired(false);

		builder.HasIndex(x => x.TenantId)
			.IsUnique()
			.HasDatabaseName("IX_DayCalculationSettings_TenantId");
	}
}
