using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// EF Core configuration for EarningComponent entity
/// </summary>
public class EarningComponentConfiguration : IEntityTypeConfiguration<EarningComponent>
{
	public void Configure(EntityTypeBuilder<EarningComponent> builder)
	{
		builder.ToTable("AppEarningComponents");

		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(EarningComponentConsts.MaxNameLength);

		builder.Property(x => x.Description)
			.HasMaxLength(EarningComponentConsts.MaxDescriptionLength);

		builder.Property(x => x.Amount)
			.IsRequired()
			.HasMaxLength(EarningComponentConsts.MaxAmountLength);

		builder.Property(x => x.ComponentType)
			.IsRequired();

		builder.Property(x => x.DependOn)
			.IsRequired();

		builder.Property(x => x.VisibleInPayslip)
			.IsRequired()
			.HasDefaultValue(true);

		builder.Property(x => x.DisplayOrder)
			.IsRequired()
			.HasDefaultValue(0);

		builder.Property(x => x.IsActive)
			.IsRequired()
			.HasDefaultValue(true);

		// Indexes for performance
		// Unique index on TenantId + Name to ensure uniqueness per tenant
		builder.HasIndex(x => new { x.TenantId, x.Name })
			.IsUnique()
			.HasDatabaseName("IX_EarningComponents_TenantId_Name");

		builder.HasIndex(x => new { x.TenantId, x.IsActive })
			.HasDatabaseName("IX_EarningComponents_TenantId_IsActive");

		builder.HasIndex(x => x.DisplayOrder)
			.HasDatabaseName("IX_EarningComponents_DisplayOrder");
	}
}
