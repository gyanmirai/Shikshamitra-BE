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

		builder.ComplexProperty(e => e.FatherDetails);
		builder.ComplexProperty(e => e.MotherDetails);
		builder.ComplexProperty(e => e.GuardianDetails);
		builder.ComplexProperty(e => e.StudentCommunicationDetails);
		builder.ComplexProperty(e => e.StudentAdditionalDetails);
		builder.ComplexProperty(e => e.StudentMedicalRecords);
		builder.ComplexProperty(e => e.PreviousSchoolDetails);
		builder.ComplexProperty(e => e.ParentBankDetails);


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


		builder.OwnsOne(e => e.Country, cb =>
		{
			cb.Property(c => c.Name).HasColumnName("CountryName");
			cb.Property(c => c.IsoCode).HasColumnName("CountryIsoCode");
		});

		builder.OwnsOne(e => e.State, sb =>
		{
			sb.Property(s => s.Name).HasColumnName("StateName");
			sb.Property(s => s.IsoCode).HasColumnName("StateIsoCode");
		});

		builder.Navigation(x => x.AcademicYear).AutoInclude();
		builder.Navigation(x => x.EducationBoard).AutoInclude();
		builder.Navigation(x => x.EducationMedium).AutoInclude();
		builder.Navigation(x => x.EducationMedium).AutoInclude();
		builder.Navigation(x => x.Standard).AutoInclude();
		builder.Navigation(x => x.Division).AutoInclude();
		builder.Navigation(x => x.Standard).AutoInclude();

	}
}
