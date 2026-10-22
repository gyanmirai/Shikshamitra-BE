using System;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionApplicationNumberingService(IAdmissionApplicationNumberingRepository repository,
	IAdmissionReferenceSequenceRepository legacySequences, IRepository<AdmissionCycle, Guid> cycles,
	IRepository<AcademicYear, Guid> years) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionApplicationNumberingDto> GetAsync() => Map(await repository.GetCurrentAsync(), await repository.HasIssuedAsync());
	public async Task<AdmissionApplicationNumberingDto> SaveAsync(SaveAdmissionApplicationNumberingDto input)
	{
		await repository.LockAsync();
		var current = await repository.GetCurrentAsync();
		if ((current?.Version ?? 0) != input.ExpectedVersion)
			throw new BusinessException(AdmissionNumberingErrorCodes.Changed, "Numbering settings changed. Close and reopen this dialog before saving.");
		var issued = await repository.HasIssuedAsync();
		if (issued && current != null && (current.StartingNumber != input.StartingNumber || current.ResetPolicy != input.ResetPolicy))
			throw new BusinessException(AdmissionNumberingErrorCodes.SequenceLocked, "Starting number and reset policy cannot change after a number is issued.");
		var rule = new AdmissionApplicationNumberingRule(GuidGenerator.Create(), CurrentTenant.Id, input.ExpectedVersion + 1,
			input.Prefix, input.Separator, input.Suffix, input.YearFormat, input.ResetPolicy, input.Digits, input.StartingNumber);
		await repository.InsertRuleAsync(rule);
		return Map(rule, issued);
	}
	public async Task<string> AllocateAsync(Guid admissionCycleId)
	{
		await repository.LockAsync();
		var rule = await repository.GetCurrentAsync();
		var calendarYear = Clock.Now.ToUniversalTime().Year;
		if (rule == null)
		{
			var legacy = AdmissionReferenceNumber.Format(AdmissionReferenceKind.Application, calendarYear,
				await legacySequences.IncrementAndGetAsync(calendarYear, AdmissionReferenceKind.Application));
			return legacy;
		}
		var cycle = await cycles.GetAsync(admissionCycleId);
		var year = await years.GetAsync(cycle.AcademicYearId);
		var yearText = rule.YearText(calendarYear, year.StartDate.Year, year.EndDate.Year);
		var scope = rule.ScopeKey(calendarYear, year.Id);
		var sequence = await repository.TakeNextAsync(rule, scope, yearText);
		var number = rule.Format(sequence, yearText);
		// Database unique index remains the final invariant. No application is renumbered.
		if (await repository.NumberExistsAsync(number))
			throw new BusinessException(AdmissionNumberingErrorCodes.Invalid, "The generated application number is already in use. Contact your administrator.");
		return number;
	}
	public async Task<AdmissionApplicationNumberPreviewDto> PreviewAsync(PreviewAdmissionApplicationNumberingDto input)
	{
		await repository.LockAsync();
		var current = await repository.GetCurrentAsync();
		if ((current?.Version ?? 0) != input.ExpectedVersion)
			throw new BusinessException(AdmissionNumberingErrorCodes.Changed, "Numbering settings changed. Close and reopen this dialog before previewing.");
		var rule = new AdmissionApplicationNumberingRule(Guid.Empty, CurrentTenant.Id, input.ExpectedVersion + 1,
			input.Prefix, input.Separator, input.Suffix, input.YearFormat, input.ResetPolicy, input.Digits, input.StartingNumber);
		var calendarYear = Clock.Now.ToUniversalTime().Year;
		AcademicYear? year = null;
		if (input.AcademicYearId.HasValue) year = await years.GetAsync(input.AcademicYearId.Value);
		if (rule.YearFormat == AdmissionNumberYearFormat.AcademicYear && year == null)
			throw new BusinessException(AdmissionNumberingErrorCodes.Invalid, "Select an academic year to preview this format.");
		var yearText = rule.YearText(calendarYear, year?.StartDate.Year ?? calendarYear, year?.EndDate.Year ?? calendarYear + 1);
		var next = await repository.PeekNextAsync(rule, rule.ScopeKey(calendarYear, year?.Id ?? Guid.Empty), yearText);
		return new() { NextNumber = rule.Format(next, yearText), NextSequence = next };
	}
	private static AdmissionApplicationNumberingDto Map(AdmissionApplicationNumberingRule? rule, bool issued) => rule == null ? new() : new()
	{
		ExpectedVersion = rule.Version, IsConfigured = true, SequenceLocked = issued,
		Prefix = rule.Prefix, Separator = rule.Separator, Suffix = rule.Suffix, YearFormat = rule.YearFormat,
		ResetPolicy = rule.ResetPolicy, Digits = rule.Digits, StartingNumber = rule.StartingNumber
	};
}
