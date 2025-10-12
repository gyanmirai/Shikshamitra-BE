using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum.Setting;
public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
	public void Configure(EntityTypeBuilder<Subject> builder)
	{
		builder.ToTable(
		ShikshaMitraConsts.DbTablePrefix + "Subjects",
		ShikshaMitraConsts.DbSchema
		);

		builder.ConfigureByConvention();
	}
}

