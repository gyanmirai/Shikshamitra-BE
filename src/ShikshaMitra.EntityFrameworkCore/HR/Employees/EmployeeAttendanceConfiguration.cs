using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Employees;

public class EmployeeAttendanceConfiguration : IEntityTypeConfiguration<EmployeeAttendance>
{
	public void Configure(EntityTypeBuilder<EmployeeAttendance> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeAttendances", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasKey(e => e.Id);

		builder.Property(e => e.StatusType)
			.IsRequired()
			.HasConversion<string>()
			.HasMaxLength(20);

		// Configure EducationMediumStageId for scope filtering
		// Nullable to support existing records created before this column was added
		builder.Property(e => e.EducationMediumStageId)
			.IsRequired(false);

		// AttendanceDate for unique constraint and date-based queries
		builder.Property(e => e.AttendanceDate)
			.IsRequired()
			.HasColumnType("date");

		// TimeIn and TimeOut are nullable (for ABSENT/PENDING days without actual clock-in)
		builder.Property(e => e.TimeIn)
			.IsRequired(false)
			.HasColumnType("timestamptz");

		builder.Property(e => e.TimeOut)
			.IsRequired(false)
			.HasColumnType("timestamptz");

		// Indexes
		builder.HasIndex(e => new { e.EmployeeId });
		builder.HasIndex(e => e.EducationMediumStageId); // Index for scope filtering

		// Unique constraint: one attendance record per employee per day per tenant
		builder.HasIndex(e => new { e.TenantId, e.EmployeeId, e.AttendanceDate })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeAttendances_Tenant_Employee_Date");

		// Index for date-wise queries (e.g., get all attendance for a specific date)
		builder.HasIndex(e => new { e.TenantId, e.AttendanceDate })
			.HasDatabaseName("IX_EmployeeAttendances_Tenant_Date");

		// Relationships
		builder.HasOne(e => e.Employee)
			.WithMany()
			.HasForeignKey(e => e.EmployeeId)
			.IsRequired();

		// Note: EducationMediumStageId is denormalized from Employee for performance
		// This allows scope filtering without joining through Employee
		// The relationship is maintained through application logic
		
		// Note: PostgreSQL CHECK constraints cannot reference other tables via subqueries.
		// EducationMediumStageId consistency is enforced in the application layer
		// via EmployeeAttendanceAppService.ValidateAndNormalizeEducationMediumStageIdAsync()
		// which validates and auto-populates the value from the Employee record.
	}
}