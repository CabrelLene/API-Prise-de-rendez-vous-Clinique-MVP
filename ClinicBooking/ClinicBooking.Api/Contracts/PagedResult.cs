namespace ClinicBooking.Api.Contracts;

public sealed record PagedResult<T>(
    int Page,
    int PageSize,
    long Total,
    IReadOnlyList<T> Items
);
