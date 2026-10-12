using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentIdCardConfiguration : IEntityTypeConfiguration<StudentIdCard>, IEntityTypeConfiguration<StudentIdCardTemplate>
{
	public void Configure(EntityTypeBuilder<StudentIdCard> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentIdCards", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention();
		b.Property(x => x.CardNumber).HasMaxLength(80).IsRequired(); b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired(); b.Property(x => x.Status).HasMaxLength(20).IsRequired();
		b.Property(x => x.StudentSnapshotJson).HasColumnType("jsonb").IsRequired(); b.Property(x => x.TemplateSnapshotJson).HasColumnType("jsonb").IsRequired();
		b.HasIndex(x => new { x.TenantId, x.CardNumber }).IsUnique(); b.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique(); b.HasIndex(x => new { x.TenantId, x.StudentId, x.CreationTime });
		b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
	}
	public void Configure(EntityTypeBuilder<StudentIdCardTemplate> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentIdCardTemplates", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention();
		b.Property(x => x.ConfigurationJson).HasColumnType("jsonb").IsRequired(); b.HasIndex(x => x.TenantId);
	}
}
