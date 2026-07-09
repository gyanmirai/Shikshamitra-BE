using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class ElectiveGroupConfiguration : IEntityTypeConfiguration<ElectiveGroup>
{
	public void Configure(EntityTypeBuilder<ElectiveGroup> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "ElectiveGroups",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();

		builder.Property(x => x.GroupType).HasMaxLength(80);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.ArchiveReason).HasMaxLength(500);

		builder.Navigation(x => x.Subjects).AutoInclude();
	}
}
