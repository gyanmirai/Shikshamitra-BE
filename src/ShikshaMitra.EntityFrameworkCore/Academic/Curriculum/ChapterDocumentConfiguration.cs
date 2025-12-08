using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Curriculum;

/// <summary>
/// EF Core configuration for ChapterDocument entity
/// </summary>
public class ChapterDocumentConfiguration : IEntityTypeConfiguration<ChapterDocument>
{
	public void Configure(EntityTypeBuilder<ChapterDocument> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "ChapterDocuments",
			ShikshaMitraConsts.DbSchema
		);

		// Apply ABP conventions
		builder.ConfigureByConvention();

		// Properties
		builder.Property(x => x.FileName)
			.IsRequired()
			.HasMaxLength(ChapterConsts.MaxFileNameLength);

		builder.Property(x => x.FileUrl)
			.IsRequired()
			.HasMaxLength(ChapterConsts.MaxFileUrlLength);

		builder.Property(x => x.ContentType)
			.IsRequired()
			.HasMaxLength(100);

		builder.Property(x => x.FileSize)
			.IsRequired();

		builder.Property(x => x.UploadedAt)
			.IsRequired();

		// Relationships
		builder.HasOne(x => x.Chapter)
			.WithMany(x => x.Documents)
			.HasForeignKey(x => x.ChapterId)
			.OnDelete(DeleteBehavior.Cascade);

		// Indexes
		builder.HasIndex(x => x.ChapterId)
			.HasDatabaseName("IX_ChapterDocuments_ChapterId");
	}
}
