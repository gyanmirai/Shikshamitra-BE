using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Curriculum;

public class StudentElectivePreferenceSubmissionConfiguration : IEntityTypeConfiguration<StudentElectivePreferenceSubmission>
{
	public void Configure(EntityTypeBuilder<StudentElectivePreferenceSubmission> builder)
	{
		builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "StudentElectivePreferenceSubmissions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.PreferredSubjectName).IsRequired().HasMaxLength(512);
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.AcademicYearId, x.ElectiveGroupId })
			.IsUnique().HasDatabaseName("UX_ElectivePreference_StudentYearGroup");
		builder.HasIndex(x => new { x.TenantId, x.ClientRequestId })
			.IsUnique().HasDatabaseName("UX_ElectivePreference_ClientRequest");
	}
}
