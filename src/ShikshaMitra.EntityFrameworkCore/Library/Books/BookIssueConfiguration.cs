using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
        builder.Property(x => x.IssueDate).IsRequired();
        builder.Property(x => x.DueDate).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasDefaultValue(BookIssueStatus.Issued);
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Remarks).HasMaxLength(500);

        builder.HasIndex(x => x.BookCopyId);
        builder.HasIndex(x => x.MemberId);
        builder.HasIndex(x => x.AccessionNumber);
    }
}
