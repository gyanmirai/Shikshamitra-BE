using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// EF Core configuration for LeavePolicy entity
/// </summary>
public class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
	public void Configure(EntityTypeBuilder<LeavePolicy> builder)
	{
		builder.ToTable("AppEmployeeLeavePolicies");

		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.EducationMediumStageId)
			.IsRequired();

		builder.Property(x => x.PolicyName)
			.IsRequired()
			.HasMaxLength(LeavePolicyConsts.MaxPolicyNameLength);

		builder.Property(x => x.LeaveTypeId)
			.IsRequired();

		builder.Property(x => x.MaximumDaysPerYear)
			.IsRequired();

		builder.Property(x => x.PaidType)
			.IsRequired();

		builder.Property(x => x.CarryForward)
			.IsRequired()
			.HasDefaultValue(false);

		builder.Property(x => x.Encashable)
			.IsRequired()
			.HasDefaultValue(false);

		builder.Property(x => x.RequiresApproval)
			.IsRequired()
			.HasDefaultValue(true);

		builder.Property(x => x.Description)
			.HasMaxLength(LeavePolicyConsts.MaxDescriptionLength);

		builder.Property(x => x.IsActive)
			.IsRequired()
			.HasDefaultValue(true);

		// Navigation properties
		builder.HasOne(x => x.LeaveType)
			.WithMany()
			.HasForeignKey(x => x.LeaveTypeId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.EducationMediumStage)
			.WithMany()
			.HasForeignKey(x => x.EducationMediumStageId)
			.OnDelete(DeleteBehavior.Restrict);

		// Indexes for performance
		// Unique index scoped per tenant and organization
		builder.HasIndex(x => new { x.TenantId, x.EducationMediumStageId, x.PolicyName })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeLeavePolicies_TenantId_MediumStageId_PolicyName");

		builder.HasIndex(x => new { x.TenantId, x.EducationMediumStageId, x.IsActive })
			.HasDatabaseName("IX_EmployeeLeavePolicies_TenantId_MediumStageId_IsActive");

		builder.HasIndex(x => x.LeaveTypeId)
			.HasDatabaseName("IX_EmployeeLeavePolicies_LeaveTypeId");

		builder.Navigation(x => x.LeaveType).AutoInclude();
		builder.Navigation(x => x.EducationMediumStage).AutoInclude();
	}
}
