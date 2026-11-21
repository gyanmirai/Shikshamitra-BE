using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Setting.Sections;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Library.Policies;

public class LibraryCalendarClosureConfiguration : IEntityTypeConfiguration<LibraryCalendarClosure>
{
	public void Configure(EntityTypeBuilder<LibraryCalendarClosure> builder)
	{
		builder.ToTable("AppLibraryCalendarClosures");
		builder.ConfigureByConvention();
		builder.Property(x => x.Reason).IsRequired().HasMaxLength(500);
		builder.HasOne<LibrarySection>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(x => new { x.TenantId, x.FromDate, x.ToDate, x.SectionId });
	}
}
