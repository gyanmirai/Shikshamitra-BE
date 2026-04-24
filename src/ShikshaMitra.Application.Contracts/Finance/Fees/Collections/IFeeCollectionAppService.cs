using ShikshaMitra.Finance.Fees.Collections.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.Collections;

public interface IFeeCollectionAppService
{
	Task<StudentFeeCollectionListDto> GetStudentsAsync(GetStudentsInput input);
	Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input);
	Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(GetStudentFeeDetailsInput input);
	Task<ComprehensiveFeeDetailsDto> GetComprehensiveFeeDetailsAsync(GetComprehensiveFeeDetailsInput input);
	Task<FeeCollectionDetailsDto> GetFeeCollectionDetailsAsync(Guid studentFeeId);
}
