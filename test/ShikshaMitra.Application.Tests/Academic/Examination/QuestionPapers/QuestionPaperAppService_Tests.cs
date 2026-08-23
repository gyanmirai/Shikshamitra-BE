using System;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Academic.QuestionBank;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.QuestionPapers;

public class QuestionPaperAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IQuestionPaperAppService _questionPaperAppService;
	private readonly IQuestionBankAppService _questionBankAppService;
	private readonly ICurrentTenant _currentTenant;

	public QuestionPaperAppService_Tests()
	{
		_questionPaperAppService = GetRequiredService<IQuestionPaperAppService>();
		_questionBankAppService = GetRequiredService<IQuestionBankAppService>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task SubmitAsync_Should_Allow_An_Empty_Optional_Chapter_Blueprint()
	{
		using (_currentTenant.Change(null))
		{
			var academicYearId = Guid.NewGuid();
			var standardId = Guid.NewGuid();
			var subjectId = Guid.NewGuid();
			var question = await _questionBankAppService.CreateQuestionAsync(new CreateUpdateQuestionDto
			{
				AcademicYearId = academicYearId,
				StandardId = standardId,
				SubjectId = subjectId,
				ChapterId = Guid.NewGuid(),
				QuestionText = "What is photosynthesis?",
				QuestionType = QuestionType.ShortAnswer,
				DifficultyLevel = DifficultyLevel.Medium,
				Marks = 5,
				AnswerKey = "The process plants use to make food.",
				Visibility = QuestionVisibility.Department
			});
			await _questionBankAppService.SubmitQuestionAsync(question.Id);
			await _questionBankAppService.ApproveQuestionAsync(question.Id, new QuestionReviewDto { Comment = "Approved" });

			var paper = await _questionPaperAppService.CreatePaperAsync(new CreateUpdateQuestionPaperDto
			{
				AcademicYearId = academicYearId,
				StandardId = standardId,
				SubjectId = subjectId,
				PaperTitle = "Science Quiz",
				TotalMarks = 5,
				DurationMinutes = 30,
				CreationMode = "Manual",
				Sections = [new QuestionPaperSectionDto { SectionName = "Section A", Marks = 5, SortOrder = 1 }]
			});
			await _questionPaperAppService.AddQuestionAsync(paper.Id, new AddPaperQuestionDto
			{
				QuestionPaperSectionId = paper.Sections[0].Id!.Value,
				QuestionBankItemId = question.Id,
				QuestionNumber = 1,
				Marks = 5,
				SortOrder = 1
			});
			await _questionPaperAppService.SaveBlueprintAsync(paper.Id, new QuestionPaperBlueprintDto
			{
				QuestionPaperId = paper.Id,
				TotalMarks = 5
			});

			var submitted = await _questionPaperAppService.SubmitAsync(paper.Id);

			submitted.Status.ShouldBe(QuestionPaperStatus.Submitted);
		}
	}

	[Fact]
	public async Task GetPapersAsync_Should_Return_Paginated_Papers_For_Academic_Year()
	{
		using (_currentTenant.Change(null))
		{
			var academicYearId = Guid.NewGuid();
			var paper = await _questionPaperAppService.CreatePaperAsync(new CreateUpdateQuestionPaperDto
			{
				AcademicYearId = academicYearId,
				StandardId = Guid.NewGuid(),
				SubjectId = Guid.NewGuid(),
				PaperTitle = "Science Midterm",
				TotalMarks = 25,
				DurationMinutes = 60,
				CreationMode = "Manual"
			});

			var result = await _questionPaperAppService.GetPapersAsync(new QuestionPaperSearchInput
			{
				AcademicYearId = academicYearId,
				Page = 1,
				PageSize = 50
			});

			result.TotalCount.ShouldBe(1);
			result.CurrentPage.ShouldBe(1);
			result.PageSize.ShouldBe(50);
			result.Items.Count.ShouldBe(1);
			result.Items[0].Id.ShouldBe(paper.Id);
		}
	}

	[Fact]
	public async Task GetPaperAsync_Should_Populate_Question_Text_From_Question_Bank()
	{
		using (_currentTenant.Change(null))
		{
			var academicYearId = Guid.NewGuid();
			var standardId = Guid.NewGuid();
			var subjectId = Guid.NewGuid();
			const string questionText = "How might eating quickly affect digestion?";

			var question = await _questionBankAppService.CreateQuestionAsync(new CreateUpdateQuestionDto
			{
				AcademicYearId = academicYearId,
				StandardId = standardId,
				SubjectId = subjectId,
				ChapterId = Guid.NewGuid(),
				QuestionText = questionText,
				QuestionType = QuestionType.ShortAnswer,
				DifficultyLevel = DifficultyLevel.Medium,
				Marks = 5,
				AnswerKey = "Chewing helps break food down and mix it with saliva.",
				Visibility = QuestionVisibility.Department
			});
			await _questionBankAppService.SubmitQuestionAsync(question.Id);
			await _questionBankAppService.ApproveQuestionAsync(question.Id, new QuestionReviewDto { Comment = "Approved for paper" });

			var paper = await _questionPaperAppService.CreatePaperAsync(new CreateUpdateQuestionPaperDto
			{
				AcademicYearId = academicYearId,
				StandardId = standardId,
				SubjectId = subjectId,
				PaperTitle = "Science Midterm",
				TotalMarks = 5,
				DurationMinutes = 60,
				CreationMode = "Manual",
				Sections = [new QuestionPaperSectionDto { SectionName = "Section A", Marks = 5, SortOrder = 1 }]
			});
			var sectionId = paper.Sections[0].Id!.Value;
			await _questionPaperAppService.AddQuestionAsync(paper.Id, new AddPaperQuestionDto
			{
				QuestionPaperSectionId = sectionId,
				QuestionBankItemId = question.Id,
				QuestionNumber = 1,
				Marks = 5,
				SortOrder = 1
			});

			var result = await _questionPaperAppService.GetPaperAsync(paper.Id);
			var preview = await _questionPaperAppService.GetPreviewAsync(paper.Id);

			result.Questions.Count.ShouldBe(1);
			result.Questions[0].QuestionBankItemId.ShouldBe(question.Id);
			result.Questions[0].QuestionText.ShouldBe(questionText);
			preview.Paper.Questions[0].QuestionText.ShouldBe(questionText);
			preview.PrintHtml.ShouldContain(questionText);
		}
	}
}
