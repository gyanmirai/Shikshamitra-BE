using FluentValidation;
using System;
using System.Collections.Generic;
using NSubstitute;
using ShikshaMitra.Academic.Syllabus;
using Shouldly;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class ChapterValidation_Tests
{
	private static readonly DateTime Today = new(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc);
	private readonly IClock _clock = CreateClock();

	[Fact]
	public void Create_Validator_Should_Reject_A_Whitespace_Only_Chapter_Name()
	{
		var result = new CreateChapterListValidator(_clock).Validate(new List<CreateChapterDto>
		{
			new()
			{
				ChapterNo = 1,
				ChapterName = "   "
			}
		});

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error =>
			error.ErrorMessage == "Chapter Name is required");
	}

	[Fact]
	public void Update_Validator_Should_Reject_A_Whitespace_Only_Chapter_Name()
	{
		var result = new CreateUpdateChapterDtoValidator(_clock).Validate(new CreateUpdateChapterDto
		{
			ChapterNo = 1,
			ChapterName = "   "
		});

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error =>
			error.PropertyName == nameof(CreateUpdateChapterDto.ChapterName) &&
			error.ErrorMessage == "Chapter Name is required");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1001)]
	public void Create_And_Update_Validators_Should_Reject_Out_Of_Range_Chapter_Numbers(int chapterNumber)
	{
		var createResult = new CreateChapterListValidator(_clock).Validate(new List<CreateChapterDto>
		{
			BuildCreateChapterDto(chapterNumber)
		});
		var updateResult = new CreateUpdateChapterDtoValidator(_clock).Validate(
			BuildUpdateChapterDto(chapterNumber));

		createResult.Errors.ShouldContain(error =>
			error.ErrorMessage == "Chapter Number must be between 1 and 1000");
		updateResult.Errors.ShouldContain(error =>
			error.PropertyName == nameof(CreateUpdateChapterDto.ChapterNo) &&
			error.ErrorMessage == "Chapter Number must be between 1 and 1000");
	}

	[Fact]
	public void Create_Validator_Should_Reject_Past_Chapter_Dates()
	{
		var pastDate = new DateTimeOffset(Today.AddDays(-1));
		var result = new CreateChapterListValidator(_clock).Validate(new List<CreateChapterDto>
		{
			new()
			{
				ChapterNo = 1,
				ChapterName = "Linear Algebra",
				StartDate = pastDate,
				EndDate = pastDate
			}
		});

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error => error.ErrorMessage == "Start Date cannot be in the past");
		result.Errors.ShouldContain(error => error.ErrorMessage == "End Date cannot be in the past");
	}

	[Fact]
	public void Update_Validator_Should_Reject_Past_Chapter_Dates()
	{
		var pastDate = new DateTimeOffset(Today.AddDays(-1));
		var result = new CreateUpdateChapterDtoValidator(_clock).Validate(new CreateUpdateChapterDto
		{
			ChapterNo = 1,
			ChapterName = "Linear Algebra",
			StartDate = pastDate,
			EndDate = pastDate
		});

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(error =>
			error.PropertyName == nameof(CreateUpdateChapterDto.StartDate) &&
			error.ErrorMessage == "Start Date cannot be in the past");
		result.Errors.ShouldContain(error =>
			error.PropertyName == nameof(CreateUpdateChapterDto.EndDate) &&
			error.ErrorMessage == "End Date cannot be in the past");
	}

	[Fact]
	public void Chapter_Validators_Should_Accept_The_Current_Date()
	{
		var today = new DateTimeOffset(Today);
		var createResult = new CreateChapterListValidator(_clock).Validate(new List<CreateChapterDto>
		{
			new()
			{
				ChapterNo = 1,
				ChapterName = "Linear Algebra",
				StartDate = today,
				EndDate = today
			}
		});
		var updateResult = new CreateUpdateChapterDtoValidator(_clock).Validate(new CreateUpdateChapterDto
		{
			ChapterNo = 1,
			ChapterName = "Linear Algebra",
			StartDate = today,
			EndDate = today
		});

		createResult.IsValid.ShouldBeTrue();
		updateResult.IsValid.ShouldBeTrue();
	}

	private static IClock CreateClock()
	{
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(Today);
		return clock;
	}

	private static CreateChapterDto BuildCreateChapterDto(int chapterNumber) => new()
	{
		ChapterNo = chapterNumber,
		ChapterName = "Linear Algebra",
		StartDate = new DateTimeOffset(Today),
		EndDate = new DateTimeOffset(Today)
	};

	private static CreateUpdateChapterDto BuildUpdateChapterDto(int chapterNumber) => new()
	{
		ChapterNo = chapterNumber,
		ChapterName = "Linear Algebra",
		StartDate = new DateTimeOffset(Today),
		EndDate = new DateTimeOffset(Today)
	};
}
