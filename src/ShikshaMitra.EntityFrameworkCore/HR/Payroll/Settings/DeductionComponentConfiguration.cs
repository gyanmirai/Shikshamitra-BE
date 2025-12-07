using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// EF Core configuration for DeductionComponent entity
/// </summary>
public class DeductionComponentConfiguration : IEntityTypeConfiguration<DeductionComponent>
{
	public void Configure(EntityTypeBuilder<DeductionComponent> builder)
	{
		builder.ToTable("AppDeductionComponents");

		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(DeductionComponentConsts.MaxNameLength);

		builder.Property(x => x.Description)
			.HasMaxLength(DeductionComponentConsts.MaxDescriptionLength);

		builder.Property(x => x.Amount)
			.IsRequired()
			.HasMaxLength(DeductionComponentConsts.MaxAmountLength);

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
		builder.HasIndex(x => x.Name)
			.IsUnique()
			.HasDatabaseName("IX_DeductionComponents_Name");

		builder.HasIndex(x => new { x.TenantId, x.IsActive })
			.HasDatabaseName("IX_DeductionComponents_TenantId_IsActive");

		builder.HasIndex(x => x.DisplayOrder)
			.HasDatabaseName("IX_DeductionComponents_DisplayOrder");
	}
}
