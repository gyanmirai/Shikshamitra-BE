using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.EntityFrameworkCore.Modeling;

public class DueFeeConfiguration : IEntityTypeConfiguration<DueFee>
{
	public void Configure(EntityTypeBuilder<DueFee> builder)
	{
		builder.ToTable("Finance_DueFees", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.InstallmentName).HasMaxLength(100);
		builder.Property(x => x.ScheduleInstanceKey).IsRequired().HasMaxLength(128);
		builder.Property(x => x.ScheduleSource).HasConversion<int>();
		builder.Property(x => x.OriginalAmount).HasPrecision(18, 2);
		builder.Property(x => x.ScheduledAmount).HasPrecision(18, 2);
		builder.Property(x => x.NetAmount).HasPrecision(18, 2);
		builder.Property(x => x.PaidAmount).HasPrecision(18, 2);
		builder.Property(x => x.OutstandingAmount).HasPrecision(18, 2);
		builder.Property(x => x.RefundedAmount).HasPrecision(18, 2);
		builder.Property(x => x.ConcessionAmount).HasPrecision(18, 2);
		builder.Property(x => x.AdjustmentCreditAmount).HasPrecision(18, 2);
		builder.Property(x => x.ProrationFactor).HasPrecision(12, 8);
		builder.Property(x => x.ProrationRuleType).HasConversion<int>();
		builder.Property(x => x.ProrationRoundingMode).HasConversion<int>();
		builder.Property(x => x.ProrationExplanation).HasMaxLength(500);
		builder.HasOne<ShikshaMitra.Domain.Finance.Policies.ProrationPolicy>()
			.WithMany().HasForeignKey(x => x.ProrationPolicyId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<ShikshaMitra.Domain.Finance.Policies.FinancePolicyVersion>()
			.WithMany().HasForeignKey(x => x.ProrationPolicyVersionId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(x => x.ProrationPolicyId)
			.HasDatabaseName("IX_DueFees_ProrationPolicyId");
		builder.HasIndex(x => x.ProrationPolicyVersionId)
			.HasDatabaseName("IX_DueFees_ProrationPolicyVersionId");

		// Legacy month/year uniqueness retained for backward-compatible queries,
		// but schedule-aware generation uniqueness uses ScheduleInstanceKey.
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.FeeHeadId, x.Month, x.Year })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false AND \"ScheduleSource\" = 1")
			.HasDatabaseName("IX_DueFees_TenantId_StudentId_FeeHeadId_Month_Year_Legacy");

		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.ScheduleInstanceKey })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false")
			.HasDatabaseName("IX_DueFees_TenantId_StudentId_ScheduleInstanceKey");
	}
}
