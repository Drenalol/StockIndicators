/// <summary>
/// X-axis conversions for the chart spec. Dates are stored as OADate (days since
/// 1899-12-30). Every point - candlesticks, series, the marker - must use this single
/// convention, or the elements land on different parts of the axis. The interactive
/// payload maps them to Unix seconds on the way out (see ChartDataMapper).
/// </summary>
public static class ChartX
{
  public static double ToX(DateTime date) => date.ToOADate();

  public static DateTime FromX(double x) => DateTime.FromOADate(x);
}
