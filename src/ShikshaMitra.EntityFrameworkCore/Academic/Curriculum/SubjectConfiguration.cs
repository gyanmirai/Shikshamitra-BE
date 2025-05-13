using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectConfiguration : IEntityTypeConfiguration<StandardSubjectAssignment>
{
	public void Configure(EntityTypeBuilder<StandardSubjectAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Subjects", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		// handle cascade delete for teachers associated with subject
		builder.HasMany(s => s.Teachers)
			.WithOne(st => st.Subject)
			.HasForeignKey(st => st.SubjectId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
