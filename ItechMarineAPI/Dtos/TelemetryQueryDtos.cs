namespace ItechMarineAPI.Dtos;

public record TelemetryQueryDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string[]? Keys,
    Guid? DeviceId,
    int Page = 1,
    int PageSize = 100
);

public record TelemetryItemDto(
    long Id, Guid BoatId, Guid? DeviceId, string Key, string Value, DateTime CreatedAt
);

public record PagedResult<T>(
    int Page, int PageSize, long Total, IReadOnlyList<T> Items
);
