using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Policies;

public class FinancePolicyVersionConfiguration : IEntityTypeConfiguration<FinancePolicyVersion>
{
	public void Configure(EntityTypeBuilder<FinancePolicyVersion> builder)
	{
		builder.ToTable("Finance_PolicyVersions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.PolicyType).IsRequired().HasMaxLength(32);
		builder.Property(x => x.PolicyId).IsRequired();
		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.VersionNumber).IsRequired();
		builder.Property(x => x.PolicyName).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
		builder.Property(x => x.SnapshotJson).IsRequired().HasColumnType("text");
		builder.Property(x => x.ChangeAction).IsRequired().HasMaxLength(64);
		builder.Property(x => x.ChangeReason).HasMaxLength(512);

		builder.HasIndex(x => new { x.TenantId, x.PolicyType, x.PolicyId, x.VersionNumber })
			.IsUnique()
			.HasDatabaseName("IX_Finance_PolicyVersions_Tenant_Type_Policy_Version");

		builder.HasIndex(x => new { x.TenantId, x.PolicyType, x.AcademicYearId })
			.HasDatabaseName("IX_Finance_PolicyVersions_Tenant_Type_AcademicYear");
	}
}
