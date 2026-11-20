using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Shared;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Books;

public class LibraryCopyEventConfiguration : IEntityTypeConfiguration<LibraryCopyEvent>
{
	public void Configure(EntityTypeBuilder<LibraryCopyEvent> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryCopyEvents", ShikshaMitraConsts.DbSchema,
			table => table.HasCheckConstraint("CK_LibraryCopyEvents_Actor", "(\"ActorKind\" = 1 AND \"ActorId\" IS NOT NULL AND \"SystemPurpose\" IS NULL) OR (\"ActorKind\" = 2 AND \"ActorId\" IS NULL AND \"SystemPurpose\" IS NOT NULL AND \"SystemPurpose\" IN (1, 2))"));
		builder.ConfigureByConvention();
		builder.Property(row => row.ActorKind).HasDefaultValue(LibraryActorKind.User);
		builder.Property(row => row.AccessionNumber).HasMaxLength(50).IsRequired();
		builder.Property(row => row.Reason).HasMaxLength(500).IsRequired();
		builder.Property(row => row.PreviousRemarks).HasMaxLength(500);
		builder.Property(row => row.NextRemarks).HasMaxLength(500);
		builder.HasOne<BookCopy>().WithMany().HasForeignKey(row => row.CopyId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Book>().WithMany().HasForeignKey(row => row.BookId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<BookIssue>().WithMany().HasForeignKey(row => row.RelatedLoanId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.CopyId, row.Sequence }).IsUnique().AreNullsDistinct(false);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
