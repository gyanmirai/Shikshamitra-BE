using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShikshaMitra.Students.StudentEnrollments;

public class StudentNotesConfiguration : IEntityTypeConfiguration<StudentNote>
{
	public void Configure(EntityTypeBuilder<StudentNote> builder)
	{
		builder.ToTable("StudentNotes");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Note)
			.IsRequired()
			.HasMaxLength(2000);
		builder.Property(x => x.NextReminderDate)
			.IsRequired();
		builder.Property(x => x.Status)
			.IsRequired();
		builder.HasIndex(x => x.StudentId);
	}
}
