using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Attendances;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityConfigurations.Students.Attendances;

/// <summary>
/// Entity Framework Core configuration for StudentLeave entity
/// </summary>
public class StudentLeaveEntityTypeConfiguration : IEntityTypeConfiguration<StudentLeave>
{
	public void Configure(EntityTypeBuilder<StudentLeave> builder)
	{
		builder.ToTable("AppStudentLeaves");

		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.Description)
			.HasMaxLength(StudentLeaveConsts.MaxDescriptionLength)
			.IsRequired(false);

		builder.Property(x => x.Remarks)
			.HasMaxLength(StudentLeaveConsts.MaxRemarksLength)
			.IsRequired(false);

		builder.Property(x => x.RejectionReason)
			.HasMaxLength(StudentLeaveConsts.MaxRejectionReasonLength)
			.IsRequired(false);

		builder.Property(x => x.AttachmentFileName)
			.HasMaxLength(StudentLeaveConsts.MaxAttachmentFileNameLength)
			.IsRequired(false);

		builder.Property(x => x.AttachmentContentType)
			.HasMaxLength(StudentLeaveConsts.MaxAttachmentContentTypeLength)
			.IsRequired(false);

		builder.Property(x => x.TotalDays)
			.HasPrecision(5, 2)
			.IsRequired();

		builder.Property(x => x.IsHalfDay)
			.IsRequired();

		builder.Property(x => x.LeaveDate)
			.IsRequired();

		builder.Property(x => x.Status)
			.IsRequired();

		// Configure relationships
		builder.HasOne(x => x.Student)
			.WithMany()
			.HasForeignKey(x => x.StudentId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired();

		builder.HasOne(x => x.StudentLeaveType)
			.WithMany()
			.HasForeignKey(x => x.StudentLeaveTypeId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired();

		// Configure indexes
		builder.HasIndex(x => x.TenantId);
		builder.HasIndex(x => x.StudentId);
		builder.HasIndex(x => x.Status);
		builder.HasIndex(x => x.LeaveDate);
		builder.HasIndex(x => x.IsDeleted);
	}
}
