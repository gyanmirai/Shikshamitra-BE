using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Employees;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Employees;

public class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
	public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeDocuments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}