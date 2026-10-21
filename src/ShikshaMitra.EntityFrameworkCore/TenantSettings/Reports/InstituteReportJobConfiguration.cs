using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.TenantSettings.Reports;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.TenantSettings.Reports;

internal class InstituteReportJobConfiguration : IEntityTypeConfiguration<InstituteReportJob>
{
	public void Configure(EntityTypeBuilder<InstituteReportJob> builder)
	{
		builder.ToTable("AppInstituteReportJobs", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.RequestJson).IsRequired();
		builder.Property(x => x.Status).HasMaxLength(24).IsRequired();
		builder.Property(x => x.BlobName).HasMaxLength(256);
		builder.Property(x => x.ErrorMessage).HasMaxLength(500);
		builder.HasIndex(x => new { x.TenantId, x.RequestedBy, x.CreationTime });
		builder.HasIndex(x => x.ExpiresAt);
	}
}
