using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipDocumentConfiguration : IEntityTypeConfiguration<PayslipDocument>
{
	public void Configure(EntityTypeBuilder<PayslipDocument> builder)
	{
		builder.ToTable("HR_PayslipDocuments");
		builder.ConfigureByConvention();
		builder.Property(x => x.MonthYear).IsRequired().HasMaxLength(7);
		builder.Property(x => x.FactsJson).IsRequired().HasColumnType("jsonb");
		builder.Property(x => x.Pdf).HasField("_pdf").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();
		builder.Property(x => x.Sha256).IsRequired().HasMaxLength(64);
		builder.Property(x => x.ReplacementReason).HasMaxLength(1000);
		builder.HasIndex(x => new { x.SalarySlipId, x.Revision }).IsUnique();
		builder.HasIndex(x => new { x.SalarySlipId, x.RequestId }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.MonthYear });
	}
}
