using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using Volo.Abp.EntityFrameworkCore.Modeling;

using ShikshaMitra.Students.studentenrollment.Inquiry;

public class StudentInquiryConfiguration : IEntityTypeConfiguration<StudentInquiry>
{
	public void Configure(EntityTypeBuilder<StudentInquiry> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentInquiries", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		
		
		builder.HasOne(e => e.AcademicYear)
			.WithMany()
			.HasForeignKey(s => s.AcademicYearId)
			.OnDelete(DeleteBehavior.Restrict);
		
		builder.HasOne(e => e.EducationMedium)
			.WithMany()
			.HasForeignKey(s => s.EducationMediumId)
			.OnDelete(DeleteBehavior.Restrict);
		
		builder.HasOne(e => e.Standard)
			.WithMany()
			.HasForeignKey(s => s.StanderdId)
			.OnDelete(DeleteBehavior.Restrict);
		
		builder.HasOne(e => e.InquiryPreference)
			.WithMany()
			.HasForeignKey(s => s.InquiryPreferenceId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}