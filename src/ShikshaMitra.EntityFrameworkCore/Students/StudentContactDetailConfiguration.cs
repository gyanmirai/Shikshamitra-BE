using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;
public class StudentContactDetailConfiguration : IEntityTypeConfiguration<StudentContactDetail>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<StudentContactDetail> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentContactDetails", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
	}
}
