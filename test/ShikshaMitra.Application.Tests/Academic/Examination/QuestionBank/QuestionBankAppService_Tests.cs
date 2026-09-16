using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Academic.QuestionBank;

public class QuestionBankAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	private readonly IQuestionBankAppService _questionBankAppService;
	private readonly IRepository<QuestionBankItem, Guid> _questionRepository;
	private readonly ICurrentTenant _currentTenant;

	public QuestionBankAppService_Tests()
	{
		_questionBankAppService = GetRequiredService<IQuestionBankAppService>();
		_questionRepository = GetRequiredService<IRepository<QuestionBankItem, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task UpdateQuestionAsync_Should_Persist_Updated_Question_Details()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var createInput = CreateQuestionInput("What is probability?");
			var userId = GetRequiredService<ICurrentUser>().GetId();
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
					new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: userId,
						academicYearId: createInput.AcademicYearId), autoSave: true);
				await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
			});
			var created = await _questionBankAppService.CreateQuestionAsync(createInput);
			var submitted = await _questionBankAppService.SubmitQuestionAsync(created.Id);

			var updateInput = CreateQuestionInput("What is probability in mathematics?");

			var updated = await _questionBankAppService.UpdateQuestionAsync(submitted.Id, updateInput);

			updated.Id.ShouldBe(created.Id);
			updated.Status.ShouldBe(QuestionStatus.Submitted);
			updated.QuestionText.ShouldBe(updateInput.QuestionText);
			updated.AnswerKey.ShouldBe(updateInput.AnswerKey);
			updated.Options.Count.ShouldBe(2);
			updated.Options[0].OptionText.ShouldBe("a");

			var reloaded = await _questionRepository.GetAsync(created.Id);
			reloaded.QuestionText.ShouldBe(updateInput.QuestionText);
		}
	}

	[Fact]
	public async Task GetDashboardAsync_Should_Preserve_Duplicate_And_Distinct_Usage_Counts_Within_Scope()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var userId = Guid.NewGuid();
			var yearId = Guid.NewGuid();
			var hiddenYearId = Guid.NewGuid();
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
					new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: userId, academicYearId: yearId), autoSave: true);
				await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
				QuestionBankItem CreateQuestion(Guid year, string text) => new(Guid.NewGuid(), TestTenantId, year,
					Guid.NewGuid(), Guid.NewGuid(), text, QuestionType.ShortAnswer, DifficultyLevel.Easy, 5, userId, QuestionVisibility.Department);
				var first = CreateQuestion(yearId, "Duplicate question text");
				var second = CreateQuestion(yearId, "Duplicate question text");
				var third = CreateQuestion(yearId, "An approved question");
				third.Status = QuestionStatus.Approved;
				var archived = CreateQuestion(yearId, "An archived question");
				archived.IsArchived = true;
				await _questionRepository.InsertManyAsync([first, second, third, archived, CreateQuestion(hiddenYearId, "Hidden question")], autoSave: true);
				var usages = GetRequiredService<IRepository<QuestionUsageLog, Guid>>();
				await usages.InsertManyAsync(
				[
					new QuestionUsageLog(Guid.NewGuid(), first.Id, QuestionUsageSource.QuestionPaper, Guid.NewGuid(), DateTime.UtcNow, userId),
					new QuestionUsageLog(Guid.NewGuid(), first.Id, QuestionUsageSource.QuestionPaper, Guid.NewGuid(), DateTime.UtcNow, userId)
				], autoSave: true);
			});
			var principal = new ClaimsPrincipal(new ClaimsIdentity(
			[
				new Claim(AbpClaimTypes.UserId, userId.ToString()),
				new Claim(AbpClaimTypes.UserName, $"bank-dashboard-{userId:N}"),
				new Claim(AbpClaimTypes.TenantId, TestTenantId.ToString())
			], "Test"));
			using (GetRequiredService<ICurrentPrincipalAccessor>().Change(principal))
			{
				var dashboard = await _questionBankAppService.GetDashboardAsync(yearId);
				dashboard.TotalQuestions.ShouldBe(3);
				dashboard.DraftQuestions.ShouldBe(2);
				dashboard.ApprovedQuestions.ShouldBe(1);
				dashboard.DuplicateRisk.ShouldBe(2);
				dashboard.NeverUsed.ShouldBe(2);
				(await _questionBankAppService.GetDashboardAsync(hiddenYearId)).TotalQuestions.ShouldBe(0);
			}
		}
	}

	private static CreateUpdateQuestionDto CreateQuestionInput(string questionText) =>
		new()
		{
			AcademicYearId = Guid.Parse("3a22d4c9-2a1f-18b8-05f4-4d1bfa053b8e"),
			StandardId = Guid.Parse("3a22d4c9-2a1f-293c-a5b3-132b5e1d3495"),
			SubjectId = Guid.Parse("3a22d4c9-325e-6ec8-c3fa-705931cc6be5"),
			ChapterId = Guid.Parse("3a22da8f-b7df-6cd6-b497-fcfe2ba76b50"),
			TopicName = null,
			QuestionText = questionText,
			QuestionType = QuestionType.McqSingleCorrect,
			DifficultyLevel = DifficultyLevel.Easy,
			BloomLevel = BloomLevel.Remembering,
			Marks = 1,
			NegativeMarks = null,
			AnswerKey = "a",
			LearningOutcome = string.Empty,
			Explanation = string.Empty,
			MarkingScheme = string.Empty,
			Visibility = QuestionVisibility.Department,
			Options =
			[
				new QuestionOptionDto { OptionText = "a", IsCorrect = true, SortOrder = 1 },
				new QuestionOptionDto { OptionText = "b", IsCorrect = false, SortOrder = 2 }
			],
			Tags = []
		};
}
