using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryReceivedCopyConfiguration : IEntityTypeConfiguration<LibraryReceivedCopy>
{
	public void Configure(EntityTypeBuilder<LibraryReceivedCopy> builder)
	{
		builder.ToTable("AppLibraryReceivedCopies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.AccessionNumber).HasMaxLength(50).IsRequired();
		builder.HasOne<BookCopy>().WithMany().HasForeignKey(row => row.CopyId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => row.CopyId).IsUnique();
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
