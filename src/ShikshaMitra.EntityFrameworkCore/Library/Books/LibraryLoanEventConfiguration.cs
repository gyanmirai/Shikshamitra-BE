using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Shared;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Books;

public sealed class LibraryLoanEventConfiguration : IEntityTypeConfiguration<LibraryLoanEvent>
{
	public void Configure(EntityTypeBuilder<LibraryLoanEvent> builder)
	{
		builder.ToTable("AppLibraryLoanEvents", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.Reason).HasMaxLength(500).IsRequired();
		builder.Property(row => row.BeforeJson).HasColumnType("text").IsRequired();
		builder.Property(row => row.AfterJson).HasColumnType("text").IsRequired();
		builder.HasOne<BookIssue>().WithMany().HasForeignKey(row => row.LoanId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryBorrowerScopeCapture>().WithMany().HasForeignKey(row => new { row.TenantId, row.BeforeBorrowerScopeCaptureId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryBorrowerScopeCapture>().WithMany().HasForeignKey(row => new { row.TenantId, row.AfterBorrowerScopeCaptureId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.LoanId, row.Sequence }).IsUnique().AreNullsDistinct(false);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
