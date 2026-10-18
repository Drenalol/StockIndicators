namespace StockIndicators.Domain.Extensions;

public static class EnumExtensions
{
  public static TAttribute Get<TAttribute>(this Enum @enum) where TAttribute : Attribute
  {
    var enumValue = @enum.ToString();
    var enumType = @enum.GetType();
    var fi = enumType.GetField(enumValue);
    var attributeType = typeof(TAttribute);
    
    if (fi?.GetCustomAttributes(attributeType, false).SingleOrDefault() is TAttribute attribute)
      return attribute;

    throw new InvalidOperationException($"Enum '{enumType.Name}.{enumValue}' does not have Attribute: {attributeType.Name}");
  }

  /// <summary>
  /// Optional variant of <see cref="Get{TAttribute}"/>: returns null when the enum value has no such attribute
  /// or the attribute is present more than once.
  /// </summary>
  public static TAttribute? GetOptional<TAttribute>(this Enum @enum) where TAttribute : Attribute
  {
    var fi = @enum.GetType().GetField(@enum.ToString());

    return fi?.GetCustomAttributes(typeof(TAttribute), false).SingleOrDefault() as TAttribute;
  }
}