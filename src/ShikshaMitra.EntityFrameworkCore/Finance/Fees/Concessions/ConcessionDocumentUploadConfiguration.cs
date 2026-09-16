using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public class ConcessionDocumentUploadConfiguration : IEntityTypeConfiguration<ConcessionDocumentUpload>
{
	public void Configure(EntityTypeBuilder<ConcessionDocumentUpload> builder)
	{
		builder.ToTable("Finance_ConcessionDocumentUploads");
		builder.ConfigureByConvention();
		builder.Property(x => x.Url).IsRequired().HasMaxLength(2048);
		builder.Property(x => x.ExpiresOn).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.Url })
			.IsUnique()
			.AreNullsDistinct(false)
			.HasDatabaseName("UX_ConcessionDocumentUpload_Tenant_Url");
		builder.HasIndex(x => new { x.TenantId, x.UploadedBy, x.ConcessionTypeId, x.DocumentId })
			.HasDatabaseName("IX_ConcessionDocumentUpload_Owner_Requirement");
		builder.HasIndex(x => new { x.TenantId, x.ExpiresOn, x.ConcessionRequestId })
			.HasDatabaseName("IX_ConcessionDocumentUpload_Expiry");
		builder.HasOne<ConcessionRequest>()
			.WithMany()
			.HasForeignKey(x => x.ConcessionRequestId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
