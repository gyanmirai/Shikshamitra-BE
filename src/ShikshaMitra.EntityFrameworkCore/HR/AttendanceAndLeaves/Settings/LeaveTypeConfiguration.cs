using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// EF Core configuration for LeaveType entity
/// </summary>
public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
	public void Configure(EntityTypeBuilder<LeaveType> builder)
	{
		builder.ToTable("AppEmployeeLeaveTypes");

		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.Type)
			.IsRequired()
			.HasMaxLength(LeaveTypeConsts.MaxTypeLength);

		builder.Property(x => x.Color)
			.IsRequired()
			.HasMaxLength(LeaveTypeConsts.MaxColorLength);

		builder.Property(x => x.IsActive)
			.IsRequired()
			.HasDefaultValue(true);

		// Indexes for performance
		// Unique index scoped per tenant
		builder.HasIndex(x => new { x.TenantId, x.Type })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeLeaveTypes_TenantId_Type");

		builder.HasIndex(x => new { x.TenantId, x.IsActive })
			.HasDatabaseName("IX_EmployeeLeaveTypes_TenantId_IsActive");
	}
}
