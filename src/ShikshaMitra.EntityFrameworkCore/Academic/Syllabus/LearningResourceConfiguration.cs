using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Syllabus;

public class LearningResourceConfiguration : IEntityTypeConfiguration<LearningResource>
{
	public void Configure(EntityTypeBuilder<LearningResource> builder)
	{
		builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "LearningResources", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Topic).HasMaxLength(256);
		builder.Property(x => x.ResourceType).IsRequired().HasMaxLength(64);
		builder.Property(x => x.BlobName).IsRequired().HasMaxLength(512);
		builder.Property(x => x.FileName).IsRequired().HasMaxLength(256);
		builder.Property(x => x.ContentType).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.Visibility).HasConversion<int>().IsRequired();
		builder.Property(x => x.ReviewRemarks).HasMaxLength(1024);
		builder.Property(x => x.ArchiveReason).HasMaxLength(1024);

		builder.HasIndex(x => new { x.StandardId, x.SubjectId, x.ChapterId });
		builder.HasIndex(x => new { x.Status, x.Visibility });
		builder.HasIndex(x => x.BlobName);
	}
}
