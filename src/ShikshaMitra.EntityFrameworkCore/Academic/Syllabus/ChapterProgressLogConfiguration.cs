using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Syllabus;

public class ChapterProgressLogConfiguration : IEntityTypeConfiguration<ChapterProgressLog>
{
    public void Configure(EntityTypeBuilder<ChapterProgressLog> builder)
    {
        builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "ChapterProgressLogs", ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();

        builder.Property(x => x.ChapterId)
            .IsRequired();

        builder.Property(x => x.DivisionId)
            .IsRequired();

        builder.Property(x => x.Progress)
            .IsRequired();

        builder.Property(x => x.ChangedByName)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(x => new { x.ChapterId, x.DivisionId });
    }
}
