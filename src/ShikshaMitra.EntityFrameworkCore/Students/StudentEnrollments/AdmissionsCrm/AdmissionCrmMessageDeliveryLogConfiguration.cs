using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmMessageDeliveryLogConfiguration : IEntityTypeConfiguration<AdmissionCrmMessageDeliveryLog>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmMessageDeliveryLog> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmMessageDeliveryLogs",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.Channel)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.Status)
			.HasMaxLength(120)
			.IsRequired();
		builder.Property(x => x.Recipient)
			.HasMaxLength(AdmissionCrmConsts.MaxEmailLength)
			.IsRequired();
		builder.Property(x => x.NormalizedRecipient)
			.HasMaxLength(AdmissionCrmConsts.MaxEmailLength)
			.IsRequired();
		builder.Property(x => x.ProviderName)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.ProviderMessageId)
			.HasMaxLength(AdmissionCrmConsts.MaxProviderMessageIdLength);
		builder.Property(x => x.SubjectSnapshot)
			.HasMaxLength(AdmissionCrmConsts.MaxMessageSubjectLength);
		builder.Property(x => x.BodySnapshot)
			.HasMaxLength(AdmissionCrmConsts.MaxMessageBodyLength)
			.IsRequired();
		builder.Property(x => x.GeneratedDocumentType).HasConversion<int>();
		builder.Property(x => x.AttachmentLanguage).HasMaxLength(8);
		builder.Property(x => x.AttachmentFileName).HasMaxLength(AdmissionCrmConsts.MaxDocumentBlobNameLength);
		builder.Property(x => x.AttachmentContentType).HasMaxLength(120);
		builder.Property(x => x.StatusDetails)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength)
			.IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.LeadId, x.CreatedAt });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Channel, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Channel, x.ProviderMessageId });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.TemplateId });
	}
}
