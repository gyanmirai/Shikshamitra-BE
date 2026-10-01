using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Policies;

public class ProrationPolicyConfiguration : IEntityTypeConfiguration<ProrationPolicy>
{
	public void Configure(EntityTypeBuilder<ProrationPolicy> builder)
	{
		builder.ToTable("Finance_ProrationPolicies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.PolicyName).IsRequired().HasMaxLength(128);
		builder.Property(x => x.PolicyCode).IsRequired().HasMaxLength(32);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.Status).IsRequired().HasConversion<int>();
		builder.Property(x => x.IsDefault).IsRequired();
		builder.Property(x => x.Version).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.PolicyName }).IsUnique()
			.HasDatabaseName("IX_Finance_ProrationPolicies_Tenant_Year_Name");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.PolicyCode }).IsUnique()
			.HasDatabaseName("IX_Finance_ProrationPolicies_Tenant_Year_Code");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsDefault })
			.IsUnique().HasFilter("\"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true")
			.HasDatabaseName("IX_Finance_ProrationPolicies_ActiveDefault");
		// PostgreSQL composite uniqueness treats null TenantId values as distinct.
		builder.HasIndex(x => x.AcademicYearId).IsUnique()
			.HasFilter("\"TenantId\" IS NULL AND \"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true")
			.HasDatabaseName("IX_Finance_ProrationPolicies_Host_ActiveDefault");
		builder.HasMany(x => x.Rules).WithOne().HasForeignKey(x => x.ProrationPolicyId)
			.OnDelete(DeleteBehavior.Cascade);
		builder.Navigation(x => x.Rules).AutoInclude();
	}
}

public class ProrationPolicyRuleConfiguration : IEntityTypeConfiguration<ProrationPolicyRule>
{
	public void Configure(EntityTypeBuilder<ProrationPolicyRule> builder)
	{
		builder.ToTable("Finance_ProrationPolicyRules", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.RuleType).IsRequired().HasConversion<int>();
		builder.Property(x => x.RoundingMode).IsRequired().HasConversion<int>();
		builder.Property(x => x.AfterCutoffAction).HasConversion<int>();
		builder.HasIndex(x => new { x.ProrationPolicyId, x.FeeHeadId }).IsUnique()
			.HasDatabaseName("IX_Finance_ProrationPolicyRules_Policy_FeeHead");
	}
}
