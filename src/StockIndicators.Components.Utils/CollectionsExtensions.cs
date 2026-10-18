using System.Diagnostics.CodeAnalysis;
using Nito.AsyncEx;

namespace StockIndicators.Components.Utils;

public static class CollectionsExtensions
{
  public static T[] AsArray<T>(this IEnumerable<T> source) =>
    source switch
    {
      T[] array => array,
      _ => source.ToArray()
    };

  public static async IAsyncEnumerable<T> WhereAsync<T>(this IEnumerable<T> source, Func<T, Task<bool>> predicate)
  {
    foreach (var item in source)
      if (await predicate(item))
        yield return item;
  }

  public static async Task WhenAll(this IAsyncEnumerable<Task> source)
  {
    var tasks = await source.ToArrayAsync();
    await tasks.WhenAll();
  }

  public static async Task<TResult[]> WhenAll<TResult>(this IAsyncEnumerable<Task<TResult>> source)
  {
    var tasks = await source.ToArrayAsync();
    return await tasks.WhenAll();
  }

  public static bool IsEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? items) => items == null || !items.Any();
  
  public static bool IsEmptyCollection<T>([NotNullWhen(false)] this ICollection<T>? items) => items == null || items.Count == 0;
  
  public static bool IsEmpty<T>([NotNullWhen(false)] this IReadOnlyCollection<T>? items) => items == null || items.Count == 0;

  public static bool IsNotEmpty<T>([NotNullWhen(true)] this IEnumerable<T>? items) => !IsEmpty(items);
  
  public static bool IsNotEmptyCollection<T>([NotNullWhen(true)] this ICollection<T>? items) => !IsEmptyCollection(items);
  
  public static bool IsNotEmpty<T>([NotNullWhen(true)] this IReadOnlyCollection<T>? items) => !IsEmpty(items);
}