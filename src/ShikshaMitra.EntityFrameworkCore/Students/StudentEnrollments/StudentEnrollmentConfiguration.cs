using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Common;
using System.Reflection.Emit;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentEnrollmentConfiguration : IEntityTypeConfiguration<StudentEnrollment>
{
	public void Configure(EntityTypeBuilder<StudentEnrollment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentEnrollments", ShikshaMitraConsts.DbSchema);

		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		//builder.ComplexProperty(e => e.StudentBasicDetails);
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


		builder.HasOne(e => e.Division)
			.WithMany()
			.HasForeignKey(s => s.DivisionId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(e => e.InquiryPreference)
			.WithMany()
			.HasForeignKey(s => s.InquiryPreferenceId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.Property(i => i.FollowUpStatusType)
		   .HasDefaultValue(FollowUpStatusType.Pending);

		builder.Property(i => i.DueDate);

		// Configure Country to be keyless

		//builder.OwnsMany(e => e.FollowUps, b =>
		//{
		//	b.ToTable("StudentEnrollmentFollowUps");
		//	b.WithOwner().HasForeignKey(x=>x.InquiryId);
		//	b.Property(f => f.Message).HasColumnName("Message");
		//});

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

		builder.HasMany(e => e.FollowUps)
				  .WithOne(f => f.StudentEnrollment)                 // the navigation property on FollowUpMessage
				  .HasForeignKey(f => f.StudentEnrollmentId)         // foreign key on FollowUpMessage
				  .OnDelete(DeleteBehavior.Cascade);

		builder.Navigation(x => x.EducationMedium).AutoInclude();
		builder.Navigation(x => x.EducationBoard).AutoInclude();
		builder.Navigation(x => x.AcademicYear).AutoInclude();
		builder.Navigation(x => x.Standard).AutoInclude();
		builder.Navigation(x => x.InquiryPreference).AutoInclude();
		builder.Navigation(x => x.Division).AutoInclude();
		builder.Navigation(x => x.FollowUps).AutoInclude();
	}
}