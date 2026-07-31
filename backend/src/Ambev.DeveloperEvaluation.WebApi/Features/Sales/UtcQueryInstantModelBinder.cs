using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class UtcQueryInstantAttribute(string name) : Attribute, IBinderTypeProviderMetadata, IModelNameProvider
{
    public Type BinderType => typeof(UtcQueryInstantModelBinder);
    public BindingSource BindingSource => BindingSource.Query;
    public string Name { get; } = name;
}

public sealed class UtcQueryInstantModelBinder : IModelBinder
{
    private const string ErrorMessage =
        "Value must be a UTC instant in the format yyyy-MM-ddTHH:mm:ss[.fffffff]Z.";
    private static readonly Regex FormatPattern = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly string[] AcceptedFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"
    ];

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);
        if (valueProviderResult.Length != 1)
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                "Query parameter must be supplied only once.");
            return Task.CompletedTask;
        }

        var value = valueProviderResult.FirstValue;
        if (value is null
            || !FormatPattern.IsMatch(value)
            || !DateTimeOffset.TryParseExact(
                value,
                AcceptedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var instant))
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, ErrorMessage);
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(instant);
        if (bindingContext.ModelState.TryGetValue(bindingContext.ModelName, out var modelStateEntry)
            && modelStateEntry.Errors.Count == 0
            && modelStateEntry.ValidationState != ModelValidationState.Invalid)
        {
            bindingContext.ModelState.MarkFieldValid(bindingContext.ModelName);
        }

        return Task.CompletedTask;
    }
}
