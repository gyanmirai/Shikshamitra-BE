using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students;

public class StudentCommunicationDetailConfiguration : IEntityTypeConfiguration<StudentCommunicationDetail>
{
	public void Configure(EntityTypeBuilder<StudentCommunicationDetail> builder)
	{
		builder.ToTable("StudentCommunicationDetails");

		builder.ConfigureByConvention();

		// Configure Local Address as owned entity (complex type)
		builder.OwnsOne(x => x.LocalAddress, la =>
		{
			la.Property(a => a.BuildingNo).HasMaxLength(100).HasColumnName("LocalAddress_BuildingNo");
			la.Property(a => a.AreaLocalityRoadName).HasMaxLength(200).HasColumnName("LocalAddress_AreaLocalityRoadName");
			la.Property(a => a.Landmark).HasMaxLength(200).HasColumnName("LocalAddress_Landmark");
			la.Property(a => a.State).HasMaxLength(100).HasColumnName("LocalAddress_State").IsRequired();
			la.Property(a => a.City).HasMaxLength(100).HasColumnName("LocalAddress_City");
			la.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("LocalAddress_Pincode");
			la.Property(a => a.Country).HasMaxLength(100).HasColumnName("LocalAddress_Country");
		});
		builder.Navigation(x => x.LocalAddress).IsRequired(false);

		// Configure Permanent Address as owned entity (complex type)
		builder.OwnsOne(x => x.PermanentAddress, pa =>
		{
			pa.Property(a => a.BuildingNo).HasMaxLength(100).HasColumnName("PermanentAddress_BuildingNo");
			pa.Property(a => a.AreaLocalityRoadName).HasMaxLength(200).HasColumnName("PermanentAddress_AreaLocalityRoadName");
			pa.Property(a => a.Landmark).HasMaxLength(200).HasColumnName("PermanentAddress_Landmark");
			pa.Property(a => a.State).HasMaxLength(100).HasColumnName("PermanentAddress_State").IsRequired();
			pa.Property(a => a.City).HasMaxLength(100).HasColumnName("PermanentAddress_City");
			pa.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("PermanentAddress_Pincode");
			pa.Property(a => a.Country).HasMaxLength(100).HasColumnName("PermanentAddress_Country");
		});
		builder.Navigation(x => x.PermanentAddress).IsRequired(false);

		// Configure Emergency Address as owned entity (complex type)
		builder.OwnsOne(x => x.EmergencyAddress, ea =>
		{
			ea.Property(a => a.BuildingNo).HasMaxLength(100).HasColumnName("EmergencyAddress_BuildingNo");
			ea.Property(a => a.AreaLocalityRoadName).HasMaxLength(200).HasColumnName("EmergencyAddress_AreaLocalityRoadName");
			ea.Property(a => a.Landmark).HasMaxLength(200).HasColumnName("EmergencyAddress_Landmark");
			ea.Property(a => a.State).HasMaxLength(100).HasColumnName("EmergencyAddress_State").IsRequired();
			ea.Property(a => a.City).HasMaxLength(100).HasColumnName("EmergencyAddress_City");
			ea.Property(a => a.Pincode).HasMaxLength(10).HasColumnName("EmergencyAddress_Pincode");
			ea.Property(a => a.Country).HasMaxLength(100).HasColumnName("EmergencyAddress_Country");
		});
		builder.Navigation(x => x.EmergencyAddress).IsRequired(false);

		// Configure scalar properties
		builder.Property(x => x.IsPermanentSameAsLocal).IsRequired();
		builder.Property(x => x.IsEmergencySameAsLocal).IsRequired();
		builder.Property(x => x.EmergencyContactName).HasMaxLength(200);
		builder.Property(x => x.EmergencyContactPhone).HasMaxLength(20);
		builder.Property(x => x.EmergencyContactRelation).HasMaxLength(100);
	}
}
