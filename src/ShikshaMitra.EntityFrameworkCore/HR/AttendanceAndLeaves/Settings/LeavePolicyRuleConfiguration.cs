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
		builder.Property(x => x.LeaveTypeId).IsRequired();
		builder.Property(x => x.AccrualPolicyType).IsRequired();
		builder.Property(x => x.LeaveCount).IsRequired();
		builder.Property(x => x.PaidType).IsRequired();
		builder.Property(x => x.CarryForward).IsRequired().HasDefaultValue(false);
		builder.Property(x => x.Encashable).IsRequired().HasDefaultValue(false);
		builder.Property(x => x.RequiresApproval).IsRequired().HasDefaultValue(true);
		builder.Property(x => x.MinNoticeDays).IsRequired().HasDefaultValue(2);
		builder.Property(x => x.MaxDaysPerRequest).IsRequired().HasDefaultValue(30);
		builder.Property(x => x.AllowHalfDay).IsRequired().HasDefaultValue(true);
		builder.Property(x => x.AllowNegativeBalance).IsRequired().HasDefaultValue(false);
		builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

		builder.HasOne(x => x.LeaveType)
			.WithMany()
			.HasForeignKey(x => x.LeaveTypeId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.LeavePolicyId, x.LeaveTypeId, x.IsActive })
			.HasDatabaseName("IX_EmployeeLeavePolicyRules_Policy_Type_Active");
		builder.HasIndex(x => x.LeaveTypeId)
			.HasDatabaseName("IX_EmployeeLeavePolicyRules_LeaveTypeId");
	}
}
