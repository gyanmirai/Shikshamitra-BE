using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class AccountHeadConfiguration : IEntityTypeConfiguration<AccountHead>
{
	public void Configure(EntityTypeBuilder<AccountHead> builder)
	{
		builder.ToTable("Finance_AccountHeads", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(AccountSettingConsts.MaxNameLength);
		builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(AccountSettingConsts.MaxNameLength);
		builder.Property(x => x.Description).HasMaxLength(AccountSettingConsts.MaxDescriptionLength);
		builder.Property(x => x.DeactivationReason).HasMaxLength(AccountSettingConsts.MaxDeactivationReasonLength);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.NormalizedName }).IsUnique();
		builder.HasOne<AccountHead>()
			.WithMany()
			.HasForeignKey(x => x.ParentAccountHeadId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
