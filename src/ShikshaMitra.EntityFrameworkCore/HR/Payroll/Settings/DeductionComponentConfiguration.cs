using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DeductionComponentConfiguration : IEntityTypeConfiguration<DeductionComponent>
{
	public void Configure(EntityTypeBuilder<DeductionComponent> builder)
	{
		builder.ToTable("AppDeductionComponents");

		builder.ConfigureByConvention();

		builder.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(DeductionComponentConsts.MaxNameLength);

		builder.Property(x => x.Description)
			.HasMaxLength(DeductionComponentConsts.MaxDescriptionLength);

		builder.Property(x => x.ComponentType)
			.IsRequired();

		builder.Property(x => x.CalculationType)
			.IsRequired();

		builder.Property(x => x.Amount)
			.HasColumnType("decimal(18,2)")
			.IsRequired(false);

		builder.Property(x => x.Percentage)
			.HasColumnType("decimal(5,2)")
			.IsRequired(false);

		builder.Property(x => x.SelectedDeductionComponentId)
			.IsRequired(false);

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

		builder.HasIndex(x => new { x.TenantId, x.Name })
			.IsUnique()
			.HasDatabaseName("IX_DeductionComponents_TenantId_Name");

		builder.HasIndex(x => new { x.TenantId, x.IsActive })
			.HasDatabaseName("IX_DeductionComponents_TenantId_IsActive");

		builder.HasIndex(x => x.DisplayOrder)
			.HasDatabaseName("IX_DeductionComponents_DisplayOrder");
	}
}
