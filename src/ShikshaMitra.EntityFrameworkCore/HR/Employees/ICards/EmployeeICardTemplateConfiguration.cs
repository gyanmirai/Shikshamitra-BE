using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees.ICards;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Employees.ICards;

public class EmployeeICardTemplateConfiguration : IEntityTypeConfiguration<EmployeeICardTemplate>
{
	public void Configure(EntityTypeBuilder<EmployeeICardTemplate> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeICardTemplates", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
		builder.Property(x => x.BackgroundColor).HasMaxLength(32);
		builder.Property(x => x.BackgroundImageBlobName).HasMaxLength(512);
		builder.Property(x => x.LogoBlobName).HasMaxLength(512);
		builder.Property(x => x.SchoolName).HasMaxLength(180);
		builder.Property(x => x.SchoolAddress).HasMaxLength(300);
		builder.Property(x => x.ThemeColor).HasMaxLength(32);
		builder.Property(x => x.Orientation).IsRequired().HasMaxLength(24);
		builder.Property(x => x.PresetKey).HasMaxLength(64);
		builder.Property(x => x.SchemaVersion).IsRequired();
		builder.Property(x => x.LayoutJson).IsRequired();
		builder.Property(x => x.PrintSettingsJson).IsRequired();
		builder.Property(x => x.FieldsJson).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.MediumStageId, x.Name });
	}
}
