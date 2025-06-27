using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class ElectiveSubjectConfiguration : IEntityTypeConfiguration<ElectiveSubject>
{
	public void Configure(EntityTypeBuilder<ElectiveSubject> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "ElectiveSubjects",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();

		builder.Navigation(x => x.Subjects).AutoInclude();
	}
}
