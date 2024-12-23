using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;	

public class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
	public void Configure(EntityTypeBuilder<LeavePolicy> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LeavePolicies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		
	}
}