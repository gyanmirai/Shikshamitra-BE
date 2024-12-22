using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity;

namespace ShikshaMitra.Employees;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
	public void Configure(EntityTypeBuilder<Employee> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Employees", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		builder.ComplexProperty(e => e.FatherDetails);
		builder.ComplexProperty(e => e.MotherDetails);
		builder.ComplexProperty(e => e.SpouseDetails);
		builder.ComplexProperty(e => e.BankDetails);

		// json column
		builder.OwnsMany(e => e.WorkExperiences, ew => ew.ToJson());

		builder.HasOne<IdentityUser>()
			.WithMany()
			.HasForeignKey(s => s.UserId)
			.OnDelete(DeleteBehavior.Restrict);


		builder.HasOne(e => e.Role)
			.WithMany()
			.HasForeignKey(s => s.RoleId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(e => e.EducationMediumStage)
			.WithMany()
			.HasForeignKey(s => s.EducationMediumStageId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.Navigation(x => x.Role).AutoInclude();
	}
}