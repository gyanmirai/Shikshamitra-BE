using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.TenantSettings.DocumentTypes;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

/// <summary>
/// Entity Framework Core configuration for StudentDocument entity
/// </summary>
public class StudentDocumentConfiguration : IEntityTypeConfiguration<StudentDocument>
{
	public void Configure(EntityTypeBuilder<StudentDocument> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentDocuments", ShikshaMitraConsts.DbSchema);
		
		// Apply default configurations based on ABP conventions
		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.BlobName)
			.IsRequired()
			.HasMaxLength(500);

		builder.Property(x => x.FileName)
			.IsRequired()
			.HasMaxLength(255);

		builder.Property(x => x.ContentType)
			.IsRequired()
			.HasMaxLength(100);

		builder.Property(x => x.Size)
			.IsRequired();

		// Configure relationships
		builder.HasOne<Student>()
			.WithMany()
			.HasForeignKey(x => x.StudentId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired();

		builder.HasOne<DocumentType>()
			.WithMany()
			.HasForeignKey(x => x.DocumentTypeId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired();

		// Add indexes for better query performance
		builder.HasIndex(x => x.StudentId);
		builder.HasIndex(x => new { x.StudentId, x.DocumentTypeId });
		builder.HasIndex(x => x.DocumentTypeId);
	}
}
