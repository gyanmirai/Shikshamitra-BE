using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProfessionalTaxConfiguration : IEntityTypeConfiguration<ProfessionalTax>
{
	public void Configure(EntityTypeBuilder<ProfessionalTax> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ProfessionalTaxes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

	}
}