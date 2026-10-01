using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Accounts.Posting;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Accounts.Posting;

public class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("Finance_AccountingPeriods", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.StartsOn, x.EndsOn });
    }
}

public class AccountsPostingConfigurationConfiguration : IEntityTypeConfiguration<AccountsPostingConfiguration>
{
    public void Configure(EntityTypeBuilder<AccountsPostingConfiguration> builder)
    {
        builder.ToTable("Finance_AccountsPostingConfigurations", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId }).IsUnique();
        builder.HasOne<ShikshaMitra.Domain.Finance.Accounts.Settings.AccountHead>()
            .WithMany().HasForeignKey(x => x.CashAccountHeadId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentDestinationRuleConfiguration : IEntityTypeConfiguration<PaymentDestinationRule>
{
    public void Configure(EntityTypeBuilder<PaymentDestinationRule> builder)
    {
        builder.ToTable("Finance_PaymentDestinationRules", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Direction, x.Method, x.EffectiveFrom });
        builder.HasOne<ShikshaMitra.Domain.Finance.Accounts.Settings.AccountHead>()
            .WithMany().HasForeignKey(x => x.AssetAccountHeadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShikshaMitra.Domain.Finance.Accounts.Settings.BankAccount>()
            .WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
