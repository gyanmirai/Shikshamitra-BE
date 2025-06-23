using ShikshaMitra.Library.BooksAssignments;
using ShikshaMitra.Library.Members;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.Fines;

public class FineAppService(
	IRepository<Fine, Guid> fineRepository,
	IRepository<BooksAssignment, Guid> booksAssignmentRepository,
	IRepository<Member, Guid> memberRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, IFineAppService
{
	public async Task<FineDto> CreateAsync(CreateFineDto createFineDto)
	{
		var member = await memberRepository.GetAsync(createFineDto.MemberId)
			?? throw new BusinessException("Invalid Member ID");

		var assignment = await booksAssignmentRepository.GetAsync(createFineDto.BooksAssignmentId)
			?? throw new BusinessException("Invalid Assignment ID");

		var returnDate = assignment.ReturnDate ?? Clock.Now;

		int lateDays = (returnDate.Date - assignment.DueDate.Date).Days;
		lateDays = Math.Max(0, lateDays);

		    decimal finePerDay = member.Role.ToString() == "Student" ? 5 : 2;

    
		decimal totalFine = lateDays * finePerDay;

		var fine = new Fine(
			guidGenerator.Create(),
			CurrentTenant.Id,
			createFineDto.BooksAssignmentId,
			createFineDto.MemberId,
			totalFine,
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
