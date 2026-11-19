using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Accounts.Posting;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Accounts.Posting;

public class FinancialEventConfiguration : IEntityTypeConfiguration<FinancialEvent>
{
	public void Configure(EntityTypeBuilder<FinancialEvent> builder)
	{
		builder.ToTable("Finance_FinancialEvents", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.EventKey).HasMaxLength(64).IsRequired();
		builder.Property(x => x.SourceType).HasMaxLength(100).IsRequired();
		builder.Property(x => x.Transition).HasMaxLength(100).IsRequired();
		builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
		builder.Property(x => x.Amount).HasColumnType("numeric(18,2)");
		builder.Property(x => x.SnapshotJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.SnapshotHash).HasMaxLength(64).IsRequired();
		builder.HasIndex(x => x.EventKey).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId });
	}
}
