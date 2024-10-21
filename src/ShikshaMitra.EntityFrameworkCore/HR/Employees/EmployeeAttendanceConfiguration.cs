using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Employees;

public class EmployeeAttendanceConfiguration : IEntityTypeConfiguration<EmployeeAttendance>
{
	public void Configure(EntityTypeBuilder<EmployeeAttendance> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeAttendances", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Status)
			.IsRequired()
			.HasConversion<string>()
			.HasMaxLength(20);


		// Configure DateOnly and TimeOnly properties
		builder.Property(e => e.AttendanceDate)
			.HasColumnType("date");

		builder.Property(e => e.TimeIn)
			.HasColumnType("time");

		builder.Property(e => e.TimeOut)
			.HasColumnType("time");

		// Indexes
		builder.HasIndex(e => new { e.EmployeeId, e.AttendanceDate });

		// Relationships
		builder.HasOne(e => e.Employee)
			.WithMany()
			.HasForeignKey(e => e.EmployeeId)
			.IsRequired();
	}
}