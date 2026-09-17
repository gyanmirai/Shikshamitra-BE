using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public class ConcessionCommandReceiptConfiguration : IEntityTypeConfiguration<ConcessionCommandReceipt>
{
	public void Configure(EntityTypeBuilder<ConcessionCommandReceipt> builder)
	{
		builder.ToTable("Finance_ConcessionCommandReceipts");
		builder.ConfigureByConvention();
		builder.Property(x => x.Action).IsRequired().HasMaxLength(32);
		builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
		builder.Property(x => x.PayloadHash).IsRequired().HasMaxLength(64).IsFixedLength();
		builder.HasIndex(x => new { x.TenantId, x.ActorId, x.Action, x.IdempotencyKey })
			.IsUnique()
			.AreNullsDistinct(false)
			.HasDatabaseName("UX_ConcessionCommandReceipt_Tenant_Actor_Action_Key");
		builder.HasIndex(x => x.ConcessionRequestId)
			.HasDatabaseName("IX_ConcessionCommandReceipt_Request");
		builder.HasOne<ConcessionRequest>()
			.WithMany()
			.HasForeignKey(x => x.ConcessionRequestId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
