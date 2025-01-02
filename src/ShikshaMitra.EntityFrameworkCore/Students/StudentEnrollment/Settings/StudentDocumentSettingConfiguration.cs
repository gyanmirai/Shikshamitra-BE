using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentEnrollment.Settings;

public class StudentDocumentSettingConfiguration
{
	public void Configure(EntityTypeBuilder<StudentDocumentSetting> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentDocumentSettings", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(e => e.Id);
		builder.Property(e => e.DocumentName)
			.IsRequired()
			.HasMaxLength(100);
	}
}