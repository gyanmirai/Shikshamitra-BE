using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Examination;

public class GradeRangeConfiguration : IEntityTypeConfiguration<GradeRange>
{
	public void Configure(EntityTypeBuilder<GradeRange> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "GradeRanges",
			ShikshaMitraConsts.DbSchema
		);

		builder.ConfigureByConvention();

		builder.Property(gr => gr.Grade)
			.IsRequired()
			.HasMaxLength(10);
	}
}
