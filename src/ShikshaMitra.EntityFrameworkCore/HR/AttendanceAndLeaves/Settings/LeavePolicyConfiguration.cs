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
		builder.Property(x => x.Version).IsRequired().HasDefaultValue(1).IsConcurrencyToken();

		builder.Property(x => x.AcademicYearId)
			.IsRequired();

		builder.Property(x => x.PolicyName)
			.IsRequired()
			.HasMaxLength(LeavePolicyConsts.MaxPolicyNameLength);

		builder.Property(x => x.NormalizedPolicyName)
			.IsRequired()
			.HasMaxLength(LeavePolicyConsts.MaxPolicyNameLength);

		builder.Property(x => x.Description)
			.HasMaxLength(LeavePolicyConsts.MaxDescriptionLength);

		builder.Property(x => x.Status)
			.IsRequired();

		builder.Property(x => x.EffectiveFrom)
			.IsRequired();

		builder.Property(x => x.IsActive)
			.IsRequired()
			.HasDefaultValue(true);

		builder.Property(x => x.IsDefault)
			.IsRequired()
			.HasDefaultValue(false);

		builder.HasOne(x => x.AcademicYear)
			.WithMany()
			.HasForeignKey(x => x.AcademicYearId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(x => x.Rules)
			.WithOne(x => x.LeavePolicy)
			.HasForeignKey(x => x.LeavePolicyId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.NormalizedPolicyName })
			.IsUnique()
			.HasFilter("\"Status\" <> 2")
			.HasDatabaseName("IX_EmployeeLeavePolicies_TenantId_AcademicYearId_PolicyName");

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsActive })
			.HasDatabaseName("IX_EmployeeLeavePolicies_TenantId_AcademicYearId_IsActive");

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status })
			.HasDatabaseName("IX_EmployeeLeavePolicies_TenantId_AcademicYearId_Status");

	}
}
