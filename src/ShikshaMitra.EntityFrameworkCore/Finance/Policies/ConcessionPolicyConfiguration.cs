using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Policies;

public class ConcessionPolicyConfiguration : IEntityTypeConfiguration<ConcessionPolicy>
{
	public void Configure(EntityTypeBuilder<ConcessionPolicy> builder)
	{
		builder.ToTable("Finance_ConcessionPolicies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.PolicyName).IsRequired().HasMaxLength(128);
		builder.Property(x => x.PolicyCode).IsRequired().HasMaxLength(32);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.Status).IsRequired().HasConversion<int>();
		builder.Property(x => x.IsDefault).IsRequired();
		builder.Property(x => x.ValueSource).IsRequired().HasConversion<int>();
		builder.Property(x => x.CalculationMethod).IsRequired().HasConversion<int>();
		builder.Property(x => x.ApplyOn).IsRequired().HasConversion<int>();
		builder.Property(x => x.AutoCreateRefund).IsRequired();
		builder.Property(x => x.RoundingMethod).IsRequired().HasConversion<int>();
		builder.Property(x => x.PercentageValue).HasPrecision(9, 4);
		builder.Property(x => x.FixedAmount).HasPrecision(18, 2);
		builder.Property(x => x.MaximumPercentage).HasPrecision(9, 4);
		builder.Property(x => x.MaximumFixedAmount).HasPrecision(18, 2);
		builder.Property(x => x.Version).IsRequired();
		builder.Property(x => x.ApplicableConcessionTypeIdsJson).IsRequired().HasColumnType("text").HasDefaultValue("[]");
		builder.Property(x => x.FeeHeadScope).IsRequired().HasConversion<int>();
		builder.Property(x => x.ApplicableFeeHeadIdsJson).IsRequired().HasColumnType("text").HasDefaultValue("[]");
		builder.Ignore(x => x.ApplicableConcessionTypeIds);
		builder.Ignore(x => x.ApplicableFeeHeadIds);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.PolicyName }).IsUnique()
			.HasDatabaseName("IX_Finance_ConcessionPolicies_Tenant_Year_Name");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.PolicyCode }).IsUnique()
			.HasDatabaseName("IX_Finance_ConcessionPolicies_Tenant_Year_Code");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsDefault })
			.IsUnique().HasFilter("\"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true")
			.HasDatabaseName("IX_Finance_ConcessionPolicies_ActiveDefault");
		// PostgreSQL composite uniqueness treats null TenantId values as distinct.
		builder.HasIndex(x => x.AcademicYearId).IsUnique()
			.HasFilter("\"TenantId\" IS NULL AND \"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true")
			.HasDatabaseName("IX_Finance_ConcessionPolicies_Host_ActiveDefault");
	}
}
