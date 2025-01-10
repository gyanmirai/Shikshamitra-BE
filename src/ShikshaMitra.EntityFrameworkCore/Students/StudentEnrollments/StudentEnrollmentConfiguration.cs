using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentEnrollmentConfiguration : IEntityTypeConfiguration<StudentEnrollment>
{
	public void Configure(EntityTypeBuilder<StudentEnrollment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentEnrollments", ShikshaMitraConsts.DbSchema);
	
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		builder.ComplexProperty(e => e.FatherDetails);
		builder.ComplexProperty(e => e.MotherDetails);
		builder.ComplexProperty(e => e.GuardianDetails);
		builder.ComplexProperty(e => e.StudentCommunicationDetails);
		builder.ComplexProperty(e => e.StudentAdditionalDetails);
		builder.ComplexProperty(e => e.StudentMedicalRecords);
		builder.ComplexProperty(e => e.PreviousSchoolDetails);
		builder.ComplexProperty(e => e.ParentBankDetails);

		// builder.HasOne(e => e.Role)
		// 	.WithMany()
		// 	.HasForeignKey(s => s.RoleId)
		// 	.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(e => e.EducationMedium)
			.WithMany()
			.HasForeignKey(s => s.EducationMediumId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(e => e.EducationBoard)
			.WithMany()
			.HasForeignKey(s => s.EducationBoardId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(e => e.AcademicYear)
			.WithMany()
			.HasForeignKey(s => s.AcademicYearId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(e => e.Standard)
			.WithMany()
			.HasForeignKey(s => s.StandardId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(e => e.InquiryPreference)
			.WithMany()
			.HasForeignKey(s => s.InquiryPreferenceId)
			.OnDelete(DeleteBehavior.Restrict);
		
		builder.Navigation(x => x.Role).AutoInclude();
		builder.Navigation(x => x.EducationMedium).AutoInclude();
		builder.Navigation(x => x.EducationBoard).AutoInclude();
		builder.Navigation(x => x.AcademicYear).AutoInclude();
		builder.Navigation(x => x.Standard).AutoInclude();
		builder.Navigation(x => x.InquiryPreference).AutoInclude();
	}
}