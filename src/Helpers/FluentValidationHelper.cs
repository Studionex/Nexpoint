
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;

namespace Nexpoint.Helpers;

public class ValidationErrorExtractor
{
  private readonly IServiceProvider _serviceProvider;

  public ValidationErrorExtractor(IServiceProvider serviceProvider)
  {
    _serviceProvider = serviceProvider;
  }

  /// <summary>
  /// Finds a validator for the given command/query type and extracts validation errors
  /// </summary>
  public Dictionary<string, List<string>>? GetValidationErrors<TCommand>()
  {
    var validatorType = typeof(IValidator<TCommand>);

    if (_serviceProvider.GetService(validatorType) is not IValidator validator)
      return null;

    return ExtractValidationRulesFromDescriptor(validator);
  }

  /// <summary>
  /// Finds a validator by command/query type name and extracts validation errors
  /// </summary>
  public Dictionary<string, List<string>>? GetValidationErrorsByTypeName(string commandTypeName)
  {
    var assembly = typeof(ValidationErrorExtractor).Assembly;
    var commandType = assembly.GetTypes()
        .FirstOrDefault(t => t.Name == commandTypeName);

    if (commandType == null)
      return null;

    var validatorType = typeof(IValidator<>).MakeGenericType(commandType);

    if (_serviceProvider.GetService(validatorType) is not IValidator validator)
      return null;

    return ExtractValidationRulesFromDescriptor(validator);
  }

  /// <summary>
  /// Finds a validator by endpoint name (e.g., "CreateCampaignEndpoint") and extracts validation errors
  /// </summary>
  public Dictionary<string, List<string>>? GetValidationErrorsByEndpointName(string endpointName)
  {
    // Convert endpoint name to command name
    // CreateCampaignEndpoint -> CreateCampaignCommand
    var commandName = endpointName
        .Replace("Endpoint", "Command");

    return GetValidationErrorsByTypeName(commandName);
  }

  private Dictionary<string, List<string>> ExtractValidationRulesFromDescriptor(IValidator validator)
  {
    var errors = new Dictionary<string, List<string>>();

    try
    {
      var descriptor = validator.CreateDescriptor();
      var members = descriptor.GetMembersWithValidators();

      foreach (var member in members)
      {
        var rules = descriptor.GetRulesForMember(member.Key);
        var errorMessages = new List<string>();

        foreach (var rule in rules)
        {
          foreach (var component in rule.Components)
          {
            var errorMessage = GetErrorMessage(component, member.Key);
            if (!string.IsNullOrEmpty(errorMessage))
            {
              errorMessages.Add(errorMessage);
            }
          }
        }

        if (errorMessages.Any())
        {
          errors[member.Key] = errorMessages.Distinct().ToList();
        }
      }
    }
    catch (Exception ex)
    {
      // Log if needed
      Console.WriteLine($"Could not extract validation rules: {ex.Message}");
    }

    return errors;
  }

