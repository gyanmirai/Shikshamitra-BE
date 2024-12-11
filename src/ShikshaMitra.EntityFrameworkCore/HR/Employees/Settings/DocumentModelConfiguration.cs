using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Employees.Settings;

public class DocumentModelConfiguration : IEntityTypeConfiguration<DocumentSetting>
{
	public void Configure(EntityTypeBuilder<DocumentSetting> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "DocumentSettings", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(e => e.Id);
		builder.Property(e => e.DocumentName)
			.IsRequired()
			.HasMaxLength(100);
	}
}
