using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class StudentSubmissionConfiguration : IEntityTypeConfiguration<StudentSubmissionEntity>
{
	public void Configure(EntityTypeBuilder<StudentSubmissionEntity> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.AcademicDbTablePrefix + "StudentHomeworkAssignmentSubmissions",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();

		// Primary Key
		builder.HasKey(x => x.Id);

		// Properties
		builder.Property(x => x.HomeworkAssignmentId).IsRequired(false); // Null for assignment submissions
		builder.Property(x => x.AssignmentId).IsRequired(false); // Nullable for backward compatibility
		builder.Property(x => x.StudentId).IsRequired();
		builder.Property(x => x.DivisionId).IsRequired();
		builder.Property(x => x.DueDate).IsRequired();
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.LastModificationTime).IsConcurrencyToken();

		builder.Property(x => x.Feedback).HasMaxLength(2000);
		builder.Property(x => x.Remarks).HasMaxLength(1000);
		builder.Property(x => x.SubmittedContent).HasMaxLength(int.MaxValue);
		builder.Property(x => x.SubmittedDocumentUrl).HasMaxLength(500);
		builder.Property(x => x.OriginalFileName).HasMaxLength(500);

		builder.Property(x => x.Marks).HasColumnType("decimal(18,2)");
		builder.Property(x => x.TotalMarks).HasColumnType("decimal(18,2)");

		// Relationships
		builder
			.HasOne(x => x.HomeworkAssignment)
			.WithMany()
			.HasForeignKey(x => x.HomeworkAssignmentId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired(false);

		builder
			.HasOne(x => x.Assignment)
			.WithMany()
			.HasForeignKey(x => x.AssignmentId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired(false);

		// Indexes
		builder.HasIndex(x => x.HomeworkAssignmentId).HasDatabaseName("IX_StudentSubmissions_HomeworkAssignmentId");
		builder.HasIndex(x => x.AssignmentId).HasDatabaseName("IX_StudentSubmissions_AssignmentId");
		builder.HasIndex(x => x.StudentId).HasDatabaseName("IX_StudentSubmissions_StudentId");
		builder.HasIndex(x => x.DivisionId).HasDatabaseName("IX_StudentSubmissions_DivisionId");
		builder.HasIndex(x => x.Status).HasDatabaseName("IX_StudentSubmissions_Status");

		// Unique constraints
		builder.HasIndex(x => new { x.StudentId, x.HomeworkAssignmentId })
			.HasDatabaseName("IX_StudentSubmissions_Student_Homework")
			.IsUnique()
			.HasFilter("\"AssignmentId\" IS NULL"); // Only for homework submissions

		builder.HasIndex(x => new { x.StudentId, x.AssignmentId })
			.HasDatabaseName("IX_StudentSubmissions_Student_Assignment")
			.IsUnique()
			.HasFilter("\"AssignmentId\" IS NOT NULL"); // Only for assignment submissions
	}
}
