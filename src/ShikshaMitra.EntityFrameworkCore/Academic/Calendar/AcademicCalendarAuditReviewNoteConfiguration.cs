using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Calendar;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Calendar;

public class AcademicCalendarAuditReviewNoteConfiguration : IEntityTypeConfiguration<AcademicCalendarAuditReviewNote>
{
    public void Configure(EntityTypeBuilder<AcademicCalendarAuditReviewNote> builder)
    {
        builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "CalendarAuditReviewNotes", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.Note).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.CreatedByName).HasMaxLength(128);
        builder.Property(x => x.CreatedByRole).HasMaxLength(64);

        builder.HasIndex(x => new { x.TenantId, x.AuditLogId, x.CreatedAt });
    }
}
