using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Policies;

public class HrPolicyVersionConfiguration : IEntityTypeConfiguration<HrPolicyVersion>
{
	public void Configure(EntityTypeBuilder<HrPolicyVersion> builder)
	{
		builder.ToTable("HR_PolicyVersions");
		builder.ConfigureByConvention();

		builder.Property(x => x.PolicyType).IsRequired().HasMaxLength(32);
		builder.Property(x => x.PolicyId).IsRequired();
		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.VersionNumber).IsRequired();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
		builder.Property(x => x.SnapshotJson).IsRequired().HasColumnType("text");
		builder.Property(x => x.ChangeAction).IsRequired().HasMaxLength(64);
		builder.Property(x => x.ChangeReason).HasMaxLength(512);

		builder.HasIndex(x => new { x.TenantId, x.PolicyType, x.PolicyId, x.VersionNumber })
			.IsUnique()
			.HasDatabaseName("IX_HR_PolicyVersions_Tenant_Type_Policy_Version");

		builder.HasIndex(x => new { x.TenantId, x.PolicyType, x.AcademicYearId })
			.HasDatabaseName("IX_HR_PolicyVersions_Tenant_Type_AcademicYear");
	}
}
