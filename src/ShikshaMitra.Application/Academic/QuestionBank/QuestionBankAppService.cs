using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.Excel.Elements;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.QuestionPapers;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.QuestionBank;

[Authorize]
[RemoteService(IsEnabled = false)]
public class QuestionBankAppService(
    IRepository<QuestionBankSetting, Guid> settingRepository,
    IRepository<QuestionBankItem, Guid> questionRepository,
    IRepository<QuestionOption, Guid> optionRepository,
    IRepository<QuestionTag, Guid> tagRepository,
    IRepository<QuestionUsageLog, Guid> usageRepository,
    IRepository<QuestionPaperQuestion, Guid> paperQuestionRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<AssignedSubjectChapter, Guid> chapterRepository,
    IExcelService excelService,
    IAsyncQueryableExecuter asyncExecuter,
    IUserScopeAccessService userScopeAccessService
) : ApplicationService, IQuestionBankAppService
{
    public async Task<QuestionBankSettingDto> GetSettingsAsync(Guid academicYearId)
    {
        var setting = await settingRepository.FirstOrDefaultAsync(x => x.AcademicYearId == academicYearId);
        if (setting == null)
        {
            setting = new QuestionBankSetting(GuidGenerator.Create(), CurrentTenant.Id, academicYearId);
            await settingRepository.InsertAsync(setting, autoSave: true);
        }

        return Map(setting);
    }

    public Task<QuestionBankTemplateDto> GetBulkTemplateAsync()
    {
        const string fileName = "question-bank-bulk-upload-template.xlsx";
        var document = new ExcelDocument
        {
            OutputFileName = fileName,
            Sheets =
            [
                new ExcelSheet
                {
                    SheetName = "Questions",
                    DefaultColumnWidth = 24,
                    Rows =
                    [
                        CreateExcelRow("Question Text", "Topic Name", "Question Type", "Difficulty", "Bloom Level", "Marks", "Negative Marks", "Answer Key", "Option 1", "Option 1 Correct", "Option 2", "Option 2 Correct", "Option 3", "Option 3 Correct", "Option 4", "Option 4 Correct", "Learning Outcome", "Explanation", "Marking Scheme", "Visibility", "Tags"),
                        CreateExcelRow("Which part of the cell controls all activities?", "Cell Organelles", (int)QuestionType.McqSingleCorrect, (int)DifficultyLevel.Easy, (int)BloomLevel.Remembering, 1, 0, "Nucleus", "Nucleus", "Yes", "Cell wall", "No", "Vacuole", "No", "Cytoplasm", "No", "Identify cell organelles and their functions", "The nucleus controls cellular activities.", "1 mark for correct answer", (int)QuestionVisibility.Department, "cell,biology"),
                        CreateExcelRow("Write one function of mitochondria.", "Cell Organelles", (int)QuestionType.ShortAnswer, (int)DifficultyLevel.Medium, (int)BloomLevel.Understanding, 2, string.Empty, "Mitochondria produce energy for the cell.", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "Explain functions of cell organelles", "Mitochondria are known as the powerhouse of the cell.", "2 marks for a correct function", (int)QuestionVisibility.Department, "mitochondria,short-answer")
                    ]
                },
                new ExcelSheet
                {
                    SheetName = "Instructions",
                    DefaultColumnWidth = 42,
                    Rows =
                    [
                        CreateExcelRow("Field", "Allowed values / Notes"),
                        CreateExcelRow("Question Text", "Required. Write the full question."),
                        CreateExcelRow("Topic Name", "Optional unless topic is required in settings. New topic names are saved with the question."),
                        CreateExcelRow("Question Type", BuildEnumInstruction<QuestionType>()),
                        CreateExcelRow("Difficulty", BuildEnumInstruction<DifficultyLevel>()),
                        CreateExcelRow("Bloom Level", BuildEnumInstruction<BloomLevel>()),
                        CreateExcelRow("Marks", "Required. Positive number."),
                        CreateExcelRow("Negative Marks", "Optional. Leave blank if not applicable."),
                        CreateExcelRow("Answer Key", "Required for most question types. For MCQ, match the correct option text."),
                        CreateExcelRow("Option N Correct", "Use Yes/No, TRUE/FALSE, or 1/0. Required for MCQ options."),
                        CreateExcelRow("Visibility", BuildEnumInstruction<QuestionVisibility>()),
                        CreateExcelRow("Tags", "Optional comma-separated values, e.g. cell,biology.")
                    ]
                }
            ]
        };

        return Task.FromResult(new QuestionBankTemplateDto
        {
            FileName = fileName,
            Content = excelService.GenerateExcelAsBytes(document)
        });
    }

    public async Task<QuestionBankSettingDto> SaveSettingsAsync(QuestionBankSettingDto input)
    {
        var setting = input.Id.HasValue
            ? await settingRepository.FindAsync(input.Id.Value)
            : await settingRepository.FirstOrDefaultAsync(x => x.AcademicYearId == input.AcademicYearId);

        var isNew = setting == null;
        setting ??= new QuestionBankSetting(GuidGenerator.Create(), CurrentTenant.Id, input.AcademicYearId);
        Apply(input, setting);

        if (isNew)
        {
            await settingRepository.InsertAsync(setting, autoSave: true);
        }
        else
        {
            await settingRepository.UpdateAsync(setting, autoSave: true);
        }

        return Map(setting);
    }

    public async Task<QuestionListResponseDto> GetQuestionsAsync(QuestionSearchInput input)
    {
        var query = await questionRepository.WithDetailsAsync(x => x.Options, x => x.Tags);
        query = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
        query = ApplySearch(query, input).Where(x => !x.IsArchived);

        var total = await asyncExecuter.CountAsync(query);
        var page = Math.Max(1, input.Page);
        var pageSize = Math.Clamp(input.PageSize, 5, 100);
        var items = await asyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Skip((page - 1) * pageSize).Take(pageSize));
        var usageCounts = await GetUsageCountsAsync(items.Select(x => x.Id).ToList());
        var duplicateIds = GetDuplicateQuestionIds(items);
        var lookup = await BuildQuestionLookupAsync(items);

        return new QuestionListResponseDto
        {
            TotalCount = total,
            CurrentPage = page,
            PageSize = pageSize,
            Items = items.Select(x => Map(x, usageCounts.GetValueOrDefault(x.Id), duplicateIds.Contains(x.Id), lookup)).ToList()
        };
    }

    public async Task<QuestionBankItemDto> GetQuestionAsync(Guid id)
    {
        var question = await GetQuestionEntityAsync(id);
        var usageCount = await GetQuestionUsageCountAsync(id);
        var lookup = await BuildQuestionLookupAsync([question]);
        return Map(question, usageCount, lookup: lookup);
    }

    public async Task<QuestionBankItemDto> CreateQuestionAsync(CreateUpdateQuestionDto input)
    {
        var setting = await GetSettingsEntityAsync(input.AcademicYearId);
        await ValidateQuestionAsync(input, setting, null);

        var question = new QuestionBankItem(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.AcademicYearId,
            input.StandardId,
            input.SubjectId,
            input.QuestionText,
            input.QuestionType,
            input.DifficultyLevel,
            input.Marks,
            CurrentUser.Id ?? Guid.Empty,
            input.Visibility ?? setting.DefaultVisibility);

        Apply(input, question);
        await userScopeAccessService.EnsureCurrentUserCanAccessAsync(question, operation: "question-bank-create");
        SyncChildren(question, input);
        await questionRepository.InsertAsync(question, autoSave: true);
        return Map(question, 0);
    }

    public async Task<QuestionBankItemDto> UpdateQuestionAsync(Guid id, CreateUpdateQuestionDto input)
    {
        var question = await GetQuestionEntityAsync(id);
        if (question.IsLocked)
        {
            throw new BusinessException("ShikshaMitra.QuestionBank:QuestionLocked");
        }

        var setting = await GetSettingsEntityAsync(input.AcademicYearId);
        await ValidateQuestionAsync(input, setting, id);

        var usageCount = await GetQuestionUsageCountAsync(id);
        if (usageCount > 0 && setting.EnableQuestionVersioning)
        {
            question.IsLocked = true;
            await questionRepository.UpdateAsync(question, autoSave: true);

            var version = new QuestionBankItem(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                input.AcademicYearId,
                input.StandardId,
                input.SubjectId,
                input.QuestionText,
                input.QuestionType,
                input.DifficultyLevel,
                input.Marks,
                CurrentUser.Id ?? Guid.Empty,
                input.Visibility ?? question.Visibility);
            version.ParentQuestionId = question.Id;
            version.VersionNumber = question.VersionNumber + 1;
            Apply(input, version);
            SyncChildren(version, input);
            await questionRepository.InsertAsync(version, autoSave: true);
            return Map(version, 0);
        }

        Apply(input, question);
        await userScopeAccessService.EnsureCurrentUserCanAccessAsync(question, operation: "question-bank-update");
        await optionRepository.DeleteDirectAsync(x => x.QuestionBankItemId == question.Id);
        await tagRepository.DeleteDirectAsync(x => x.QuestionBankItemId == question.Id);
        question.Options.Clear();
        question.Tags.Clear();
        SyncChildren(question, input);
        await questionRepository.UpdateAsync(question, autoSave: true);
        return Map(question, usageCount);
    }

    public async Task DeleteQuestionAsync(Guid id)
    {
        var question = await GetQuestionEntityAsync(id);
        var usageCount = await GetQuestionUsageCountAsync(id);
        if (usageCount > 0 || question.IsLocked || question.Status is QuestionStatus.Submitted or QuestionStatus.Approved)
        {
            question.IsArchived = true;
            question.Status = QuestionStatus.Archived;
            await questionRepository.UpdateAsync(question, autoSave: true);
            return;
        }

        await questionRepository.DeleteAsync(question, autoSave: true);
    }

    public async Task<QuestionBankItemDto> SubmitQuestionAsync(Guid id)
    {
        var question = await GetQuestionEntityAsync(id);
        if (question.IsArchived || question.IsLocked)
        {
            throw new BusinessException("ShikshaMitra.QuestionBank:QuestionLocked");
        }

        if (question.Status != QuestionStatus.Draft && question.Status != QuestionStatus.Rejected)
        {
            throw new BusinessException("ShikshaMitra.QuestionBank:CannotSubmit");
        }

        var setting = await GetSettingsEntityAsync(question.AcademicYearId);
        question.Status = setting.ApprovalMode == QuestionApprovalMode.NoApproval ? QuestionStatus.Approved : QuestionStatus.Submitted;
        await questionRepository.UpdateAsync(question, autoSave: true);
        return Map(question, await GetQuestionUsageCountAsync(id));
    }

    public Task<QuestionBankItemDto> ApproveQuestionAsync(Guid id, QuestionReviewDto input) => ReviewAsync(id, QuestionStatus.Approved, input.Comment);
    public Task<QuestionBankItemDto> RejectQuestionAsync(Guid id, QuestionReviewDto input) => ReviewAsync(id, QuestionStatus.Rejected, input.Comment);

    public async Task<QuestionBankItemDto> ArchiveQuestionAsync(Guid id)
    {
        var question = await GetQuestionEntityAsync(id);
        question.IsArchived = true;
        question.Status = QuestionStatus.Archived;
        await questionRepository.UpdateAsync(question, autoSave: true);
        return Map(question, await GetQuestionUsageCountAsync(id));
    }

    public async Task<QuestionBankItemDto> DuplicateQuestionAsync(Guid id)
    {
        var question = await GetQuestionEntityAsync(id);
        if (question.IsArchived)
        {
            throw new BusinessException("ShikshaMitra.QuestionBank:ArchivedQuestionCannotDuplicate");
        }

        var copy = new CreateUpdateQuestionDto
        {
            AcademicYearId = question.AcademicYearId,
            BoardId = question.BoardId,
            MediumId = question.MediumId,
            StandardId = question.StandardId,
            SubjectId = question.SubjectId,
            ChapterId = question.ChapterId,
            TopicId = question.TopicId,
            TopicName = question.TopicName,
            QuestionText = $"{question.QuestionText} (Copy)",
            QuestionType = question.QuestionType,
            DifficultyLevel = question.DifficultyLevel,
            BloomLevel = question.BloomLevel,
            Marks = question.Marks,
            NegativeMarks = question.NegativeMarks,
            AnswerKey = question.AnswerKey,
            LearningOutcome = question.LearningOutcome,
            Explanation = question.Explanation,
            MarkingScheme = question.MarkingScheme,
            Visibility = question.Visibility,
            Options = question.Options.Select(o => new QuestionOptionDto { OptionText = o.OptionText, IsCorrect = o.IsCorrect, SortOrder = o.SortOrder }).ToList(),
            Tags = question.Tags.Select(t => t.TagName).ToList()
        };

        return await CreateQuestionAsync(copy);
    }

    public async Task<DuplicateQuestionResultDto> CheckDuplicateAsync(CheckDuplicateQuestionDto input)
    {
        var setting = await GetSettingsEntityAsync(input.AcademicYearId);
        var normalized = QuestionBankItem.Normalize(input.QuestionText);
        var query = await questionRepository.WithDetailsAsync(x => x.Options, x => x.Tags);
        query = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
        query = query.Where(x =>
            x.AcademicYearId == input.AcademicYearId &&
            x.StandardId == input.StandardId &&
            x.SubjectId == input.SubjectId &&
            !x.IsArchived &&
            x.PlainTextSearchContent == normalized);

        if (input.ExcludeQuestionId.HasValue)
        {
            query = query.Where(x => x.Id != input.ExcludeQuestionId.Value);
        }

        var exactMatches = await asyncExecuter.ToListAsync(query.Take(10));

        var similarQuery = await questionRepository.WithDetailsAsync(x => x.Options, x => x.Tags);
        similarQuery = await userScopeAccessService.ApplyCurrentUserScopeAsync(similarQuery);
        var similarCandidates = await asyncExecuter.ToListAsync(similarQuery
            .Where(x =>
                x.AcademicYearId == input.AcademicYearId &&
                x.StandardId == input.StandardId &&
                x.SubjectId == input.SubjectId &&
                !x.IsArchived &&
                (!input.ExcludeQuestionId.HasValue || x.Id != input.ExcludeQuestionId.Value))
            .OrderByDescending(x => x.CreationTime)
            .Take(100));
        var similarMatches = similarCandidates
            .Select(x => new { Question = x, Similarity = CalculateSimilarity(normalized, x.PlainTextSearchContent ?? QuestionBankItem.Normalize(x.QuestionText)) })
            .Where(x => x.Similarity >= 70)
            .OrderByDescending(x => x.Similarity)
            .Take(10)
            .ToList();
        var matches = exactMatches
            .Concat(similarMatches.Select(x => x.Question))
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();
        var highestSimilarity = exactMatches.Count > 0
            ? 100
            : similarMatches.Select(x => x.Similarity).DefaultIfEmpty(0).Max();
        return new DuplicateQuestionResultDto
        {
            HasDuplicate = matches.Count > 0,
            IsBlocked = setting.EnableDuplicateCheck && setting.DuplicateQuestionAction switch
            {
                DuplicateQuestionAction.BlockExactDuplicate => exactMatches.Count > 0,
                DuplicateQuestionAction.BlockSimilarDuplicate => matches.Count > 0,
                _ => false
            },
            Message = matches.Count > 0 ? "A matching or similar question already exists for this class and subject." : null,
            HighestSimilarity = highestSimilarity,
            Matches = matches.Select(x => Map(x, 0)).ToList()
        };
    }

    public async Task<BulkQuestionWorkflowResultDto> BulkApproveAsync(BulkQuestionReviewDto input) => await BulkReviewAsync(input, QuestionStatus.Approved);
    public async Task<BulkQuestionWorkflowResultDto> BulkRejectAsync(BulkQuestionReviewDto input) => await BulkReviewAsync(input, QuestionStatus.Rejected);

    public async Task<QuestionBankDashboardDto> GetDashboardAsync(Guid academicYearId)
    {
        var rows = await questionRepository.GetListAsync(x => x.AcademicYearId == academicYearId && !x.IsArchived);
        rows = (await userScopeAccessService.ApplyCurrentUserScopeAsync(rows.AsQueryable())).ToList();
        var rowIds = rows.Select(r => r.Id).ToList();
        var usageRows = await usageRepository.GetListAsync(x => rowIds.Contains(x.QuestionBankItemId));
        var paperQuestionRows = await paperQuestionRepository.GetListAsync(x => rowIds.Contains(x.QuestionBankItemId));
        var usedQuestionIds = usageRows
            .Select(x => x.QuestionBankItemId)
            .Concat(paperQuestionRows.Select(x => x.QuestionBankItemId))
            .ToHashSet();
        var duplicateIds = GetDuplicateQuestionIds(rows);
        return new QuestionBankDashboardDto
        {
            TotalQuestions = rows.Count,
            ApprovedQuestions = rows.Count(x => x.Status == QuestionStatus.Approved),
            DraftQuestions = rows.Count(x => x.Status == QuestionStatus.Draft),
            PendingReview = rows.Count(x => x.Status == QuestionStatus.Submitted),
            RejectedQuestions = rows.Count(x => x.Status == QuestionStatus.Rejected),
            NeedsImprovement = rows.Count(x => CalculateQualityScore(x) < 70),
            DuplicateRisk = duplicateIds.Count,
            NeverUsed = rows.Count(x => !usedQuestionIds.Contains(x.Id))
        };
    }

    private async Task<QuestionBankItemDto> ReviewAsync(Guid id, QuestionStatus status, string? comment)
    {
        var question = await GetQuestionEntityAsync(id);
        EnsureCanReview(question);
        question.SetReview(status, CurrentUser.Id ?? Guid.Empty, comment);
        await questionRepository.UpdateAsync(question, autoSave: true);
        return Map(question, await GetQuestionUsageCountAsync(id));
    }

    private static ExcelRow CreateExcelRow(params object?[] values)
    {
        return new ExcelRow
        {
            Cells = values.Select(value => new ExcelCell { Value = value ?? string.Empty }).ToList()
        };
    }

    private static string BuildEnumInstruction<TEnum>() where TEnum : struct, Enum
    {
        return string.Join("; ", Enum.GetValues<TEnum>().Select(value => $"{Convert.ToInt32(value)} = {value}"));
    }

    private async Task<BulkQuestionWorkflowResultDto> BulkReviewAsync(BulkQuestionReviewDto input, QuestionStatus status)
    {
        var rows = await questionRepository.GetListAsync(x => input.QuestionIds.Contains(x.Id));
        if (rows.Count != input.QuestionIds.Distinct().Count())
        {
            throw new BusinessException("ShikshaMitra.QuestionBank:NotFound");
        }

        foreach (var row in rows)
        {
            await userScopeAccessService.EnsureCurrentUserCanAccessAsync(row, operation: "question-bank-review");
            EnsureCanReview(row);
            row.SetReview(status, CurrentUser.Id ?? Guid.Empty, input.Comment);
        }

        await questionRepository.UpdateManyAsync(rows, autoSave: true);
        return new BulkQuestionWorkflowResultDto { UpdatedCount = rows.Count };
    }

    private async Task<QuestionBankSetting> GetSettingsEntityAsync(Guid academicYearId)
    {
        return await settingRepository.FirstOrDefaultAsync(x => x.AcademicYearId == academicYearId)
            ?? new QuestionBankSetting(GuidGenerator.Create(), CurrentTenant.Id, academicYearId);
    }

    private async Task<QuestionBankItem> GetQuestionEntityAsync(Guid id)
    {
        var query = await questionRepository.WithDetailsAsync(x => x.Options, x => x.Tags);
        query = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
        return await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
            ?? throw new BusinessException("ShikshaMitra.QuestionBank:NotFound").WithData("Id", id);
    }

    private async Task ValidateQuestionAsync(CreateUpdateQuestionDto input, QuestionBankSetting setting, Guid? excludeId)
    {
        if (input.AcademicYearId == Guid.Empty) throw new BusinessException("ShikshaMitra.QuestionBank:AcademicYearRequired");
        if (input.StandardId == Guid.Empty) throw new BusinessException("ShikshaMitra.QuestionBank:StandardRequired");
        if (input.SubjectId == Guid.Empty) throw new BusinessException("ShikshaMitra.QuestionBank:SubjectRequired");
        if (string.IsNullOrWhiteSpace(input.QuestionText)) throw new BusinessException("ShikshaMitra.QuestionBank:QuestionTextRequired");
        if (!Enum.IsDefined(input.QuestionType)) throw new BusinessException("ShikshaMitra.QuestionBank:InvalidQuestionType");
        if (!Enum.IsDefined(input.DifficultyLevel)) throw new BusinessException("ShikshaMitra.QuestionBank:InvalidDifficulty");
        if (setting.RequireChapter && !input.ChapterId.HasValue) throw new BusinessException("ShikshaMitra.QuestionBank:ChapterRequired");
        if (setting.RequireTopic && string.IsNullOrWhiteSpace(input.TopicName)) throw new BusinessException("ShikshaMitra.QuestionBank:TopicRequired");
        if (setting.RequireBloomLevel && !input.BloomLevel.HasValue) throw new BusinessException("ShikshaMitra.QuestionBank:BloomRequired");
        if (setting.RequireAnswerKey && string.IsNullOrWhiteSpace(input.AnswerKey)) throw new BusinessException("ShikshaMitra.QuestionBank:AnswerKeyRequired");
        if (setting.RequireLearningOutcome && string.IsNullOrWhiteSpace(input.LearningOutcome)) throw new BusinessException("ShikshaMitra.QuestionBank:LearningOutcomeRequired");
        if (input.Marks <= 0) throw new BusinessException("ShikshaMitra.QuestionBank:InvalidMarks");
        if (!setting.AllowDecimalMarks && input.Marks != Math.Truncate(input.Marks)) throw new BusinessException("ShikshaMitra.QuestionBank:DecimalMarksNotAllowed");
        if (input.NegativeMarks.HasValue && (!setting.EnableNegativeMarking || input.NegativeMarks.Value > input.Marks)) throw new BusinessException("ShikshaMitra.QuestionBank:InvalidNegativeMarks");

        if ((input.QuestionType == QuestionType.McqSingleCorrect && !setting.EnableMcq) ||
            (input.QuestionType == QuestionType.McqMultipleCorrect && !setting.EnableMcq) ||
            (input.QuestionType == QuestionType.TrueFalse && !setting.EnableTrueFalse) ||
            (input.QuestionType == QuestionType.FillInTheBlank && !setting.EnableFillInTheBlank) ||
            ((input.QuestionType is QuestionType.ShortAnswer or QuestionType.LongAnswer) && !setting.EnableSubjective) ||
            (input.QuestionType == QuestionType.MatchTheFollowing && !setting.EnableMatchTheFollowing) ||
            (input.QuestionType == QuestionType.AssertionReason && !setting.EnableAssertionReason) ||
            (input.QuestionType == QuestionType.CaseStudy && !setting.EnableCaseStudy) ||
            (input.QuestionType == QuestionType.DiagramBased && !setting.EnableDiagramBased))
        {
            throw new BusinessException("ShikshaMitra.QuestionBank:QuestionTypeDisabled");
        }

        if (input.QuestionType is QuestionType.McqSingleCorrect or QuestionType.McqMultipleCorrect)
        {
            if (input.Options.Count < 2) throw new BusinessException("ShikshaMitra.QuestionBank:McqOptionsRequired");
            if (input.Options.Any(x => string.IsNullOrWhiteSpace(x.OptionText))) throw new BusinessException("ShikshaMitra.QuestionBank:OptionTextRequired");
            var correctCount = input.Options.Count(x => x.IsCorrect);
            if (input.QuestionType == QuestionType.McqSingleCorrect && correctCount != 1) throw new BusinessException("ShikshaMitra.QuestionBank:SingleCorrectRequired");
            if (input.QuestionType == QuestionType.McqMultipleCorrect && correctCount < 1) throw new BusinessException("ShikshaMitra.QuestionBank:CorrectOptionRequired");
        }

        if (setting.EnableDuplicateCheck)
        {
            var result = await CheckDuplicateAsync(new CheckDuplicateQuestionDto
            {
                AcademicYearId = input.AcademicYearId,
                StandardId = input.StandardId,
                SubjectId = input.SubjectId,
                QuestionText = input.QuestionText,
                ExcludeQuestionId = excludeId
            });

            if (result.IsBlocked)
            {
                throw new BusinessException("ShikshaMitra.QuestionBank:DuplicateQuestion");
            }
        }
    }

    private void EnsureCanReview(QuestionBankItem question)
    {
        if (question.Status != QuestionStatus.Submitted)
        {
            throw new BusinessException("ShikshaMitra.QuestionBank:OnlySubmittedCanReview");
        }

    }

    private static IQueryable<QuestionBankItem> ApplySearch(IQueryable<QuestionBankItem> query, QuestionSearchInput input)
    {
        if (input.AcademicYearId.HasValue) query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
        if (input.StandardId.HasValue) query = query.Where(x => x.StandardId == input.StandardId);
        if (input.SubjectId.HasValue) query = query.Where(x => x.SubjectId == input.SubjectId);
        if (input.ChapterId.HasValue) query = query.Where(x => x.ChapterId == input.ChapterId);
        if (input.TopicId.HasValue) query = query.Where(x => x.TopicId == input.TopicId);
        if (!string.IsNullOrWhiteSpace(input.TopicName))
        {
            var topicName = input.TopicName.Trim().ToLower();
            query = query.Where(x => x.TopicName != null && x.TopicName.ToLower().Contains(topicName));
        }
        if (input.QuestionType.HasValue) query = query.Where(x => x.QuestionType == input.QuestionType);
        if (input.DifficultyLevel.HasValue) query = query.Where(x => x.DifficultyLevel == input.DifficultyLevel);
        if (input.BloomLevel.HasValue) query = query.Where(x => x.BloomLevel == input.BloomLevel);
        if (input.Marks.HasValue) query = query.Where(x => x.Marks == input.Marks);
        if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status);
        if (input.Visibility.HasValue) query = query.Where(x => x.Visibility == input.Visibility);
        if (input.CreatedByTeacherId.HasValue) query = query.Where(x => x.CreatedByTeacherId == input.CreatedByTeacherId);
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim().ToLower();
            query = query.Where(x => x.QuestionText.ToLower().Contains(keyword) || (x.PlainTextSearchContent != null && x.PlainTextSearchContent.Contains(keyword)));
        }

        return query;
    }

    private async Task<Dictionary<Guid, int>> GetUsageCountsAsync(List<Guid> questionIds)
    {
        if (questionIds.Count == 0) return new Dictionary<Guid, int>();
        var rows = await usageRepository.GetListAsync(x => questionIds.Contains(x.QuestionBankItemId));
        var paperQuestionRows = await paperQuestionRepository.GetListAsync(x => questionIds.Contains(x.QuestionBankItemId));
        return rows.Select(x => new { x.QuestionBankItemId, Source = $"{x.Source}:{x.SourceEntityId}" })
            .Concat(paperQuestionRows.Select(x => new { x.QuestionBankItemId, Source = $"QuestionPaper:{x.QuestionPaperId}" }))
            .GroupBy(x => x.QuestionBankItemId)
            .ToDictionary(x => x.Key, x => x.Select(row => row.Source).Distinct().Count());
    }

    private async Task<int> GetQuestionUsageCountAsync(Guid questionId)
    {
        var rows = await usageRepository.GetListAsync(x => x.QuestionBankItemId == questionId);
        var paperQuestionRows = await paperQuestionRepository.GetListAsync(x => x.QuestionBankItemId == questionId);
        return rows.Select(x => $"{x.Source}:{x.SourceEntityId}")
            .Concat(paperQuestionRows.Select(x => $"QuestionPaper:{x.QuestionPaperId}"))
            .Distinct()
            .Count();
    }

    private static HashSet<Guid> GetDuplicateQuestionIds(IEnumerable<QuestionBankItem> questions)
    {
        return questions
            .Where(x => !string.IsNullOrWhiteSpace(x.PlainTextSearchContent))
            .GroupBy(x => x.PlainTextSearchContent)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(question => question.Id))
            .ToHashSet();
    }

    private static int CalculateQualityScore(QuestionBankItem question)
    {
        var score = 0;
        if (question.StandardId != Guid.Empty && question.SubjectId != Guid.Empty) score += 15;
        if (question.ChapterId.HasValue) score += 10;
        if (!string.IsNullOrWhiteSpace(question.QuestionText) && question.QuestionText.Trim().Length >= 10) score += 15;
        if (question.Marks > 0) score += 10;
        if (question.BloomLevel.HasValue) score += 10;
        if (!string.IsNullOrWhiteSpace(question.AnswerKey)) score += 10;
        if (!string.IsNullOrWhiteSpace(question.Explanation) || !string.IsNullOrWhiteSpace(question.MarkingScheme)) score += 10;
        if (!string.IsNullOrWhiteSpace(question.LearningOutcome)) score += 10;
        if (question.Tags.Count > 0) score += 5;

        if (question.QuestionType is QuestionType.McqSingleCorrect or QuestionType.McqMultipleCorrect or QuestionType.TrueFalse)
        {
            var correctCount = question.Options.Count(x => x.IsCorrect);
            var hasEnoughOptions = question.Options.Count >= 2;
            var hasValidCorrectAnswer = question.QuestionType == QuestionType.McqSingleCorrect
                ? correctCount == 1
                : correctCount >= 1;
            if (hasEnoughOptions && hasValidCorrectAnswer) score += 5;
        }
        else
        {
            score += 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static string GetQualityBand(int score)
    {
        if (score >= 85) return "Excellent";
        if (score >= 70) return "Ready";
        if (score >= 50) return "Needs Review";
        return "Needs Improvement";
    }

    private static int CalculateSimilarity(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return 0;
        if (left == right) return 100;

        var leftWords = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightWords = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (leftWords.Count == 0 || rightWords.Count == 0) return 0;

        var intersection = leftWords.Intersect(rightWords, StringComparer.OrdinalIgnoreCase).Count();
        var union = leftWords.Union(rightWords, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (int)Math.Round(intersection / (double)union * 100);
    }

    private static string? NormalizeTopicName(string? topicName)
    {
        var normalized = topicName?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private void SyncChildren(QuestionBankItem question, CreateUpdateQuestionDto input)
    {
        foreach (var option in input.Options.OrderBy(x => x.SortOrder))
        {
            question.Options.Add(new QuestionOption(GuidGenerator.Create(), question.Id, option.OptionText, option.IsCorrect, option.SortOrder));
        }

        foreach (var tag in input.Tags.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            question.Tags.Add(new QuestionTag(GuidGenerator.Create(), question.Id, tag));
        }
    }

    private static void Apply(CreateUpdateQuestionDto input, QuestionBankItem question)
    {
        question.AcademicYearId = input.AcademicYearId;
        question.BoardId = input.BoardId;
        question.MediumId = input.MediumId;
        question.StandardId = input.StandardId;
        question.SubjectId = input.SubjectId;
        question.ChapterId = input.ChapterId;
        question.TopicId = input.TopicId;
        question.TopicName = NormalizeTopicName(input.TopicName);
        question.QuestionText = input.QuestionText;
        question.PlainTextSearchContent = QuestionBankItem.Normalize(input.QuestionText);
        question.QuestionType = input.QuestionType;
        question.DifficultyLevel = input.DifficultyLevel;
        question.BloomLevel = input.BloomLevel;
        question.Marks = input.Marks;
        question.NegativeMarks = input.NegativeMarks;
        question.AnswerKey = input.AnswerKey;
        question.LearningOutcome = input.LearningOutcome;
        question.Explanation = input.Explanation;
        question.MarkingScheme = input.MarkingScheme;
        if (input.Visibility.HasValue) question.Visibility = input.Visibility.Value;
    }

    private async Task<QuestionLookup> BuildQuestionLookupAsync(IReadOnlyCollection<QuestionBankItem> questions)
    {
        if (questions.Count == 0) return new QuestionLookup();

        var standardIds = questions.Select(x => x.StandardId).Distinct().ToList();
        var subjectIds = questions.Select(x => x.SubjectId).Distinct().ToList();
        var chapterIds = questions.Where(x => x.ChapterId.HasValue).Select(x => x.ChapterId!.Value).Distinct().ToList();

        var standards = await standardRepository.GetListAsync(x => standardIds.Contains(x.Id));
        var subjects = await subjectRepository.GetListAsync(x => subjectIds.Contains(x.Id));
        var chapters = chapterIds.Count > 0
            ? await chapterRepository.GetListAsync(x => chapterIds.Contains(x.Id))
            : new List<AssignedSubjectChapter>();

        return new QuestionLookup(
            standards.ToDictionary(x => x.Id, x => x.Name),
            subjects.ToDictionary(x => x.Id, x => x.DisplayName ?? x.Name),
            chapters.ToDictionary(x => x.Id, x => x));
    }

    private static QuestionBankItemDto Map(
        QuestionBankItem question,
        int usedCount,
        bool duplicateRisk = false,
        QuestionLookup? lookup = null)
    {
        var qualityScore = CalculateQualityScore(question);
        lookup ??= new QuestionLookup();
        lookup.Chapters.TryGetValue(question.ChapterId ?? Guid.Empty, out var chapter);

        return new QuestionBankItemDto
        {
            Id = question.Id,
            AcademicYearId = question.AcademicYearId,
            BoardId = question.BoardId,
            MediumId = question.MediumId,
            StandardId = question.StandardId,
            StandardName = lookup.StandardNames.GetValueOrDefault(question.StandardId),
            SubjectId = question.SubjectId,
            SubjectName = lookup.SubjectNames.GetValueOrDefault(question.SubjectId),
            ChapterId = question.ChapterId,
            ChapterName = chapter?.ChapterName,
            ChapterNumber = chapter?.ChapterNumber,
            TopicId = question.TopicId,
            TopicName = question.TopicName,
            QuestionText = question.QuestionText,
            QuestionType = question.QuestionType,
            DifficultyLevel = question.DifficultyLevel,
            BloomLevel = question.BloomLevel,
            Marks = question.Marks,
            NegativeMarks = question.NegativeMarks,
            AnswerKey = question.AnswerKey,
            LearningOutcome = question.LearningOutcome,
            Explanation = question.Explanation,
            MarkingScheme = question.MarkingScheme,
            Status = question.Status,
            Visibility = question.Visibility,
            CreatedByTeacherId = question.CreatedByTeacherId,
            ReviewedByUserId = question.ReviewedByUserId,
            ReviewedAt = question.ReviewedAt,
            ReviewComment = question.ReviewComment,
            VersionNumber = question.VersionNumber,
            ParentQuestionId = question.ParentQuestionId,
            IsLocked = question.IsLocked,
            IsArchived = question.IsArchived,
            UsedCount = usedCount,
            QualityScore = qualityScore,
            DuplicateRisk = duplicateRisk,
            QualityBand = GetQualityBand(qualityScore),
            Options = question.Options.OrderBy(x => x.SortOrder).Select(x => new QuestionOptionDto { Id = x.Id, OptionText = x.OptionText, IsCorrect = x.IsCorrect, SortOrder = x.SortOrder }).ToList(),
            Tags = question.Tags.Select(x => x.TagName).ToList()
        };
    }

    private sealed record QuestionLookup(
        Dictionary<Guid, string> StandardNames,
        Dictionary<Guid, string> SubjectNames,
        Dictionary<Guid, AssignedSubjectChapter> Chapters)
    {
        public QuestionLookup() : this(new Dictionary<Guid, string>(), new Dictionary<Guid, string>(), new Dictionary<Guid, AssignedSubjectChapter>())
        {
        }
    }

    private static QuestionBankSettingDto Map(QuestionBankSetting setting) => new()
    {
        Id = setting.Id,
        AcademicYearId = setting.AcademicYearId,
        ApprovalMode = setting.ApprovalMode,
        DefaultVisibility = setting.DefaultVisibility,
        EnableMcq = setting.EnableMcq,
        EnableTrueFalse = setting.EnableTrueFalse,
        EnableFillInTheBlank = setting.EnableFillInTheBlank,
        EnableSubjective = setting.EnableSubjective,
        EnableMatchTheFollowing = setting.EnableMatchTheFollowing,
        EnableAssertionReason = setting.EnableAssertionReason,
        EnableCaseStudy = setting.EnableCaseStudy,
        EnableDiagramBased = setting.EnableDiagramBased,
        RequireChapter = setting.RequireChapter,
        RequireTopic = setting.RequireTopic,
        RequireDifficulty = setting.RequireDifficulty,
        RequireBloomLevel = setting.RequireBloomLevel,
        RequireAnswerKey = setting.RequireAnswerKey,
        RequireLearningOutcome = setting.RequireLearningOutcome,
        AllowDecimalMarks = setting.AllowDecimalMarks,
        EnableNegativeMarking = setting.EnableNegativeMarking,
        EnableDuplicateCheck = setting.EnableDuplicateCheck,
        DuplicateQuestionAction = setting.DuplicateQuestionAction,
        EnableQuestionVersioning = setting.EnableQuestionVersioning,
        EnableBulkImport = setting.EnableBulkImport,
        EnableRichTextEditor = setting.EnableRichTextEditor,
        EnableLatexEquation = setting.EnableLatexEquation,
        EnableImageAttachment = setting.EnableImageAttachment,
        MaxAttachmentSizeMb = setting.MaxAttachmentSizeMb,
        EnableAiQuestionGenerator = setting.EnableAiQuestionGenerator,
        EnableAiBloomClassifier = setting.EnableAiBloomClassifier,
        EnableAiDuplicateDetector = setting.EnableAiDuplicateDetector
    };

    private static void Apply(QuestionBankSettingDto input, QuestionBankSetting setting)
    {
        setting.AcademicYearId = input.AcademicYearId;
        setting.ApprovalMode = input.ApprovalMode;
        setting.DefaultVisibility = input.DefaultVisibility;
        setting.EnableMcq = input.EnableMcq;
        setting.EnableTrueFalse = input.EnableTrueFalse;
        setting.EnableFillInTheBlank = input.EnableFillInTheBlank;
        setting.EnableSubjective = input.EnableSubjective;
        setting.EnableMatchTheFollowing = input.EnableMatchTheFollowing;
        setting.EnableAssertionReason = input.EnableAssertionReason;
        setting.EnableCaseStudy = input.EnableCaseStudy;
        setting.EnableDiagramBased = input.EnableDiagramBased;
        setting.RequireChapter = input.RequireChapter;
        setting.RequireTopic = input.RequireTopic;
        setting.RequireDifficulty = input.RequireDifficulty;
        setting.RequireBloomLevel = input.RequireBloomLevel;
        setting.RequireAnswerKey = input.RequireAnswerKey;
        setting.RequireLearningOutcome = input.RequireLearningOutcome;
        setting.AllowDecimalMarks = input.AllowDecimalMarks;
        setting.EnableNegativeMarking = input.EnableNegativeMarking;
        setting.EnableDuplicateCheck = input.EnableDuplicateCheck;
        setting.DuplicateQuestionAction = input.DuplicateQuestionAction;
        setting.EnableQuestionVersioning = input.EnableQuestionVersioning;
        setting.EnableBulkImport = input.EnableBulkImport;
        setting.EnableRichTextEditor = input.EnableRichTextEditor;
        setting.EnableLatexEquation = input.EnableLatexEquation;
        setting.EnableImageAttachment = input.EnableImageAttachment;
        setting.MaxAttachmentSizeMb = input.MaxAttachmentSizeMb;
        setting.EnableAiQuestionGenerator = input.EnableAiQuestionGenerator;
        setting.EnableAiBloomClassifier = input.EnableAiBloomClassifier;
        setting.EnableAiDuplicateDetector = input.EnableAiDuplicateDetector;
    }
}
