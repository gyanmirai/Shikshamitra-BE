using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.Fines;

public interface IFineAppService
{
	Task<FineDto> CreateAsync(CreateFineDto input);
	Task<ListResultDto<FineDto>> GetAllAsync();
	Task<FineDto> GetByIdAsync(Guid id);
}
