using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students;

public class StudentPreviousSchoolDetailConfiguration : IEntityTypeConfiguration<StudentPreviousSchoolDetail>
{
	public void Configure(EntityTypeBuilder<StudentPreviousSchoolDetail> builder)
	{
		builder.ToTable("StudentPreviousSchoolDetails");

		builder.ConfigureByConvention();

		// Configure scalar properties
		builder.Property(x => x.SchoolName).HasMaxLength(300);
		builder.Property(x => x.SchoolAddress).HasMaxLength(500);
		builder.Property(x => x.Board).HasMaxLength(100);
		builder.Property(x => x.Medium).HasMaxLength(100);
		builder.Property(x => x.LastClassAttended).HasMaxLength(100);
		builder.Property(x => x.SchoolIndexNumber).HasMaxLength(50);
		builder.Property(x => x.TCNumber).HasMaxLength(50);
		builder.Property(x => x.PercentageObtained).HasPrecision(5, 2);
		builder.Property(x => x.AcademicYear).HasMaxLength(20);
		builder.Property(x => x.ReasonForLeaving).HasMaxLength(500);
		builder.Property(x => x.Remarks).HasMaxLength(1000);
	}
}
