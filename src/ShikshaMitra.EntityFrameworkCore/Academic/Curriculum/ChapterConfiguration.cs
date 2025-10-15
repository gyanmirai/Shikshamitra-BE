using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum
{
    public class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
    {
        public void Configure(EntityTypeBuilder<Chapter> builder)
        {
            builder.ToTable(
                ShikshaMitraConsts.DbTablePrefix + "Chapters",
                ShikshaMitraConsts.DbSchema
            );
            builder.HasKey(sc => sc.Id);

            builder.Property(sc => sc.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(sc => sc.Description)
                .HasMaxLength(1000);

            builder.Property(sc => sc.SubjectId)
                .IsRequired();

            builder.ConfigureByConvention();
        }
    }
}
