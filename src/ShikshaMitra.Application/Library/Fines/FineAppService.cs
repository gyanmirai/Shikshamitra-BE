using ShikshaMitra.Library.BooksAssignments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.Fines;

public class FineAppService(
	IRepository<Fine, Guid> fineRepository,
	IRepository<BooksAssignment, Guid> booksAssignmentRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, IFineAppService
{
	public async Task<FineDto> CreateAsync(CreateFineDto createFineDto)
	{
		var fine = new Fine(
			guidGenerator.Create(),
			CurrentTenant.Id,
			createFineDto.BooksAssignmentId,
			createFineDto.FineAmount,
			createFineDto.PaymentMode,
			createFineDto.PenaltyAmount,
			createFineDto.PenaltyReason,
			createFineDto.BankAccountNumber,
			createFineDto.IFSCCode,
			createFineDto.AccountHolderName
		);

		await fineRepository.InsertAsync(fine, autoSave: true);
		return ObjectMapper.Map<Fine, FineDto>(fine);
	}

	public async Task<ListResultDto<FineDto>> GetAllAsync()
	{
		var fines = await fineRepository.GetListAsync();
		var dtoList = ObjectMapper.Map<List<Fine>, List<FineDto>>(fines);
		return new ListResultDto<FineDto>(dtoList);
	}
	public async Task<FineDto> GetByIdAsync(Guid id)
	{
		var fine = await fineRepository.GetAsync(id);
		return ObjectMapper.Map<Fine, FineDto>(fine);
	}
}
