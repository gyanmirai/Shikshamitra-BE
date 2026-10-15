using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Collections;

public class FinanceFeeAccountConfiguration : IEntityTypeConfiguration<FinanceFeeAccount>
{
	public void Configure(EntityTypeBuilder<FinanceFeeAccount> builder)
	{
		builder.ToTable("Finance_FeeAccounts", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.ApplicantName).IsRequired().HasMaxLength(250);
		builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
		builder.Property(x => x.AssignmentSnapshotJson).HasColumnType("text");
		builder.Property(x => x.AssignmentRequestFingerprint).HasMaxLength(64);
		builder.HasIndex(x => new { x.TenantId, x.ApplicationId, x.AcademicYearId })
			.IsUnique().HasFilter("\"IsDeleted\" = false")
			.HasDatabaseName("UX_FeeAccounts_Tenant_Application_Year");
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.AcademicYearId })
			.HasDatabaseName("IX_FeeAccounts_Tenant_Student_Year");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.BoardId, x.EducationMediumId, x.StandardId })
			.HasDatabaseName("IX_FeeAccounts_AcademicScope");
		builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<StudentFeeStructure>().WithMany().HasForeignKey(x => x.StudentFeeStructureId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
