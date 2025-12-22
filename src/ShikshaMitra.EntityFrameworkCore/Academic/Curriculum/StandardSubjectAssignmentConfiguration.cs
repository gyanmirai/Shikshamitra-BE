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

		// Configure new properties
		builder.Property(x => x.AssignmentGroupName)
			.HasMaxLength(SubjectAssignmentConsts.MaxGroupNameLength)
			.IsRequired(false);

		builder.Property(x => x.SubjectCode)
			.HasMaxLength(SubjectAssignmentConsts.MaxSubjectCodeLength)
			.IsRequired();

		builder.Property(x => x.SubjectColor)
			.HasMaxLength(10)
			.IsRequired(false);

		builder.Property(x => x.Documents)
			.HasColumnType("text")
			.IsRequired(false);

		builder.Property(x => x.IsSubjectOpen)
			.HasDefaultValue(true)
			.IsRequired();

		builder.Property(x => x.EducationMediumId)
			.IsRequired();

		builder.Property(x => x.SubjectType)
			.HasDefaultValue(SubjectType.Core)
			.IsRequired();

		builder.Property(x => x.PublishedAt)
			.IsRequired(false);

		// Create indexes
		builder.HasIndex(x => x.AssignmentGroupId)
			.HasDatabaseName("IX_StandardSubjectAssignments_AssignmentGroupId");

		builder.HasIndex(x => x.EducationMediumId)
			.HasDatabaseName("IX_StandardSubjectAssignments_EducationMediumId");

		builder.HasIndex(x => new { x.StandardId, x.SubjectCode })
			.HasDatabaseName("IX_StandardSubjectAssignments_StandardId_SubjectCode")
			.IsUnique();
	}
}
