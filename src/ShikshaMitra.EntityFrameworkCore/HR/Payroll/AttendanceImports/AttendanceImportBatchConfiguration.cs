using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public class AttendanceImportBatchConfiguration : IEntityTypeConfiguration<AttendanceImportBatch>
{
	public void Configure(EntityTypeBuilder<AttendanceImportBatch> builder)
	{
		builder.ToTable("HR_AttendanceImportBatches");
		builder.ConfigureByConvention();

		builder.HasKey(x => x.Id);

		builder.Property(x => x.FileName)
			.IsRequired()
			.HasMaxLength(255);

		builder.Property(x => x.ImportedByName)
			.IsRequired()
			.HasMaxLength(128);

		builder.Property(x => x.ImportedAt)
			.IsRequired();

		builder.Property(x => x.PayrollMonthYear)
			.HasMaxLength(7);

		builder.Property(x => x.PayrollMonth)
			.HasMaxLength(20);

		builder.Property(x => x.Status)
			.IsRequired()
			.HasConversion<int>();

		builder.Property(x => x.PreviewToken)
			.HasMaxLength(64);

		builder.Property(x => x.OriginalFileBlobName)
			.HasMaxLength(512);

		builder.Property(x => x.OriginalFileContentType)
			.HasMaxLength(100);

		builder.Property(x => x.ErrorsSummary)
			.HasConversion(
				v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
				v => string.IsNullOrEmpty(v)
					? new List<string>()
					: JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
			.HasColumnType("jsonb");

		builder.Property(x => x.WarningsSummary)
			.HasConversion(
				v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
				v => string.IsNullOrEmpty(v)
					? new List<string>()
					: JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
			.HasColumnType("jsonb");

		builder.Property(x => x.RunSource)
			.IsRequired()
			.HasConversion<int>();

		builder.Property(x => x.ExternalCorrelationId)
			.HasMaxLength(128);

		builder.Property(x => x.IntegrationProvider)
			.HasMaxLength(128);

		builder.Property(x => x.IsBatchLocked)
			.IsRequired();

		builder.HasIndex(x => x.ExternalCorrelationId)
			.HasDatabaseName("IX_HR_AttendanceImportBatches_ExternalCorrelationId");

		builder.HasIndex(x => x.RunSource)
			.HasDatabaseName("IX_HR_AttendanceImportBatches_RunSource");

		builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_HR_AttendanceImportBatches_TenantId");
		builder.HasIndex(x => x.ImportedAt).HasDatabaseName("IX_HR_AttendanceImportBatches_ImportedAt");
		builder.HasIndex(x => x.PayrollMonthYear).HasDatabaseName("IX_HR_AttendanceImportBatches_PayrollMonthYear");
		builder.HasIndex(x => x.Status).HasDatabaseName("IX_HR_AttendanceImportBatches_Status");
	}
}
