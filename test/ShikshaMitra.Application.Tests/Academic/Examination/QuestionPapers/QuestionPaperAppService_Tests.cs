using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using ShikshaMitra.Academic.QuestionBank;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Academic.QuestionPapers;

public class QuestionPaperAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IQuestionPaperAppService _questionPaperAppService;
	private readonly IQuestionBankAppService _questionBankAppService;
	private readonly ICurrentTenant _currentTenant;
	private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
	private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
	private readonly IUserScopeService _userScopeService;

	public QuestionPaperAppService_Tests()
	{
		_questionPaperAppService = GetRequiredService<IQuestionPaperAppService>();
		_questionBankAppService = GetRequiredService<IQuestionBankAppService>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
		_scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		_userScopeService = GetRequiredService<IUserScopeService>();
	}

	[Fact]
	public async Task SubmitAsync_Should_Allow_An_Empty_Optional_Chapter_Blueprint()
	{
		using (_currentTenant.Change(null))
		{
			var academicYearId = Guid.NewGuid();
			await SeedAcademicYearScopeAsync(GetRequiredService<ICurrentUser>().GetId(), academicYearId);
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
			await SeedAcademicYearScopeAsync(GetRequiredService<ICurrentUser>().GetId(), academicYearId);
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
			await SeedAcademicYearScopeAsync(GetRequiredService<ICurrentUser>().GetId(), academicYearId);
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

	[Fact]
	public async Task UpdatePaperAsync_Should_Update_Tracked_Section_In_Place()
	{
		using (_currentTenant.Change(null))
		{
			var academicYearId = Guid.NewGuid();
			var standardId = Guid.NewGuid();
			var subjectId = Guid.NewGuid();
			var userId = Guid.NewGuid();
			await SeedAcademicYearScopeAsync(userId, academicYearId);

			using (_currentPrincipalAccessor.Change(BuildPrincipal(userId)))
			{
				var paper = await _questionPaperAppService.CreatePaperAsync(new CreateUpdateQuestionPaperDto
				{
					AcademicYearId = academicYearId,
					StandardId = standardId,
					SubjectId = subjectId,
					PaperTitle = "Gujarati Test",
					TotalMarks = 50,
					DurationMinutes = 120,
					CreationMode = "Manual",
					Sections = [new QuestionPaperSectionDto
					{
						SectionName = "Section A",
						Marks = 50,
						SortOrder = 1
					}]
				});
				var sectionId = paper.Sections[0].Id!.Value;

				var updated = await _questionPaperAppService.UpdatePaperAsync(paper.Id, new CreateUpdateQuestionPaperDto
				{
					AcademicYearId = academicYearId,
					StandardId = standardId,
					SubjectId = subjectId,
					PaperTitle = "Gujarati Test Updated",
					TotalMarks = 50,
					DurationMinutes = 120,
					CreationMode = "Manual",
					Sections = [new QuestionPaperSectionDto
					{
						Id = sectionId,
						SectionName = "Main Section",
						Marks = 50,
						SortOrder = 1,
						Instructions = "Answer every question."
					}]
				});

				updated.Sections.Count.ShouldBe(1);
				updated.Sections[0].Id.ShouldBe(sectionId);
				updated.Sections[0].SectionName.ShouldBe("Main Section");
				updated.Sections[0].Instructions.ShouldBe("Answer every question.");
			}
		}
	}

	[Fact]
	public async Task GetDashboardAsync_Should_Aggregate_Only_Papers_In_The_Users_Academic_Year_Scope()
	{
		using (_currentTenant.Change(null))
		{
			var userId = Guid.NewGuid();
			var academicYearId = Guid.NewGuid();
			var hiddenYearId = Guid.NewGuid();
			await SeedAcademicYearScopeAsync(userId, academicYearId);
			await WithUnitOfWorkAsync(async () =>
			{
				var repository = GetRequiredService<IRepository<QuestionPaper, Guid>>();
				await repository.InsertManyAsync(
				[
					new QuestionPaper(Guid.NewGuid(), null, academicYearId, Guid.NewGuid(), Guid.NewGuid(), "Draft", 10, 30, "Manual"),
					new QuestionPaper(Guid.NewGuid(), null, academicYearId, Guid.NewGuid(), Guid.NewGuid(), "Approved", 10, 30, "Manual")
						{ Status = QuestionPaperStatus.Approved },
					new QuestionPaper(Guid.NewGuid(), null, hiddenYearId, Guid.NewGuid(), Guid.NewGuid(), "Hidden", 10, 30, "Manual")
				], autoSave: true);
			});

			using (_currentPrincipalAccessor.Change(BuildPrincipal(userId)))
			{
				var dashboard = await _questionPaperAppService.GetDashboardAsync(academicYearId);
				dashboard.TotalPapers.ShouldBe(2);
				dashboard.DraftPapers.ShouldBe(1);
				dashboard.ApprovedPapers.ShouldBe(1);
				dashboard.MissingQuestions.ShouldBe(2);
				var hiddenDashboard = await _questionPaperAppService.GetDashboardAsync(hiddenYearId);
				hiddenDashboard.TotalPapers.ShouldBe(0);
				hiddenDashboard.MissingQuestions.ShouldBe(0);
			}
		}
	}

	private async Task SeedAcademicYearScopeAsync(Guid userId, Guid academicYearId)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(
				new UserScopeAssignment(
					Guid.NewGuid(),
					_currentTenant.Id,
					userId: userId,
					academicYearId: academicYearId,
					description: "Question paper section reconciliation regression test"),
				autoSave: true);

			await _userScopeService.InvalidateUserScopeCacheAsync(userId);
		});
	}

	private ClaimsPrincipal BuildPrincipal(Guid userId)
	{
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, userId.ToString()),
			new(AbpClaimTypes.UserName, $"question-paper-{userId:N}"),
			new(AbpClaimTypes.Email, $"question-paper-{userId:N}@test.local")
		};

		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
	}
}
