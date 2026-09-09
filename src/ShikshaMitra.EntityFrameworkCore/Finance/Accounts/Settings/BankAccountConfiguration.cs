using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
	public void Configure(EntityTypeBuilder<BankAccount> builder)
	{
		builder.ToTable("Finance_BankAccounts", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Code).HasMaxLength(AccountSettingConsts.MaxCodeLength);
		builder.Property(x => x.NormalizedCode).HasMaxLength(AccountSettingConsts.MaxCodeLength);
		builder.Property(x => x.BankName).IsRequired().HasMaxLength(AccountSettingConsts.MaxNameLength);
		builder.Property(x => x.BranchName).IsRequired().HasMaxLength(AccountSettingConsts.MaxNameLength);
		builder.Property(x => x.AccountHolderName).IsRequired().HasMaxLength(AccountSettingConsts.MaxNameLength);
		builder.Property(x => x.AccountNumber).IsRequired().HasMaxLength(34);
		builder.Property(x => x.IfscCode).IsRequired().HasMaxLength(11);
		builder.Property(x => x.UpiId).HasMaxLength(128);
		builder.Property(x => x.DeactivationReason).HasMaxLength(AccountSettingConsts.MaxDeactivationReasonLength);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.NormalizedCode }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.AccountNumber })
			.IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.AccountHeadId }).IsUnique();
		builder.HasOne<AccountHead>()
			.WithMany()
			.HasForeignKey(x => x.AccountHeadId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
