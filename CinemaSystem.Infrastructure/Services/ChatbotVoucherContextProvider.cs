using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Chatbot;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Services;

public sealed class ChatbotVoucherContextProvider : IChatbotVoucherContextProvider
{
    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IVoucherAccessPolicy _voucherAccessPolicy;

    public ChatbotVoucherContextProvider(
        CinemaDbContext dbContext,
        IClock clock,
        IVoucherAccessPolicy voucherAccessPolicy)
    {
        _dbContext = dbContext;
        _clock = clock;
        _voucherAccessPolicy = voucherAccessPolicy;
    }

    public async Task<IReadOnlyList<PublicVoucherChatContext>> GetPublicVouchersAsync(
        CancellationToken cancellationToken)
    {
        var predicate = _voucherAccessPolicy.GetPublicDisclosurePredicate(_clock.UtcNow);
        var vouchers = await _dbContext.Vouchers
            .AsNoTracking()
            .Where(predicate)
            .Select(voucher => new
            {
                voucher.VoucherCode,
                voucher.Title,
                voucher.DiscountType,
                voucher.DiscountValue,
                voucher.MinOrderAmount,
                voucher.EndDate
            })
            .ToListAsync(cancellationToken);

        return vouchers
            .Select(voucher => new PublicVoucherChatContext(
                voucher.VoucherCode,
                voucher.Title,
                string.Equals(
                    voucher.DiscountType,
                    DomainConstants.DiscountType.Percent,
                    StringComparison.OrdinalIgnoreCase)
                    ? $"{voucher.DiscountValue}%"
                    : $"{voucher.DiscountValue:N0} VND",
                voucher.MinOrderAmount,
                voucher.EndDate))
            .ToList();
    }
}
