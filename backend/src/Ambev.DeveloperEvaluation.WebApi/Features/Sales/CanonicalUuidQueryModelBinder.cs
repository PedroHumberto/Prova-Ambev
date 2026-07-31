using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.RegularExpressions;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class CanonicalUuidQueryAttribute(string name) : Attribute, IBinderTypeProviderMetadata, IModelNameProvider
{
    public Type BinderType => typeof(CanonicalUuidQueryModelBinder);
    public BindingSource BindingSource => BindingSource.Query;
    public string Name { get; } = name;
}

public sealed class CanonicalUuidQueryModelBinder : IModelBinder
{
    private const string ErrorMessage =
        "Value must be a non-empty lowercase UUID in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.";
    private static readonly Regex FormatPattern = new(
        "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

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
            || !Guid.TryParseExact(value, "D", out var id)
            || id == Guid.Empty)
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, ErrorMessage);
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(id);
        if (bindingContext.ModelState.TryGetValue(bindingContext.ModelName, out var modelStateEntry)
            && modelStateEntry.Errors.Count == 0
            && modelStateEntry.ValidationState != ModelValidationState.Invalid)
        {
            bindingContext.ModelState.MarkFieldValid(bindingContext.ModelName);
        }

        return Task.CompletedTask;
    }
}
