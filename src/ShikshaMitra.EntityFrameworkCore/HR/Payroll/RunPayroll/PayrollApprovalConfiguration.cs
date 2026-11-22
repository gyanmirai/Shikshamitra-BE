using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollApprovalConfiguration :
	IEntityTypeConfiguration<PayrollApprovalSettings>,
	IEntityTypeConfiguration<PayrollSubmission>,
	IEntityTypeConfiguration<PayrollSubmissionEmployee>,
	IEntityTypeConfiguration<PayrollEmployeeClaim>,
	IEntityTypeConfiguration<PayrollCommandReceipt>
{
	public void Configure(EntityTypeBuilder<PayrollApprovalSettings> builder)
	{
		builder.ToTable("HR_PayrollApprovalSettings");
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.Mode).IsRequired().HasMaxLength(16);
		builder.HasIndex(x => x.TenantId).IsUnique();
	}

	public void Configure(EntityTypeBuilder<PayrollSubmission> builder)
	{
		builder.ToTable("HR_PayrollSubmissions");
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.Month).IsRequired().HasMaxLength(7);
		builder.Property(x => x.State).IsRequired().HasMaxLength(12);
		builder.Property(x => x.ReasonCode).HasMaxLength(48);
		builder.Property(x => x.ResolutionReason).HasMaxLength(500);
		builder.Property(x => x.NetPay).HasPrecision(18, 2);
		builder.HasIndex(x => new { x.TenantId, x.Month, x.ApproverId, x.SubmittedAt });
		builder.HasIndex(x => new { x.TenantId, x.Month, x.ReviewerRoleId, x.SubmittedAt });
		builder.HasIndex(x => new { x.TenantId, x.Month, x.SubmitterId, x.SubmittedAt });
	}

	public void Configure(EntityTypeBuilder<PayrollSubmissionEmployee> builder)
	{
		builder.ToTable("HR_PayrollSubmissionEmployees");
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.EvidenceDigest).IsRequired().HasMaxLength(512);
		builder.Property(x => x.NetPay).HasPrecision(18, 2);
		builder.HasIndex(x => new { x.SubmissionId, x.EmployeeId }).IsUnique();
		builder.HasOne<PayrollSubmission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<PayrollPreviewEmployee>().WithMany().HasForeignKey(x => x.PreviewEmployeeId).OnDelete(DeleteBehavior.Restrict);
	}

	public void Configure(EntityTypeBuilder<PayrollEmployeeClaim> builder)
	{
		builder.ToTable("HR_PayrollEmployeeClaims");
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.Month).IsRequired().HasMaxLength(7);
		builder.HasIndex(x => new { x.TenantId, x.Month, x.EmployeeId }).IsUnique();
		builder.HasIndex(x => x.SubmissionId);
		builder.HasOne<PayrollSubmission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
	}

	public void Configure(EntityTypeBuilder<PayrollCommandReceipt> builder)
	{
		builder.ToTable("HR_PayrollCommandReceipts");
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.Property(x => x.Command).IsRequired().HasMaxLength(32);
		builder.Property(x => x.Fingerprint).IsRequired().HasMaxLength(64);
		builder.Property(x => x.ResultJson).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.ActorId, x.Command, x.Key }).IsUnique();
	}
}
