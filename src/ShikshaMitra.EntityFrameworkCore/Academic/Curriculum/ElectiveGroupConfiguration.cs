using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

		builder.Navigation(x => x.Subjects).AutoInclude();
	}
}
