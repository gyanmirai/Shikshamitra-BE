using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyRuleConfiguration : IEntityTypeConfiguration<LeavePolicyRule>
{
	public void Configure(EntityTypeBuilder<LeavePolicyRule> builder)
	{
		builder.ToTable("AppEmployeeLeavePolicyRules");
		builder.ConfigureByConvention();

		builder.Property(x => x.LeavePolicyId).IsRequired();
		builder.Property(x => x.AccrualPolicyType).IsRequired();
		builder.Property(x => x.EntitlementMode).IsRequired().HasDefaultValue(LeaveEntitlementMode.Limited);
		builder.Property(x => x.LeaveCount).IsRequired(false);
		builder.Property(x => x.PaidType).IsRequired();
		builder.Property(x => x.CarryForward).IsRequired().HasDefaultValue(false);
		builder.Property(x => x.Encashable).IsRequired().HasDefaultValue(false);
		builder.Property(x => x.RequiresApproval).IsRequired().HasDefaultValue(true);
		builder.Property(x => x.MinNoticeDays).IsRequired().HasDefaultValue(2);
		builder.Property(x => x.MaxDaysPerRequest).IsRequired().HasDefaultValue(30);
		builder.Property(x => x.AllowHalfDay).IsRequired().HasDefaultValue(true);
		builder.Property(x => x.AllowNegativeBalance).IsRequired().HasDefaultValue(false);
		builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

		builder.HasIndex(x => new { x.LeavePolicyId, x.PaidType, x.IsActive })
			.HasDatabaseName("IX_EmployeeLeavePolicyRules_Policy_PaidType_Active");
	}
}
