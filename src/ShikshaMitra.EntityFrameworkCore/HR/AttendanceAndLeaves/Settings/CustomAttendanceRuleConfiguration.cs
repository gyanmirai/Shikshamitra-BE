using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Employees;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class CustomAttendanceRuleConfiguration : IEntityTypeConfiguration<CustomAttendanceRule>
{
	public void Configure(EntityTypeBuilder<CustomAttendanceRule> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "CustomAttendanceRules", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}