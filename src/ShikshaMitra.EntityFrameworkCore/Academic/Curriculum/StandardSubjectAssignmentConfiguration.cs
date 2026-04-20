using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class StandardSubjectAssignmentConfiguration : IEntityTypeConfiguration<AssignedSubject>
{
	public void Configure(EntityTypeBuilder<AssignedSubject> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StandardSubjectAssignments", ShikshaMitraConsts.DbSchema);

		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		builder.Property(x => x.SubjectCode)
			.HasMaxLength(SubjectAssignmentConsts.MaxSubjectCodeLength)
			.IsRequired();

		builder.Property(x => x.SubjectType)
			.HasDefaultValue(SubjectType.Core)
			.IsRequired();

		builder.Property(x => x.PublishedAt)
			.IsRequired(false);
	}
}
