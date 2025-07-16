using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;
public class StudentAdditionalDetailConfiguration : IEntityTypeConfiguration<StudentAdditionalDetail>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<StudentAdditionalDetail> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentAdditionalDetails", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
	}
}
