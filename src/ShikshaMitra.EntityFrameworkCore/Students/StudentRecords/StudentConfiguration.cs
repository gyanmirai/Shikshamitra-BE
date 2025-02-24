using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentConfigurationa : IEntityTypeConfiguration<Student>
{
	public void Configure(EntityTypeBuilder<Student> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Students", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Foreign Key Constraints
		builder.HasOne(s => s.AcademicYear)
			.WithMany()
			.HasForeignKey(s => s.AcademicYearId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(s => s.EducationBoard)
			.WithMany() 
			.HasForeignKey(s => s.EducationBoardId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(s => s.EducationMedium)
			.WithMany()
			.HasForeignKey(s => s.EducationMediumId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(s => s.EducationMediumStage)
			.WithMany()
			.HasForeignKey(s => s.EducationMediumStageId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(s => s.Standard)
			.WithMany()
			.HasForeignKey(s => s.StandardId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(s => s.Division)
			.WithMany()
			.HasForeignKey(s => s.DivisionId)
			.OnDelete(DeleteBehavior.Restrict);

	}
}
