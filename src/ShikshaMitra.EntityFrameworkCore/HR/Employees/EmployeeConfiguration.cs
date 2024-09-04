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

		builder.HasOne<IdentityUser>()
			.WithMany()
			.HasForeignKey(s => s.UserId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(e => e.WorkExperiences).WithOne()
			.HasForeignKey(e => e.EmployeeId);

		// TODO: Remove this when we write custom repository methods
		builder.Navigation(e => e.WorkExperiences).AutoInclude();
	}
}