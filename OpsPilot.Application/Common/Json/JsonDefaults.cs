using System.Text.Json;

namespace OpsPilot.Application.Common.Json;

/// <summary>
/// The serializer settings used for everything that crosses a process boundary.
/// </summary>
/// <remarks>
/// One place, because the two sides of every wire format have to agree: a payload written with
/// one set of rules and read with another fails at runtime rather than at compile time, and the
/// failure lands on a message in a queue rather than on a line of code.
/// </remarks>
public static class JsonDefaults
{
    /// <summary>
    /// Web defaults: camelCase names and case-insensitive reads. That is the shape the JSON in
    /// this system is written in, and it is what a producer outside the system is likely to use.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
