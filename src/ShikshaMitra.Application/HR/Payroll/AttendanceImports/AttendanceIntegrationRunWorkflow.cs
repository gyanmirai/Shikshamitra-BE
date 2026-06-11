using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceIntegrationRunWorkflow(
	IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository,
	IRepository<AttendanceImportRow, Guid> attendanceImportRowRepository,
	AttendanceIntegrationRunAttendanceWriter attendanceWriter)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendanceIntegrationRunResponseDto> RecordIntegrationRunAsync(AttendanceIntegrationRunRequestDto input)
	{
		if (input.Rows == null || input.Rows.Count == 0)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportNoValidRows);
		}

		var batchId = GuidGenerator.Create();
		var parsedRows = input.Rows
			.Select(x => new ParsedIntegrationRow(x, AttendanceIntegrationRunParsing.ParseRowDate(x.AttendanceDate)))
			.ToList();
		var monthYear = ResolvePayrollMonth(parsedRows);
		var batch = CreateBatch(input, batchId, monthYear);

		await attendanceImportBatchRepository.InsertAsync(batch, autoSave: true);

		var outcome = await attendanceWriter.UpsertAttendanceAndBuildImportRowsAsync(parsedRows, batchId);

		if (outcome.ImportRows.Count > 0)
		{
			await attendanceImportRowRepository.InsertManyAsync(outcome.ImportRows, autoSave: false);
		}

		await MarkBatchConfirmedAsync(batchId, batch.RunStartedAt, input.Rows.Count, outcome.Created, outcome.Replaced);

		return new AttendanceIntegrationRunResponseDto
		{
			BatchId = batchId,
			CreatedCount = outcome.Created,
			ReplacedCount = outcome.Replaced
		};
	}

	private AttendanceImportBatch CreateBatch(AttendanceIntegrationRunRequestDto input, Guid batchId, string monthYear)
	{
		var userId = CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		var userName = CurrentUser.UserName ?? "system";
		var payrollMonthApi = AttendanceIntegrationRunParsing.ToApiMonthYear(monthYear);
		var payrollMonthStored = string.IsNullOrWhiteSpace(payrollMonthApi)
			? null
			: (payrollMonthApi.Length > 20 ? payrollMonthApi[..20] : payrollMonthApi);

		return new AttendanceImportBatch(
			id: batchId,
			tenantId: CurrentTenant.Id,
			fileName: string.IsNullOrWhiteSpace(input.FileName) ? "integration-run" : input.FileName[..Math.Min(input.FileName.Length, 255)],
			importedByUserId: userId,
			importedByName: userName.Length > 128 ? userName[..128] : userName,
			importedAt: Clock.Now,
			payrollMonthYear: monthYear,
			payrollMonth: payrollMonthStored,
			totalRows: input.Rows.Count,
			validRows: input.Rows.Count,
			invalidRows: 0,
			createCount: 0,
			replaceCount: 0,
			errorsSummary: [],
			warningsSummary: [],
			runSource: input.RunSource,
			externalCorrelationId: AttendanceIntegrationRunParsing.Truncate(input.ExternalCorrelationId, 128),
			integrationProvider: AttendanceIntegrationRunParsing.Truncate(input.IntegrationProvider, 128),
			runStartedAt: Clock.Now,
			runFinishedAt: null,
			isBatchLocked: false);
	}

	private static string ResolvePayrollMonth(List<ParsedIntegrationRow> parsedRows)
	{
		var firstDate = parsedRows[0].AttendanceDate;
		if (parsedRows.Any(x => x.AttendanceDate.Year != firstDate.Year || x.AttendanceDate.Month != firstDate.Month))
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportMultiplePayrollMonths);
		}

		return $"{firstDate:yyyy-MM}";
	}

	private async Task MarkBatchConfirmedAsync(Guid batchId, DateTime? runStartedAt, int totalRows, int created, int replaced)
	{
		var persisted = await attendanceImportBatchRepository.FindAsync(batchId);
		if (persisted != null)
		{
			persisted.SetRunTiming(runStartedAt, Clock.Now);
			persisted.MarkConfirmed(totalRows, created, replaced, Clock.Now);
			await attendanceImportBatchRepository.UpdateAsync(persisted, autoSave: true);
		}
	}

}
