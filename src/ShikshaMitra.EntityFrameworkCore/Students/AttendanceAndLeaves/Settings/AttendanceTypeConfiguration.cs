using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Attendance.settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.Attendance.Settings;

public class AttendanceTypeConfiguration : IEntityTypeConfiguration<AttendanceType>
{
	public void Configure(EntityTypeBuilder<AttendanceType> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AttendanceTypes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		
	}
}