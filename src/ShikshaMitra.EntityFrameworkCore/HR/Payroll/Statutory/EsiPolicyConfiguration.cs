using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EsiPolicyConfiguration : IEntityTypeConfiguration<EsiPolicy>
{
	public void Configure(EntityTypeBuilder<EsiPolicy> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EsiPolicies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Code).IsRequired().HasMaxLength(60);
		builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => x.TenantId).IsUnique().HasFilter("\"IsDefault\" = true AND \"IsDeleted\" = false");
	}
}
