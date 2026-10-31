using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	[Authorize(AssignPermission)]
	public async Task<List<AssignmentTransportSourceDto>> GetTransportSourcesAsync(Guid studentId, Guid academicYearId)
	{
		await RequireEnrollmentAsync(studentId, academicYearId, true);
		var year = await academicYearRepository.GetAsync(academicYearId);
		var members = (await transportMemberRepository.GetListAsync(x => x.StudentId == studentId)).Select(x => x.Id).ToList();
		var routes = await transportRouteRepository.GetListAsync(x => members.Contains(x.MemberId) && x.EffectiveFrom <= year.EndDate && (!x.EffectiveTo.HasValue || x.EffectiveTo >= year.StartDate));
		return routes.OrderBy(x => x.EffectiveFrom).Select(x => new AssignmentTransportSourceDto {
			Id = x.Id, Label = $"Transport service from {x.EffectiveFrom:dd MMM yyyy}",
			StartDate = x.EffectiveFrom.Date < year.StartDate.Date ? year.StartDate.Date : x.EffectiveFrom.Date,
			EndDate = x.EffectiveTo?.Date < year.EndDate.Date ? x.EffectiveTo.Value.Date : year.EndDate.Date
		}).ToList();
	}
	[Authorize(ViewPermission)]
	public async Task<PagedResultDto<AssignmentStudentDto>> GetStudentsAsync(AssignmentStudentListInput input)
	{
		if (input.MaxResultCount is < 1 or > 100 || input.SkipCount < 0 || input.Search?.Length > 128)
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		var enrollments = await ScopedEnrollmentsAsync(input.AcademicYearId);
		if (input.StandardId.HasValue) enrollments = enrollments.Where(e => e.StandardId == input.StandardId);
		if (input.DivisionId.HasValue) enrollments = enrollments.Where(e => e.DivisionId == input.DivisionId);
		var standards = await standardRepository.GetQueryableAsync();
		if (input.BoardId.HasValue) enrollments = enrollments.Where(e => standards.Any(s => s.Id == e.StandardId && s.EducationMediumStage.EducationMedium.EducationBoardId == input.BoardId));
		if (input.EducationMediumId.HasValue) enrollments = enrollments.Where(e => standards.Any(s => s.Id == e.StandardId && s.EducationMediumStage.EducationMediumId == input.EducationMediumId));
		if (input.FeeHeadId.HasValue)
		{
			var dueQuery = await dueFeeRepository.GetQueryableAsync();
			var headQuery = await feeHeadRepository.GetQueryableAsync();
			var assignmentQuery = await studentFeeStructureRepository.GetQueryableAsync();
			var detailQuery = await feeStructureDetailRepository.GetQueryableAsync();
			var relevantAssignments = await AsyncExecuter.ToListAsync(assignmentQuery.Where(a => a.AcademicYearId == input.AcademicYearId &&
				detailQuery.Any(d => d.FeeStructureId == a.FeeStructureStandardId && d.FeeHeadId == input.FeeHeadId))
				.Select(a => new { a.StudentId, a.AssignmentSnapshotJson }));
			var selectedStudentIds = relevantAssignments.Where(a => {
				if (a.AssignmentSnapshotJson == null) return true;
				try { return JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(a.AssignmentSnapshotJson)?.Heads?.Any(h => h.FeeHeadId == input.FeeHeadId && h.Included) ?? true; }
				catch (JsonException) { return true; }
			}).Select(a => a.StudentId).Distinct().ToArray();
			enrollments = enrollments.Where(e => headQuery.Any(h => h.Id == input.FeeHeadId && h.AcademicYearId == input.AcademicYearId) &&
				(Enumerable.Contains(selectedStudentIds, e.StudentId) || dueQuery.Any(d => d.StudentId == e.StudentId && d.FeeHeadId == input.FeeHeadId &&
					(!d.StudentFeeStructureId.HasValue || assignmentQuery.Any(a => a.Id == d.StudentFeeStructureId && a.AcademicYearId == input.AcademicYearId)))));
		}
		var students = await studentRepository.GetQueryableAsync();
		var divisions = await divisionRepository.GetQueryableAsync();
		var assignments = await studentFeeStructureRepository.GetQueryableAsync();
		var rows = from e in enrollments join s in students on e.StudentId equals s.Id
			join standard in standards on e.StandardId equals standard.Id into standardGroup
			from standard in standardGroup.DefaultIfEmpty()
			join division in divisions on e.DivisionId equals division.Id into divisionGroup
			from division in divisionGroup.DefaultIfEmpty()
			select new AssignmentStudentDto { EnrollmentId = e.Id, StudentId = s.Id, StandardId = e.StandardId, DivisionId = e.DivisionId,
				StudentName = ((s.FirstName ?? "") + (s.MiddleName == null || s.MiddleName == "" ? "" : " " + s.MiddleName) + " " + (s.LastName ?? "")).Trim(),
				GrNumber = s.GrNumber ?? (s.GrNo.HasValue ? s.GrNo!.Value.ToString() : ""),
				RollNumber = e.RollNumber,
				ClassName = standard == null ? "" : standard.Name,
				DivisionName = division == null ? "" : division.Name,
				StandardDivision = (standard == null ? "" : standard.Name) + (division == null ? "" : " - " + division.Name),
				BoardName = standard == null ? "" : standard.EducationMediumStage.EducationMedium.EducationBoard.Type ==
					ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards.EducationBoardType.CBSE ? "CBSE" : "GSEB",
				MediumName = standard == null ? "" : standard.EducationMediumStage.EducationMedium.Name,
				StageName = standard == null ? "" : standard.EducationMediumStage.Name,
				IsActive = e.IsActive,
				IsFeeStructureAssigned = assignments.Any(a => a.StudentId == s.Id && a.AcademicYearId == input.AcademicYearId) };
		var tableQuery = ShikshaMitra.Finance.Common.FinanceTableQuery.Parse(input.Filters, input.Search, input.SortBy, input.SortOrder, AssignmentColumns);
		if (!string.IsNullOrWhiteSpace(tableQuery.Search))
		{
			var search = tableQuery.Search.ToLowerInvariant();
			rows = rows.Where(x => x.StudentName.ToLower().Contains(search) || x.GrNumber.ToLower().Contains(search) ||
				(x.RollNumber.HasValue && x.RollNumber.Value.ToString().Contains(search)));
		}
		rows = tableQuery.Apply(rows, AssignmentFields);
		var count = await AsyncExecuter.CountAsync(rows);
		var page = await AsyncExecuter.ToListAsync(tableQuery.Order(rows, AssignmentFields, "studentName", false, x => x.EnrollmentId)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		await PopulateAssignmentStatesAsync(page, input.AcademicYearId);
		return new PagedResultDto<AssignmentStudentDto>(count, page);
	}

	[Authorize(AssignPermission)]
	public async Task<List<AssignmentCandidateDto>> GetCandidatesAsync(Guid studentId, Guid academicYearId)
	{
		var enrollment = await RequireEnrollmentAsync(studentId, academicYearId, true);
		var metadataIds = (await feeStructureMetadataRepository.GetListAsync(x => x.AcademicYearId == academicYearId)).Select(x => x.Id).ToList();
		var structures = await feeStructureRepository.GetListAsync(x => x.IsLocked && x.StandardId == enrollment.StandardId && metadataIds.Contains(x.FeeStructureMetadataId));
		var ids = structures.Select(x => x.Id).ToList();
		var details = await feeStructureDetailRepository.GetListAsync(x => ids.Contains(x.FeeStructureId));
		var headIds = details.Select(x => x.FeeHeadId).Distinct().ToList();
		var heads = (await feeHeadRepository.GetListAsync(x => headIds.Contains(x.Id))).ToDictionary(x => x.Id);
		var candidates = structures.OrderBy(x => x.Name).ThenByDescending(x => x.RevisionNumber).Select(s => new AssignmentCandidateDto {
			Id = s.Id, Name = s.Name, RevisionNumber = s.RevisionNumber, AcademicYearId = academicYearId, StandardId = s.StandardId,
			Total = details.Where(d => d.FeeStructureId == s.Id).Sum(d => d.Amount ?? 0m),
			Heads = details.Where(d => d.FeeStructureId == s.Id).Select(d => new AssignmentHeadDto {
				FeeHeadId = d.FeeHeadId, FeeHeadName = heads.GetValueOrDefault(d.FeeHeadId)?.Name ?? "Fee head", SupportsTransportSource = !d.IsVariable && heads.GetValueOrDefault(d.FeeHeadId)?.Module == ShikshaMitra.Finance.Fees.Settings.FeeHeadModule.Transport, IsOptional = d.IsOptional,
				IsVariable = d.IsVariable, IsOneTime = d.IsOneTime, Included = !d.IsOptional, ConfiguredAmount = d.Amount, EffectiveAmount = d.Amount,
				AmountBasis = d.IsVariable ? "CoverageTotal" : d.IsOneTime ? "OneTime" : "AnnualTotal", PaymentTermId = d.PaymentTermId, PaymentTermVersionId = d.PaymentTermVersionId }).ToList()
		}).ToList();
		var versionIds = structures.SelectMany(s => StructurePolicyVersions(s).Values).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
		var versions = await financePolicyVersionRepository.GetListAsync(x => versionIds.Contains(x.Id));
		foreach (var candidate in candidates)
			candidate.Policies = versions.Where(v => StructurePolicyVersions(structures.Single(s => s.Id == candidate.Id)).Values.Contains(v.Id)).Select(ToAssignmentPolicy).ToList();
		return candidates;
	}

	[Authorize(ViewPermission)]
	public async Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId, Guid academicYearId)
	{
		var enrollment = await RequireEnrollmentAsync(studentId, academicYearId, false);
		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == studentId && x.AcademicYearId == academicYearId)
			?? throw new BusinessException(AssignmentErrorCodes.NotAssigned);
		var result = new StudentFeeStructureResponseDto { ConcurrencyStamp = assignment.ConcurrencyStamp, StudentFeeStructureId = assignment.Id, StandardFeeStructureId = assignment.FeeStructureStandardId,
			StudentId = studentId, AcademicYearId = academicYearId, StandardId = enrollment.StandardId ?? Guid.Empty,
			ConcessionPolicyId = assignment.ConcessionPolicyId, ConcessionPolicyVersionId = assignment.ConcessionPolicyVersionId,
			RefundPolicyId = assignment.RefundPolicyId, RefundPolicyVersionId = assignment.RefundPolicyVersionId,
			ProrationPolicyId = assignment.ProrationPolicyId, ProrationPolicyVersionId = assignment.ProrationPolicyVersionId };
		StudentFeeAssignmentPreviewDto? snapshot = null;
		if (assignment.AssignmentSnapshotJson != null)
		{
			try { snapshot = JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(assignment.AssignmentSnapshotJson); }
			catch (JsonException) { result.Warnings.Add("Historical assignment evidence is unavailable. Canonical obligations are shown without recalculation."); }
		}
		var dues = await dueFeeRepository.GetListAsync(d => d.StudentFeeStructureId == assignment.Id && d.StudentId == studentId);
		if (assignment.AssignmentSnapshotJson == null)
		{
			var year = await academicYearRepository.GetAsync(academicYearId);
			var details = await feeStructureDetailRepository.GetListAsync(x => x.FeeStructureId == assignment.FeeStructureStandardId);
			var detailIds = details.Select(d => d.Id).ToList();
			var legacy = await dueFeeRepository.GetListAsync(d => d.StudentId == studentId && d.StudentFeeStructureId == null &&
				((d.FeeStructureDetailId.HasValue && detailIds.Contains(d.FeeStructureDetailId.Value)) ||
				(!d.FeeStructureDetailId.HasValue && d.DueDate >= year.StartDate && d.DueDate <= year.EndDate)));
			dues.AddRange(legacy);
			result.Warnings.Add("Legacy assignment: historical service facts and optional-head selections were not recorded. Existing dues are shown; no history has been repaired or recalculated.");
		}
		if (snapshot == null)
		{
			var structure = await feeStructureRepository.FindAsync(assignment.FeeStructureStandardId);
			var headIds = dues.Select(d => d.FeeHeadId).Distinct().ToList();
			var names = (await feeHeadRepository.GetListAsync(x => headIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
			snapshot = new StudentFeeAssignmentPreviewDto { StudentId = studentId, AcademicYearId = academicYearId,
				StructureId = assignment.FeeStructureStandardId, StructureName = structure?.Name ?? "Historical structure", CalculationVersion = "LegacyUnknown",
				Heads = dues.GroupBy(x => x.FeeHeadId).Select(g => new AssignmentHeadDto { FeeHeadId = g.Key, FeeHeadName = names.GetValueOrDefault(g.Key, "Fee head"),
					Included = true, Obligation = g.Sum(d => d.OriginalAmount), Verification = "Legacy facts unavailable" }).ToList(),
				Lines = dues.Select(d => new StudentFeeAssignmentPreviewLineDto { DueFeeId = d.Id, FeeHeadId = d.FeeHeadId, FeeHeadName = names.GetValueOrDefault(d.FeeHeadId, "Fee head"),
					InstallmentLabel = d.GetDisplayInstallmentLabel(), DueDate = d.DueDate, DemandDate = d.DemandDate,
					ScheduledAmount = d.ScheduledAmount, ProratedAmount = d.OriginalAmount, PaymentTermVersionId = d.PaymentTermVersionId }).ToList() };
		}
		// Resolve current master labels without changing immutable financial evidence.
		var currentHeadIds = snapshot.Heads.Select(x => x.FeeHeadId).Concat(snapshot.Lines.Select(x => x.FeeHeadId)).Distinct().ToList();
		var currentNames = (await feeHeadRepository.GetListAsync(x => currentHeadIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
		foreach (var head in snapshot.Heads)
			if (currentNames.TryGetValue(head.FeeHeadId, out var name)) head.FeeHeadName = name;
		foreach (var line in snapshot.Lines)
			if (currentNames.TryGetValue(line.FeeHeadId, out var name)) line.FeeHeadName = name;
		snapshot.ScheduledTotal = snapshot.Lines.Sum(x => x.ScheduledAmount);
		snapshot.ProratedTotal = snapshot.Lines.Sum(x => x.ProratedAmount);
		snapshot.ReductionTotal = snapshot.ScheduledTotal - snapshot.ProratedTotal;
		foreach (var line in snapshot.Lines)
		{
			var due = dues.FirstOrDefault(d => d.Id == line.DueFeeId);
			if (due == null) { result.Warnings.Add("An obligation referenced by the assignment is unavailable; reconciliation is required."); continue; }
			line.PaidAmount = due.PaidAmount;
			line.OutstandingAmount = due.OutstandingAmount;
		}
		snapshot.OriginalPolicies = snapshot.Policies.ToList();
		if (assignment.FinanceFeeAccountId.HasValue) snapshot.AssignmentSource = "Admission";
		var currentVersions = new[] { assignment.ConcessionPolicyVersionId, assignment.CollectionPolicyVersionId, assignment.RefundPolicyVersionId, assignment.ProrationPolicyVersionId }
			.Where(x => x.HasValue).Select(x => x!.Value).ToList();
		var currentPolicies = await financePolicyVersionRepository.GetListAsync(x => currentVersions.Contains(x.Id));
		snapshot.Policies = currentPolicies.Select(version => {
			var original = snapshot.OriginalPolicies.FirstOrDefault(p => p.VersionId == version.Id);
			var policy = original ?? ToAssignmentPolicy(version);
			if (original == null) {
				policy.Source = "StudentOverride";
				policy.Description = version.PolicyType == "proration" ? "Updated selection; existing obligations retain their original proration calculation." : "Applies to subsequent policy evaluations; existing financial history is preserved.";
			}
			return policy;
		}).ToList();
		if (assignment.ExtraProperties.TryGetValue("AssignedPolicyReason", out var reason)) snapshot.PolicyOverrideReason = reason?.ToString();
		result.Assignment = snapshot;
		result.StructureName = snapshot.StructureName;
		result.ChargeStartDate = snapshot.ChargeStartDate;
		result.ChargeEndDate = snapshot.ChargeEndDate;
		result.FeeHeads = snapshot.Heads.Where(h => h.Included).Select(h => new StudentFeeHeadDto {
			FeeHeadId = h.FeeHeadId, FeeHeadName = h.FeeHeadName, Amount = h.Obligation,
			IsSelected = true, IsOptional = h.IsOptional, IsOneTime = h.IsOneTime, SelectedAmount = h.EffectiveAmount ?? h.Obligation }).ToList();
		return result;
	}
}
