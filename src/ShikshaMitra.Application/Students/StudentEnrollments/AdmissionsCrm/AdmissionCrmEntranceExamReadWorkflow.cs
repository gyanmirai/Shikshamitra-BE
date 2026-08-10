using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamReadWorkflow(
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	AdmissionCrmEntranceExamAccess access) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionCrmEntranceExamDto>> GetListAsync(
		Guid academicYearId,
		GetAdmissionCrmEntranceExamListInput input)
	{
		await access.EnsureYearAsync(academicYearId, "GetListAsync");
		var exams = await examRepository.GetQueryableAsync();
		var leads = await leadRepository.GetQueryableAsync();
		leads = leads.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		leads = await access.ApplyLeadScopeAsync(leads);
		var scopedLeadIds = leads.Select(x => x.Id);
		var query = exams.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && scopedLeadIds.Contains(x.LeadId));

		if (!string.IsNullOrWhiteSpace(input.FilterText))
		{
			var filter = input.FilterText.Trim().ToLowerInvariant();
			var number = AdmissionCrmEntranceExamMapper.ParseApplicantNumber(input.FilterText);
			var matching = leads.Where(x =>
				(number.HasValue && x.ApplicantNumber == number.Value) ||
				(x.FirstName != null && x.FirstName.ToLower().Contains(filter)) ||
				(x.MiddleName != null && x.MiddleName.ToLower().Contains(filter)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(filter)) ||
				(x.ParentGuardianName != null && x.ParentGuardianName.ToLower().Contains(filter)) ||
				(x.PrimaryContactNo != null && x.PrimaryContactNo.Contains(filter)) ||
				(x.Email != null && x.Email.ToLower().Contains(filter))).Select(x => x.Id);
			query = query.Where(x => matching.Contains(x.LeadId) ||
				(x.SlotNameSnapshot != null && x.SlotNameSnapshot.ToLower().Contains(filter)));
		}

		if (!string.IsNullOrWhiteSpace(input.ApplicantSearch))
		{
			var filter = input.ApplicantSearch.Trim().ToLowerInvariant();
			var number = AdmissionCrmEntranceExamMapper.ParseApplicantNumber(input.ApplicantSearch);
			var matching = leads.Where(x =>
				(number.HasValue && x.ApplicantNumber == number.Value) ||
				(x.FirstName != null && x.FirstName.ToLower().Contains(filter)) ||
				(x.MiddleName != null && x.MiddleName.ToLower().Contains(filter)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(filter)) ||
				(x.ParentGuardianName != null && x.ParentGuardianName.ToLower().Contains(filter))).Select(x => x.Id);
			query = query.Where(x => matching.Contains(x.LeadId));
		}

		if (input.ExamStartFrom.HasValue) query = query.Where(x => x.ExamStartTime >= input.ExamStartFrom.Value);
		if (input.ExamStartUntil.HasValue) query = query.Where(x => x.ExamStartTime < input.ExamStartUntil.Value.AddDays(1));
		if (input.AttendanceStatus.HasValue) query = query.Where(x => x.AttendanceStatus == input.AttendanceStatus.Value);
		if (input.ExamStatus.HasValue) query = query.Where(x => x.ExamStatus == input.ExamStatus.Value);
		if (input.EligibilityDecision.HasValue) query = query.Where(x => x.EligibilityDecision == input.EligibilityDecision.Value);

		var total = await AsyncExecuter.CountAsync(query);
		var page = await GetPageAsync(query, leads, input);
		var leadIds = page.Select(x => x.LeadId).Distinct().ToList();
		var leadPage = await AsyncExecuter.ToListAsync(leads.Where(x => leadIds.Contains(x.Id)));
		var byId = leadPage.ToDictionary(x => x.Id);
		return new PagedResultDto<AdmissionCrmEntranceExamDto>(total,
			page.Select(x => AdmissionCrmEntranceExamMapper.ToDto(x, byId.GetValueOrDefault(x.LeadId))).ToList());
	}

	public async Task<AdmissionCrmEntranceExamDto> GetByLeadAsync(Guid leadId)
	{
		var lead = await access.GetLeadAsync(leadId, "GetByLeadAsync");
		var exam = await access.FindExamByLeadAsync(lead.Id) ??
			throw new BusinessException("AdmissionCrm:ExamNotScheduled").WithData("LeadId", leadId);
		return AdmissionCrmEntranceExamMapper.ToDto(exam, lead);
	}

	public async Task<AdmissionCrmEligibilityHandoffDto> GetEligibilityHandoffAsync(Guid examRecordId)
	{
		var exam = await access.GetExamAsync(examRecordId, "GetEligibilityHandoffAsync");
		var lead = await access.GetLeadAsync(exam.LeadId, "GetEligibilityHandoffAsync");
		return AdmissionCrmEntranceExamMapper.ToHandoffDto(lead, exam);
	}

	private async Task<List<AdmissionCrmEntranceExamRecord>> GetPageAsync(
		IQueryable<AdmissionCrmEntranceExamRecord> query,
		IQueryable<AdmissionInquiryLead> leads,
		GetAdmissionCrmEntranceExamListInput input)
	{
		try
		{
			return await AsyncExecuter.ToListAsync(
				ApplySorting(query, leads, input.Sorting).Skip(input.SkipCount).Take(input.MaxResultCount));
		}
		catch (NotSupportedException ex) when (IsDateTimeOffsetOrderingNotSupported(ex) && UsesExamStartTimeSorting(input.Sorting))
		{
			var all = await AsyncExecuter.ToListAsync(query);
			return ApplySortingInMemory(all, input.Sorting).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
		}
	}

	private static IOrderedQueryable<AdmissionCrmEntranceExamRecord> ApplySorting(
		IQueryable<AdmissionCrmEntranceExamRecord> query,
		IQueryable<AdmissionInquiryLead> leads,
		string? sorting)
	{
		var desc = sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var field = sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
		return field switch
		{
			"applicantnumber" => desc
				? query.OrderByDescending(exam => leads.Where(lead => lead.Id == exam.LeadId).Select(lead => lead.ApplicantNumber).FirstOrDefault())
				: query.OrderBy(exam => leads.Where(lead => lead.Id == exam.LeadId).Select(lead => lead.ApplicantNumber).FirstOrDefault()),
			"applicantname" => desc
				? query.OrderByDescending(exam => leads.Where(lead => lead.Id == exam.LeadId).Select(lead => lead.FirstName).FirstOrDefault())
					.ThenByDescending(exam => leads.Where(lead => lead.Id == exam.LeadId).Select(lead => lead.LastName).FirstOrDefault())
				: query.OrderBy(exam => leads.Where(lead => lead.Id == exam.LeadId).Select(lead => lead.FirstName).FirstOrDefault())
					.ThenBy(exam => leads.Where(lead => lead.Id == exam.LeadId).Select(lead => lead.LastName).FirstOrDefault()),
			"examstarttime" => desc ? query.OrderByDescending(x => x.ExamStartTime) : query.OrderBy(x => x.ExamStartTime),
			"attendancestatus" => desc ? query.OrderByDescending(x => x.AttendanceStatus) : query.OrderBy(x => x.AttendanceStatus),
			"examstatus" => desc ? query.OrderByDescending(x => x.ExamStatus) : query.OrderBy(x => x.ExamStatus),
			"obtainedmarks" => desc ? query.OrderByDescending(x => x.ObtainedMarks) : query.OrderBy(x => x.ObtainedMarks),
			"eligibilitydecision" => desc ? query.OrderByDescending(x => x.EligibilityDecision) : query.OrderBy(x => x.EligibilityDecision),
			_ => query.OrderBy(x => x.ExamStartTime).ThenBy(x => x.LeadId)
		};
	}

	private static bool UsesExamStartTimeSorting(string? sorting)
	{
		var field = sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
		return string.IsNullOrWhiteSpace(field) || field == "examstarttime";
	}

	private static bool IsDateTimeOffsetOrderingNotSupported(NotSupportedException ex) =>
		ex.Message.Contains("DateTimeOffset", StringComparison.OrdinalIgnoreCase) &&
		ex.Message.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase);

	private static IOrderedEnumerable<AdmissionCrmEntranceExamRecord> ApplySortingInMemory(
		IEnumerable<AdmissionCrmEntranceExamRecord> exams,
		string? sorting)
	{
		var desc = sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var field = sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
		return field == "examstarttime" && desc
			? exams.OrderByDescending(x => x.ExamStartTime).ThenBy(x => x.LeadId)
			: exams.OrderBy(x => x.ExamStartTime).ThenBy(x => x.LeadId);
	}
}
