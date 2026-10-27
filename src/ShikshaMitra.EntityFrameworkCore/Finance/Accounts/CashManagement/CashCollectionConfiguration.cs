using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public class CashCollectionConfiguration : IEntityTypeConfiguration<CashCollection>
{
	public void Configure(EntityTypeBuilder<CashCollection> b)
	{
		b.ToTable("Finance_CashCollections", ShikshaMitraConsts.DbSchema);
		b.ConfigureByConvention();
		b.HasIndex(x => new { x.TenantId, x.IncomeSourceKeyPrefix }).IsUnique();
		b.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
		b.Property(x => x.IncomeSourceKeyPrefix).HasMaxLength(200).IsRequired();
		b.Property(x => x.Reference).HasMaxLength(200).IsRequired();
		b.Property(x => x.PayerName).HasMaxLength(500).IsRequired();
		b.Property(x => x.BankLabel).HasMaxLength(500);
		b.Property(x => x.Notes).HasMaxLength(2000);
		b.Property(x => x.Amount).HasPrecision(18, 2);
		b.Property(x => x.SpentAmount).HasPrecision(18, 2);
		b.Property(x => x.DepositedAmount).HasPrecision(18, 2);
		b.Property(x => x.InTransitAmount).HasPrecision(18, 2);
		b.Ignore(x => x.RemainingAmount);
		b.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
		b.HasOne<ShikshaMitra.Finance.Fees.FeeCollection.FinanceFeeAccount>().WithMany()
			.HasForeignKey(x => x.FinanceFeeAccountId).OnDelete(DeleteBehavior.Restrict);
		b.OwnsMany(x => x.Deposits, d =>
		{
			d.ToTable("Finance_CashDeposits", ShikshaMitraConsts.DbSchema); d.WithOwner().HasForeignKey("CashCollectionId");
			d.HasKey(x => x.Id); d.Property(x => x.Id).ValueGeneratedNever();
			d.Property(x => x.Amount).HasPrecision(18, 2); d.Property(x => x.CreditedAmount).HasPrecision(18, 2);
			d.Property(x => x.Reference).HasMaxLength(200).IsRequired(); d.Property(x => x.Notes).HasMaxLength(2000);
			d.OwnsMany(x => x.Credits, c =>
			{
				c.ToTable("Finance_CashDepositCredits", ShikshaMitraConsts.DbSchema); c.WithOwner().HasForeignKey("CashDepositId");
				c.HasKey(x => x.Id); c.Property(x => x.Id).ValueGeneratedNever(); c.Property(x => x.Amount).HasPrecision(18, 2);
				c.Property(x => x.Reference).HasMaxLength(200).IsRequired(); c.Property(x => x.Notes).HasMaxLength(2000);
			});
		});
		b.OwnsMany(x => x.Expenses, e =>
		{
			e.ToTable("Finance_CashExpenses", ShikshaMitraConsts.DbSchema); e.WithOwner().HasForeignKey("CashCollectionId");
			e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever(); e.Property(x => x.Name).HasMaxLength(200).IsRequired();
			e.Property(x => x.Amount).HasPrecision(18, 2);
		});
		b.OwnsMany(x => x.Commands, c =>
		{
			c.ToTable("Finance_CashCommands", ShikshaMitraConsts.DbSchema); c.WithOwner().HasForeignKey("CashCollectionId");
			c.HasKey("CashCollectionId", "Id"); c.Property(x => x.Id).ValueGeneratedNever();
			c.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired(); c.Property(x => x.ResultJson).IsRequired();
		});
	}
}
