using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Policies;

public class RefundPolicyConfiguration : IEntityTypeConfiguration<RefundPolicy>
{
	public void Configure(EntityTypeBuilder<RefundPolicy> builder)
	{
		builder.ToTable("Finance_RefundPolicies", ShikshaMitraConsts.DbSchema);
        builder.HasOne<ShikshaMitra.Domain.Finance.Accounts.Settings.TransactionCategory>().WithMany().HasForeignKey(x=>x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShikshaMitra.Domain.Finance.Accounts.Settings.BankAccount>().WithMany().HasForeignKey(x=>x.DefaultPayingBankAccountId).OnDelete(DeleteBehavior.Restrict);
		builder.ConfigureByConvention();
		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.PolicyName).IsRequired().HasMaxLength(128);
		builder.Property(x => x.PolicyCode).IsRequired().HasMaxLength(32);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.Status).IsRequired().HasConversion<int>();
		builder.Property(x => x.IsDefault).IsRequired();
		builder.Property(x => x.FeeHeadScope).IsRequired().HasConversion<int>();
		builder.Property(x => x.ApplicableFeeHeadIdsJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.AmountMode).IsRequired().HasConversion<int>();
		builder.Property(x => x.PercentageValue).HasPrecision(9, 4);
		builder.Property(x => x.FixedAmount).HasPrecision(18, 2);
		builder.Property(x => x.MaximumPerRequest).HasPrecision(18, 2);
		builder.Property(x => x.MaximumPerStudentYear).HasPrecision(18, 2);
		builder.Property(x => x.RoundingMethod).IsRequired().HasConversion<int>();
		builder.Property(x => x.DeductionType).IsRequired().HasConversion<int>();
		builder.Property(x => x.DeductionValue).HasPrecision(18, 4);
		builder.Property(x => x.MaximumDeduction).HasPrecision(18, 2);
		builder.Property(x => x.DeductionExplanation).HasMaxLength(500);
		builder.Property(x => x.AllowedPayoutMethodsJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.Version).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.PolicyName }).IsUnique()
			.HasDatabaseName("IX_Finance_RefundPolicies_Tenant_Year_Name");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.PolicyCode }).IsUnique()
			.HasDatabaseName("IX_Finance_RefundPolicies_Tenant_Year_Code");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsDefault })
			.IsUnique().HasFilter("\"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true")
			.HasDatabaseName("IX_Finance_RefundPolicies_ActiveDefault");
		// PostgreSQL composite uniqueness treats null TenantId values as distinct.
		builder.HasIndex(x => x.AcademicYearId).IsUnique()
			.HasFilter("\"TenantId\" IS NULL AND \"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true")
			.HasDatabaseName("IX_Finance_RefundPolicies_Host_ActiveDefault");
	}
}
