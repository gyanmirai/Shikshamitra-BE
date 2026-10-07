using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipCorrectionConfiguration : IEntityTypeConfiguration<PayslipCorrection>
{
	public void Configure(EntityTypeBuilder<PayslipCorrection> builder)
	{
		builder.ToTable("HR_PayslipCorrections");
		builder.ConfigureByConvention();
		builder.Property(x => x.ReviewedAt).HasColumnType("timestamp with time zone");
		builder.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
		builder.Property(x => x.CreationTime).HasColumnType("timestamp with time zone");
		builder.Property(x => x.LastModificationTime).HasColumnType("timestamp with time zone");
		builder.Property(x => x.DeletionTime).HasColumnType("timestamp with time zone");
		builder.Ignore(x => x.IsActive);
		builder.Property(x => x.Reason).IsRequired().HasMaxLength(1000);
		builder.Property(x => x.SourceMonth).IsRequired().HasMaxLength(7);
		builder.Property(x => x.SettlementMonth).HasMaxLength(7);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
		builder.Property(x => x.OriginalSnapshotJson).IsRequired().HasColumnType("jsonb");
		builder.Property(x => x.AttendanceJson).IsRequired().HasColumnType("jsonb");
		builder.Property(x => x.CandidateJson).HasColumnType("jsonb");
		builder.HasOne<PayslipDocument>().WithMany().HasForeignKey(x => x.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(x => new { x.SalarySlipId, x.RequestId }).IsUnique();
		builder.HasIndex(x => x.SalarySlipId).IsUnique()
			.HasFilter("\"Status\" IN ('DRAFT', 'CALCULATED', 'REVIEWED') AND NOT \"IsDeleted\"");
		builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.SourceMonth });
	}
}