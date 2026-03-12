using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class TeacherSubjectAllocationConfiguration : IEntityTypeConfiguration<TeacherSubjectAllocation>
{
	public void Configure(EntityTypeBuilder<TeacherSubjectAllocation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TeacherSubjectAllocations", ShikshaMitraConsts.DbSchema);

		// Apply default configurations based on ABP conventions
		builder.ConfigureByConvention();

		// Configure the relationship with AllocatedTeacher as owned entities
		builder.OwnsMany(x => x.Teachers, teachers =>
		{
			teachers.ToTable(ShikshaMitraConsts.DbTablePrefix + "AllocatedTeachers", ShikshaMitraConsts.DbSchema);

			// Configure composite key
			teachers.HasKey(t => new { t.TeacherSubjectAllocationId, t.TeacherId });

			// Configure the foreign key relationship
			teachers.WithOwner(t => t.Allocation)
				.HasForeignKey(t => t.TeacherSubjectAllocationId);

			// Index for performance
			teachers.HasIndex(t => t.TeacherId);
		});

		// Indexes for common queries
		builder.HasIndex(x => new { x.StandardId, x.DivisionId, x.SubjectId });
		builder.HasIndex(x => x.StandardId);
		builder.HasIndex(x => x.DivisionId);
		builder.HasIndex(x => x.SubjectId);
	}
}
