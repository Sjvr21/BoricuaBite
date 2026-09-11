using System.ComponentModel.DataAnnotations;

namespace BoricuaBite.Api.Endpoints;

internal static class RequestValidation
{
    public static Dictionary<string, string[]> Errors(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results.SelectMany(x => x.MemberNames.Select(member => (member, x.ErrorMessage)))
            .GroupBy(x => x.member).ToDictionary(x => x.Key, x => x.Select(y => y.ErrorMessage!).ToArray());
    }

    public static Dictionary<string, string[]> PageErrors(int? page, int? pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page is < 1 or > 10000) errors["page"] = ["Page must be between 1 and 10000."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
        return errors;
    }
}
