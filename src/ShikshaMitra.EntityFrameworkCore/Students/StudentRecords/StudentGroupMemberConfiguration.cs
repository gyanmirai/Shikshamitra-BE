using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentGroupMemberConfiguration : IEntityTypeConfiguration<StudentGroupMember>
{
	public void Configure(EntityTypeBuilder<StudentGroupMember> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentGroupMembers", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).IsRequired();
		builder.HasOne(x => x.StudentGroup).WithMany().HasForeignKey(x => x.StudentGroupId).IsRequired();

		builder.HasIndex(x => new { x.StudentId, x.StudentGroupId }).IsUnique();

		builder.HasIndex(x => new { x.StudentGroupId, x.IsLeader });
	}
}
