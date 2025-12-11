using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;
public class StudentContactDetailConfiguration : IEntityTypeConfiguration<StudentContactDetail>
{
	public void Configure(EntityTypeBuilder<StudentContactDetail> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentContactDetails", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		// Configure State as owned entity (value object)
		builder.OwnsOne(x => x.State, state =>
		{
			state.Property(s => s.Name).HasMaxLength(100).HasColumnName("State_Name");
			state.Property(s => s.IsoCode).HasMaxLength(10).HasColumnName("State_IsoCode");
		});

		// Configure Country as owned entity (value object)
		builder.OwnsOne(x => x.Country, country =>
		{
			country.Property(c => c.Name).HasMaxLength(100).HasColumnName("Country_Name");
			country.Property(c => c.IsoCode).HasMaxLength(10).HasColumnName("Country_IsoCode");
		});

		// Configure scalar properties
		builder.Property(x => x.PrimaryContactNo).HasMaxLength(20).IsRequired();
		builder.Property(x => x.SecondaryContactNo).HasMaxLength(20);
		builder.Property(x => x.Email).HasMaxLength(256);
		builder.Property(x => x.Address1).HasMaxLength(200);
		builder.Property(x => x.Address2).HasMaxLength(200);
		builder.Property(x => x.Address3).HasMaxLength(200);
		builder.Property(x => x.City).HasMaxLength(100);
		builder.Property(x => x.Pincode).HasMaxLength(10);
	}
}
