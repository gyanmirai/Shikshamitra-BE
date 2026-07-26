using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Policies;

public class PayrollPolicyConfiguration : IEntityTypeConfiguration<PayrollPolicy>
{
	public void Configure(EntityTypeBuilder<PayrollPolicy> builder)
	{
		builder.ToTable("HR_PayrollPolicies");
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.WorkCalendarPolicyId).IsRequired();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Status).IsRequired().HasConversion<int>();
		builder.Property(x => x.IsDefault).IsRequired();
		builder.Property(x => x.DayCalculationType).IsRequired().HasConversion<int>();
		builder.Property(x => x.ManualMonthDaysJson).HasColumnType("text");
		builder.Property(x => x.SaturdayWorkPolicy).IsRequired().HasConversion<int>();
		builder.Property(x => x.HolidayOverridesJson).HasColumnType("text");
		builder.Property(x => x.LockedMonthSettingsJson).HasColumnType("text");
		builder.Property(x => x.Version).IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Name })
			.IsUnique()
			.HasFilter("\"Status\" <> 2")
			.HasDatabaseName("IX_HR_PayrollPolicies_Tenant_AcademicYear_Name");

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsDefault })
			.HasFilter("\"IsDefault\" = true")
			.HasDatabaseName("IX_HR_PayrollPolicies_Tenant_AcademicYear_Default");
	}
}
