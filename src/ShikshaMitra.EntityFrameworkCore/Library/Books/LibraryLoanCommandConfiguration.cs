using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Books;

public sealed class LibraryLoanCommandConfiguration : IEntityTypeConfiguration<LibraryLoanCommand>
{
	public void Configure(EntityTypeBuilder<LibraryLoanCommand> builder)
	{
		builder.ToTable("AppLibraryLoanCommands", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.IdempotencyKey).HasMaxLength(100).IsRequired();
		builder.Property(row => row.PayloadFingerprint).HasMaxLength(64).IsRequired();
		builder.Property(row => row.ResultJson).HasColumnType("text").IsRequired();
		builder.HasOne<BookIssue>().WithMany().HasForeignKey(row => row.LoanId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.ActorId, row.Operation, row.IdempotencyKey })
			.IsUnique().AreNullsDistinct(false);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
