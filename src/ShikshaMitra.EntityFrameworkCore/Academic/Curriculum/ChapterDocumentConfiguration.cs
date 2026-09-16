using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Curriculum;

/// <summary>
/// EF Core configuration for ChapterDocument entity
/// </summary>
public class ChapterDocumentConfiguration : IEntityTypeConfiguration<AssignedSubjectDocument>
{
	public void Configure(EntityTypeBuilder<AssignedSubjectDocument> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.AcademicDbTablePrefix + "ChapterDocuments",
			ShikshaMitraConsts.DbSchema
		);

		// Apply ABP conventions
		builder.ConfigureByConvention();
	}
}
