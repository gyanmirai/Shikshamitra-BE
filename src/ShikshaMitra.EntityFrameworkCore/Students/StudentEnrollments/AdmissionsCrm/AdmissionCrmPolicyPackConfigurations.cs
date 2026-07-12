using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPolicyPackConfiguration : IEntityTypeConfiguration<AdmissionCrmPolicyPack>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPolicyPack> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPolicyPacks", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).HasMaxLength(AdmissionCrmConsts.MaxPolicyCodeLength).IsRequired();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.Jurisdiction).HasMaxLength(AdmissionCrmConsts.MaxPolicyJurisdictionLength).IsRequired();
		builder.Property(x => x.Board).HasMaxLength(AdmissionCrmConsts.MaxPolicyJurisdictionLength).IsRequired();
		builder.Property(x => x.SourcesJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.RulesJson).HasColumnType("text").IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.Code, x.Version }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.Status, x.ReviewDueAt });
	}
}

public class AdmissionCrmPolicyAdoptionConfiguration : IEntityTypeConfiguration<AdmissionCrmPolicyAdoption>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPolicyAdoption> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPolicyAdoptions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.ApplicabilityJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.OverridesJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.ImpactPreviewJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.OverrideReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.PolicyPackId, x.IsActive });
	}
}

public class AdmissionCrmPolicyEvaluationConfiguration : IEntityTypeConfiguration<AdmissionCrmPolicyEvaluation>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPolicyEvaluation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPolicyEvaluations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.RuleCode).HasMaxLength(AdmissionCrmConsts.MaxPolicyCodeLength).IsRequired();
		builder.Property(x => x.InputSnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.EvidenceJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.Explanation).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.PolicyAdoptionId, x.RuleCode, x.EvaluatedAt });
	}
}

public class AdmissionCrmPolicyExceptionConfiguration : IEntityTypeConfiguration<AdmissionCrmPolicyException>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPolicyException> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPolicyExceptions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Reason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.Property(x => x.EvidenceReference).HasMaxLength(AdmissionCrmConsts.MaxFinanceReferenceLength).IsRequired();
		builder.Property(x => x.DecisionReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.EvaluationId, x.Status });
	}
}
