using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProvidentFundConfiguration : IEntityTypeConfiguration<ProvidentFund>
{
	public void Configure(EntityTypeBuilder<ProvidentFund> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ProvidentFunds", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(100);

		builder.Property(x => x.Code)
			.IsRequired()
			.HasMaxLength(20);

		builder.HasIndex(x => new { x.TenantId, x.Code })
			.IsUnique()
			.HasDatabaseName("IX_ProvidentFunds_TenantId_Code")
			.HasFilter("\"IsDeleted\" = false");

		builder.HasIndex(x => new { x.TenantId, x.Name })
			.IsUnique()
			.HasDatabaseName("IX_ProvidentFunds_TenantId_Name")
			.HasFilter("\"IsDeleted\" = false");
	}
}
