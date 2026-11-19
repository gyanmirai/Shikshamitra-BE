using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.Records;

public class StudentRecordReturnHandoffConfiguration : IEntityTypeConfiguration<StudentRecordReturnHandoff>
{
	public void Configure(EntityTypeBuilder<StudentRecordReturnHandoff> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordReturnHandoffs", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention(); builder.Property(x => x.Route).HasMaxLength(32).IsRequired();
		builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
		builder.Property(x => x.RequestJson).HasColumnType("text").IsRequired(); builder.Property(x => x.PolicyJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.SourceSnapshotJson).HasColumnType("text").IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.TransitionId }).IsUnique().HasFilter("\"TransitionId\" IS NOT NULL");
		builder.HasIndex(x => new { x.TenantId, x.AdmissionApplicationId }).IsUnique().HasFilter("\"TransitionId\" IS NOT NULL AND \"AdmissionApplicationId\" IS NOT NULL");
		builder.HasIndex(x => new { x.TenantId, x.AdmissionInquiryId }).IsUnique().HasFilter("\"TransitionId\" IS NOT NULL AND \"AdmissionInquiryId\" IS NOT NULL");
		builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.AcademicYearId });
	}
}
