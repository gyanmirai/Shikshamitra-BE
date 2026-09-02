using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.QuestionBank;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.QuestionPapers;

[Authorize]
[RemoteService(IsEnabled = false)]
public class QuestionPaperAppService(
    IRepository<QuestionPaperSetting, Guid> settingRepository,
    IRepository<QuestionPaper, Guid> paperRepository,
    IRepository<QuestionPaperSection, Guid> sectionRepository,
    IRepository<QuestionPaperQuestion, Guid> paperQuestionRepository,
    IRepository<QuestionPaperBlueprint, Guid> blueprintRepository,
    IRepository<QuestionPaperBlueprintChapterRule, Guid> chapterRuleRepository,
    IRepository<QuestionPaperBlueprintDifficultyRule, Guid> difficultyRuleRepository,
    IRepository<QuestionPaperBlueprintBloomRule, Guid> bloomRuleRepository,
    IRepository<QuestionBankItem, Guid> questionRepository,
    IRepository<QuestionUsageLog, Guid> usageRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<Exam, Guid> examRepository,
    IAsyncQueryableExecuter asyncExecuter,
    IUserScopeAccessService userScopeAccessService,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    IRepository<Employee, Guid> employeeRepository,
    IRepository<TeacherSubjectAllocation, Guid> teacherSubjectAllocationRepository,
    IReportingManagerAccessService reportingManagerAccessService
) : ApplicationService, IQuestionPaperAppService
{
    public async Task<QuestionPaperSettingDto> GetSettingsAsync(Guid academicYearId)
    {
        var setting = await settingRepository.FirstOrDefaultAsync(x => x.AcademicYearId == academicYearId);
        if (setting == null)
        {
            setting = new QuestionPaperSetting(GuidGenerator.Create(), CurrentTenant.Id, academicYearId);
            await settingRepository.InsertAsync(setting, autoSave: true);
        }

        return Map(setting);
    }

    public async Task<QuestionPaperSettingDto> SaveSettingsAsync(QuestionPaperSettingDto input)
    {
        var setting = input.Id.HasValue
            ? await settingRepository.FindAsync(input.Id.Value)
            : await settingRepository.FirstOrDefaultAsync(x => x.AcademicYearId == input.AcademicYearId);
        var isNew = setting == null;
        setting ??= new QuestionPaperSetting(GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId);
        Apply(input, setting);
        if (isNew) await settingRepository.InsertAsync(setting, autoSave: true);
        else await settingRepository.UpdateAsync(setting, autoSave: true);
        return Map(setting);
    }

    public async Task<QuestionPaperListResponseDto> GetPapersAsync(QuestionPaperSearchInput input)
    {
        var query = await paperRepository.WithDetailsAsync(x => x.Sections, x => x.Questions);
        query = ApplySearch(query, input);
		query = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
        var total = await asyncExecuter.CountAsync(query);
        var page = Math.Max(1, input.Page);
        var pageSize = Math.Clamp(input.PageSize, 5, 100);
        var items = await asyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Skip((page - 1) * pageSize).Take(pageSize));
        var mappedItems = items.Select(x => Map(x)).ToList();
        await EnrichDisplayFieldsAsync(mappedItems);

        return new QuestionPaperListResponseDto
        {
            TotalCount = total,
            CurrentPage = page,
            PageSize = pageSize,
            Items = mappedItems
        };
    }

    public async Task<QuestionPaperDto> GetPaperAsync(Guid id)
    {
        var paper = await GetPaperEntityAsync(id, allowTeacherSubjectRead: true);
        var questionTextMap = await GetQuestionTextMapAsync(paper);
        return Map(paper, questionTextMap);
    }

    public async Task<QuestionPaperDto> CreatePaperAsync(CreateUpdateQuestionPaperDto input)
    {
        ValidatePaperInput(input);
        var paper = new QuestionPaper(GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId, input.StandardId, input.SubjectId, input.PaperTitle, input.TotalMarks, input.DurationMinutes, input.CreationMode);
        Apply(input, paper);
		await userScopeAccessService.EnsureCurrentUserCanAccessAsync(paper, operation: "question-paper-create");
        foreach (var section in input.Sections.OrderBy(x => x.SortOrder))
        {
            paper.Sections.Add(new QuestionPaperSection(section.Id ?? GuidGenerator.Create(), paper.Id, section.SectionName, section.Marks, section.SortOrder, section.Instructions));
        }

        await paperRepository.InsertAsync(paper, autoSave: true);
        return Map(paper);
    }

    public async Task<QuestionPaperDto> UpdatePaperAsync(Guid id, CreateUpdateQuestionPaperDto input)
    {
        var paper = await GetPaperEntityAsync(id);
        EnsureEditable(paper);
        ValidatePaperInput(input);
        Apply(input, paper);
		await userScopeAccessService.EnsureCurrentUserCanAccessAsync(paper, operation: "question-paper-update");
        await ReconcileSectionsAsync(paper, input.Sections);

        await paperRepository.UpdateAsync(paper, autoSave: true);
        return Map(paper);
    }

    private async Task ReconcileSectionsAsync(
        QuestionPaper paper,
        IReadOnlyCollection<QuestionPaperSectionDto> requestedSections)
    {
        var requestedIds = requestedSections
            .Where(section => section.Id.HasValue)
            .Select(section => section.Id!.Value)
            .ToList();
        if (requestedIds.Count != requestedIds.Distinct().Count())
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:DuplicateSectionId");
        }

        var existingById = paper.Sections.ToDictionary(section => section.Id);
        if (requestedIds.Any(id => !existingById.ContainsKey(id)))
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:SectionNotFound");
        }

        var sectionsToRemove = paper.Sections
            .Where(section => !requestedIds.Contains(section.Id))
            .ToList();
        if (sectionsToRemove.Any(section => paper.Questions.Any(question =>
                question.QuestionPaperSectionId == section.Id)))
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:CannotRemoveNonEmptySection");
        }

        if (sectionsToRemove.Count > 0)
        {
            await sectionRepository.DeleteManyAsync(sectionsToRemove);
            foreach (var section in sectionsToRemove)
            {
                paper.Sections.Remove(section);
            }
        }

        foreach (var requested in requestedSections.OrderBy(section => section.SortOrder))
        {
            if (requested.Id.HasValue)
            {
                var existing = existingById[requested.Id.Value];
                existing.SectionName = requested.SectionName;
                existing.Marks = requested.Marks;
                existing.SortOrder = requested.SortOrder;
                existing.Instructions = requested.Instructions;
                continue;
            }

            paper.Sections.Add(new QuestionPaperSection(
                GuidGenerator.Create(),
                paper.Id,
                requested.SectionName,
                requested.Marks,
                requested.SortOrder,
                requested.Instructions));
        }
    }

    public async Task DeletePaperAsync(Guid id)
    {
        var paper = await GetPaperEntityAsync(id);
        var usageCount = await usageRepository.CountAsync(x =>
            x.Source == QuestionUsageSource.QuestionPaper &&
            x.SourceEntityId == paper.Id);
        if (usageCount > 0 || paper.IsLocked || paper.Status is QuestionPaperStatus.Submitted or QuestionPaperStatus.Approved or QuestionPaperStatus.Published or QuestionPaperStatus.Locked)
        {
            paper.Status = QuestionPaperStatus.Archived;
            await paperRepository.UpdateAsync(paper, autoSave: true);
            return;
        }

        await paperRepository.DeleteAsync(paper, autoSave: true);
    }

    public Task<QuestionPaperDto> SubmitAsync(Guid id) => ChangeStatusAsync(id, QuestionPaperStatus.Submitted, null);
    public Task<QuestionPaperDto> ApproveAsync(Guid id, PaperReviewDto input) => ChangeStatusAsync(id, QuestionPaperStatus.Approved, input);
    public Task<QuestionPaperDto> RejectAsync(Guid id, PaperReviewDto input) => ChangeStatusAsync(id, QuestionPaperStatus.Rejected, input);
    public Task<QuestionPaperDto> ArchiveAsync(Guid id) => ChangeStatusAsync(id, QuestionPaperStatus.Archived, null);

    public async Task<QuestionPaperDto> PublishAsync(Guid id)
    {
        var paper = await GetPaperEntityAsync(id);
        if (paper.Status != QuestionPaperStatus.Approved)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:OnlyApprovedCanPublish");
        }

        await ValidatePaperMarksAsync(paper, requireQuestions: true);
        await ValidateBlueprintIfEnabledAsync(paper);
        paper.Status = QuestionPaperStatus.Published;

        var existingLogs = await usageRepository.GetListAsync(x => x.Source == QuestionUsageSource.QuestionPaper && x.SourceEntityId == paper.Id);
        if (existingLogs.Count == 0)
        {
            foreach (var row in paper.Questions)
            {
                await usageRepository.InsertAsync(new QuestionUsageLog(GuidGenerator.Create(), row.QuestionBankItemId, QuestionUsageSource.QuestionPaper, paper.Id, DateTime.UtcNow, CurrentUser.Id ?? Guid.Empty));
            }
        }

        var setting = await GetSettingsEntityAsync(paper.AcademicYearId);
        if (setting.LockAfterPublish)
        {
            paper.IsLocked = true;
            paper.Status = QuestionPaperStatus.Locked;
        }

        await paperRepository.UpdateAsync(paper, autoSave: true);
        return Map(paper);
    }

    public async Task<QuestionPaperDto> LockAsync(Guid id)
    {
        var paper = await GetPaperEntityAsync(id);
        if (paper.Status is not (QuestionPaperStatus.Approved or QuestionPaperStatus.Published))
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:OnlyApprovedOrPublishedCanLock");
        }

        paper.IsLocked = true;
        paper.Status = QuestionPaperStatus.Locked;
        await paperRepository.UpdateAsync(paper, autoSave: true);
        return Map(paper);
    }

    public async Task<QuestionPaperBlueprintDto?> GetBlueprintAsync(Guid paperId)
    {
        var blueprint = await GetBlueprintEntityAsync(paperId, false);
        return blueprint == null ? null : Map(blueprint);
    }

    public async Task<QuestionPaperBlueprintDto> SaveBlueprintAsync(Guid paperId, QuestionPaperBlueprintDto input)
    {
        var paper = await GetPaperEntityAsync(paperId);
        EnsureEditable(paper);
        var blueprint = await GetBlueprintEntityAsync(paperId, false);
        var isNew = blueprint == null;
        blueprint ??= new QuestionPaperBlueprint(GuidGenerator.Create(), CurrentTenant.Id, paperId, input.TotalMarks);
        blueprint.TotalMarks = input.TotalMarks;

        if (!isNew)
        {
            await chapterRuleRepository.DeleteManyAsync(blueprint.ChapterRules.ToList());
            await difficultyRuleRepository.DeleteManyAsync(blueprint.DifficultyRules.ToList());
            await bloomRuleRepository.DeleteManyAsync(blueprint.BloomRules.ToList());
            blueprint.ChapterRules.Clear();
            blueprint.DifficultyRules.Clear();
            blueprint.BloomRules.Clear();
        }

        foreach (var rule in input.ChapterRules)
        {
            blueprint.ChapterRules.Add(new QuestionPaperBlueprintChapterRule(GuidGenerator.Create(), blueprint.Id, rule.ChapterId, rule.Marks, rule.NumberOfQuestions));
        }
        foreach (var rule in input.DifficultyRules)
        {
            blueprint.DifficultyRules.Add(new QuestionPaperBlueprintDifficultyRule(GuidGenerator.Create(), blueprint.Id, rule.DifficultyLevel, rule.Percentage));
        }
        foreach (var rule in input.BloomRules)
        {
            blueprint.BloomRules.Add(new QuestionPaperBlueprintBloomRule(GuidGenerator.Create(), blueprint.Id, rule.BloomLevel, rule.Percentage));
        }

        if (isNew) await blueprintRepository.InsertAsync(blueprint, autoSave: true);
        else await blueprintRepository.UpdateAsync(blueprint, autoSave: true);
        return Map(blueprint);
    }

    public async Task<BlueprintValidationResultDto> ValidateBlueprintAsync(Guid paperId)
    {
        var paper = await GetPaperEntityAsync(paperId);
        var blueprint = await GetBlueprintEntityAsync(paperId, true);
        if (blueprint == null)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:BlueprintRequired");
        }

        var questionMarks = paper.Questions.Sum(x => x.Marks);
        var result = new BlueprintValidationResultDto
        {
            IsValid = true,
            AssignedMarks = questionMarks,
            RemainingMarks = Math.Max(0, paper.TotalMarks - questionMarks),
            QuestionCount = paper.Questions.Count
        };
        var chapterTotal = blueprint.ChapterRules.Sum(x => x.Marks);
        if (blueprint.ChapterRules.Count > 0 && chapterTotal != paper.TotalMarks)
        {
            result.Errors.Add($"Chapter marks total {chapterTotal} must equal paper total {paper.TotalMarks}.");
        }
        if (blueprint.DifficultyRules.Count > 0 && blueprint.DifficultyRules.Sum(x => x.Percentage) != 100)
        {
            result.Errors.Add("Difficulty distribution total must be 100%.");
        }
        if (blueprint.BloomRules.Count > 0 && blueprint.BloomRules.Sum(x => x.Percentage) != 100)
        {
            result.Errors.Add("Bloom distribution total must be 100%.");
        }
        if (paper.Questions.Count > 0 && questionMarks != paper.TotalMarks)
        {
            result.Errors.Add($"Selected question marks {questionMarks} must equal paper total {paper.TotalMarks}.");
        }
        if (paper.Questions.Count == 0)
        {
            result.Warnings.Add("No questions have been added to this paper yet.");
        }
        if (blueprint.ChapterRules.Count == 0)
        {
            result.Warnings.Add("Chapter coverage is not configured.");
        }

        result.IsValid = result.Errors.Count == 0;
        result.BlockingErrorCount = result.Errors.Count;
        result.WarningCount = result.Warnings.Count;
        result.BalanceScore = CalculateBlueprintBalanceScore(paper, blueprint, result);
        return result;
    }

    public async Task<QuestionPaperDto> GenerateAsync(Guid paperId)
    {
        var paper = await GetPaperEntityAsync(paperId);
        EnsureEditable(paper);
        var blueprint = (await GetBlueprintEntityAsync(paperId, true))!;
        var validation = await ValidateBlueprintAsync(paperId);
        if (!validation.IsValid)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:InvalidBlueprint").WithData("Errors", string.Join("; ", validation.Errors));
        }

        var approvedQuestions = await questionRepository.GetListAsync(x =>
            x.AcademicYearId == paper.AcademicYearId &&
            x.StandardId == paper.StandardId &&
            x.SubjectId == paper.SubjectId &&
            x.Status == QuestionStatus.Approved &&
            !x.IsArchived);
        var setting = await GetSettingsEntityAsync(paper.AcademicYearId);
        var approvedQuestionIds = approvedQuestions.Select(x => x.Id).ToList();
        var recentlyUsedQuestionIds = await GetRecentlyUsedQuestionIdsAsync(setting, approvedQuestionIds);

        var selected = blueprint.ChapterRules.Count > 0
            ? approvedQuestions.Where(q => q.ChapterId.HasValue && blueprint.ChapterRules.Any(r => r.ChapterId == q.ChapterId.Value)).ToList()
            : approvedQuestions;
        selected = selected.Where(q => !recentlyUsedQuestionIds.Contains(q.Id)).ToList();

        if (selected.Count == 0)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:InsufficientQuestions");
        }

        await paperQuestionRepository.DeleteManyAsync(paper.Questions.ToList());
        paper.Questions.Clear();

        var section = paper.Sections.OrderBy(x => x.SortOrder).FirstOrDefault();
        if (section == null)
        {
            section = new QuestionPaperSection(GuidGenerator.Create(), paper.Id, "Section A", paper.TotalMarks, 1);
            paper.Sections.Add(section);
        }

        decimal total = 0;
        var number = 1;
        foreach (var question in selected.OrderBy(x => x.DifficultyLevel).ThenBy(x => x.CreationTime))
        {
            if (total + question.Marks > paper.TotalMarks) continue;
            paper.Questions.Add(new QuestionPaperQuestion(GuidGenerator.Create(), paper.Id, section.Id, question.Id, number, question.Marks, number));
            total += question.Marks;
            number++;
            if (total == paper.TotalMarks) break;
        }

        if (total != paper.TotalMarks)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:InsufficientMarks").WithData("SelectedMarks", total).WithData("TotalMarks", paper.TotalMarks);
        }

        paper.Status = QuestionPaperStatus.Generated;
        await paperRepository.UpdateAsync(paper, autoSave: true);
        return Map(paper);
    }

    public async Task<QuestionPaperDto> AddQuestionAsync(Guid paperId, AddPaperQuestionDto input)
    {
        var paper = await GetPaperEntityAsync(paperId);
        EnsureEditable(paper);
        var question = await questionRepository.FindAsync(input.QuestionBankItemId) ?? throw new BusinessException("ShikshaMitra.QuestionBank:NotFound");
        var setting = await GetSettingsEntityAsync(paper.AcademicYearId);
        if (input.QuestionPaperSectionId == Guid.Empty ||
            paper.Sections.All(x => x.Id != input.QuestionPaperSectionId))
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:SectionNotFound");
        }

        if (input.Marks <= 0)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:InvalidQuestionMarks");
        }

        if (paper.Questions.Any(x => x.QuestionBankItemId == input.QuestionBankItemId))
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:DuplicateQuestionInPaper");
        }

        if (question.IsArchived ||
            question.AcademicYearId != paper.AcademicYearId ||
            question.StandardId != paper.StandardId ||
            question.SubjectId != paper.SubjectId)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:QuestionNotEligibleForPaper");
        }

        if (setting.RequireApprovedQuestions && question.Status != QuestionStatus.Approved)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:OnlyApprovedQuestionsAllowed");
        }

        await EnsureQuestionIsNotRecentlyUsedAsync(setting, question.Id);

        if (paper.Questions.Sum(x => x.Marks) + input.Marks > paper.TotalMarks)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:QuestionMarksExceedPaperTotal");
        }

        paper.Questions.Add(new QuestionPaperQuestion(GuidGenerator.Create(), paper.Id, input.QuestionPaperSectionId, input.QuestionBankItemId, input.QuestionNumber, input.Marks, input.SortOrder, input.ChoiceGroupCode));

        await paperRepository.UpdateAsync(paper, autoSave: true);
        return Map(paper);
    }

    public async Task<QuestionPaperDto> RemoveQuestionAsync(Guid paperId, Guid paperQuestionId)
    {
        var paper = await GetPaperEntityAsync(paperId);
        EnsureEditable(paper);
        var row = paper.Questions.FirstOrDefault(x => x.Id == paperQuestionId) ?? throw new BusinessException("ShikshaMitra.QuestionPapers:QuestionNotFound");
        paper.Questions.Remove(row);
        await paperQuestionRepository.DeleteAsync(row, autoSave: true);
        return Map(paper);
    }

    public async Task<QuestionPaperDto> ReorderQuestionsAsync(Guid paperId, ReorderPaperQuestionsDto input)
    {
        var paper = await GetPaperEntityAsync(paperId);
        EnsureEditable(paper);
        foreach (var item in input.Items)
        {
            var row = paper.Questions.FirstOrDefault(x => x.Id == item.PaperQuestionId);
            if (row == null) continue;
            row.QuestionNumber = item.QuestionNumber;
            row.SortOrder = item.SortOrder;
        }

        await paperRepository.UpdateAsync(paper, autoSave: true);
        return Map(paper);
    }

    public async Task<QuestionPaperPreviewDto> GetPreviewAsync(Guid paperId)
    {
        var paper = await GetPaperEntityAsync(paperId);
        var questionMap = await GetQuestionTextMapAsync(paper);
        var html = new StringBuilder();
        html.Append($"<h1>{paper.PaperTitle}</h1><p>Duration: {paper.DurationMinutes} minutes | Total Marks: {paper.TotalMarks}</p>");
        if (!string.IsNullOrWhiteSpace(paper.GeneralInstructions)) html.Append($"<p>{paper.GeneralInstructions}</p>");
        foreach (var section in paper.Sections.OrderBy(x => x.SortOrder))
        {
            html.Append($"<h2>{section.SectionName}</h2>");
            foreach (var q in paper.Questions.Where(x => x.QuestionPaperSectionId == section.Id).OrderBy(x => x.SortOrder))
            {
                var text = questionMap.TryGetValue(q.QuestionBankItemId, out var questionText)
                    ? questionText
                    : "Question unavailable";
                html.Append($"<p>{q.QuestionNumber}. {text} <strong>[{q.Marks}]</strong></p>");
            }
        }

        return new QuestionPaperPreviewDto { Paper = Map(paper, questionMap), PrintHtml = html.ToString() };
    }

    public async Task<QuestionPaperDashboardDto> GetDashboardAsync(Guid academicYearId)
    {
        var query = await paperRepository.WithDetailsAsync(x => x.Questions);
        var rows = await asyncExecuter.ToListAsync(query.Where(x => x.AcademicYearId == academicYearId));
        return new QuestionPaperDashboardDto
        {
            TotalPapers = rows.Count,
            DraftPapers = rows.Count(x => x.Status == QuestionPaperStatus.Draft),
            GeneratedPapers = rows.Count(x => x.Status == QuestionPaperStatus.Generated),
            PendingApproval = rows.Count(x => x.Status == QuestionPaperStatus.Submitted),
            ApprovedPapers = rows.Count(x => x.Status == QuestionPaperStatus.Approved),
            PublishedPapers = rows.Count(x => x.Status == QuestionPaperStatus.Published),
            LockedPapers = rows.Count(x => x.Status == QuestionPaperStatus.Locked),
            NeedsAttention = rows.Count(x => x.Status == QuestionPaperStatus.Rejected),
            MissingQuestions = rows.Count(x => x.Questions.Count == 0)
        };
    }

    private async Task<QuestionPaperDto> ChangeStatusAsync(Guid id, QuestionPaperStatus status, PaperReviewDto? input)
    {
        var paper = await GetPaperEntityAsync(id);
        switch (status)
        {
            case QuestionPaperStatus.Submitted:
                if (paper.Status is not (QuestionPaperStatus.Draft or QuestionPaperStatus.Generated or QuestionPaperStatus.Rejected))
                {
                    throw new BusinessException("ShikshaMitra.QuestionPapers:CannotSubmit");
                }

                await ValidatePaperMarksAsync(paper, requireQuestions: true);
                await ValidateBlueprintIfEnabledAsync(paper);
                break;
            case QuestionPaperStatus.Approved:
            case QuestionPaperStatus.Rejected:
                if (paper.Status != QuestionPaperStatus.Submitted)
                {
                    throw new BusinessException("ShikshaMitra.QuestionPapers:OnlySubmittedCanReview");
                }

                EnsureCanReview(paper);
                if (status == QuestionPaperStatus.Rejected && string.IsNullOrWhiteSpace(input?.Comment))
                {
                    throw new BusinessException("ShikshaMitra.QuestionPapers:RejectCommentRequired");
                }

                break;
            case QuestionPaperStatus.Archived:
                break;
            default:
                throw new BusinessException("ShikshaMitra.QuestionPapers:InvalidStatusTransition");
        }

        paper.Status = status;
        await paperRepository.UpdateAsync(paper, autoSave: true);
        return Map(paper);
    }

    private async Task ValidateBlueprintIfEnabledAsync(QuestionPaper paper)
    {
        var setting = await GetSettingsEntityAsync(paper.AcademicYearId);
        if (!setting.EnableBlueprintValidation)
        {
            return;
        }

        var validation = await ValidateBlueprintAsync(paper.Id);
        if (!validation.IsValid)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:InvalidBlueprint")
                .WithData("Errors", string.Join("; ", validation.Errors));
        }
    }

    private async Task ValidatePaperMarksAsync(QuestionPaper paper, bool requireQuestions = false)
    {
        var sectionMarks = paper.Sections.Sum(x => x.Marks);
        var questionMarks = paper.Questions.Sum(x => x.Marks);
        if (requireQuestions && paper.Questions.Count == 0)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:QuestionsRequired");
        }

        if (paper.Sections.Count > 0 && sectionMarks != paper.TotalMarks)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:SectionMarksMismatch");
        }
        if (paper.Questions.Count > 0 && questionMarks != paper.TotalMarks)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:QuestionMarksMismatch");
        }
        await Task.CompletedTask;
    }

    private static int CalculateBlueprintBalanceScore(
        QuestionPaper paper,
        QuestionPaperBlueprint blueprint,
        BlueprintValidationResultDto validation)
    {
        var score = 100;
        score -= validation.Errors.Count * 20;
        score -= validation.Warnings.Count * 8;

        if (paper.TotalMarks > 0)
        {
            var questionGap = Math.Abs(paper.TotalMarks - validation.AssignedMarks);
            score -= (int)Math.Min(25, Math.Round((double)(questionGap / paper.TotalMarks * 100)));

            var chapterGap = Math.Abs(paper.TotalMarks - blueprint.ChapterRules.Sum(x => x.Marks));
            score -= (int)Math.Min(20, Math.Round((double)(chapterGap / paper.TotalMarks * 100)));
        }

        if (blueprint.DifficultyRules.Count > 0)
        {
            score -= (int)Math.Min(15, Math.Abs(100 - blueprint.DifficultyRules.Sum(x => x.Percentage)));
        }

        if (blueprint.BloomRules.Count > 0)
        {
            score -= (int)Math.Min(15, Math.Abs(100 - blueprint.BloomRules.Sum(x => x.Percentage)));
        }

        return Math.Clamp(score, 0, 100);
    }

    private async Task<QuestionPaperSetting> GetSettingsEntityAsync(Guid academicYearId)
    {
        return await settingRepository.FirstOrDefaultAsync(x => x.AcademicYearId == academicYearId)
            ?? new QuestionPaperSetting(GuidGenerator.Create(), CurrentTenant.Id, academicYearId);
    }

    private async Task<QuestionPaper> GetPaperEntityAsync(Guid id, bool allowTeacherSubjectRead = false)
    {
        var query = await paperRepository.WithDetailsAsync(x => x.Sections, x => x.Questions);
        var paper = await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
            ?? throw new BusinessException("ShikshaMitra.QuestionPapers:NotFound").WithData("Id", id);
		if (!allowTeacherSubjectRead || !await HasTeacherSubjectReadAccessAsync(paper))
		{
			await userScopeAccessService.EnsureCurrentUserCanAccessAsync(paper, operation: "question-paper-access");
		}
		return paper;
    }

    private async Task<bool> HasTeacherSubjectReadAccessAsync(QuestionPaper paper)
    {
        if (!CurrentUser.Id.HasValue || await userScopeAccessService.CurrentUserHasTenantWideScopeAsync())
        {
            return false;
        }

        var employee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == CurrentUser.Id.Value);
        var employeeIds = new HashSet<Guid>();
        if (employee != null)
        {
            employeeIds.Add(employee.Id);
        }

        employeeIds.UnionWith(await reportingManagerAccessService.GetReporteeEmployeeIdsAsync(CurrentUser.Id.Value));
        if (employeeIds.Count == 0)
        {
            return false;
        }

        var assignedSubject = (await assignedSubjectRepository.GetListAsync(
            x => x.StandardId == paper.StandardId && x.SubjectId == paper.SubjectId,
            includeDetails: true)).FirstOrDefault();
        if (assignedSubject == null)
        {
            return false;
        }

        if (assignedSubject.Teachers.Any(x => employeeIds.Contains(x.TeacherId)))
        {
            return true;
        }

        return await teacherSubjectAllocationRepository.AnyAsync(x =>
            x.StandardId == paper.StandardId &&
            x.SubjectId == paper.SubjectId &&
            x.Teachers.Any(teacher => employeeIds.Contains(teacher.TeacherId)));
    }

    private async Task<Dictionary<Guid, string>> GetQuestionTextMapAsync(QuestionPaper paper)
    {
        var questionIds = paper.Questions
            .Select(x => x.QuestionBankItemId)
            .Distinct()
            .ToList();
        if (questionIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return (await questionRepository.GetListAsync(x => questionIds.Contains(x.Id)))
            .ToDictionary(x => x.Id, x => x.QuestionText);
    }

    private async Task<QuestionPaperBlueprint?> GetBlueprintEntityAsync(Guid paperId, bool required)
    {
        var query = await blueprintRepository.WithDetailsAsync(x => x.ChapterRules, x => x.DifficultyRules, x => x.BloomRules);
        var blueprint = await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.QuestionPaperId == paperId));
        if (blueprint == null && required) throw new BusinessException("ShikshaMitra.QuestionPapers:BlueprintRequired");
        return blueprint;
    }

    private static void EnsureEditable(QuestionPaper paper)
    {
        if (paper.IsLocked || paper.Status is QuestionPaperStatus.Submitted or QuestionPaperStatus.Approved or QuestionPaperStatus.Published or QuestionPaperStatus.Locked or QuestionPaperStatus.Archived)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:PaperLocked");
        }
    }

    private static void ValidatePaperInput(CreateUpdateQuestionPaperDto input)
    {
        if (input.AcademicYearId == Guid.Empty) throw new BusinessException("ShikshaMitra.QuestionPapers:AcademicYearRequired");
        if (input.StandardId == Guid.Empty) throw new BusinessException("ShikshaMitra.QuestionPapers:StandardRequired");
        if (input.SubjectId == Guid.Empty) throw new BusinessException("ShikshaMitra.QuestionPapers:SubjectRequired");
        if (string.IsNullOrWhiteSpace(input.PaperTitle)) throw new BusinessException("ShikshaMitra.QuestionPapers:TitleRequired");
        if (input.TotalMarks <= 0) throw new BusinessException("ShikshaMitra.QuestionPapers:InvalidMarks");
        if (input.DurationMinutes <= 0) throw new BusinessException("ShikshaMitra.QuestionPapers:InvalidDuration");
        if (input.Sections.Any(x => string.IsNullOrWhiteSpace(x.SectionName))) throw new BusinessException("ShikshaMitra.QuestionPapers:SectionNameRequired");
        if (input.Sections.Any(x => x.Marks <= 0)) throw new BusinessException("ShikshaMitra.QuestionPapers:InvalidSectionMarks");
        if (input.Sections.Count > 0 && input.Sections.Sum(x => x.Marks) != input.TotalMarks) throw new BusinessException("ShikshaMitra.QuestionPapers:SectionMarksMismatch");
    }

    private async Task<HashSet<Guid>> GetRecentlyUsedQuestionIdsAsync(QuestionPaperSetting setting, List<Guid> questionIds)
    {
        if (!setting.EnableRecentQuestionBlock || questionIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var days = Math.Clamp(setting.RecentQuestionBlockDays, 1, 365);
        var cutoff = Clock.Now.ToUniversalTime().AddDays(-days);
        var recentUsage = await usageRepository.GetListAsync(x =>
            questionIds.Contains(x.QuestionBankItemId) &&
            x.Source == QuestionUsageSource.QuestionPaper &&
            x.UsedAt >= cutoff);

        return recentUsage.Select(x => x.QuestionBankItemId).ToHashSet();
    }

    private async Task EnsureQuestionIsNotRecentlyUsedAsync(QuestionPaperSetting setting, Guid questionId)
    {
        var recentlyUsedQuestionIds = await GetRecentlyUsedQuestionIdsAsync(setting, new List<Guid> { questionId });
        if (recentlyUsedQuestionIds.Contains(questionId))
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:QuestionRecentlyUsed");
        }
    }

    private void EnsureCanReview(QuestionPaper paper)
    {
        if (CurrentUser.Id.HasValue && paper.CreatorId == CurrentUser.Id.Value)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:CannotReviewOwnPaper");
        }
    }

    private static IQueryable<QuestionPaper> ApplySearch(IQueryable<QuestionPaper> query, QuestionPaperSearchInput input)
    {
        if (input.AcademicYearId.HasValue) query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
        if (input.ExamId.HasValue) query = query.Where(x => x.ExamId == input.ExamId);
        if (input.ExamScheduleId.HasValue) query = query.Where(x => x.ExamScheduleId == input.ExamScheduleId);
        if (input.StandardId.HasValue) query = query.Where(x => x.StandardId == input.StandardId);
        if (input.SubjectId.HasValue) query = query.Where(x => x.SubjectId == input.SubjectId);
        if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status);
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim().ToLower();
            query = query.Where(x => x.PaperTitle.ToLower().Contains(keyword));
        }
        return query;
    }

    private static void Apply(CreateUpdateQuestionPaperDto input, QuestionPaper paper)
    {
        paper.AcademicYearId = input.AcademicYearId;
        paper.ExamId = input.ExamId;
        paper.ExamScheduleId = input.ExamScheduleId;
        paper.StandardId = input.StandardId;
        paper.SubjectId = input.SubjectId;
        paper.PaperTitle = input.PaperTitle;
        paper.TotalMarks = input.TotalMarks;
        paper.DurationMinutes = input.DurationMinutes;
        paper.CreationMode = input.CreationMode;
        paper.GeneralInstructions = input.GeneralInstructions;
    }

    private static QuestionPaperDto Map(
        QuestionPaper paper,
        IReadOnlyDictionary<Guid, string>? questionTextMap = null) => new()
    {
        Id = paper.Id,
        AcademicYearId = paper.AcademicYearId,
        ExamId = paper.ExamId,
        ExamScheduleId = paper.ExamScheduleId,
        StandardId = paper.StandardId,
        SubjectId = paper.SubjectId,
        PaperTitle = paper.PaperTitle,
        TotalMarks = paper.TotalMarks,
        DurationMinutes = paper.DurationMinutes,
        UpdatedAt = paper.LastModificationTime ?? paper.CreationTime,
        CreationMode = paper.CreationMode,
        GeneralInstructions = paper.GeneralInstructions,
        Status = paper.Status,
        VersionNumber = paper.VersionNumber,
        IsLocked = paper.IsLocked,
        Sections = paper.Sections.OrderBy(x => x.SortOrder).Select(x => new QuestionPaperSectionDto { Id = x.Id, SectionName = x.SectionName, Instructions = x.Instructions, Marks = x.Marks, SortOrder = x.SortOrder }).ToList(),
        Questions = paper.Questions.OrderBy(x => x.SortOrder).Select(x => new QuestionPaperQuestionDto
        {
            Id = x.Id,
            QuestionPaperSectionId = x.QuestionPaperSectionId,
            QuestionBankItemId = x.QuestionBankItemId,
            Marks = x.Marks,
            QuestionNumber = x.QuestionNumber,
            SortOrder = x.SortOrder,
            ChoiceGroupCode = x.ChoiceGroupCode,
            QuestionText = questionTextMap != null && questionTextMap.TryGetValue(x.QuestionBankItemId, out var questionText)
                ? questionText
                : null
        }).ToList()
    };

    private async Task EnrichDisplayFieldsAsync(IReadOnlyCollection<QuestionPaperDto> papers)
    {
        if (papers.Count == 0)
        {
            return;
        }

        var standardIds = papers.Select(x => x.StandardId).Distinct().ToList();
        var subjectIds = papers.Select(x => x.SubjectId).Distinct().ToList();
        var examIds = papers.Where(x => x.ExamId.HasValue).Select(x => x.ExamId!.Value).Distinct().ToList();

        var standardQuery = await standardRepository.GetQueryableAsync();
        var subjectQuery = await subjectRepository.GetQueryableAsync();
        var examQuery = await examRepository.GetQueryableAsync();

        // EF Core DbContexts do not support multiple active operations. All three
        // repositories share this request's scoped context, so execute the lookup
        // queries in sequence rather than concurrently.
        var standards = await asyncExecuter.ToListAsync(
            standardQuery.Where(x => standardIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
        var subjects = await asyncExecuter.ToListAsync(
            subjectQuery.Where(x => subjectIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
        var exams = await asyncExecuter.ToListAsync(
            examQuery.Where(x => examIds.Contains(x.Id)).Select(x => new { x.Id, x.ExamName }));

        var standardNames = standards.ToDictionary(x => x.Id, x => x.Name);
        var subjectNames = subjects.ToDictionary(x => x.Id, x => x.Name);
        var examNames = exams.ToDictionary(x => x.Id, x => x.ExamName);

        foreach (var paper in papers)
        {
            if (standardNames.TryGetValue(paper.StandardId, out var standardName))
            {
                paper.StandardName = standardName;
            }

            if (subjectNames.TryGetValue(paper.SubjectId, out var subjectName))
            {
                paper.SubjectName = subjectName;
            }

            if (paper.ExamId.HasValue)
            {
                if (examNames.TryGetValue(paper.ExamId.Value, out var examName))
                {
                    paper.ExamName = examName;
                    paper.ExamTitle = examName;
                }
            }
        }
    }

    private static QuestionPaperBlueprintDto Map(QuestionPaperBlueprint blueprint) => new()
    {
        Id = blueprint.Id,
        QuestionPaperId = blueprint.QuestionPaperId,
        TotalMarks = blueprint.TotalMarks,
        ChapterRules = blueprint.ChapterRules.Select(x => new BlueprintChapterRuleDto { ChapterId = x.ChapterId, Marks = x.Marks, NumberOfQuestions = x.NumberOfQuestions }).ToList(),
        DifficultyRules = blueprint.DifficultyRules.Select(x => new BlueprintDifficultyRuleDto { DifficultyLevel = x.DifficultyLevel, Percentage = x.Percentage }).ToList(),
        BloomRules = blueprint.BloomRules.Select(x => new BlueprintBloomRuleDto { BloomLevel = x.BloomLevel, Percentage = x.Percentage }).ToList()
    };

    private static QuestionPaperSettingDto Map(QuestionPaperSetting setting) => new()
    {
        Id = setting.Id,
        AcademicYearId = setting.AcademicYearId,
        RequireApprovedQuestions = setting.RequireApprovedQuestions,
        EnableBlueprintValidation = setting.EnableBlueprintValidation,
        EnableRecentQuestionBlock = setting.EnableRecentQuestionBlock,
        RecentQuestionBlockDays = setting.RecentQuestionBlockDays,
        DistributionTolerancePercentage = setting.DistributionTolerancePercentage,
        EnablePdfCompilation = setting.EnablePdfCompilation,
        LockAfterPublish = setting.LockAfterPublish,
        EnableAiBlueprintGenerator = setting.EnableAiBlueprintGenerator,
        EnableAiPaperGenerator = setting.EnableAiPaperGenerator
    };

    private static void Apply(QuestionPaperSettingDto input, QuestionPaperSetting setting)
    {
        setting.AcademicYearId = input.AcademicYearId;
        setting.RequireApprovedQuestions = input.RequireApprovedQuestions;
        setting.EnableBlueprintValidation = input.EnableBlueprintValidation;
        setting.EnableRecentQuestionBlock = input.EnableRecentQuestionBlock;
        setting.RecentQuestionBlockDays = input.RecentQuestionBlockDays;
        setting.DistributionTolerancePercentage = input.DistributionTolerancePercentage;
        setting.EnablePdfCompilation = input.EnablePdfCompilation;
        setting.LockAfterPublish = input.LockAfterPublish;
        setting.EnableAiBlueprintGenerator = input.EnableAiBlueprintGenerator;
        setting.EnableAiPaperGenerator = input.EnableAiPaperGenerator;
    }
}
