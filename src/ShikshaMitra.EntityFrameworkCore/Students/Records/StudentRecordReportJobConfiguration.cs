using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentRecordReportJobConfiguration : IEntityTypeConfiguration<StudentRecordReportJob>
{
	public void Configure(EntityTypeBuilder<StudentRecordReportJob> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordReportJobs", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention();
		b.Property(x => x.ReportType).HasMaxLength(40).IsRequired(); b.Property(x => x.ClientKey).HasMaxLength(80).IsRequired();
		b.Property(x => x.AuthorityHash).HasMaxLength(64).IsRequired(); b.Property(x => x.RequestJson).HasColumnType("jsonb");
		b.Property(x => x.Status).HasMaxLength(20).IsRequired(); b.Property(x => x.BlobName).HasMaxLength(512);
		b.Property(x => x.ErrorCode).HasMaxLength(120);
		b.HasIndex(x => new { x.TenantId, x.RequestedBy, x.ClientKey }).IsUnique(); b.HasIndex(x => new { x.Status, x.LeaseUntil });
	}
}
