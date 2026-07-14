using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPersonConfiguration : IEntityTypeConfiguration<AdmissionCrmPerson>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPerson> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPersons", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.FirstName).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.MiddleName).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.LastName).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.Phone).HasMaxLength(AdmissionCrmConsts.MaxContactLength);
		builder.Property(x => x.NormalizedPhone).HasMaxLength(AdmissionCrmConsts.MaxContactLength);
		builder.Property(x => x.Email).HasMaxLength(AdmissionCrmConsts.MaxEmailLength);
		builder.Property(x => x.NormalizedEmail).HasMaxLength(AdmissionCrmConsts.MaxEmailLength);
		builder.Property(x => x.AadhaarEncrypted).HasMaxLength(1000);
		builder.Property(x => x.AadhaarHash).HasMaxLength(AdmissionCrmConsts.MaxTokenHashLength);
		builder.Property(x => x.AadhaarLast4).HasMaxLength(4);
		builder.Property(x => x.PenNumber).HasMaxLength(AdmissionCrmConsts.MaxIdentityNumberLength);
		builder.Property(x => x.ApaarId).HasMaxLength(AdmissionCrmConsts.MaxIdentityNumberLength);
		builder.Property(x => x.UdiseStudentCode).HasMaxLength(AdmissionCrmConsts.MaxIdentityNumberLength);
		builder.HasIndex(x => new { x.TenantId, x.AadhaarHash }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"AadhaarHash\" IS NOT NULL");
		builder.HasIndex(x => new { x.TenantId, x.PenNumber }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"PenNumber\" IS NOT NULL");
		builder.HasIndex(x => new { x.TenantId, x.ApaarId }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"ApaarId\" IS NOT NULL");
		builder.HasIndex(x => new { x.TenantId, x.NormalizedPhone });
		builder.HasIndex(x => new { x.TenantId, x.NormalizedEmail });
	}
}

public class AdmissionCrmHouseholdConfiguration : IEntityTypeConfiguration<AdmissionCrmHousehold>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmHousehold> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmHouseholds", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).HasMaxLength(AdmissionCrmConsts.MaxHouseholdCodeLength).IsRequired();
		builder.Property(x => x.DisplayName).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.PreferredLanguage).HasMaxLength(AdmissionCrmConsts.MaxLanguageCodeLength).IsRequired();
		builder.Property(x => x.AddressesJson).HasMaxLength(AdmissionCrmConsts.MaxHouseholdJsonLength).IsRequired();
		builder.Property(x => x.CommunicationPreferencesJson).HasMaxLength(AdmissionCrmConsts.MaxHouseholdJsonLength).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
	}
}

public class AdmissionCrmHouseholdMembershipConfiguration : IEntityTypeConfiguration<AdmissionCrmHouseholdMembership>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmHouseholdMembership> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmHouseholdMemberships", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasIndex(x => new { x.TenantId, x.HouseholdId, x.PersonId, x.IsActive })
			.IsUnique().HasFilter("\"IsDeleted\" = false AND \"IsActive\" = true");
		builder.HasIndex(x => new { x.TenantId, x.PersonId, x.IsActive });
	}
}

public class AdmissionCrmApplicantIdentityProfileConfiguration : IEntityTypeConfiguration<AdmissionCrmApplicantIdentityProfile>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmApplicantIdentityProfile> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmApplicantIdentityProfiles", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.ConflictReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.OverrideReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.LeadId }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
	}
}

public class AdmissionCrmGuardianRelationshipConfiguration : IEntityTypeConfiguration<AdmissionCrmGuardianRelationship>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmGuardianRelationship> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmGuardianRelationships", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.AuthorityEvidenceReference).HasMaxLength(AdmissionCrmConsts.MaxFinanceReferenceLength);
		builder.Property(x => x.ProtectedRestrictionEncrypted).HasMaxLength(AdmissionCrmConsts.MaxProtectedRestrictionLength);
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.GuardianPersonId, x.IsActive })
			.IsUnique().HasFilter("\"IsDeleted\" = false AND \"IsActive\" = true");
		builder.HasIndex(x => new { x.TenantId, x.HouseholdId, x.IsActive });
	}
}

public class AdmissionCrmIdentityConsentConfiguration : IEntityTypeConfiguration<AdmissionCrmIdentityConsent>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmIdentityConsent> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmIdentityConsents", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Purpose).HasMaxLength(AdmissionCrmConsts.MaxPurposeLength).IsRequired();
		builder.Property(x => x.TextVersion).HasMaxLength(AdmissionCrmConsts.MaxConsentVersionLength).IsRequired();
		builder.Property(x => x.Language).HasMaxLength(AdmissionCrmConsts.MaxLanguageCodeLength).IsRequired();
		builder.Property(x => x.Actor).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.Channel).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.WithdrawalReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.PersonId, x.Purpose, x.RecordedAt });
	}
}
