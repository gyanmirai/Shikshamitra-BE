using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;
public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
	public void Configure(EntityTypeBuilder<Student> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Students", ShikshaMitraConsts.DbSchema);

		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		// Configure admission approval columns
		builder.Property(x => x.AdmissionApprovalStatus)
			.HasDefaultValue(StudentEnrollmentApprovalState.Pending);

		builder.Property(x => x.AdmissionApprovalRemarks)
			.HasMaxLength(1000);

		// Add indexes for filtering by admission status and academic year
		builder.HasIndex(x => new { x.AdmissionApprovalStatus, x.DivisionId });
		builder.HasIndex(x => new { x.AcademicYearId, x.Status });

		// Configure navigation properties to load aggregate parts together
		// This ensures the aggregate is loaded as a complete unit
		builder.HasOne(x => x.AdditionalDetail)
			.WithOne()
			.HasForeignKey<StudentAdditionalDetail>(x => x.Id)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.ContactDetail)
			.WithOne()
			.HasForeignKey<StudentContactDetail>(x => x.Id)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.FamilyDetail)
			.WithOne()
			.HasForeignKey<StudentFamilyDetail>(x => x.Id)
			.OnDelete(DeleteBehavior.Cascade);

		// Auto-include aggregate parts when loading Student
		builder.Navigation(x => x.AdditionalDetail).AutoInclude();
		builder.Navigation(x => x.ContactDetail).AutoInclude();
		builder.Navigation(x => x.FamilyDetail).AutoInclude();

		// Status history: owned by the aggregate, loaded explicitly via includeDetails.
		builder.HasMany(x => x.StatusHistories)
			.WithOne()
			.HasForeignKey(h => h.StudentId)
			.OnDelete(DeleteBehavior.Cascade);

		// set navigation properties to be loaded together with the main entity for EducationBoard, Medium, Division, AcademicYear
		builder.HasOne(x => x.AcademicBoard)
			.WithMany()
			.HasForeignKey(x => x.AcademicBoardId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(false);

		builder.Navigation(x => x.AcademicBoard).AutoInclude();

		builder.HasOne(x => x.AcademicMedium)
			.WithMany()
			.HasForeignKey(x => x.AcademicMediumId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(false);

		builder.Navigation(x => x.AcademicMedium).AutoInclude();

		builder.HasOne(x => x.Standard)
			.WithMany()
			.HasForeignKey(x => x.StandardId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(false);

		builder.Navigation(x => x.Standard).AutoInclude();

		builder.HasOne(x => x.Division)
			.WithMany()
			.HasForeignKey(x => x.DivisionId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired(false);

		builder.Navigation(x => x.Division).AutoInclude();
	}
}
