using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.Records.Settings;

public class StudentManagementSettingsConfiguration : IEntityTypeConfiguration<StudentManagementSettings>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<StudentManagementSettings> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentManagementSettings", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
	}
}
