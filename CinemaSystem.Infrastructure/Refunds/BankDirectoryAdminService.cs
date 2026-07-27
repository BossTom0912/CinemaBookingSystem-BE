using System.Net;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Refunds;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Refunds;

public sealed class BankDirectoryAdminService : IBankDirectoryAdminService
{
    private readonly CinemaDbContext _db;
    private readonly IClock _clock;

    public BankDirectoryAdminService(CinemaDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ServiceResult<IReadOnlyList<BankDirectoryResponse>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var banks = await _db.BankDirectories
            .AsNoTracking()
            .OrderBy(item => item.ShortName)
            .Select(item => new BankDirectoryResponse
            {
                BankCode = item.BankCode,
                BankBin = item.BankBin,
                ShortName = item.ShortName,
                FullName = item.FullName,
                IsActive = item.IsActive,
                SupportsAccountInquiry = item.SupportsAccountInquiry,
                SupportsPayout = item.SupportsPayout,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<BankDirectoryResponse>>.Ok(banks);
    }

    public async Task<ServiceResult<BankDirectoryResponse>> UpsertAsync(
        string bankCode,
        UpsertBankDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedCode = bankCode.Trim().ToUpperInvariant();
        var normalizedBin = request.BankBin.Trim();
        var normalizedShortName = request.ShortName.Trim();
        var normalizedFullName = request.FullName.Trim();
        if (normalizedCode.Length is < 1 or > RefundContractConstants.BankCodeMaxLength
            || normalizedBin.Length is < 1 or > RefundContractConstants.BankBinMaxLength
            || normalizedShortName.Length is < 1 or > RefundContractConstants.BankShortNameMaxLength
            || normalizedFullName.Length is < 1 or > RefundContractConstants.BankFullNameMaxLength)
        {
            return ServiceResult<BankDirectoryResponse>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Bank directory fields are invalid.",
                DomainConstants.RefundErrorCode.InvalidBankDirectoryEntry);
        }

        var duplicateBin = await _db.BankDirectories
            .AsNoTracking()
            .AnyAsync(
                item => item.BankBin == normalizedBin && item.BankCode != normalizedCode,
                cancellationToken);
        if (duplicateBin)
        {
            return ServiceResult<BankDirectoryResponse>.Fail(
                (int)HttpStatusCode.Conflict,
                "Bank BIN is already assigned to another bank.",
                DomainConstants.RefundErrorCode.BankBinDuplicate);
        }

        var existingBank = await _db.BankDirectories
            .FirstOrDefaultAsync(item => item.BankCode == normalizedCode, cancellationToken);
        var isNew = existingBank is null;
        BankDirectory bank;
        if (existingBank is null)
        {
            bank = new BankDirectory
            {
                BankCode = normalizedCode,
                CreatedAt = _clock.UtcNow
            };
            _db.BankDirectories.Add(bank);
        }
        else
        {
            bank = existingBank;
            bank.UpdatedAt = _clock.UtcNow;
        }

        bank.BankBin = normalizedBin;
        bank.ShortName = normalizedShortName;
        bank.FullName = normalizedFullName;
        bank.IsActive = request.IsActive;
        bank.SupportsAccountInquiry = request.SupportsAccountInquiry;
        bank.SupportsPayout = request.SupportsPayout;

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<BankDirectoryResponse>.Ok(
            ToResponse(bank),
            isNew ? "Bank directory entry created." : "Bank directory entry updated.",
            isNew ? (int)HttpStatusCode.Created : (int)HttpStatusCode.OK);
    }

    private static BankDirectoryResponse ToResponse(BankDirectory bank)
    {
        return new BankDirectoryResponse
        {
            BankCode = bank.BankCode,
            BankBin = bank.BankBin,
            ShortName = bank.ShortName,
            FullName = bank.FullName,
            IsActive = bank.IsActive,
            SupportsAccountInquiry = bank.SupportsAccountInquiry,
            SupportsPayout = bank.SupportsPayout,
            CreatedAt = bank.CreatedAt,
            UpdatedAt = bank.UpdatedAt
        };
    }
}
