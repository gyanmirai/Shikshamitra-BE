using FluentValidation;
using System;
using System.Collections.Generic;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Curriculum;

public class CreateChapterDtoValidator : AbstractValidator<CreateChapterDto>
{
	public CreateChapterDtoValidator(IClock clock)
	{
		RuleFor(x => x.ChapterNo)
			.InclusiveBetween(ChapterConsts.MinChapterNumber, ChapterConsts.MaxChapterNumber)
			.WithMessage($"Chapter Number must be between {ChapterConsts.MinChapterNumber} and {ChapterConsts.MaxChapterNumber}");

		RuleFor(x => x.ChapterName)
			.Cascade(CascadeMode.Stop)
			.NotEmpty()
			.WithMessage("Chapter Name is required")
			.Must(chapterName => !string.IsNullOrWhiteSpace(chapterName))
			.WithMessage("Chapter Name is required")
			.Must(chapterName => chapterName.Trim().Length >= ChapterConsts.MinChapterNameLength)
			.WithMessage($"Chapter name must be at least {ChapterConsts.MinChapterNameLength} characters")
			.Must(chapterName => chapterName.Trim().Length <= ChapterConsts.MaxChapterNameLength)
			.WithMessage($"Chapter name cannot exceed {ChapterConsts.MaxChapterNameLength} characters");

		RuleFor(x => x.StartDate)
			.Must(startDate => DateOnly.FromDateTime(startDate.DateTime) >= DateOnly.FromDateTime(clock.Now))
			.WithMessage("Start Date cannot be in the past");

		RuleFor(x => x.EndDate)
			.Must(endDate => DateOnly.FromDateTime(endDate.DateTime) >= DateOnly.FromDateTime(clock.Now))
			.WithMessage("End Date cannot be in the past")
			.GreaterThanOrEqualTo(x => x.StartDate)
			.WithMessage("End Date cannot be before Start Date");
	}
}

public class CreateChapterListValidator : AbstractValidator<List<CreateChapterDto>>
{
	public CreateChapterListValidator(IClock clock)
	{
		RuleForEach(chapters => chapters).SetValidator(new CreateChapterDtoValidator(clock));
	}
}
