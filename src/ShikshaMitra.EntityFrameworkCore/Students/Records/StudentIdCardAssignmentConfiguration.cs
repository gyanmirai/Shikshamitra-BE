using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentIdCardAssignmentConfiguration : IEntityTypeConfiguration<StudentIdCardAssignment>
{
	public void Configure(EntityTypeBuilder<StudentIdCardAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentIdCardAssignments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(assignment => assignment.TenantId).IsRequired();
		builder.HasIndex(assignment => new { assignment.TenantId, assignment.AcademicYearId, assignment.StandardId })
			.HasDatabaseName("IX_StudentIdCardAssignments_TenantYearClass").IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(assignment => new { assignment.TenantId, assignment.TemplateId })
			.HasDatabaseName("IX_StudentIdCardAssignments_TenantTemplate").HasFilter("\"IsDeleted\" = false");
		builder.HasOne<StudentIdCardTemplate>().WithMany().HasForeignKey(assignment => assignment.TemplateId)
			.HasConstraintName("FK_StudentIdCardAssignments_Templates")
			.OnDelete(DeleteBehavior.Restrict);
	}
}
