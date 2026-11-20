using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationBaselineConfiguration : IEntityTypeConfiguration<LibraryStockVerificationBaseline>
{
	public void Configure(EntityTypeBuilder<LibraryStockVerificationBaseline> builder)
	{
		builder.ToTable("AppLibraryStockVerificationBaselines", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.AccessionNumber).HasMaxLength(BookCopy.MaximumAccessionLength);
		builder.HasOne<LibraryStockVerificationSession>().WithMany().HasForeignKey(row => new { row.TenantId, row.SessionId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).HasConstraintName("FK_LibraryStockVerificationBaseline_Session").OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.SessionId, row.CopyId })
			.IsUnique().HasDatabaseName("UX_LibraryStockVerificationBaseline_Tenant_Session_Copy");
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
