using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class TransactionCategoryConfiguration : IEntityTypeConfiguration<TransactionCategory>
{
	public void Configure(EntityTypeBuilder<TransactionCategory> builder)
	{
		builder.ToTable("Finance_TransactionCategories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Ignore(x => x.IsSystemCategory);

		builder.Property(x => x.Code).HasMaxLength(AccountSettingConsts.MaxCodeLength);
		builder.Property(x => x.NormalizedCode).HasMaxLength(AccountSettingConsts.MaxCodeLength);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(AccountSettingConsts.MaxNameLength);
		builder.Property(x => x.NormalizedName).HasMaxLength(AccountSettingConsts.MaxNameLength);
		builder.Property(x => x.Description).HasMaxLength(AccountSettingConsts.MaxDescriptionLength);
		builder.Property(x => x.DeactivationReason).HasMaxLength(AccountSettingConsts.MaxDeactivationReasonLength);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.CategoryType, x.NormalizedCode }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.CategoryType, x.NormalizedName }).IsUnique();
		builder.HasOne<AccountHead>()
			.WithMany()
			.HasForeignKey(x => x.DefaultAccountHeadId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.OwnsMany(x => x.Tags, tag =>
		{
			tag.WithOwner().HasForeignKey("TransactionCategoryId");
			tag.Property(t => t.Value)
				.HasColumnName("Value")
				.HasMaxLength(Tag.MaxLength)
				.IsRequired();
			tag.ToTable(ShikshaMitraConsts.DbTablePrefix + "IncomeCategoryTags", ShikshaMitraConsts.DbSchema);
		});
	}
}
