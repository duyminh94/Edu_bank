using MiniBankDTOs;

namespace MiniBankWebApi.Core.Services
{
    public interface ITransferService
    {
        Task<OtpSentDto> RequestAsync(int fromAccountId, TransferRequestDto dto);
        Task<TransferResultDto> ConfirmAsync(int fromAccountId, ConfirmOtpDto dto);
        Task<List<TransferHistoryDto>> HistoryAsync(int accountId);
    }
}
