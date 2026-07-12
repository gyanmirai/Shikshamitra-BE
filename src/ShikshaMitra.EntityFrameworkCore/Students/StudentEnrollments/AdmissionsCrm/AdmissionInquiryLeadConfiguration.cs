using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionInquiryLeadConfiguration : IEntityTypeConfiguration<AdmissionInquiryLead>
{
	public void Configure(EntityTypeBuilder<AdmissionInquiryLead> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionInquiryLeads",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.InquirySourceSnapshot)
			.HasMaxLength(AdmissionCrmConsts.MaxSourceSnapshotLength);
		builder.Property(x => x.CampaignName)
			.HasMaxLength(AdmissionCrmConsts.MaxCampaignLength);
		builder.Property(x => x.CampusBranchName)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.PreferredCommunicationLanguage)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.FirstName)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.MiddleName)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.LastName)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.DateOfBirth);
		builder.Property(x => x.ParentGuardianName)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.PrimaryContactNo)
			.HasMaxLength(AdmissionCrmConsts.MaxContactLength);
		builder.Property(x => x.NormalizedPrimaryContactNo)
			.HasMaxLength(AdmissionCrmConsts.MaxContactLength);
		builder.Property(x => x.Email)
			.HasMaxLength(AdmissionCrmConsts.MaxEmailLength);
		builder.Property(x => x.NormalizedEmail)
			.HasMaxLength(AdmissionCrmConsts.MaxEmailLength);
		builder.Property(x => x.AdmissionIntent)
			.HasMaxLength(AdmissionCrmConsts.MaxIntentLength)
			.IsRequired();
		builder.Property(x => x.DesiredClassName)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.Notes)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.ArchiveReason)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.ReopenReason)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.MergeReason)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.DuplicateContinueReason)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.RegistrationFormNumber)
			.HasMaxLength(AdmissionCrmConsts.MaxRegistrationFormNumberLength);
		builder.Property(x => x.Channel)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.Stage)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.Priority)
			.HasConversion<int>()
			.IsRequired();

		builder.HasOne(x => x.InquirySource)
			.WithMany()
			.HasForeignKey(x => x.InquirySourceId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Stage });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Priority });
		builder.HasIndex(x => new { x.TenantId, x.MergedIntoLeadId });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.NormalizedPrimaryContactNo });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.NormalizedEmail });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.RegistrationFormNumber })
			.HasDatabaseName("IX_AppAdmissionInquiryLeads_TenantId_AcademicYearId_RegistrationF~1");
	}
}
