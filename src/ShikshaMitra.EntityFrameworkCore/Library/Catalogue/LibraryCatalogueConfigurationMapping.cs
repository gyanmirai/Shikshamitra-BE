using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Catalogue;

public sealed class LibraryCatalogueConfigurationMapping : IEntityTypeConfiguration<LibraryCatalogueConfiguration>
{
	public void Configure(EntityTypeBuilder<LibraryCatalogueConfiguration> builder)
	{
		builder.ToTable("AppLibraryCatalogueConfigurations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.DefinitionJson).IsRequired().HasColumnType("text");
		builder.Property(row => row.Revision).IsRequired().HasMaxLength(32);
		builder.HasIndex(row => row.TenantId).IsUnique();
	}
}
