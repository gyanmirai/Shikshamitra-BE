using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public class AttendanceImportRowConfiguration : IEntityTypeConfiguration<AttendanceImportRow>
{
	public void Configure(EntityTypeBuilder<AttendanceImportRow> builder)
	{
		builder.ToTable("HR_AttendanceImportRows");
		builder.ConfigureByConvention();

		builder.HasKey(x => x.Id);

		builder.Property(x => x.Action)
			.IsRequired()
			.HasMaxLength(32);

		builder.Property(x => x.AttendanceDate)
			.IsRequired()
			.HasMaxLength(32);

		builder.Property(x => x.EmployeeCode)
			.IsRequired()
			.HasMaxLength(64);

		builder.Property(x => x.EmployeeName)
			.HasMaxLength(256);

		builder.Property(x => x.ContactNo)
			.HasMaxLength(64);

		builder.Property(x => x.TimeIn)
			.HasMaxLength(32);

		builder.Property(x => x.TimeOut)
			.HasMaxLength(32);

		builder.Property(x => x.Status)
			.HasMaxLength(64);

		builder.Property(x => x.Remarks)
			.HasMaxLength(512);

		builder.Property(x => x.Errors)
			.HasConversion(
				v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
				v => string.IsNullOrEmpty(v)
					? new List<string>()
					: JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
			.HasColumnType("jsonb");

		builder.Property(x => x.Warnings)
			.HasConversion(
				v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
				v => string.IsNullOrEmpty(v)
					? new List<string>()
					: JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
			.HasColumnType("jsonb");

		builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_HR_AttendanceImportRows_TenantId");
		builder.HasIndex(x => x.BatchId).HasDatabaseName("IX_HR_AttendanceImportRows_BatchId");
		builder.HasIndex(x => new { x.BatchId, x.RowNumber }).HasDatabaseName("IX_HR_AttendanceImportRows_BatchId_RowNumber");

		builder.HasOne<AttendanceImportBatch>()
			.WithMany()
			.HasForeignKey(x => x.BatchId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
