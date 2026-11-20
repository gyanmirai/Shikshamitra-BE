using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Shared;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Books;

public class BookIssueConfiguration : IEntityTypeConfiguration<BookIssue>
{
    public void Configure(EntityTypeBuilder<BookIssue> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "BookIssues", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.BookCopyId).IsRequired();
        builder.Property(x => x.AccessionNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.MemberId).IsRequired();
        builder.Property(x => x.MemberRole).IsRequired();
        builder.Property(x => x.IssueDate).IsRequired(false);
        builder.Property(x => x.DueDate).IsRequired(false);
        builder.Property(x => x.PolicySnapshotJson).HasColumnType("text");
        builder.Property(x => x.BorrowerSnapshotJson).HasColumnType("text");
        builder.Property(x => x.OriginalCalendarSnapshotJson).HasColumnType("text");
		builder.HasOne<LibraryBorrowerScopeCapture>().WithMany().HasForeignKey(row => new { row.TenantId, row.BorrowerScopeCaptureId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status, x.SchoolDueDate });
        builder.Property(x => x.Status).IsRequired().HasDefaultValue(BookIssueStatus.Issued);
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Remarks).HasMaxLength(500);

        builder.HasIndex(x => x.BookCopyId);
        builder.HasIndex(x => x.MemberId);
        builder.HasIndex(x => x.AccessionNumber);
    }
}
