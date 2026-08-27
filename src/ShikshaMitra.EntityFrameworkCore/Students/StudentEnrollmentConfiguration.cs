using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students;

public class StudentEnrollmentConfiguration : IEntityTypeConfiguration<StudentEnrollment>
{
	public void Configure(EntityTypeBuilder<StudentEnrollment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentEnrollments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.AcademicYearId }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.StandardId, x.DivisionId });
		builder.HasOne<ShikshaMitra.Students.Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
	}
}
