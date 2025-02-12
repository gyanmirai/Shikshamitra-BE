using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.Attendance;

public class StudentAttendanceConfiguration : IEntityTypeConfiguration<StudentAttendance>
{
	public void Configure(EntityTypeBuilder<StudentAttendance> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAttendances", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Student Id is a foreign key to the Student entity
		builder.HasOne<Student>().WithMany().HasForeignKey(e => e.StudentId).IsRequired();
	}
}
