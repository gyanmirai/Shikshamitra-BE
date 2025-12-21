using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// EF Core configuration for SalarySlip entity
/// Includes proper indexing for performance optimization
/// </summary>
public class SalarySlipConfiguration : IEntityTypeConfiguration<SalarySlip>
{
	public void Configure(EntityTypeBuilder<SalarySlip> builder)
	{
		builder.ToTable("HR_SalarySlips");

		builder.ConfigureByConvention(); // ABP convention configuration

		// Primary Key
		builder.HasKey(x => x.Id);

		// Indexes for query optimization
		builder.HasIndex(x => x.EmployeeId)
			.HasDatabaseName("IX_HR_SalarySlips_EmployeeId");

		builder.HasIndex(x => new { x.EmployeeId, x.MonthYear })
			.IsUnique()
			.HasDatabaseName("IX_HR_SalarySlips_EmployeeId_MonthYear");

		builder.HasIndex(x => new { x.Year, x.Month })
			.HasDatabaseName("IX_HR_SalarySlips_Year_Month");

		builder.HasIndex(x => x.Status)
			.HasDatabaseName("IX_HR_SalarySlips_Status");

		// Properties
		builder.Property(x => x.MonthYear)
			.IsRequired()
			.HasMaxLength(7); // YYYY-MM format

		builder.Property(x => x.Year)
			.IsRequired();

		builder.Property(x => x.Month)
			.IsRequired();

		builder.Property(x => x.TotalEarning)
			.HasPrecision(18, 2)
			.IsRequired();

		builder.Property(x => x.TotalDeduction)
			.HasPrecision(18, 2)
			.IsRequired();

		builder.Property(x => x.NetSalary)
			.HasPrecision(18, 2)
			.IsRequired();

		builder.Property(x => x.Status)
			.IsRequired()
			.HasConversion<int>();

		builder.Property(x => x.ProcessedDate)
			.IsRequired();

		builder.Property(x => x.Remarks)
			.HasMaxLength(1000);

		// JSON columns for line items (EF Core 8+ feature)
		builder.Property(x => x.Earnings)
			.HasConversion(
				v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
				v => JsonSerializer.Deserialize<System.Collections.Generic.List<SalarySlipLineItem>>(v, JsonSerializerOptions.Default) 
					?? new System.Collections.Generic.List<SalarySlipLineItem>())
			.HasColumnType("nvarchar(max)");

		builder.Property(x => x.Deductions)
			.HasConversion(
				v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
				v => JsonSerializer.Deserialize<System.Collections.Generic.List<SalarySlipLineItem>>(v, JsonSerializerOptions.Default) 
					?? new System.Collections.Generic.List<SalarySlipLineItem>())
			.HasColumnType("nvarchar(max)");

		// Multi-tenancy
		builder.HasIndex(x => x.TenantId)
			.HasDatabaseName("IX_HR_SalarySlips_TenantId");
	}
}
