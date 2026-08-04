using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public interface ISubstitutionAppService : IApplicationService
{
    Task<PagedResultDto<SubstitutionDto>> GetListAsync(GetSubstitutionsInput input);
    Task<SubstitutionDashboardDto> GetDashboardAsync(DateTimeOffset date, Guid? academicYearId = null, Guid? academicMediumId = null);
    Task<SubstitutionDto> GetAsync(Guid id);
    Task<SubstitutionDto> CreateAsync(CreateSubstitutionDto input);
    Task<SubstitutionDto> UpdateAsync(Guid id, UpdateSubstitutionDto input);
    Task<SubstitutionDto> AssignAsync(Guid id, AssignSubstituteDto input);
    Task<SubstitutionDto> SendReminderAsync(Guid id);
    Task<SubstitutionDto> CompleteAsync(Guid id);
    Task<SubstitutionDto> CancelAsync(Guid id, SubstitutionActionDto input);
    Task<SubstitutionDto> RejectAsync(Guid id, SubstitutionActionDto input);
    Task<SubstitutionDto> AcknowledgeAsync(Guid id, SubstitutionActionDto? input = null);
    Task<SubstitutionDto> RequestChangeAsync(Guid id, SubstitutionActionDto input);
    Task<SubstitutionDto> RejectDutyAsync(Guid id, SubstitutionActionDto input);
    Task<SubstitutionCreateFormMetadataDto> GetCreateFormMetadataAsync(SubstitutionCreateFormMetadataInput input);
    Task<List<SubstitutionLectureLookupDto>> GetLecturesAsync(SubstitutionLectureLookupInput input);
    Task<List<SuggestedTeacherDto>> GetSuggestedTeachersAsync(SuggestedTeacherInput input);
    Task<List<SuggestedTeacherDto>> GetSuggestedTeachersAsync(Guid id);
    Task<List<SubstitutionAuditLogDto>> GetAuditLogsAsync(Guid id);
    Task<PagedResultDto<SubstitutionDto>> GetMyDutiesAsync(GetSubstitutionsInput input);
    Task<SubstitutionDailySheetDto> GetDailySheetAsync(SubstitutionDailySheetInput input);
    Task<byte[]> GetDailySheetPdfAsync(SubstitutionDailySheetInput input);
    Task<byte[]> GetDailySheetExcelAsync(SubstitutionDailySheetInput input);
    Task<SubstitutionAnalyticsDto> GetAnalyticsAsync(SubstitutionAnalyticsInput input);
    Task<SubstitutionSettingsDto> GetSettingsAsync(Guid? academicYearId = null);
    Task<SubstitutionSettingsDto> UpdateSettingsAsync(SubstitutionSettingsDto input);
}
