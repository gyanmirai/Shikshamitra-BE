using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDataQualityIssueConfiguration : IEntityTypeConfiguration<AdmissionCrmDataQualityIssue>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmDataQualityIssue> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmDataQualityIssues", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Fingerprint).HasMaxLength(128).IsRequired();
		builder.Property(x => x.Title).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.DetailsJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.ImpactedReportsJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.ResolutionReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Fingerprint }).IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status, x.Severity });
		builder.HasIndex(x => new { x.TenantId, x.LeadId });
		builder.HasIndex(x => new { x.TenantId, x.StudentId });
	}
}
