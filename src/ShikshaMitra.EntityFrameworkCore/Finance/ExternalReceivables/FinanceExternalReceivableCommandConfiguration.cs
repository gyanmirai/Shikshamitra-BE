using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalReceivableCommandConfiguration : IEntityTypeConfiguration<FinanceExternalReceivableCommand>
{
	public void Configure(EntityTypeBuilder<FinanceExternalReceivableCommand> builder)
	{
		builder.ToTable("Finance_ExternalReceivableCommands", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Operation).HasMaxLength(32).IsRequired();
		builder.Property(row => row.IdempotencyKey).HasMaxLength(100).IsRequired();
		builder.Property(row => row.CommandFingerprint).HasMaxLength(64).IsRequired();
		builder.Property(row => row.ResultEntityType).HasMaxLength(32);
		builder.HasIndex(row => new { row.TenantId, row.Operation, row.IdempotencyKey }).IsUnique()
			.HasDatabaseName("UX_ExternalReceivableCommands_Tenant_Operation_Key");
		builder.HasOne<FinanceExternalReceivableSourceLink>().WithMany()
			.HasForeignKey(row => row.SourceLinkId).OnDelete(DeleteBehavior.Restrict);
	}
}
