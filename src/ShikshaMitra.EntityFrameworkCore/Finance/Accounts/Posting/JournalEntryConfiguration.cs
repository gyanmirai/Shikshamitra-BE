using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Accounts.Posting;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Accounts.Posting;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
	public void Configure(EntityTypeBuilder<JournalEntry> builder)
	{
		builder.ToTable("Finance_JournalEntries", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.JournalNumber).HasMaxLength(50).IsRequired();
		builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
		builder.Ignore(x => x.TotalDebits);
		builder.Ignore(x => x.TotalCredits);
		builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.JournalEntryId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
		builder.HasIndex(x => x.FinancialEventId).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.JournalNumber }).IsUnique();
		builder.HasIndex(x => x.JournalNumber).IsUnique().HasFilter("\"TenantId\" IS NULL");
		builder.HasOne<FinancialEvent>().WithMany().HasForeignKey(x => x.FinancialEventId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.CorrectsJournalEntryId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
	public void Configure(EntityTypeBuilder<JournalLine> builder)
	{
		builder.ToTable("Finance_JournalLines", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Debit).HasColumnType("numeric(18,2)");
		builder.Property(x => x.Credit).HasColumnType("numeric(18,2)");
		builder.HasOne<ShikshaMitra.Domain.Finance.Accounts.Settings.AccountHead>()
			.WithMany().HasForeignKey(x => x.AccountHeadId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(x => x.AccountHeadId);
		builder.ToTable(table => table.HasCheckConstraint("CK_Finance_JournalLines_OneSide",
			"(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)"));
	}
}
