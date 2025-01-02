using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentEnrollment.Settings;

public class InquiryPreferenceConfiguration
{
	public void Configure(EntityTypeBuilder<InquiryPreference> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "InquiryPreferences", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}