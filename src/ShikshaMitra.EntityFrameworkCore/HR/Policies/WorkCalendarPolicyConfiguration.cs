using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Policies;

public class WorkCalendarPolicyConfiguration : IEntityTypeConfiguration<WorkCalendarPolicy>
{
	public void Configure(EntityTypeBuilder<WorkCalendarPolicy> builder)
	{
		builder.ToTable("HR_WorkCalendarPolicies");
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Status).IsRequired().HasConversion<int>();
		builder.Property(x => x.WeeklyOffPattern).IsRequired().HasConversion<int>();
		builder.Property(x => x.CustomWeeklyOffsJson).HasColumnType("text");
		builder.Property(x => x.HolidaySyncMode).IsRequired().HasConversion<int>();
		builder.Property(x => x.Version).IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Name })
			.IsUnique()
			.HasFilter("\"Status\" <> 2")
			.HasDatabaseName("IX_HR_WorkCalendarPolicies_Tenant_AcademicYear_Name");
	}
}