  private string GetErrorMessage(IRuleComponent component, string propertyName)
  {
    try
    {
      var errorMessageSource = component.GetType()
          .GetProperty("ErrorMessageSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
          ?.GetValue(component);

      if (errorMessageSource != null)
      {
        var getStringMethod = errorMessageSource.GetType().GetMethod("GetString");
        if (getStringMethod != null)
        {
          var message = getStringMethod.Invoke(errorMessageSource, new object[] { null }) as string;

          if (!string.IsNullOrEmpty(message))
          {
            message = message
                .Replace("{PropertyName}", propertyName)
                .Replace("{PropertyValue}", "value");

            if (component.Validator is ILengthValidator lengthValidator)
            {
              message = message
                  .Replace("{MinLength}", lengthValidator.Min.ToString())
                  .Replace("{MaxLength}", lengthValidator.Max.ToString())
                  .Replace("{TotalLength}", lengthValidator.Max.ToString());
            }
            else if (component.Validator is IComparisonValidator comparisonValidator)
            {
              var comparisonValue = comparisonValidator.ValueToCompare;
              message = message
                  .Replace("{ComparisonValue}", comparisonValue?.ToString() ?? "")
                  .Replace("{ComparisonProperty}", comparisonValidator.MemberToCompare?.Name ?? "");
            }
            else
            {
              message = ReplacePlaceholders(message, component.Validator);
            }

            return message;
          }
        }
      }

      return GenerateFallbackMessage(component.Validator, propertyName);
    }
    catch
    {
      return GenerateFallbackMessage(component.Validator, propertyName);
    }
  }

  private string ReplacePlaceholders(string message, IPropertyValidator validator)
  {
    var validatorType = validator.GetType();

    var propertyMappings = new Dictionary<string, string[]>
    {
      ["{MinValue}"] = new[] { "Min", "Minimum", "MinValue" },
      ["{MaxValue}"] = new[] { "Max", "Maximum", "MaxValue" },
      ["{From}"] = new[] { "From", "MinValue" },
      ["{To}"] = new[] { "To", "MaxValue" },
      ["{MinLength}"] = new[] { "MinLength", "Min" },
      ["{MaxLength}"] = new[] { "MaxLength", "Max" },
      ["{Length}"] = new[] { "Length", "ExactLength" },
      ["{RegularExpression}"] = new[] { "Expression", "Pattern" },
      ["{Scale}"] = new[] { "Scale" },
      ["{Precision}"] = new[] { "Precision" }
    };

    foreach (var mapping in propertyMappings)
    {
      if (message.Contains(mapping.Key))
      {
        foreach (var propName in mapping.Value)
        {
          var prop = validatorType.GetProperty(propName);
          if (prop != null)
          {
            var value = prop.GetValue(validator);
            message = message.Replace(mapping.Key, value?.ToString() ?? "");
            break;
          }
        }
      }
    }

    return message;
  }

  private string GenerateFallbackMessage(IPropertyValidator validator, string propertyName)
  {
    var validatorName = validator.Name;

    return validatorName switch
    {
      "NotEmptyValidator" => $"'{propertyName}' must not be empty.",
      "NotNullValidator" => $"'{propertyName}' must not be empty.",
      "EmailValidator" => $"'{propertyName}' is not a valid email address.",
      "MaximumLengthValidator" => $"'{propertyName}' must be {GetValidatorProperty(validator, "Max")} characters or fewer.",
      "MinimumLengthValidator" => $"'{propertyName}' must be at least {GetValidatorProperty(validator, "Min")} characters.",
      "ExactLengthValidator" => $"'{propertyName}' must be {GetValidatorProperty(validator, "Length")} characters in length.",
      "LengthValidator" => $"'{propertyName}' must be between {GetValidatorProperty(validator, "Min")} and {GetValidatorProperty(validator, "Max")} characters.",
      "GreaterThanValidator" => $"'{propertyName}' must be greater than '{GetValidatorProperty(validator, "ValueToCompare")}'.",
      "GreaterThanOrEqualValidator" => $"'{propertyName}' must be greater than or equal to '{GetValidatorProperty(validator, "ValueToCompare")}'.",
      "LessThanValidator" => $"'{propertyName}' must be less than '{GetValidatorProperty(validator, "ValueToCompare")}'.",
      "LessThanOrEqualValidator" => $"'{propertyName}' must be less than or equal to '{GetValidatorProperty(validator, "ValueToCompare")}'.",
      "RegularExpressionValidator" => $"'{propertyName}' is not in the correct format.",
      "CreditCardValidator" => $"'{propertyName}' is not a valid credit card number.",
      "EnumValidator" => $"'{propertyName}' has a range of values which does not include the value provided.",
      "EmptyValidator" => $"'{propertyName}' must be empty.",
      "NullValidator" => $"'{propertyName}' must be empty.",
      "ExclusiveBetweenValidator" => $"'{propertyName}' must be between {GetValidatorProperty(validator, "From")} and {GetValidatorProperty(validator, "To")} (exclusive).",
      "InclusiveBetweenValidator" => $"'{propertyName}' must be between {GetValidatorProperty(validator, "From")} and {GetValidatorProperty(validator, "To")}.",
      "ScalePrecisionValidator" => $"'{propertyName}' must not be more than {GetValidatorProperty(validator, "Scale")} decimal places.",
      "PredicateValidator" => $"'{propertyName}' does not meet the required condition.",
      "AsyncPredicateValidator" => $"'{propertyName}' does not meet the required condition.",
      _ => $"'{propertyName}' failed validation."
    };
  }

  private string GetValidatorProperty(IPropertyValidator validator, string propertyName)
  {
    try
    {
      var prop = validator.GetType().GetProperty(propertyName);
      if (prop != null)
      {
        var value = prop.GetValue(validator);
        return value?.ToString() ?? "?";
      }

      var alternatives = new Dictionary<string, string[]>
      {
        ["ValueToCompare"] = new[] { "ValueToCompare", "Value", "ComparisonValue" },
        ["Min"] = new[] { "Min", "Minimum", "MinValue" },
        ["Max"] = new[] { "Max", "Maximum", "MaxValue" },
        ["From"] = new[] { "From", "MinValue" },
        ["To"] = new[] { "To", "MaxValue" }
      };

      if (alternatives.ContainsKey(propertyName))
      {
        foreach (var altName in alternatives[propertyName])
        {
          prop = validator.GetType().GetProperty(altName);
          if (prop != null)
          {
            var value = prop.GetValue(validator);
            return value?.ToString() ?? "?";
          }
        }
      }

      return "?";
    }
    catch
    {
      return "?";
    }
  }
}
