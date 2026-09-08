using System;
using ShikshaMitra.Academic.Curriculum;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class AssignedSubjectChapter_Tests
{
	[Fact]
	public void Constructor_And_Update_Should_Trim_Chapter_Name()
	{
		var chapter = CreateChapter("  Linear Algebra  ");

		chapter.ChapterName.ShouldBe("Linear Algebra");

		chapter.UpdateDetails(2, "  Quadratic Equations  ", "Updated description");

		chapter.ChapterName.ShouldBe("Quadratic Equations");
	}

	[Fact]
	public void Constructor_Should_Reject_A_Whitespace_Only_Chapter_Name()
	{
		Should.Throw<ArgumentException>(() => CreateChapter("   "));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1001)]
	public void Constructor_Should_Reject_An_Out_Of_Range_Chapter_Number(int chapterNumber)
	{
		Should.Throw<ArgumentOutOfRangeException>(() => CreateChapter("Linear Algebra", chapterNumber));
	}

	[Fact]
	public void Update_Should_Reject_An_Out_Of_Range_Chapter_Number()
	{
		var chapter = CreateChapter("Linear Algebra");

		Should.Throw<ArgumentOutOfRangeException>(() =>
			chapter.UpdateDetails(1001, "Linear Algebra", null));
	}

	private static AssignedSubjectChapter CreateChapter(string chapterName, int chapterNumber = 1) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			chapterNumber,
			chapterName,
			new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));
}
