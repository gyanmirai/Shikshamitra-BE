using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Policies;

public class HrPolicyAssignmentConfiguration : IEntityTypeConfiguration<HrPolicyAssignment>
{
	public void Configure(EntityTypeBuilder<HrPolicyAssignment> builder)
	{
		builder.ToTable("HR_PolicyAssignments");
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.TargetType).IsRequired();
		builder.Property(x => x.TargetKey).IsRequired().HasMaxLength(128);
		builder.Property(x => x.TargetDisplayName).HasMaxLength(256);
		builder.Property(x => x.AttendancePolicyId).IsRequired();
		builder.Property(x => x.PayrollPolicyId).IsRequired();
		builder.Property(x => x.EffectiveFrom).IsRequired();
		builder.Property(x => x.IsActive).IsRequired();
		builder.Property(x => x.Priority).IsRequired();
		builder.Property(x => x.AssignmentReason).HasMaxLength(256);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.TargetType, x.TargetKey, x.IsActive })
			.HasDatabaseName("IX_HR_PolicyAssignments_Target_Active");
		builder.HasIndex(x => new { x.AcademicYearId, x.TargetType, x.TargetKey, x.EffectiveFrom, x.EffectiveTo, x.IsActive })
			.HasDatabaseName("IX_HR_PolicyAssignments_Resolver");
	}
}
