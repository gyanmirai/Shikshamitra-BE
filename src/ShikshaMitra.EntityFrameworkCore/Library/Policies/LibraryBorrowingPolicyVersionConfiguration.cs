using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Library.Policies;

public class LibraryBorrowingPolicyVersionConfiguration : IEntityTypeConfiguration<LibraryBorrowingPolicyVersion>
{
	public void Configure(EntityTypeBuilder<LibraryBorrowingPolicyVersion> builder)
	{
		builder.ToTable("AppLibraryBorrowingPolicyVersions");
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(160);
		builder.Property(x => x.ArchiveReason).HasMaxLength(500);
		builder.Ignore(x => x.Specificity);
		builder.Ignore(x => x.LastEffectiveDate);
		builder.OwnsOne(x => x.Terms, terms =>
		{
			terms.Property(x => x.DueDateMode).HasColumnName("DueDateMode").HasDefaultValue(LibraryDueDateMode.Automatic)
				.HasSentinel((LibraryDueDateMode)(-1));
			terms.Property(x => x.MaxBooksAtTime).HasColumnName("MaxBooksAtTime");
			terms.Property(x => x.ReservationLimit).HasColumnName("ReservationLimit");
			terms.Property(x => x.LoanDays).HasColumnName("LoanDays");
			terms.Property(x => x.DayCountingMode).HasColumnName("DayCountingMode");
			terms.Property(x => x.MaxRenewals).HasColumnName("MaxRenewals");
			terms.Property(x => x.RenewalDays).HasColumnName("RenewalDays");
			terms.Property(x => x.PickupWindowDays).HasColumnName("PickupWindowDays");
			terms.Property(x => x.MoveDueDateToNextOpenDay).HasColumnName("MoveDueDateToNextOpenDay");
		});
		builder.Navigation(x => x.Terms).IsRequired();
		builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.OriginAcademicYearId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<BookCategory>().WithMany().HasForeignKey(x => x.BookCategoryId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibrarySection>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<EducationMediumStage>().WithMany().HasForeignKey(x => x.EducationMediumStageId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Standard>().WithMany().HasForeignKey(x => x.StandardId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(x => new { x.TenantId, x.PolicyId, x.Version }).IsUnique().AreNullsDistinct(false);
		builder.HasIndex(x => new { x.TenantId, x.Role, x.Status, x.EffectiveFrom, x.EffectiveTo });
	}
}
