using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Examination.Settings;

public class ExamTypeConfiguration : IEntityTypeConfiguration<ExamType>
{
    public void Configure(EntityTypeBuilder<ExamType> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamTypes", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention(); //auto configure for the base class props

        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Description).HasMaxLength(512);

        builder.HasIndex(x => new { x.Name, x.AcademicMediumId }).IsUnique();
    }
}
