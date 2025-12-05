using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Attendance.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.Attendance.Settings;

/// <summary>
/// EF Core configuration for StudentLeaveType entity
/// </summary>
public class StudentLeaveTypeConfiguration : IEntityTypeConfiguration<StudentLeaveType>
{
	public void Configure(EntityTypeBuilder<StudentLeaveType> builder)
	{
		builder.ToTable("AppStudentLeaveTypes");

		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.LeaveTypeName)
			.IsRequired()
			.HasMaxLength(StudentLeaveTypeConsts.MaxLeaveTypeNameLength)
			.HasColumnName(nameof(StudentLeaveType.LeaveTypeName));

		builder.Property(x => x.Color)
			.IsRequired()
			.HasMaxLength(StudentLeaveTypeConsts.MaxColorLength)
			.HasColumnName(nameof(StudentLeaveType.Color));

		builder.Property(x => x.IsActive)
			.IsRequired()
			.HasColumnName(nameof(StudentLeaveType.IsActive));

		// Create index for performance optimization
		builder.HasIndex(x => x.LeaveTypeName);
		builder.HasIndex(x => x.IsActive);
		builder.HasIndex(x => new { x.TenantId, x.LeaveTypeName });
	}
}
