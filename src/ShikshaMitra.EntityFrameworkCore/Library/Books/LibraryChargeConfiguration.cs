using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Books;

public class LibraryChargeConfiguration : IEntityTypeConfiguration<LibraryCharge>
{
    public void Configure(EntityTypeBuilder<LibraryCharge> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryCharges", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
		builder.Property(row => row.Currency).HasMaxLength(3);
		builder.Property(row => row.AcademicYearEvidenceJson).HasColumnType("text");
		builder.Property(row => row.SourceDateEvidenceJson).HasColumnType("text");
		builder.HasOne<ShikshaMitra.Finance.ExternalReceivables.FinanceExternalReceivableSourceLink>().WithMany()
			.HasForeignKey(row => row.FinanceSourceLinkId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.MemberId).IsRequired();
        builder.Property(x => x.MemberRole).IsRequired();
        builder.Property(x => x.ChargeCategoryId).IsRequired();
        builder.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(x => x.ChargeDate).IsRequired(false);
        builder.Property(x => x.DueDate).IsRequired(false);
        builder.Property(x => x.PaymentStatus).IsRequired().HasDefaultValue(ChargePaymentStatus.Pending);
        builder.Property(x => x.Remarks).HasMaxLength(500);

        builder.HasIndex(x => x.MemberId);
        builder.HasIndex(x => x.ChargeCategoryId);
        builder.HasIndex(x => x.BookIssueId);
    }
}
