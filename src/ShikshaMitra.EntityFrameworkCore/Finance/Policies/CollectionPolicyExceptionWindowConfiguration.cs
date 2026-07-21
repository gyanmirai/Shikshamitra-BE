using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Policies;

public class CollectionPolicyExceptionWindowConfiguration : IEntityTypeConfiguration<CollectionPolicyExceptionWindow>
{
	public void Configure(EntityTypeBuilder<CollectionPolicyExceptionWindow> builder)
	{
		builder.ToTable("Finance_CollectionPolicyExceptionWindows", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Scope).IsRequired().HasConversion<int>();
		builder.Property(x => x.Effect).IsRequired().HasConversion<int>();
		builder.Property(x => x.ReasonCode).IsRequired().HasMaxLength(64);
		builder.Property(x => x.Reason).IsRequired().HasMaxLength(500);

		builder.HasIndex(x => new { x.TenantId, x.Scope, x.ScopeId, x.StartDate, x.EndDate })
			.HasDatabaseName("IX_Finance_ExceptionWindows_Tenant_Scope_DateRange");
	}
}
