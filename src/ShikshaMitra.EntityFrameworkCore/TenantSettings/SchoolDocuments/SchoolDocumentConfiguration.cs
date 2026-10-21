using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.TenantSettings.SchoolDocuments;

internal class SchoolDocumentConfiguration : IEntityTypeConfiguration<SchoolDocument>
{
	public void Configure(EntityTypeBuilder<SchoolDocument> builder)
	{
		builder.ToTable("AppSchoolDocuments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.Name).HasMaxLength(SchoolDocumentConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.Notes).HasMaxLength(SchoolDocumentConsts.MaxNotesLength);
		builder.Property(x => x.UploadedBy).HasMaxLength(256).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.IsDeleted, x.Category, x.CreationTime });
	}
}

internal class SchoolDocumentFileConfiguration : IEntityTypeConfiguration<SchoolDocumentFile>
{
	public void Configure(EntityTypeBuilder<SchoolDocumentFile> builder)
	{
		builder.ToTable("AppSchoolDocumentFiles", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
		builder.Property(x => x.ContentType).HasMaxLength(64).IsRequired();
		builder.Property(x => x.BlobName).HasMaxLength(256).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.DocumentId, x.IsDeleted });
		builder.HasOne<SchoolDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
	}
}
