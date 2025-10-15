using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum
{
    public class ChapterDocumentConfiguration : IEntityTypeConfiguration<ChapterDocument>
    {
        public void Configure(EntityTypeBuilder<ChapterDocument> builder)
        {
            builder.ToTable(
                ShikshaMitraConsts.DbTablePrefix + "ChapterDocuments",
                ShikshaMitraConsts.DbSchema
            );

            builder.ConfigureByConvention();

            builder.Property(x => x.Base64Content)
                .IsRequired()
                .HasColumnType("text");
        }
    }
}
