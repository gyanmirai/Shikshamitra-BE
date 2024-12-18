using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayElementConfiguration : IEntityTypeConfiguration<PayElement>
{
	public void Configure(EntityTypeBuilder<PayElement> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PayElements", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Configure the DependentProperties as an owned type
		builder.OwnsOne(e => e.Dependent, dp =>
		{
			dp.Property(p => p.ElementId)
				.IsRequired()
				.HasMaxLength(50);
			dp.Property(p => p.Percentage);
			dp.Property(p => p.Amount);
		});
	}
}