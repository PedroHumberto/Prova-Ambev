using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ambev.DeveloperEvaluation.WebApi.Common;

[AttributeUsage(AttributeTargets.Method)]
public sealed class StrictQueryParametersAttribute(params string[] allowedParameters) : ActionFilterAttribute
{
    private readonly HashSet<string> _allowedParameters = new(allowedParameters, StringComparer.Ordinal);

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var failures = new List<ValidationFailure>();

        foreach (var parameter in context.HttpContext.Request.Query)
        {
            if (!_allowedParameters.Contains(parameter.Key))
            {
                failures.Add(new ValidationFailure(parameter.Key, "Query parameter is not supported."));
                continue;
            }

            if (parameter.Value.Count != 1)
                failures.Add(new ValidationFailure(parameter.Key, "Query parameter must be supplied only once."));
            else if (string.IsNullOrEmpty(parameter.Value[0]))
                failures.Add(new ValidationFailure(parameter.Key, "Query parameter must not be empty."));
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);
    }
}
