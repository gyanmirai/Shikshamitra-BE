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
