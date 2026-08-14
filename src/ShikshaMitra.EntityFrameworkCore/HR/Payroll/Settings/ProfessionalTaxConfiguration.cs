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
		builder.Property(x => x.JurisdictionCode).IsRequired().HasMaxLength(16);
		builder.Property(x => x.EffectiveFrom).HasColumnType("date");
		builder.Property(x => x.EffectiveTo).HasColumnType("date");
		builder.Property(x => x.FromSalary).HasPrecision(18, 2);
		builder.Property(x => x.ToSalary).HasPrecision(18, 2);
		builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
		builder.Property(x => x.DeductInCessationMonth).HasDefaultValue(true);
		builder.HasIndex(x => new
		{
			x.TenantId,
			x.JurisdictionCode,
			x.EffectiveFrom,
			x.FromSalary
		}).IsUnique().HasFilter("\"IsDeleted\" = false");
	}
}
