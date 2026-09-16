using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Syllabus;

public class ChapterProgressConfiguration : IEntityTypeConfiguration<ChapterProgress>
{
    public void Configure(EntityTypeBuilder<ChapterProgress> builder)
    {
        builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "ChapterProgresses", ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();

        builder.Property(x => x.ChapterId)
            .IsRequired();

        builder.Property(x => x.DivisionId)
            .IsRequired();

        builder.Property(x => x.Progress)
            .IsRequired();

        builder.Property(x => x.Remarks)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.HasOne(x => x.Chapter)
            .WithMany()
            .HasForeignKey(x => x.ChapterId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.ChapterId, x.DivisionId });
    }
}
