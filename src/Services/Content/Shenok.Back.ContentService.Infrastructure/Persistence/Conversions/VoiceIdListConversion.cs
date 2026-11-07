using System.Text.Json;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Conversions;

internal static class VoiceIdListConversion
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static readonly ValueConverter<IReadOnlyList<VoiceId>, string> Converter = new(
        list => Write(list),
        json => Read(json));

    public static readonly ValueComparer<IReadOnlyList<VoiceId>> Comparer = new(
        (left, right) => Same(left, right),
        list => Hash(list),
        list => Copy(list));

    private static string Write(IReadOnlyList<VoiceId> list)
    {
        if (list.Count > AudioLimits.MaxVoicesPerPage)
            throw new InvalidOperationException("A page cannot store that many voices.");

        var values = new Guid[list.Count];
        var seen = new HashSet<Guid>(list.Count);
        for (var index = 0; index < list.Count; index++)
        {
            var value = list[index].Value;
            if (!seen.Add(value))
                throw new InvalidOperationException("Voice order contains a duplicate.");

            values[index] = value;
        }

        return JsonSerializer.Serialize(values, Json);
    }

    private static IReadOnlyList<VoiceId> Read(string json)
    {
        var values = JsonSerializer.Deserialize<Guid[]>(json, Json)
            ?? throw new InvalidOperationException("Voice order could not be read.");
        if (values.Length > AudioLimits.MaxVoicesPerPage)
            throw new InvalidOperationException("A page cannot store that many voices.");

        var list = new List<VoiceId>(values.Length);
        var seen = new HashSet<Guid>(values.Length);
        foreach (var value in values)
        {
            if (!seen.Add(value))
                throw new InvalidOperationException("Voice order contains a duplicate.");

            list.Add(VoiceId.From(value));
        }

        return list;
    }

    private static bool Same(IReadOnlyList<VoiceId>? left, IReadOnlyList<VoiceId>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Count != right.Count)
            return false;

        for (var index = 0; index < left.Count; index++)
        {
            if (left[index] != right[index])
                return false;
        }

        return true;
    }

    private static int Hash(IReadOnlyList<VoiceId> list)
    {
        var hash = new HashCode();
        foreach (var id in list)
            hash.Add(id.Value);

        return hash.ToHashCode();
    }

    private static IReadOnlyList<VoiceId> Copy(IReadOnlyList<VoiceId> list)
    {
        var copy = new List<VoiceId>(list.Count);
        foreach (var id in list)
            copy.Add(VoiceId.From(id.Value));

        return copy;
    }
}
