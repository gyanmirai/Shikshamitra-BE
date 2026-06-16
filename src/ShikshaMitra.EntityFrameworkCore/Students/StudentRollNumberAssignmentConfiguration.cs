using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentRollNumberAssignmentConfiguration : IEntityTypeConfiguration<StudentRollNumberAssignment>
{
	public void Configure(EntityTypeBuilder<StudentRollNumberAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRollNumberAssignments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasOne<Student>()
			.WithMany()
			.HasForeignKey(x => x.StudentId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired();

		builder.HasOne<AcademicTerm>()
			.WithMany()
			.HasForeignKey(x => x.AcademicTermId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired();

		builder.HasOne<Division>()
			.WithMany()
			.HasForeignKey(x => x.DivisionId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.AcademicTermId, x.StudentId })
			.IsUnique();

		builder.HasIndex(x => new { x.TenantId, x.AcademicTermId, x.DivisionId, x.RollNo });
		builder.HasIndex(x => x.StudentId);
		builder.HasIndex(x => x.DivisionId);
	}
}
