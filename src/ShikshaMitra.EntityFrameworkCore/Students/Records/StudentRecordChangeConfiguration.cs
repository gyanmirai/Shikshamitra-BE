using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentRecordChangeConfiguration : IEntityTypeConfiguration<StudentRecordChange>
{
	public void Configure(EntityTypeBuilder<StudentRecordChange> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordChanges", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Section).HasMaxLength(40).IsRequired();
		builder.Property(x => x.Action).HasMaxLength(40).IsRequired();
		builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
		builder.Property(x => x.ClientKey).HasMaxLength(80).IsRequired();
		builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
		builder.Property(x => x.ExceptionCode).HasMaxLength(120);
		builder.Property(x => x.BeforeJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.PolicyJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.ApprovalStagesJson).HasColumnType("jsonb").IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.ClientKey }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.Status, x.EffectiveAt });
		builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
	}
}
public class StudentRecordChangeDecisionConfiguration : IEntityTypeConfiguration<StudentRecordChangeDecision>
{
	public void Configure(EntityTypeBuilder<StudentRecordChangeDecision> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordChangeDecisions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Decision).HasMaxLength(20).IsRequired();
		builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
		builder.HasIndex(x => new { x.ChangeId, x.Stage }).IsUnique();
		builder.HasOne<StudentRecordChange>().WithMany().HasForeignKey(x => x.ChangeId).OnDelete(DeleteBehavior.Restrict);
	}
}
