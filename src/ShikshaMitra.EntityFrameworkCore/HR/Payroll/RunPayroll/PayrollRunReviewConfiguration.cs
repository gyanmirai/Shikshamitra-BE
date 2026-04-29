using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollRunReviewConfiguration : IEntityTypeConfiguration<PayrollRunReview>
{
	public void Configure(EntityTypeBuilder<PayrollRunReview> builder)
	{
		builder.ToTable("HR_PayrollRunReviews");

		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.MonthYear)
			.IsRequired()
			.HasMaxLength(7);

		builder.Property(x => x.EmployeeId)
			.IsRequired();

		builder.Property(x => x.ReviewedByUserId)
			.IsRequired();

		builder.Property(x => x.ReviewedAt)
			.IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.MonthYear, x.EmployeeId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false")
			.HasDatabaseName("IX_HR_PayrollRunReviews_Tenant_Month_Employee");

		builder.HasIndex(x => new { x.MonthYear, x.EmployeeId })
			.HasDatabaseName("IX_HR_PayrollRunReviews_Month_Employee");
	}
}
