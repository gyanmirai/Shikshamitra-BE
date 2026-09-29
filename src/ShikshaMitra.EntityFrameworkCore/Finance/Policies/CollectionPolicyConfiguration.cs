using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Policies;

public class CollectionPolicyConfiguration : IEntityTypeConfiguration<CollectionPolicy>
{
	public void Configure(EntityTypeBuilder<CollectionPolicy> builder)
	{
		builder.ToTable("Finance_CollectionPolicies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.PolicyName).IsRequired().HasMaxLength(128);
		builder.Property(x => x.PolicyCode).IsRequired().HasMaxLength(32);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.Status).IsRequired().HasConversion<int>();
		builder.Property(x => x.GracePeriodType).IsRequired().HasConversion<int>();
		builder.Property(x => x.LateFeeType).IsRequired().HasConversion<int>();
		builder.Property(x => x.PercentageBase).IsRequired().HasConversion<int>();
		builder.Property(x => x.CompoundFrequency).IsRequired().HasConversion<int>();
		builder.Property(x => x.RoundingMethod).IsRequired().HasConversion<int>();

		builder.Property(x => x.FlatAmount).HasPrecision(18, 2);
		builder.Property(x => x.Percentage).HasPrecision(9, 4);
		builder.Property(x => x.AmountPerDay).HasPrecision(18, 2);
		builder.Property(x => x.AmountPerWeek).HasPrecision(18, 2);
		builder.Property(x => x.AmountPerMonth).HasPrecision(18, 2);
		builder.Property(x => x.MaximumLateFeeAmount).HasPrecision(18, 2);
		builder.Property(x => x.MaximumWaiverAmount).HasPrecision(18, 2);
		builder.Property(x => x.MaximumWaiverPercentage).HasPrecision(9, 4);
		builder.Property(x => x.ApprovalThresholdAmount).HasPrecision(18, 2);

		builder.Property(x => x.LateFeeApplyOnFeeHeadsJson).IsRequired().HasColumnType("text");
		builder.Property(x => x.WaiverApplyOnFeeHeadsJson).IsRequired().HasColumnType("text");
		builder.Property(x => x.EscalationRulesJson).IsRequired().HasColumnType("text");
		builder.Property(x => x.FeeHeadOverridesJson).IsRequired().HasColumnType("text");
		builder.Property(x => x.ApprovalRoleIdsJson).IsRequired().HasColumnType("text");

		builder.Property(x => x.Version).IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.PolicyName })
			.IsUnique()
			.HasDatabaseName("IX_Finance_CollectionPolicies_Tenant_AcademicYear_Name");

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsDefault })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false AND \"Status\" = 0 AND \"IsDefault\" = true")
			.HasDatabaseName("IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default");
	}
}
