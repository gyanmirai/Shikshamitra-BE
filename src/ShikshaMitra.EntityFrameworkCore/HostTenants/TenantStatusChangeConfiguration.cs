using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HostTenants;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HostTenants;

public class TenantStatusChangeConfiguration : IEntityTypeConfiguration<TenantStatusChange>
{
	public void Configure(EntityTypeBuilder<TenantStatusChange> builder)
	{
		builder.ToTable("TenantStatusChanges");
		builder.ConfigureByConvention();
		builder.Property(x => x.Reason).HasMaxLength(HostTenantConsts.MaxReasonLength);
		builder.HasIndex(x => new { x.TargetTenantId, x.CreationTime });
	}
}
