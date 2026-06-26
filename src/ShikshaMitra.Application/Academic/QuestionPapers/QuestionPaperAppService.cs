using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.QuestionBank;
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
    IAsyncQueryableExecuter asyncExecuter
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
        var total = await asyncExecuter.CountAsync(query);
        var page = Math.Max(1, input.Page);
        var pageSize = Math.Clamp(input.PageSize, 5, 100);
        var items = await asyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Skip((page - 1) * pageSize).Take(pageSize));
        return new QuestionPaperListResponseDto
        {
            TotalCount = total,
            CurrentPage = page,
            PageSize = pageSize,
            Items = items.Select(Map).ToList()
        };
    }

    public async Task<QuestionPaperDto> GetPaperAsync(Guid id) => Map(await GetPaperEntityAsync(id));

    public async Task<QuestionPaperDto> CreatePaperAsync(CreateUpdateQuestionPaperDto input)
    {
        ValidatePaperInput(input);
        var paper = new QuestionPaper(GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId, input.StandardId, input.SubjectId, input.PaperTitle, input.TotalMarks, input.DurationMinutes, input.CreationMode);
        Apply(input, paper);
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
        await sectionRepository.DeleteManyAsync(paper.Sections.ToList());
        paper.Sections.Clear();
        foreach (var section in input.Sections.OrderBy(x => x.SortOrder))
        {
            paper.Sections.Add(new QuestionPaperSection(section.Id ?? GuidGenerator.Create(), paper.Id, section.SectionName, section.Marks, section.SortOrder, section.Instructions));
        }

        await paperRepository.UpdateAsync(paper, autoSave: true);
        return Map(paper);
    }

    public async Task DeletePaperAsync(Guid id)
    {
        var paper = await GetPaperEntityAsync(id);
        if (paper.Status is QuestionPaperStatus.Published or QuestionPaperStatus.Locked)
        {
            paper.Status = QuestionPaperStatus.Archived;
            await paperRepository.UpdateAsync(paper, autoSave: true);
            return;
        }

        await paperRepository.DeleteAsync(paper, autoSave: true);
    }

    public Task<QuestionPaperDto> SubmitAsync(Guid id) => ChangeStatusAsync(id, QuestionPaperStatus.Submitted);
    public Task<QuestionPaperDto> ApproveAsync(Guid id, PaperReviewDto input) => ChangeStatusAsync(id, QuestionPaperStatus.Approved);
    public Task<QuestionPaperDto> RejectAsync(Guid id, PaperReviewDto input) => ChangeStatusAsync(id, QuestionPaperStatus.Rejected);
    public Task<QuestionPaperDto> ArchiveAsync(Guid id) => ChangeStatusAsync(id, QuestionPaperStatus.Archived);

    public async Task<QuestionPaperDto> PublishAsync(Guid id)
    {
        var paper = await GetPaperEntityAsync(id);
        if (paper.Status != QuestionPaperStatus.Approved)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:OnlyApprovedCanPublish");
        }

        await ValidatePaperMarksAsync(paper);
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
        var blueprint = (await GetBlueprintEntityAsync(paperId, true))!;
        var result = new BlueprintValidationResultDto { IsValid = true };
        var chapterTotal = blueprint.ChapterRules.Sum(x => x.Marks);
        if (chapterTotal != paper.TotalMarks)
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

        result.IsValid = result.Errors.Count == 0;
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

        var selected = blueprint.ChapterRules.Count > 0
            ? approvedQuestions.Where(q => q.ChapterId.HasValue && blueprint.ChapterRules.Any(r => r.ChapterId == q.ChapterId.Value)).ToList()
            : approvedQuestions;

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
        if (setting.RequireApprovedQuestions && question.Status != QuestionStatus.Approved)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:OnlyApprovedQuestionsAllowed");
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
        var questionIds = paper.Questions.Select(x => x.QuestionBankItemId).Distinct().ToList();
        var questionMap = (await questionRepository.GetListAsync(x => questionIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.QuestionText);
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

        return new QuestionPaperPreviewDto { Paper = Map(paper), PrintHtml = html.ToString() };
    }

    public async Task<QuestionPaperDashboardDto> GetDashboardAsync(Guid academicYearId)
    {
        var rows = await paperRepository.GetListAsync(x => x.AcademicYearId == academicYearId);
        return new QuestionPaperDashboardDto
        {
            TotalPapers = rows.Count,
            DraftPapers = rows.Count(x => x.Status == QuestionPaperStatus.Draft),
            GeneratedPapers = rows.Count(x => x.Status == QuestionPaperStatus.Generated),
            PendingApproval = rows.Count(x => x.Status == QuestionPaperStatus.Submitted),
            ApprovedPapers = rows.Count(x => x.Status == QuestionPaperStatus.Approved),
            PublishedPapers = rows.Count(x => x.Status == QuestionPaperStatus.Published),
            LockedPapers = rows.Count(x => x.Status == QuestionPaperStatus.Locked)
        };
    }

    private async Task<QuestionPaperDto> ChangeStatusAsync(Guid id, QuestionPaperStatus status)
    {
        var paper = await GetPaperEntityAsync(id);
        if (status == QuestionPaperStatus.Submitted) await ValidatePaperMarksAsync(paper);
        paper.Status = status;
        await paperRepository.UpdateAsync(paper, autoSave: true);
        return Map(paper);
    }

    private async Task ValidatePaperMarksAsync(QuestionPaper paper)
    {
        var sectionMarks = paper.Sections.Sum(x => x.Marks);
        var questionMarks = paper.Questions.Sum(x => x.Marks);
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

    private async Task<QuestionPaperSetting> GetSettingsEntityAsync(Guid academicYearId)
    {
        return await settingRepository.FirstOrDefaultAsync(x => x.AcademicYearId == academicYearId)
            ?? new QuestionPaperSetting(GuidGenerator.Create(), CurrentTenant.Id, academicYearId);
    }

    private async Task<QuestionPaper> GetPaperEntityAsync(Guid id)
    {
        var query = await paperRepository.WithDetailsAsync(x => x.Sections, x => x.Questions);
        return await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
            ?? throw new BusinessException("ShikshaMitra.QuestionPapers:NotFound").WithData("Id", id);
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
        if (paper.IsLocked || paper.Status is QuestionPaperStatus.Published or QuestionPaperStatus.Locked)
        {
            throw new BusinessException("ShikshaMitra.QuestionPapers:PaperLocked");
        }
    }

    private static void ValidatePaperInput(CreateUpdateQuestionPaperDto input)
    {
        if (input.StandardId == Guid.Empty) throw new BusinessException("ShikshaMitra.QuestionPapers:StandardRequired");
        if (input.SubjectId == Guid.Empty) throw new BusinessException("ShikshaMitra.QuestionPapers:SubjectRequired");
        if (string.IsNullOrWhiteSpace(input.PaperTitle)) throw new BusinessException("ShikshaMitra.QuestionPapers:TitleRequired");
        if (input.TotalMarks <= 0) throw new BusinessException("ShikshaMitra.QuestionPapers:InvalidMarks");
        if (input.DurationMinutes <= 0) throw new BusinessException("ShikshaMitra.QuestionPapers:InvalidDuration");
        if (input.Sections.Count > 0 && input.Sections.Sum(x => x.Marks) != input.TotalMarks) throw new BusinessException("ShikshaMitra.QuestionPapers:SectionMarksMismatch");
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

    private static QuestionPaperDto Map(QuestionPaper paper) => new()
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
        CreationMode = paper.CreationMode,
        GeneralInstructions = paper.GeneralInstructions,
        Status = paper.Status,
        VersionNumber = paper.VersionNumber,
        IsLocked = paper.IsLocked,
        Sections = paper.Sections.OrderBy(x => x.SortOrder).Select(x => new QuestionPaperSectionDto { Id = x.Id, SectionName = x.SectionName, Instructions = x.Instructions, Marks = x.Marks, SortOrder = x.SortOrder }).ToList(),
        Questions = paper.Questions.OrderBy(x => x.SortOrder).Select(x => new QuestionPaperQuestionDto { Id = x.Id, QuestionPaperSectionId = x.QuestionPaperSectionId, QuestionBankItemId = x.QuestionBankItemId, Marks = x.Marks, QuestionNumber = x.QuestionNumber, SortOrder = x.SortOrder, ChoiceGroupCode = x.ChoiceGroupCode }).ToList()
    };

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
