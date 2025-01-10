using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentDocumentConfiguration : IEntityTypeConfiguration<StudentDocument>
{
	public void Configure(EntityTypeBuilder<StudentDocument> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentDocuments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}