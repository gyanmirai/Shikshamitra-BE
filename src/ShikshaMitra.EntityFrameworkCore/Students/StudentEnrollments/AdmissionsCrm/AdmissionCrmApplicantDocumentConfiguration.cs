using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmApplicantDocumentConfiguration : IEntityTypeConfiguration<AdmissionCrmApplicantDocument>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmApplicantDocument> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmApplicantDocuments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.RequirementKey).HasMaxLength(80).IsRequired();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.BlobName).HasMaxLength(AdmissionCrmConsts.MaxDocumentBlobNameLength);
		builder.Property(x => x.FileName).HasMaxLength(AdmissionCrmConsts.MaxDocumentFileNameLength);
		builder.Property(x => x.ContentType).HasMaxLength(AdmissionCrmConsts.MaxDocumentContentTypeLength);
		builder.Property(x => x.Sha256Hash).HasMaxLength(AdmissionCrmConsts.MaxDocumentHashLength);
		builder.Property(x => x.DecisionReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.ChecklistAssignmentId, x.RequirementKey, x.Version }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.Status });
	}
}
