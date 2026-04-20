using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.FeeCollection;

public class FeeCollectionAppService : ApplicationService, IFeeCollectionAppService
{
	public Task<StudentFeeCollectionListDto> GetStudentsAsync(GetStudentsInput input)
	{
		throw new NotImplementedException();
	}

	public Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input)
	{
		throw new NotImplementedException();
	}

	public Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(GetStudentFeeDetailsInput input)
	{
		throw new NotImplementedException();
	}

	public Task<ComprehensiveFeeDetailsDto> GetComprehensiveFeeDetailsAsync(GetComprehensiveFeeDetailsInput input)
	{
		throw new NotImplementedException();
	}
}
