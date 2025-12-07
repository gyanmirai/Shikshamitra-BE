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
		builder.Property(x => x.PolicyName)
			.IsRequired()
			.HasMaxLength(LeavePolicyConsts.MaxPolicyNameLength);

		builder.Property(x => x.LeaveType)
			.IsRequired()
			.HasMaxLength(LeavePolicyConsts.MaxLeaveTypeLength);

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

		// Indexes for performance
		builder.HasIndex(x => x.PolicyName)
			.IsUnique()
			.HasDatabaseName("IX_EmployeeLeavePolicies_PolicyName");

		builder.HasIndex(x => new { x.TenantId, x.IsActive })
			.HasDatabaseName("IX_EmployeeLeavePolicies_TenantId_IsActive");

		builder.HasIndex(x => x.LeaveType)
			.HasDatabaseName("IX_EmployeeLeavePolicies_LeaveType");
	}
}
