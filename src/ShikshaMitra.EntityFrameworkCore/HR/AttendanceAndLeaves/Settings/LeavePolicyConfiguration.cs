using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
	public void Configure(EntityTypeBuilder<LeavePolicy> builder)
	{
		builder.ToTable("AppEmployeeLeavePolicies");

		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicYearId)
			.IsRequired();

		builder.Property(x => x.PolicyName)
			.IsRequired()
			.HasMaxLength(LeavePolicyConsts.MaxPolicyNameLength);

		builder.Property(x => x.LeaveTypeId)
			.IsRequired();

		builder.Property(x => x.AccrualPolicyType)
			.IsRequired();

		builder.Property(x => x.LeaveCount)
			.IsRequired();

		builder.Property(x => x.PaidType)
			.IsRequired();

		builder.Property(x => x.CarryForward)
			.IsRequired()
			.HasDefaultValue(false);

		builder.Property(x => x.Encashable)
			.IsRequired()
			.HasDefaultValue(false);

		builder.Property(x => x.EncashmentTiming);

		builder.Property(x => x.RequiresApproval)
			.IsRequired()
			.HasDefaultValue(true);

		builder.Property(x => x.Description)
			.HasMaxLength(LeavePolicyConsts.MaxDescriptionLength);

		builder.Property(x => x.IsActive)
			.IsRequired()
			.HasDefaultValue(true);

		builder.HasOne(x => x.LeaveType)
			.WithMany()
			.HasForeignKey(x => x.LeaveTypeId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.AcademicYear)
			.WithMany()
			.HasForeignKey(x => x.AcademicYearId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.PolicyName })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName");

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsActive })
			.HasDatabaseName("IX_EmployeeLeavePolicies_TenantId_AcademicYearId_IsActive");

		builder.HasIndex(x => x.LeaveTypeId)
			.HasDatabaseName("IX_EmployeeLeavePolicies_LeaveTypeId");
	}
}
