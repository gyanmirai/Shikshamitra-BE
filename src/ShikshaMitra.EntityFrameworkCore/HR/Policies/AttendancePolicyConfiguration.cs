using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Policies;

public class AttendancePolicyConfiguration : IEntityTypeConfiguration<AttendancePolicy>
{
	public void Configure(EntityTypeBuilder<AttendancePolicy> builder)
	{
		builder.ToTable("HR_AttendancePolicies");
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.WorkCalendarPolicyId).IsRequired();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Status).IsRequired().HasConversion<int>();
		builder.Property(x => x.IsDefault).IsRequired();
		builder.Property(x => x.MissingPunchBehavior).IsRequired().HasConversion<int>();
		builder.Property(x => x.PenaltyType).IsRequired().HasConversion<int>();
		builder.Property(x => x.OvertimeRate).HasConversion<int?>();
		builder.Property(x => x.HolidayWorkBehavior).IsRequired().HasConversion<int>();
		builder.Property(x => x.Version).IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Name })
			.IsUnique()
			.HasDatabaseName("IX_HR_AttendancePolicies_Tenant_AcademicYear_Name");

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsDefault })
			.HasFilter("\"IsDefault\" = true")
			.HasDatabaseName("IX_HR_AttendancePolicies_Tenant_AcademicYear_Default");
	}
}
