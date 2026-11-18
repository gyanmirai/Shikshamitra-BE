using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	// This read model lives only for one context request. Commands never reuse it.
	private sealed class AssignmentContextReads
	{
		public Dictionary<Guid, EnrollmentFact> Enrollments { get; init; } = [];
		public Dictionary<Guid, StudentFeeStructure> Assignments { get; init; } = [];
		public Dictionary<Guid, string?> Blockers { get; init; } = [];
		public List<FinancePolicyVersion> Policies { get; init; } = [];
		public Dictionary<Guid, AssignmentAdmissionResult> Admissions { get; init; } = [];
		public AcademicYear? Year { get; init; }
		public List<DueFee> PreparedDues { get; init; } = [];
		public Dictionary<Guid, FeeStructure> Structures { get; init; } = [];
		public Task<FinancePolicyVersion?>? DefaultProration { get; set; }
	}

	private async Task<AssignmentContextReads> ReadContextsAsync(AssignmentContextsInput input)
	{
		var ids = input.StudentIds.ToArray();
		var enrollments = (await AsyncExecuter.ToListAsync((await ScopedEnrollmentsAsync(input.AcademicYearId))
			.Where(e => Enumerable.Contains(ids, e.StudentId))))
			.GroupBy(e => e.StudentId).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.IsActive).First());
		// Complete authorization before loading any admission or financial evidence.
		if (ids.Any(id => !enrollments.ContainsKey(id))) throw new BusinessException(AssignmentErrorCodes.EnrollmentRequired);
		var assignments = await studentFeeStructureRepository.GetListAsync(a => a.AcademicYearId == input.AcademicYearId && Enumerable.Contains(ids, a.StudentId));
		var policyIds = input.IncludeEditDetails ? assignments.SelectMany(a => new[] {
			a.CollectionPolicyVersionId, a.ConcessionPolicyVersionId, a.RefundPolicyVersionId, a.ProrationPolicyVersionId })
			.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray() : [];
		var policies = policyIds.Length == 0 ? [] : await financePolicyVersionRepository.GetListAsync(p => Enumerable.Contains(policyIds, p.Id));
		var blockers = input.IncludeEditDetails ? await StructureEditBlockersAsync(assignments) : new Dictionary<Guid, string?>();
		var unassigned = enrollments.Where(e => assignments.All(a => a.StudentId != e.Key)).ToDictionary(e => e.Key, e => e.Value.StandardId);
		var admissions = await ReadAssignmentAdmissionsAsync(unassigned, input.AcademicYearId);
		var accountIds = admissions.Values.Where(a => a.Error == null && a.Facts?.Account?.AssignmentSnapshotJson != null)
			.Select(a => a.Facts!.Account!.Id).Distinct().ToArray();
		var dues = accountIds.Length == 0 ? [] : await dueFeeRepository.GetListAsync(d => d.FinanceFeeAccountId.HasValue && Enumerable.Contains(accountIds, d.FinanceFeeAccountId.Value));
		var structureIds = admissions.Values.Where(a => a.Error == null && a.Facts?.Account?.AssignmentSnapshotJson != null && a.Facts.Account.StructureId.HasValue)
			.Select(a => a.Facts!.Account!.StructureId!.Value).Distinct().ToArray();
		var structures = structureIds.Length == 0 ? [] : await feeStructureRepository.GetListAsync(s => Enumerable.Contains(structureIds, s.Id));
		return new() {
			Enrollments = enrollments, Assignments = assignments.ToDictionary(a => a.StudentId), Blockers = blockers, Policies = policies,
			Admissions = admissions, Year = unassigned.Count == 0 ? null : await academicYearRepository.GetAsync(input.AcademicYearId),
			PreparedDues = dues, Structures = structures.ToDictionary(s => s.Id)
		};
	}

	private sealed record AssignmentAdmissionResult(AssignmentAdmissionFacts? Facts, BusinessException? Error = null);

	private async Task<Dictionary<Guid, AssignmentAdmissionResult>> ReadAssignmentAdmissionsAsync(IReadOnlyDictionary<Guid, Guid?> students, Guid yearId)
	{
		var result = new Dictionary<Guid, AssignmentAdmissionResult>();
		if (students.Count == 0) return result;
		var studentIds = students.Keys.ToArray();
		var applications = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplication, Guid>>().GetQueryableAsync();
		var offerings = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionOffering, Guid>>().GetQueryableAsync();
		var cycles = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionCycle, Guid>>().GetQueryableAsync();
		var matches = await AsyncExecuter.ToListAsync(from application in applications
			join offering in offerings on application.AdmissionOfferingId equals offering.Id
			join cycle in cycles on offering.AdmissionCycleId equals cycle.Id
			where application.TenantId == CurrentTenant.Id && offering.TenantId == CurrentTenant.Id && cycle.TenantId == CurrentTenant.Id &&
				cycle.AcademicYearId == yearId && (application.StudentId.HasValue && Enumerable.Contains(studentIds, application.StudentId.Value) ||
				application.EnrolledStudentId.HasValue && Enumerable.Contains(studentIds, application.EnrolledStudentId.Value))
			select new { Application = application, Offering = offering });
		var applicationIds = matches.Select(m => m.Application.Id).Distinct().ToArray();
		var accounts = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>()
			.GetListAsync(a => a.TenantId == CurrentTenant.Id && a.AcademicYearId == yearId &&
				(a.StudentId.HasValue && Enumerable.Contains(studentIds, a.StudentId.Value) || Enumerable.Contains(applicationIds, a.ApplicationId)));
		var joiningDates = await LazyServiceProvider.LazyGetRequiredService<AdmissionJoiningDateReader>()
			.ReadManyAsync(matches.Select(m => m.Application).DistinctBy(a => a.Id).ToList());
		foreach (var (studentId, standardId) in students)
		{
			try
			{
				var sources = matches.Where(m => m.Application.StudentId == studentId || m.Application.EnrolledStudentId == studentId).ToList();
				if (sources.Count == 0)
				{
					if (accounts.Any(a => a.StudentId == studentId && a.AssignmentSnapshotJson != null))
						throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
					result[studentId] = new(null); continue;
				}
				if (sources.Count != 1) throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
				var match = sources[0];
				if (match.Offering.StandardId != standardId || match.Application.Status is AdmissionApplicationStatus.Withdrawn or AdmissionApplicationStatus.Rejected or AdmissionApplicationStatus.OfferDeclined)
					throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
				var selection = match.Application.GetFeeSelection();
				if (selection != null && (selection.AcademicYearId != yearId || selection.StandardId != standardId))
					throw new BusinessException(AssignmentErrorCodes.StructureMismatch);
				var linked = accounts.Where(a => a.ApplicationId == match.Application.Id).ToList();
				if (linked.Count > 1 || accounts.Any(a => a.StudentId == studentId && a.ApplicationId != match.Application.Id && (a.AssignmentSnapshotJson != null || a.StudentFeeStructureId.HasValue)))
					throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
				var account = linked.SingleOrDefault();
				if (account != null && (account.StudentId.HasValue && account.StudentId != studentId || account.ApplicantId != match.Application.ApplicantId ||
					account.StandardId != standardId || account.BoardId != match.Offering.BoardId || account.EducationMediumId != match.Offering.MediumId))
					throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
				var joining = joiningDates[match.Application.Id];
				if (joining.Error != null) throw joining.Error;
				result[studentId] = new(new(match.Application, match.Offering, selection, account, joining.Date, Hash(new {
					ApplicationRevision = match.Application.ConcurrencyStamp, OfferingRevision = match.Offering.ConcurrencyStamp, JoiningDate = joining.Date, Selection = selection })));
			}
			catch (BusinessException error) { result[studentId] = new(null, error); }
		}
		return result;
	}
}
