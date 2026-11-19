using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentRecordDocumentEventConfiguration : IEntityTypeConfiguration<StudentRecordDocumentEvent>
{
	public void Configure(EntityTypeBuilder<StudentRecordDocumentEvent> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordDocumentEvents", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention();
		b.Property(x => x.DocumentType).HasMaxLength(20).IsRequired(); b.Property(x => x.Action).HasMaxLength(20).IsRequired(); b.Property(x => x.Reason).HasMaxLength(2000);
		b.HasIndex(x => new { x.TenantId, x.StudentId, x.CreationTime }); b.HasIndex(x => new { x.TenantId, x.DocumentId, x.CreationTime });
	}
}
