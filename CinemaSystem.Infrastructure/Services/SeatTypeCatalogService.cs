using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Domain.Utilities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Services;

public sealed class SeatTypeCatalogService : ISeatTypeCatalogService
{
    private readonly CinemaDbContext _dbContext;

    public SeatTypeCatalogService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<IReadOnlyList<SeatTypeResponse>>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SeatTypes.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        var seatTypes = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.TypeName)
            .Select(item => new SeatTypeResponse
            {
                SeatTypeId = item.SeatTypeId,
                TypeName = item.TypeName,
                ExtraFee = item.ExtraFee,
                SeatSpan = item.SeatSpan,
                IsActive = item.IsActive,
                SortOrder = item.SortOrder,
                UsageCount = item.Seats.Count
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<SeatTypeResponse>>.Ok(seatTypes);
    }

    public async Task<ServiceResult<SeatTypeResponse>> CreateAsync(
        UpsertSeatTypeRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(request.TypeName);
        var validationFailure = Validate(request, normalizedName);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        if (await NameExistsAsync(normalizedName, null, cancellationToken))
        {
            return ServiceResult<SeatTypeResponse>.Fail(
                409,
                "Seat type name already exists.",
                "SEAT_TYPE_NAME_DUPLICATE");
        }

        var seatType = new SeatType
        {
            SeatTypeId = IdGenerator.NewId(DomainConstants.EntityIdPrefix.SeatType),
            TypeName = normalizedName,
            ExtraFee = request.ExtraFee,
            SeatSpan = request.SeatSpan,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };

        _dbContext.SeatTypes.Add(seatType);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SeatTypeResponse>.Ok(
            ToResponse(seatType),
            "Seat type created.",
            201);
    }

    public async Task<ServiceResult<SeatTypeResponse>> UpdateAsync(
        string seatTypeId,
        UpsertSeatTypeRequest request,
        CancellationToken cancellationToken)
    {
        var seatType = await _dbContext.SeatTypes
            .FirstOrDefaultAsync(item => item.SeatTypeId == seatTypeId, cancellationToken);
        if (seatType is null)
        {
            return ServiceResult<SeatTypeResponse>.Fail(
                404,
                "Seat type not found.",
                "SEAT_TYPE_NOT_FOUND");
        }

        var normalizedName = NormalizeName(request.TypeName);
        var validationFailure = Validate(request, normalizedName);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        if (await NameExistsAsync(normalizedName, seatTypeId, cancellationToken))
        {
            return ServiceResult<SeatTypeResponse>.Fail(
                409,
                "Seat type name already exists.",
                "SEAT_TYPE_NAME_DUPLICATE");
        }

        seatType.TypeName = normalizedName;
        seatType.ExtraFee = request.ExtraFee;
        seatType.SeatSpan = request.SeatSpan;
        seatType.IsActive = request.IsActive;
        seatType.SortOrder = request.SortOrder;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<SeatTypeResponse>.Ok(
            ToResponse(seatType),
            "Seat type updated.");
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        string seatTypeId,
        CancellationToken cancellationToken)
    {
        var seatType = await _dbContext.SeatTypes
            .FirstOrDefaultAsync(item => item.SeatTypeId == seatTypeId, cancellationToken);
        if (seatType is null)
        {
            return ServiceResult<bool>.Fail(
                404,
                "Seat type not found.",
                "SEAT_TYPE_NOT_FOUND");
        }

        var isInUse = await _dbContext.Seats
            .AsNoTracking()
            .AnyAsync(item => item.SeatTypeId == seatTypeId, cancellationToken);
        if (isInUse)
        {
            return ServiceResult<bool>.Fail(
                409,
                "Seat type is still assigned to one or more seats.",
                "SEAT_TYPE_IN_USE");
        }

        _dbContext.SeatTypes.Remove(seatType);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(
            true,
            "Seat type deleted.");
    }

    public async Task<ServiceResult<int>> MergeAsync(
        string seatTypeId,
        string replacementSeatTypeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(replacementSeatTypeId) ||
            string.Equals(seatTypeId, replacementSeatTypeId, StringComparison.Ordinal))
        {
            return ServiceResult<int>.Fail(
                400,
                "Replacement seat type must be different from the source seat type.",
                "INVALID_REPLACEMENT_SEAT_TYPE");
        }

        var seatTypes = await _dbContext.SeatTypes
            .Where(item => item.SeatTypeId == seatTypeId ||
                item.SeatTypeId == replacementSeatTypeId)
            .ToListAsync(cancellationToken);
        var source = seatTypes.FirstOrDefault(item => item.SeatTypeId == seatTypeId);
        if (source is null)
        {
            return ServiceResult<int>.Fail(
                404,
                "Seat type not found.",
                "SEAT_TYPE_NOT_FOUND");
        }

        var replacement = seatTypes.FirstOrDefault(
            item => item.SeatTypeId == replacementSeatTypeId);
        if (replacement is null)
        {
            return ServiceResult<int>.Fail(
                404,
                "Replacement seat type not found.",
                "REPLACEMENT_SEAT_TYPE_NOT_FOUND");
        }

        if (!replacement.IsActive ||
            source.SeatSpan != replacement.SeatSpan ||
            source.ExtraFee != replacement.ExtraFee)
        {
            return ServiceResult<int>.Fail(
                409,
                "Seat types must have the same seat span and extra fee before merging.",
                "SEAT_TYPE_MERGE_INCOMPATIBLE");
        }

        var seats = await _dbContext.Seats
            .Where(item => item.SeatTypeId == seatTypeId)
            .ToListAsync(cancellationToken);
        foreach (var seat in seats)
        {
            seat.SeatTypeId = replacementSeatTypeId;
        }

        _dbContext.SeatTypes.Remove(source);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<int>.Ok(
            seats.Count,
            $"Seat type merged into {replacement.TypeName}.");
    }

    private async Task<bool> NameExistsAsync(
        string typeName,
        string? excludedSeatTypeId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.SeatTypes
            .AsNoTracking()
            .AnyAsync(
                item => item.TypeName == typeName &&
                    item.SeatTypeId != excludedSeatTypeId,
                cancellationToken);
    }

    private static ServiceResult<SeatTypeResponse>? Validate(
        UpsertSeatTypeRequest request,
        string normalizedName)
    {
        if (normalizedName.Length is < 1 or > 100 ||
            request.ExtraFee < 0 ||
            request.SeatSpan is < 1 or > 2 ||
            request.SortOrder < 0)
        {
            return ServiceResult<SeatTypeResponse>.Fail(
                400,
                "Seat type fields are invalid.",
                "INVALID_SEAT_TYPE");
        }

        return null;
    }

    private static string NormalizeName(string value) =>
        value.Trim().ToUpperInvariant();

    private static SeatTypeResponse ToResponse(SeatType seatType)
    {
        return new SeatTypeResponse
        {
            SeatTypeId = seatType.SeatTypeId,
            TypeName = seatType.TypeName,
            ExtraFee = seatType.ExtraFee,
            SeatSpan = seatType.SeatSpan,
            IsActive = seatType.IsActive,
            SortOrder = seatType.SortOrder,
            UsageCount = seatType.Seats.Count
        };
    }
}
