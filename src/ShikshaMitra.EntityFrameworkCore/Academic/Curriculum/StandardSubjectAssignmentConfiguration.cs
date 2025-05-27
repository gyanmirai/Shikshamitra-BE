using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class StandardSubjectAssignmentConfiguration : IEntityTypeConfiguration<StandardSubjectAssignment>
{
	public void Configure(EntityTypeBuilder<StandardSubjectAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StandardSubjectAssignments", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
	}
}
