using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentRecordGrNumberService(IStudentRepository students,
	IRepository<StudentEnrollment, Guid> enrollments, IRepository<StudentOnboardingCase, Guid> onboardingCases,
	IRepository<Standard, Guid> standards, OnboardingConfigurationManager configuration,
	StudentGrNumberAllocator allocator) : ApplicationService, ITransientDependency
{
	internal const string Section = "gr-number";
	internal const string AlreadyAssigned = "Students:Records:GrAlreadyAssigned";
	internal const string Ineligible = "Students:Records:GrAssignmentIneligible";
	internal const string Pending = "Students:Records:GrAssignmentPending";
	internal const string PreviewChanged = "Students:Records:GrAutoPreviewChanged";
	internal const string InvalidNumber = "Students:Records:GrInvalidNumber";
	internal const string ReferenceRequired = "Students:Records:GrRegisterReferenceRequired";

	internal static StudentRecordGrNumberPayload Normalize(StudentRecordGrNumberPayload input)
	{
		var number = input.GrNumber?.Trim();
		var reference = input.RegisterReference?.Trim();
		if (input.Mode is not ("auto" or "manual") || string.IsNullOrEmpty(number) || number.Length > 100 ||
			number.Any(char.IsControl) || (int.TryParse(number, out var legacy) && legacy <= 0))
			throw new BusinessException(InvalidNumber);
		if (input.Mode == "manual" && (string.IsNullOrEmpty(reference) || reference.Length > 500 || reference.Any(char.IsControl)))
			throw new BusinessException(ReferenceRequired);
		if (input.Mode == "auto" && !string.IsNullOrEmpty(reference))
			throw new BusinessException("Students:Records:InvalidPayload");
		return new(input.Mode, number, input.Mode == "manual" ? reference : null);
	}

	internal async Task<string> CaptureAsync(Guid studentId)
	{
		var (student, enrollment) = await ReadAsync(studentId);
		return StudentRecordPayloadService.Canonical(JsonSerializer.Serialize(new
		{
			student.GrNumber, student.GrNo, student.Status, student.AcademicYearId,
			EnrollmentId = enrollment?.Id, enrollment?.StandardId, enrollment?.DivisionId
		}, StudentRecordPayloadService.JsonOptions));
	}

	internal async Task<JsonElement> GetOptionsAsync(Guid studentId, string snapshot)
	{
		var (student, enrollment) = await GetEligibleAsync(studentId);
		var data = JsonNode.Parse(snapshot)!.AsObject();
		try
		{
			var series = await GetAutoSeriesAsync(student.Id, enrollment, persist: false);
			data["autoGrNumber"] = await NextAvailableAsync(series, student.Id);
		}
		catch (BusinessException error) when (error.Code is StudentOnboardingErrorCodes.IdentifierSeriesUnavailable
			or StudentOnboardingErrorCodes.PolicyScopeInvalid)
		{
			data["autoGrNumber"] = null;
			data["autoUnavailableReason"] = error.Data["ReasonCode"]?.ToString() switch
			{
				"SeriesLimitReached" => "The GR series has reached its limit. Ask your administrator to extend it in Onboarding Settings.",
				"ManualOnly" => "The school’s GR rule allows manual entry only.",
				_ => error.Code == StudentOnboardingErrorCodes.PolicyScopeInvalid
					? "Review the matching GR rules in Onboarding Settings before using Auto."
					: "No published GR series covers this student. Ask your administrator to check Onboarding Settings."
			};
		}
		return JsonSerializer.SerializeToElement(data, StudentRecordPayloadService.JsonOptions);
	}

	internal async Task ValidateAsync(Guid studentId, StudentRecordGrNumberPayload input)
	{
		var (student, enrollment) = await GetEligibleAsync(studentId);
		await EnsureNumberAvailableAsync(student.Id, input.GrNumber);
		if (input.Mode == "auto") await VerifyAutoAsync(student.Id, enrollment, input.GrNumber, persist: false);
	}

	internal async Task AssignAsync(Guid studentId, StudentRecordGrNumberPayload input)
	{
		var (student, enrollment) = await GetEligibleAsync(studentId);
		await EnsureNumberAvailableAsync(student.Id, input.GrNumber);
		if (input.Mode == "auto") await VerifyAutoAsync(student.Id, enrollment, input.GrNumber, persist: true);
		student.AssignGrNumber(input.GrNumber, int.TryParse(input.GrNumber, out var numeric) ? numeric : null);
		await students.UpdateAsync(student, false);
	}

	private async Task VerifyAutoAsync(Guid studentId, StudentEnrollment enrollment, string expected, bool persist)
	{
		try
		{
			var series = await GetAutoSeriesAsync(studentId, enrollment, persist);
			if (!string.Equals(await NextAvailableAsync(series, studentId), expected, StringComparison.Ordinal))
				throw new BusinessException(PreviewChanged);
		}
		catch (BusinessException error) when (error.Code is StudentOnboardingErrorCodes.IdentifierSeriesUnavailable
			or StudentOnboardingErrorCodes.PolicyScopeInvalid)
		{
			throw new BusinessException(PreviewChanged);
		}
	}

	private async Task<string> NextAvailableAsync(StudentIdentifierSeries series, Guid studentId)
	{
		var read = new GrCollisionReadContext();
		string number;
		do { (_, number) = series.Allocate(Clock.Now); }
		while (await allocator.IsInUseAsync(number, studentId, null, read));
		if (number.Length > 100) throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		return number;
	}

	private async Task EnsureNumberAvailableAsync(Guid studentId, string number)
	{
		if (await allocator.IsInUseAsync(number, studentId, null, new GrCollisionReadContext()))
			throw new BusinessException(StudentOnboardingErrorCodes.IdentifierAlreadyAllocated);
	}

	private async Task<StudentIdentifierSeries> GetAutoSeriesAsync(Guid studentId, StudentEnrollment enrollment, bool persist)
	{
		var settings = await configuration.GetAsync(enrollment.AcademicYearId);
		if (settings == null || !enrollment.StandardId.HasValue) throw SeriesUnavailable();
		if (OnboardingRuleConfiguration.Text(settings.Root.GetProperty("grNumber"), "assignmentMode") == "manual")
			throw SeriesUnavailable().WithData("ReasonCode", "ManualOnly");
		var standard = await standards.FindAsync(x => x.Id == enrollment.StandardId && x.TenantId == CurrentTenant.Id, includeDetails: true);
		if (standard == null) throw SeriesUnavailable();
		var stage = standard.EducationMediumStage;
		var medium = stage.EducationMedium;
		if (medium.EducationBoard.AcademicYearId != enrollment.AcademicYearId) throw SeriesUnavailable();
		var source = await onboardingCases.FindAsync(x => x.Id == enrollment.OnboardingCaseId &&
			x.TenantId == CurrentTenant.Id && x.StudentId == studentId);
		var rule = settings.ResolveRule("grNumber", medium.EducationBoardId, medium.Id, stage.Id, standard.Id,
			enrollment.DivisionId, source?.GetCampusId(), DateTime.UtcNow);
		return await allocator.GetSeriesAsync(settings, rule, enrollment.AcademicYearId, persist) ?? throw SeriesUnavailable();
	}

	private static BusinessException SeriesUnavailable() => new(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);

	private async Task<(Student Student, StudentEnrollment Enrollment)> GetEligibleAsync(Guid studentId)
	{
		var (student, enrollment) = await ReadAsync(studentId);
		if (student.GrNo.HasValue || !string.IsNullOrWhiteSpace(student.GrNumber)) throw new BusinessException(AlreadyAssigned);
		if (enrollment == null || student.Status != StudentLifeCycleStatusEnum.Enrolled ||
			student.StandardId != enrollment.StandardId || student.DivisionId != enrollment.DivisionId)
			throw new BusinessException(Ineligible);
		return (student, enrollment);
	}

	private async Task<(Student Student, StudentEnrollment? Enrollment)> ReadAsync(Guid studentId)
	{
		var student = await AsyncExecuter.FirstOrDefaultAsync((await students.GetQueryableWithoutAutoIncludesAsync())
			.Where(s => s.Id == studentId && s.TenantId == CurrentTenant.Id)) ?? throw new BusinessException(Ineligible);
		var enrollment = await enrollments.FindAsync(e => e.StudentId == student.Id && e.TenantId == CurrentTenant.Id &&
			e.AcademicYearId == student.AcademicYearId && e.IsActive);
		return (student, enrollment);
	}
}

internal sealed record StudentRecordGrNumberPayload(string Mode, string GrNumber, string? RegisterReference);
