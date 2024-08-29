using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
	public void Configure(EntityTypeBuilder<Student> builder)
	{
		// Define the table name and schema for the Student entity.
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Students", ShikshaMitraConsts.DbSchema);

		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		// Value objects
		builder.ComplexProperty(s => s.Address);
		// builder.ComplexProperty(s => s.AdmissionDetails, a =>
		// {
		// 	a.IsRequired();
		// });
		// builder.ComplexProperty(s => s.CommunicationDetails, a =>
		// {
		// 	a.IsRequired();
		// });

		builder.HasOne<IdentityUser>()
			.WithMany()
			.HasForeignKey(s => s.UserId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(s => s.Standard)
			.WithMany()
			.HasForeignKey(s => s.StandardId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(s => s.Division)
			.WithMany()
			.HasForeignKey(s => s.DivisionId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}