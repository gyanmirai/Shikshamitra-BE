using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.TenantSettings.SchoolIdentity;

internal class SchoolIdentityProfileConfiguration : IEntityTypeConfiguration<SchoolIdentityProfile>
{
	public void Configure(EntityTypeBuilder<SchoolIdentityProfile> builder)
	{
		builder.ToTable("AppSchoolIdentityProfiles", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.ScopeKey).HasMaxLength(96).IsRequired();
		builder.Property(x => x.ChoicesJson).HasMaxLength(8192).IsRequired();
		builder.Ignore(x => x.Choices);
		builder.HasIndex(x => new { x.TenantId, x.ScopeKey }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId });
	}
}

internal class SchoolIdentityAssetConfiguration : IEntityTypeConfiguration<SchoolIdentityAsset>
{
	public void Configure(EntityTypeBuilder<SchoolIdentityAsset> builder)
	{
		builder.ToTable("AppSchoolIdentityAssets", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.BlobName).HasMaxLength(256).IsRequired();
		builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
		builder.Property(x => x.ContentType).HasMaxLength(64).IsRequired();
		builder.Property(x => x.IsRetained).IsConcurrencyToken();
		builder.Property(x => x.IsCleanupPending).IsConcurrencyToken();
		builder.HasIndex(x => new { x.TenantId, x.IsRetained, x.CreationTime });
	}
}

internal class SchoolIdentityRevisionConfiguration : IEntityTypeConfiguration<SchoolIdentityRevision>
{
	public void Configure(EntityTypeBuilder<SchoolIdentityRevision> builder)
	{
		builder.ToTable("AppSchoolIdentityRevisions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.ChoicesJson).HasMaxLength(8192).IsRequired();
		builder.Property(x => x.Reason).HasMaxLength(256).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.ProfileId, x.CreationTime });
	}
}
