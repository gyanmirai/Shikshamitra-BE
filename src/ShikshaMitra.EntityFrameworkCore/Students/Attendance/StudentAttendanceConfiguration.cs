using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.Attendance;

public class StudentAttendanceConfiguration : IEntityTypeConfiguration<StudentAttendance>
{
	public void Configure(EntityTypeBuilder<StudentAttendance> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAttendances", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
