using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFeePolicyConfiguration : IEntityTypeConfiguration<AdmissionCrmFeePolicy>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmFeePolicy> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmFeePolicies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).HasMaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength).IsRequired();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.ApplicabilityJson).HasMaxLength(AdmissionCrmConsts.MaxFinancialPolicyJsonLength).IsRequired();
		builder.Property(x => x.ComponentsJson).HasMaxLength(AdmissionCrmConsts.MaxFinancialPolicyJsonLength).IsRequired();
		builder.Property(x => x.Currency).HasMaxLength(AdmissionCrmConsts.MaxCurrencyCodeLength).IsRequired();
		builder.Property(x => x.RequiredAmount).HasPrecision(18, 2);
		builder.Property(x => x.RequiredPercentage).HasPrecision(7, 4);
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.GateType).HasConversion<int>().IsRequired();
		builder.Property(x => x.GateMilestone).HasConversion<int>().IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code, x.Version })
			.IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
	}
}
