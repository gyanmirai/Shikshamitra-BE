using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Documents;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentDocumentAccessAuditConfiguration : IEntityTypeConfiguration<StudentDocumentAccessAudit>
{
	public void Configure(EntityTypeBuilder<StudentDocumentAccessAudit> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentDocumentAccessAudits", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.FileName).HasMaxLength(255);
		builder.Property(x => x.ContentType).HasMaxLength(100);
		builder.Property(x => x.Sha256Hash).HasMaxLength(128);
		builder.Property(x => x.ReasonCode).HasMaxLength(100);

		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.EventTime });
		builder.HasIndex(x => new { x.TenantId, x.DocumentId, x.EventTime });
		builder.HasIndex(x => new { x.TenantId, x.Action, x.EventTime });
	}
}
