using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Employees;

public class EmployeeOrganizationAssignmentConfiguration : IEntityTypeConfiguration<EmployeeOrganizationAssignment>
{
	public void Configure(EntityTypeBuilder<EmployeeOrganizationAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeOrganizationAssignments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasOne(x => x.Employee)
			.WithMany(x => x.OrganizationAssignments)
			.HasForeignKey(x => x.EmployeeId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired();

		builder.HasOne(x => x.EducationMediumStage)
			.WithMany()
			.HasForeignKey(x => x.EducationMediumStageId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired();

		builder.HasIndex(x => x.EmployeeId)
			.HasDatabaseName("IX_EmployeeOrganizationAssignments_EmployeeId");

		builder.HasIndex(x => x.EducationMediumStageId)
			.HasDatabaseName("IX_EmployeeOrganizationAssignments_EducationMediumStageId");

		builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.EducationMediumStageId })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeOrganizationAssignments_Tenant_Employee_Stage_Unique");

		builder.Navigation(x => x.EducationMediumStage).AutoInclude();
	}
}
