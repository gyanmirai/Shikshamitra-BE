using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Admissions;

public class AdmissionPaymentTransactionConfiguration : IEntityTypeConfiguration<AdmissionPaymentTransaction>
{
	public void Configure(EntityTypeBuilder<AdmissionPaymentTransaction> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionPaymentTransactions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.PurposeCode).HasMaxLength(32).IsRequired();
		builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
		builder.Property(item => item.Amount).HasPrecision(18, 2).IsRequired();
		builder.Property(item => item.Method).HasMaxLength(32).IsRequired();
		builder.Property(item => item.CollectionMode).HasMaxLength(16).IsRequired();
		builder.Property(item => item.Status).HasMaxLength(32).IsRequired();
		builder.Property(item => item.IdempotencyKey).HasMaxLength(80).IsRequired();
		builder.Property(item => item.ExternalReference).HasMaxLength(160);
		builder.Property(item => item.Provider).HasMaxLength(80);
		builder.Property(item => item.ReceiptNumber).HasMaxLength(80);
		builder.Property(item => item.Notes).HasMaxLength(1024);
		builder.Property(item => item.DecisionReason).HasMaxLength(1024);
		builder.Property(item => item.PolicySnapshotJson).HasMaxLength(8000).IsRequired();
		builder.Property(item => item.LastOperationType).HasMaxLength(16);
		builder.Property(item => item.LastOperationIdempotencyKey).HasMaxLength(80);
		builder.HasIndex(item => new { item.TenantId, item.StageInstanceId, item.AttemptNumber }).IsUnique();
		builder.HasIndex(item => new { item.TenantId, item.StageInstanceId, item.IdempotencyKey }).IsUnique();
		builder.HasIndex(item => new { item.TenantId, item.Provider, item.ExternalReference }).IsUnique();
		builder.HasIndex(item => new { item.TenantId, item.ReceiptNumber }).IsUnique();
		builder.HasIndex(item => new { item.TenantId, item.ApplicationId, item.StageInstanceId, item.Status });
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(item => item.ApplicationId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplicationStageInstance>().WithMany().HasForeignKey(item => item.StageInstanceId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
