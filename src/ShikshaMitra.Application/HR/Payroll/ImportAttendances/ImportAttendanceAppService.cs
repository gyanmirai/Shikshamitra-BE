using AutoMapper.Internal.Mappers;
using ShikshaMitra.HR.Payroll.PayrollStructures;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace ShikshaMitra.HR.Payroll.ImportAttendances;

public class ImportAttendanceAppService : IImportAttendanceAppService
{
	//public async Task<List<PayrollStructureDto>> GetImportAttendancesByEducationMediumStageId(
	//	Guid educationMediumStageId)
	//{
	//	IQueryable<PayrollStructure> query = await payrollStructureRepository.GetQueryableAsync();
	//	query = query.Where(x => x.EducationMediumStageId == educationMediumStageId);
	//	return ObjectMapper.Map<List<PayrollStructure>, List<PayrollStructureDto>>(
	//		await AsyncExecuter.ToListAsync(query));
	//}
}
