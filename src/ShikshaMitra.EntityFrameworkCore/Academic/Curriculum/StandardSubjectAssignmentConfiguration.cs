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

		builder.HasMany(x => x.Chapters)
			.WithOne()
			.HasForeignKey(c => c.AssignedSubjectId)
			.IsRequired();

		builder.HasMany(x => x.Documents)
			.WithOne()
			.HasForeignKey(d => d.AssignedSubjectId)
			.IsRequired();

		builder.HasMany(x => x.Teachers)
			.WithOne()
			.HasForeignKey(t => t.AssignedSubjectId)
			.IsRequired();

		// Prevent duplicate active assignments for the same (tenant, standard, subject).
		// Filtered to IsDeleted = false so soft-deleted rows don't block re-assignment.
		builder.HasIndex(x => new { x.TenantId, x.StandardId, x.SubjectId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
	}
}
