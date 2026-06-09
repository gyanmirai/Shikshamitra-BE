using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeLeaveConfiguration : IEntityTypeConfiguration<EmployeeLeave>
{
	public void Configure(EntityTypeBuilder<EmployeeLeave> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeLeaves", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// PublicId: Stable, auto-increment ID for API routes and display
		// Uses DB identity generation for scalability
		builder.Property(e => e.PublicId)
			.UseIdentityAlwaysColumn()
			.ValueGeneratedOnAdd();

		builder.Property(e => e.ApprovedLeaveUnits)
			.HasPrecision(18, 2)
			.HasDefaultValue(0m);

		// Unique index on PublicId per tenant for fast lookups
		builder.HasIndex(e => new { e.TenantId, e.PublicId })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeLeaves_Tenant_PublicId");

		// Composite index for overlap & balance queries (employee + status + date range)
		builder.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Status, e.StartDate, e.EndDate })
			.HasDatabaseName("IX_EmployeeLeaves_Overlap");

		builder.Property(e => e.StartDateOnly)
			.HasColumnType("date");

		builder.Property(e => e.EndDateOnly)
			.HasColumnType("date");

		builder.HasIndex(e => new { e.TenantId, e.Status, e.StartDateOnly, e.EndDateOnly, e.EmployeeId })
			.HasDatabaseName("IX_EmployeeLeaves_Dashboard_DateScope");

		builder.HasOne(e => e.Employee)
			.WithMany()
			.HasForeignKey(e => e.EmployeeId)
			.OnDelete(DeleteBehavior.Restrict);

		// Auto Include
		builder.Navigation(e => e.Employee).AutoInclude();

		// reference to the leavePolicy
		builder.HasOne(e => e.LeavePolicy)
			.WithMany()
			.HasForeignKey(e => e.LeavePolicyId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(e => e.LeavePolicyRule)
			.WithMany()
			.HasForeignKey(e => e.LeavePolicyRuleId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(e => e.LeaveType)
			.WithMany()
			.HasForeignKey(e => e.LeaveTypeId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(e => new { e.EmployeeId, e.LeavePolicyRuleId, e.Status })
			.HasDatabaseName("IX_EmployeeLeaves_Rule_Balance");

		builder.HasIndex(e => new { e.EmployeeId, e.LeaveTypeId, e.Status })
			.HasDatabaseName("IX_EmployeeLeaves_LeaveType_Balance");

		// Auto Include
		builder.Navigation(e => e.LeavePolicy).AutoInclude();
		builder.Navigation(e => e.LeavePolicyRule).AutoInclude();
		builder.Navigation(e => e.LeaveType).AutoInclude();
	}
}
