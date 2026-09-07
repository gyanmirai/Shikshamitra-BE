using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.SeatAllocation;

public class ExamSeatingPublicationConfiguration : IEntityTypeConfiguration<ExamSeatingPublication>
{
    public void Configure(EntityTypeBuilder<ExamSeatingPublication> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamSeatingPublications", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ExamId).IsRequired();
        builder.Property(x => x.PublishedAt).IsRequired();
        builder.Property(x => x.PublishedByUserId).IsRequired();

        builder.HasIndex(x => x.ExamId).IsUnique();
    }
}
