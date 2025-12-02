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
		builder.Property(e => e.EducationMediumStageId)
			.IsRequired();

		// Modify TimeIn and TimeOut to use DateTimeOffset
		builder.Property(e => e.TimeIn)
			.IsRequired()
			.HasColumnType("timestamptz");

		builder.Property(e => e.TimeOut)
			.HasColumnType("timestamptz");

		// Indexes
		builder.HasIndex(e => new { e.EmployeeId });
		builder.HasIndex(e => e.EducationMediumStageId); // Index for scope filtering

		// Relationships
		builder.HasOne(e => e.Employee)
			.WithMany()
			.HasForeignKey(e => e.EmployeeId)
			.IsRequired();

		// Note: EducationMediumStageId is denormalized from Employee for performance
		// This allows scope filtering without joining through Employee
		// The relationship is maintained through application logic
	}
}