using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.Transport;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public sealed class StudentRecordTransitionSnapshot
{
	public Guid AcademicYearId { get; set; }
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }
	public Guid StageId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public int? RollNumber { get; set; }
	public StudentLifeCycleStatusEnum StudentStatus { get; set; }
	public Guid EnrollmentId { get; set; }
	public bool EnrollmentActive { get; set; }
}

[RemoteService(false)]
public class StudentRecordTransitionReadinessService
	: ApplicationService, ITransientDependency
{
	private readonly StudentRecordsAccessService access;
	private readonly StudentScopeAccessService scope;
	private readonly IRepository<StudentEnrollment, Guid> enrollments;
	private readonly IRepository<AcademicYear, Guid> years;
	private readonly IRepository<Standard, Guid> standards;
	private readonly IRepository<Division, Guid> divisions;
	private readonly IRepository<StudentSeatReservation, Guid> reservations;
	private readonly IRepository<StudentOnboardingCase, Guid> onboarding;
	private readonly IRepository<ExamResultDetail, Guid> results;
	private readonly IRepository<ExamResultHeader, Guid> headers;
	private readonly IRepository<Exam, Guid> exams;
	private readonly IRepository<LibraryMember, Guid> members;
	private readonly IRepository<BookIssue, Guid> bookIssues;
	private readonly IRepository<TransportMember, Guid> transportMembers;
	private readonly StudentTransitionFinanceClearanceReader finance;
	private readonly IRepository<StudentRecordTransition, Guid> transitions;
	private readonly StudentOnboardingRollRuleReader rollRules;
	private readonly StudentOnboardingRollNumberAllocator rollAllocator;
	private readonly OnboardingFeeReadinessReader feeReadiness;
	private readonly StudentRecordsEnrollmentScopeService recordScope;

	public StudentRecordTransitionReadinessService(
		StudentRecordsAccessService access,
		StudentScopeAccessService scope,
		IRepository<StudentEnrollment, Guid> enrollments,
		IRepository<AcademicYear, Guid> years,
		IRepository<Standard, Guid> standards,
		IRepository<Division, Guid> divisions,
		IRepository<StudentSeatReservation, Guid> reservations,
		IRepository<StudentOnboardingCase, Guid> onboarding,
		IRepository<ExamResultDetail, Guid> results,
		IRepository<ExamResultHeader, Guid> headers,
		IRepository<Exam, Guid> exams,
		IRepository<LibraryMember, Guid> members,
		IRepository<BookIssue, Guid> bookIssues,
		IRepository<TransportMember, Guid> transportMembers,
		StudentTransitionFinanceClearanceReader finance,
		IRepository<StudentRecordTransition, Guid> transitions,
		StudentOnboardingRollRuleReader rollRules,
		StudentOnboardingRollNumberAllocator rollAllocator,
		IRepository<AdmissionApplication, Guid> admissionApplications,
		IRepository<StudentIdentifierAllocation, Guid> identifiers,
		OnboardingFeeReadinessReader feeReadiness,
		StudentRecordsEnrollmentScopeService recordScope
	)
	{
		this.access = access;
		this.scope = scope;
		this.enrollments = enrollments;
		this.years = years;
		this.standards = standards;
		this.divisions = divisions;
		this.reservations = reservations;
		this.onboarding = onboarding;
		this.results = results;
		this.headers = headers;
		this.exams = exams;
		this.members = members;
		this.bookIssues = bookIssues;
		this.transportMembers = transportMembers;
		this.finance = finance;
		this.transitions = transitions;
		this.rollRules = rollRules;
		this.rollAllocator = rollAllocator;
		_ = admissionApplications;
		_ = identifiers;
		this.feeReadiness = feeReadiness;
		this.recordScope = recordScope;
	}

	public async Task<List<StudentRecordTransitionInput>> OrderCohortAsync(List<StudentRecordTransitionInput> inputs)
	{
		var ordered = new List<StudentRecordTransitionInput>();
		var sources = (await LazyServiceProvider.LazyGetRequiredService<StudentRollSourceReader>().ReadAsync(inputs.Select(x => x.StudentId).Distinct().ToArray())).ToDictionary(x => x.StudentId);
		var captured = new Dictionary<Guid, string>(); var scopeDivisions = new Dictionary<Guid, Guid?>();
		foreach (var input in inputs)
		{
			var json = input.TargetAcademicYearId.HasValue && input.TargetStandardId.HasValue && input.TargetDivisionId.HasValue ? await CaptureRollRuleAsync(input) : "null";
			captured[input.StudentId] = json; var rule = StudentOnboardingRollRuleReader.Parse(json);
			scopeDivisions[input.StudentId] = rule.HasValue && OnboardingRuleConfiguration.Text(rule.Value, "numberingScope", "class-division") == "class" ? null : input.TargetDivisionId;
		}
		foreach (var group in inputs.GroupBy(x => new { x.TargetAcademicYearId, x.TargetStandardId, ScopeDivision = scopeDivisions[x.StudentId] }))
		{
			var first = group.First();
			if (!first.TargetAcademicYearId.HasValue || !first.TargetStandardId.HasValue || !first.TargetDivisionId.HasValue) { ordered.AddRange(group); continue; }
			var ruleJson = captured[first.StudentId];
			if (!group.Any(x => StudentOnboardingRollNumberAllocator.ResolveMode(ruleJson, x.RollAssignmentMode) == "automatic")) { ordered.AddRange(group); continue; }
			var rule = StudentOnboardingRollRuleReader.Parse(ruleJson);
			var ordering = new StudentRollOrdering(rule);
			foreach (var input in group)
			{
				if (OrderingSignature(StudentOnboardingRollRuleReader.Parse(captured[input.StudentId])) != OrderingSignature(rule)) throw OnboardingRuleConfiguration.Invalid("This cohort shares a numbering scope but has conflicting ordering rules. Align those school rules before assigning automatic numbers together.");
				await access.GetScopedStudentAsync(input.StudentId);
				if (!sources.ContainsKey(input.StudentId)) throw new BusinessException(StudentRollNumberErrorCodes.BatchBlocked);
			}
			var commands = group.ToDictionary(x => x.StudentId);
			ordered.AddRange(ordering.Order(group.Select(x => sources[x.StudentId])).Select(x => commands[x.StudentId]));
		}
		return ordered;
	}
	private static string OrderingSignature(JsonElement? rule)
	{
		return new StudentRollOrdering(rule).Signature;
	}
	public async Task<(Student Student, StudentEnrollment Enrollment)> GetSourceAsync(Guid studentId, Guid yearId, bool writable = true)
	{
		var student = writable ? (await access.EnsureWritableAsync(studentId, placement: true)).Student : await access.GetScopedStudentAsync(studentId);
		var enrollment = await enrollments.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId)
			?? throw new BusinessException("Students:Records:EnrollmentMissing");
		if (student.AcademicYearId != yearId) throw new BusinessException("Students:Records:InvalidTransitionYear");
		return (student, enrollment);
	}
	public async Task<StudentRecordTransitionPreviewRowDto> PreviewAsync(Guid yearId, StudentRecordTransitionInput input, StudentRecordsPolicy policy, bool checkStamps, bool validateDate = true, string? frozenRollRule = null, IEnumerable<int>? cohortReservations = null)
	{
		var source = await GetSourceAsync(input.StudentId, yearId);
		var row = new StudentRecordTransitionPreviewRowDto { StudentId = input.StudentId, Outcome = input.Outcome,
			StudentConcurrencyStamp = source.Student.ConcurrencyStamp, EnrollmentConcurrencyStamp = source.Enrollment.ConcurrencyStamp };
		if (!policy.IsMasked("Identity", await access.GetDatabaseRoleNamesAsync())) row.StudentName = string.Join(" ", new[] { source.Student.FirstName, source.Student.MiddleName, source.Student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
		if (!StudentRecordsPolicy.Outcomes.Contains(input.Outcome)) Add(row, "InvalidTransition", "Choose a supported transition outcome.");
		if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000) Add(row, "InvalidPayload", "Provide a transition reason up to 1000 characters.");
		if (input.EffectiveDate == default) Add(row, "EffectiveDateNotAllowed", "Choose an effective date.");
		else if (validateDate) ValidateDate(row, input.EffectiveDate, policy);
		if (input.Outcome == "Return")
		{
			if (source.Enrollment.IsActive || source.Student.Status != StudentLifeCycleStatusEnum.Left) Add(row, "InvalidTransition", "Only a former student can use a return route.");
		}
		else if (!source.Enrollment.IsActive || source.Student.Status == StudentLifeCycleStatusEnum.Left) Add(row, "InvalidTransition", "Use a return route for a former student.");
		if (checkStamps && (input.StudentConcurrencyStamp != source.Student.ConcurrencyStamp || input.EnrollmentConcurrencyStamp != source.Enrollment.ConcurrencyStamp))
			Add(row, "StaleSnapshot", "The student or source enrollment changed after preview. Refresh the preview.");
		if (input.Outcome is "Promote" or "Repeat" or "Return")
		{
			if (!input.TargetAcademicYearId.HasValue || !input.TargetStandardId.HasValue || !input.TargetDivisionId.HasValue)
				Add(row, "InvalidPlacement", "Choose the next academic year, class, and division.");
			else
			{
				try
				{
					var rollJson = frozenRollRule ?? await CaptureRollRuleAsync(input);
					if (frozenRollRule == null)
					{
						var proposal = await rollAllocator.ProposeAsync(input.StudentId, input.TargetAcademicYearId.Value, input.TargetStandardId.Value, input.TargetDivisionId.Value, input.RollNumber, input.RollAssignmentMode, rollJson, input.Reason, cohortReservations);
						row.RollAssignmentMode = proposal.AssignmentMode; row.ProposedRollNumber = proposal.RollNumber;
					}
					else
					{
						row.RollAssignmentMode = StudentOnboardingRollNumberAllocator.ResolveMode(rollJson, input.RollAssignmentMode); row.ProposedRollNumber = input.RollNumber;
						await rollRules.ValidateAsync(input.StudentId, input.TargetAcademicYearId.Value, input.TargetStandardId.Value, input.TargetDivisionId.Value, input.RollNumber, input.Reason, rollJson);
					}
					await ValidateTargetAsync(input.StudentId, yearId, input.TargetAcademicYearId.Value, input.TargetStandardId.Value, input.TargetDivisionId.Value, row.ProposedRollNumber, input.Outcome == "Return");
					var rollRule = StudentOnboardingRollRuleReader.Parse(rollJson);
					if (rollRule.HasValue) row.RollNumberScope = OnboardingRuleConfiguration.Text(rollRule.Value, "numberingScope", "class-division");
					// Annual and return approvals are governed exclusively by the frozen Records rule.
					row.OnboardingRollApprovalRequired = false;
				}
				catch (BusinessException e)
				{
					Add(row, e.Code ?? "InvalidPlacement", e.Code == "Students:Records:FinancialPlacementImpactRequired" ? "Finance has assigned fees for the existing class. Returning into another class requires an owner fee-impact review, which is not yet supported." : e.Data.Contains("MinimumRoll") ? $"Enter a target roll number of {e.Data["MinimumRoll"]} or higher under the school's numbering rule." :
						e.Code == StudentOnboardingErrorCodes.RollNumberDuplicate ? "The roll number is already assigned or reserved within the school's numbering scope." : "The target year, placement, seat capacity, or roll number is unavailable.");
				}
			}
		}
		else if (input.TargetAcademicYearId.HasValue || input.TargetStandardId.HasValue || input.TargetDivisionId.HasValue || input.RollNumber.HasValue)
			Add(row, "InvalidPlacement", "This outcome does not create a new-year placement.");
		if (policy.Transitions.OutcomeChecks.TryGetValue(input.Outcome, out var checks))
			await AddOwnerChecksAsync(row, source.Student.Id, source.Enrollment.AcademicYearId, source.Enrollment.StandardId, checks,
				input.Outcome == "Withdraw" ? input.EffectiveDate : null);
		StudentSchoolExitOutstanding.Complete(row); return row;
	}
	public Task<string> CaptureRollRuleAsync(StudentRecordTransitionInput input) => rollRules.CaptureAsync(input.StudentId, input.TargetAcademicYearId!.Value, input.TargetStandardId!.Value, input.TargetDivisionId!.Value);
	public Task ValidateRollRuleAsync(StudentRecordRenewalCase item, string frozenJson, string reason) => rollRules.ValidateAsync(item.StudentId, item.AcademicYearId, item.StandardId, item.DivisionId, item.RollNumber, reason, frozenJson);
	public async Task<Standard> ValidateTargetAsync(Guid studentId, Guid sourceYearId, Guid yearId, Guid standardId, Guid divisionId, int? roll, bool reviewedReturn = false, bool allowHistoricalFirstEnrollment = false, Guid? excludeOnboardingCaseId = null)
	{
		await scope.EnsureCanAccessStandardAsync(standardId, "target transition class");
		await scope.EnsureCanAccessDivisionAsync(divisionId, "target transition division");
		await recordScope.EnsureTargetPlacementAsync(yearId, standardId, divisionId);
		var targetYear = await years.GetAsync(x => x.Id == yearId && x.TenantId == CurrentTenant.Id);
		var sourceYear = await years.GetAsync(x => x.Id == sourceYearId && x.TenantId == CurrentTenant.Id);
		if (yearId == sourceYearId ? !reviewedReturn : targetYear.StartDate <= sourceYear.StartDate) throw new BusinessException("Students:Records:InvalidTransitionYear");
		var standard = await standards.GetAsync(x => x.Id == standardId && x.TenantId == CurrentTenant.Id, includeDetails: true);
		var division = await divisions.GetAsync(x => x.Id == divisionId && x.TenantId == CurrentTenant.Id);
		if (division.StandardId != standardId || standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId != yearId)
			throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
		var existing = await enrollments.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId);
		var sameYearReturn = reviewedReturn && yearId == sourceYearId;
		if (sameYearReturn && (existing == null && !allowHistoricalFirstEnrollment || existing?.IsActive == true || (await access.GetScopedStudentAsync(studentId)).Status != StudentLifeCycleStatusEnum.Left)) throw new BusinessException("Students:Records:InvalidTransition");
		if (sameYearReturn && (existing?.StandardId ?? (await access.GetScopedStudentAsync(studentId)).StandardId) != standardId && await feeReadiness.HasAssignmentAsync(studentId, yearId)) throw new BusinessException("Students:Records:FinancialPlacementImpactRequired");
		if (existing != null && !sameYearReturn && !await CanReuseCorrectedTargetAsync(existing)) throw new BusinessException("Students:Records:TargetEnrollmentExists");
		await ValidateCapacityAsync(studentId, yearId, divisionId, roll, division.Strength, excludeOnboardingCaseId);
		return standard;
	}
	private async Task<bool> CanReuseCorrectedTargetAsync(StudentEnrollment enrollment)
	{
		if (enrollment.IsActive) return false;
		var query = await transitions.GetQueryableAsync();
		return await AsyncExecuter.AnyAsync(query.Where(original => original.TenantId == CurrentTenant.Id && original.TargetEnrollmentId == enrollment.Id && original.Status == "Applied" && original.CorrectionOfId == null &&
			query.Any(correction => correction.TenantId == CurrentTenant.Id && correction.CorrectionOfId == original.Id && correction.Status == "Applied")));
	}
	public async Task<StudentEnrollment> ActivateTargetEnrollmentAsync(Guid studentId, Guid yearId, Guid standardId, Guid divisionId, int? roll, bool reviewedSameYearReturn = false, DateTime? effectiveDate = null)
	{
		var existing = await enrollments.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId);
		if (existing == null) return await enrollments.InsertAsync(StudentEnrollment.Create(GuidGenerator.Create(), CurrentTenant.Id, studentId, yearId, standardId, divisionId, roll));
		if (!(reviewedSameYearReturn && !existing.IsActive && (await access.GetScopedStudentAsync(studentId)).Status == StudentLifeCycleStatusEnum.Left) && !await CanReuseCorrectedTargetAsync(existing)) throw new BusinessException("Students:Records:TargetEnrollmentExists");
		if (existing.StandardId.HasValue && existing.DivisionId.HasValue && existing.RollNumber.HasValue)
			await LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>().RecordIssuedAsync(
				existing.AcademicYearId, existing.StandardId.Value, existing.DivisionId.Value, existing.RollNumber.Value);
		existing.ChangePlacement(standardId, divisionId, roll, effectiveDate); existing.Activate(effectiveDate); return await enrollments.UpdateAsync(existing);
	}
	public async Task ValidateCapacityAsync(Guid studentId, Guid yearId, Guid divisionId, int? roll, int strength, Guid? excludeOnboardingCaseId = null)
	{
		var now = Clock.Now.ToUniversalTime();
		var occupied = await enrollments.CountAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != studentId && x.AcademicYearId == yearId && x.DivisionId == divisionId && x.IsActive);
		var reserved = await reservations.CountAsync(x => x.TenantId == CurrentTenant.Id && x.OnboardingCaseId != excludeOnboardingCaseId && x.AcademicYearId == yearId && x.DivisionId == divisionId && x.Status == StudentSeatReservationStatus.Reserved && x.ExpiresAt > now);
		if (occupied + reserved >= strength) throw new BusinessException(StudentOnboardingErrorCodes.DivisionFull);
		if (roll.HasValue && (roll <= 0 || await enrollments.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != studentId && x.AcademicYearId == yearId && x.DivisionId == divisionId && x.RollNumber == roll && x.IsActive) ||
			await onboarding.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId != studentId && x.AcademicYearId == yearId && x.DivisionId == divisionId && x.RollNumber == roll && x.Status != StudentOnboardingStatus.Completed && x.Status != StudentOnboardingStatus.Cancelled)))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberDuplicate);
	}
	public async Task ValidateBatchCapacityAsync(System.Collections.Generic.List<StudentRecordTransitionInput> inputs, System.Collections.Generic.List<StudentRecordTransitionPreviewRowDto> rows)
	{
		var acceptedRolls = new System.Collections.Generic.List<StudentRecordTransitionInput>();
		foreach (var input in inputs.Where(x => x.RollNumber.HasValue && rows.Single(r => r.StudentId == x.StudentId).Ready))
		{
			var row = rows.Single(x => x.StudentId == input.StudentId);
			if (acceptedRolls.Any(previous => previous.TargetAcademicYearId == input.TargetAcademicYearId && previous.TargetStandardId == input.TargetStandardId && previous.RollNumber == input.RollNumber &&
				(row.RollNumberScope == "class" || rows.Single(r => r.StudentId == previous.StudentId).RollNumberScope == "class")))
			{
				Add(row, "RollNumberDuplicate", "This batch repeats a roll number under the school's class-wide numbering rule."); row.Ready = false;
			}
			else acceptedRolls.Add(input);
		}
		foreach (var group in inputs.Where(x => x.TargetAcademicYearId.HasValue && x.TargetDivisionId.HasValue && rows.Single(r => r.StudentId == x.StudentId).Ready).GroupBy(x => new { x.TargetAcademicYearId, x.TargetDivisionId }))
		{
			var division = await divisions.GetAsync(x => x.Id == group.Key.TargetDivisionId && x.TenantId == CurrentTenant.Id);
			var occupied = await enrollments.CountAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == group.Key.TargetAcademicYearId && x.DivisionId == group.Key.TargetDivisionId && x.IsActive);
			var now = Clock.Now.ToUniversalTime();
			var reserved = await reservations.CountAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == group.Key.TargetAcademicYearId && x.DivisionId == group.Key.TargetDivisionId && x.Status == StudentSeatReservationStatus.Reserved && x.ExpiresAt > now);
			var available = Math.Max(0, division.Strength - occupied - reserved);
			var rolls = new System.Collections.Generic.HashSet<int>(); var accepted = 0;
			foreach (var input in group)
			{
				var row = rows.Single(x => x.StudentId == input.StudentId);
				if (input.RollNumber.HasValue && !rolls.Add(input.RollNumber.Value)) Add(row, "RollNumberDuplicate", "Two students in this batch have the same target roll number.");
				else if (accepted >= available) Add(row, "DivisionFull", "The batch exceeds the available target seats.");
				else accepted++;
				row.Ready = row.Issues.Count == 0;
			}
		}
	}
	private Task AddOwnerChecksAsync(StudentRecordTransitionPreviewRowDto row, StudentEnrollment source, StudentRecordsOutcomeChecks checks) =>
		AddOwnerChecksAsync(row, source.StudentId, source.AcademicYearId, source.StandardId, checks);
	private async Task AddOwnerChecksAsync(StudentRecordTransitionPreviewRowDto row, Guid studentId, Guid academicYearId, Guid? standardId, StudentRecordsOutcomeChecks checks, DateTime? withdrawalDate = null)
	{
		if (checks.RequireAcademicResult)
		{
			var resultQuery = await results.GetQueryableAsync(); var headerQuery = await headers.GetQueryableAsync(); var examQuery = await exams.GetQueryableAsync();
			if (!await AsyncExecuter.AnyAsync(resultQuery.Where(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.ResultStatus != ExamStudentResultStatus.Withheld &&
				headerQuery.Any(h => h.Id == x.ResultHeaderId && h.TenantId == CurrentTenant.Id && (h.Status == ExamResultStatus.Published || h.Status == ExamResultStatus.Frozen)) &&
				examQuery.Any(e => e.Id == x.ExamId && e.TenantId == CurrentTenant.Id && e.StandardId == standardId))))
				Add(row, "AcademicResultRequired", "Publish an available source-class academic result before this transition.");
		}
		var isExit = StudentSchoolExitOutstanding.IsExit(row.Outcome);
		var evidence = new SortedDictionary<string, string>();
		if (checks.RequireFinanceClearance || withdrawalDate.HasValue || isExit)
		{
			var clearance = await finance.GetAsync(studentId, academicYearId, withdrawalDate, checks.RequireFinanceClearance, isExit);
			row.Proration = clearance.Proration;
			if (!clearance.Cleared) Add(row, isExit && clearance.OutstandingOnly ? "OutstandingFees" : "FinanceClearanceRequired", clearance.Detail);
			if (clearance.OutstandingOnly) evidence["Finance"] = clearance.Evidence;
		}
		if (checks.RequireLibraryClearance || isExit)
		{
			var memberQuery = await members.GetQueryableAsync(); var issueQuery = await bookIssues.GetQueryableAsync();
			var pending = await AsyncExecuter.ToListAsync(issueQuery.Where(x => x.TenantId == CurrentTenant.Id && x.MemberRole == 0 && x.Status != BookIssueStatus.Returned &&
				(x.MemberId == studentId || memberQuery.Any(m => m.Id == x.MemberId && m.StudentId == studentId && m.TenantId == CurrentTenant.Id))).OrderBy(x => x.Id).Select(x => x.Id));
			if (pending.Count > 0) { Add(row, isExit ? "OutstandingBooks" : "LibraryClearanceRequired", "Outstanding books remain recorded in Library."); evidence["Library"] = JsonSerializer.Serialize(pending); }
		}
		row.OutstandingEvidence = JsonSerializer.Serialize(evidence);
		if (checks.RequireTransportClearance && await transportMembers.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.CurrentRouteId.HasValue && x.Status == TransportMemberStatus.Active))
			Add(row, "TransportClearanceRequired", "Close the current route assignment in Transport before continuing.");
	}
	public async Task RequireReturnOwnerChecksAsync(Guid studentId, Guid sourceYearId, StudentRecordsPolicy policy)
	{
		var source = await GetSourceAsync(studentId, sourceYearId);
		var row = new StudentRecordTransitionPreviewRowDto { StudentId = studentId, Outcome = "Return" };
		await AddOwnerChecksAsync(row, source.Enrollment, policy.Transitions.OutcomeChecks["Return"]);
		if (row.Issues.Count > 0) throw new BusinessException(row.Issues[0].Code).WithData("Issues", string.Join(" ", row.Issues.Select(x => x.Message)));
	}
	public async Task RequireAdmissionReturnOwnerChecksAsync(Student student, StudentRecordsPolicy policy)
	{
		var row = new StudentRecordTransitionPreviewRowDto { StudentId = student.Id, Outcome = "Return" };
		await AddOwnerChecksAsync(row, student.Id, student.AcademicYearId!.Value, student.StandardId, policy.Transitions.OutcomeChecks["Return"]);
		if (row.Issues.Count > 0) throw new BusinessException(row.Issues[0].Code).WithData("Issues", string.Join(" ", row.Issues.Select(x => x.Message)));
	}
	public static string Capture(Student student, StudentEnrollment enrollment) => JsonSerializer.Serialize(new StudentRecordTransitionSnapshot
	{
		AcademicYearId = enrollment.AcademicYearId, BoardId = student.AcademicBoardId!.Value, MediumId = student.AcademicMediumId,
		StageId = student.AcademicMediumStageId!.Value, StandardId = enrollment.StandardId, DivisionId = enrollment.DivisionId,
		RollNumber = enrollment.RollNumber, StudentStatus = student.Status, EnrollmentId = enrollment.Id, EnrollmentActive = enrollment.IsActive
	});
	public void ValidateAdmissionReturnDate(StudentRecordTransitionPreviewRowDto row, DateTime date, StudentRecordsPolicy policy) => ValidateDate(row, date, policy);
	private void ValidateDate(StudentRecordTransitionPreviewRowDto row, DateTime date, StudentRecordsPolicy policy)
	{
		var days = (date.Date - AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now)).TotalDays;
		if ((days < 0 && (!policy.Transitions.AllowPastDates || -days > policy.Transitions.MaxPastDays)) || (days > 0 && (!policy.Transitions.AllowFutureDates || days > policy.Transitions.MaxFutureDays)))
			Add(row, "EffectiveDateNotAllowed", "The effective date falls outside this school's permitted range.");
	}
	public static void Add(StudentRecordTransitionPreviewRowDto row, string code, string message) => row.Issues.Add(new()
	{
		Code = code.Contains(':') || code.StartsWith("Students.", StringComparison.Ordinal) ? code : "Students:Records:" + code,
		Message = message
	});
}
