using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Attendance.settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.Attendance.Settings;

public class StudentAttendanceRuleConfiguration : IEntityTypeConfiguration<StudentAttendanceRule>
{
	public void Configure(EntityTypeBuilder<StudentAttendanceRule> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAttendanceRules", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}