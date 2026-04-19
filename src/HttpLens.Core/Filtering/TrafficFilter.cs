using HttpLens.Core.Models;

namespace HttpLens.Core.Filtering;

/// <summary>
/// Applies server-side filtering to a collection of <see cref="HttpTrafficRecord"/> objects.
/// All criteria are combined with AND logic — a record must satisfy every non-empty filter.
/// </summary>
public static class TrafficFilter
{
    /// <summary>
    /// Filters the given records based on the provided criteria.
    /// Returns all records when the criteria is empty.
    /// </summary>
    /// <param name="records">The records to filter.</param>
    /// <param name="criteria">The filter criteria. All non-empty fields are combined with AND logic.</param>
    /// <returns>The subset of records matching all criteria.</returns>
    public static IReadOnlyList<HttpTrafficRecord> Apply(
        IReadOnlyList<HttpTrafficRecord> records,
        TrafficFilterCriteria criteria)
    {
        if (criteria.IsEmpty)
            return records;

        var result = new List<HttpTrafficRecord>();

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];

            if (!string.IsNullOrEmpty(criteria.Method) &&
                !string.Equals(record.RequestMethod, criteria.Method, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(criteria.Status) &&
                (record.ResponseStatusCode is null ||
                 !record.ResponseStatusCode.Value.ToString().StartsWith(criteria.Status, StringComparison.Ordinal)))
                continue;

            if (!string.IsNullOrEmpty(criteria.Host) &&
                !record.RequestUri.Contains(criteria.Host, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(criteria.Search) &&
                !record.RequestUri.Contains(criteria.Search, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(record);
        }

        return result;
    }
}
