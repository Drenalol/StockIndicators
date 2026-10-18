using System.Text.Json.Serialization;

namespace StockIndicators.Providers.BusinessQuant.Schema;

public class BusinessQuantQuotesResponse
{
  public BusinessQuantQuotesMetadata? Metadata { get; set; }
  public List<BusinessQuantQuoteBar>? Data { get; set; }
}

public class BusinessQuantQuotesMetadata
{
  public long? Cik { get; set; }
  public string? Ticker { get; set; }
  public string? CompanyName { get; set; }

  [JsonPropertyName("companyname_short")]
  public string? CompanyNameShort { get; set; }

  [JsonPropertyName("from_date")]
  public string? FromDate { get; set; }

  [JsonPropertyName("till_date")]
  public string? TillDate { get; set; }

  public string? Mode { get; set; }
  public BusinessQuantPagination? Pagination { get; set; }
}

public class BusinessQuantPagination
{
  [JsonPropertyName("current_page")]
  public int CurrentPage { get; set; }

  public int Limit { get; set; }

  [JsonPropertyName("total_records")]
  public int TotalRecords { get; set; }

  [JsonPropertyName("total_pages")]
  public int TotalPages { get; set; }
}

public class BusinessQuantQuoteBar
{
  public string Date { get; set; } = null!;
  public decimal Open { get; set; }
  public decimal High { get; set; }
  public decimal Low { get; set; }
  public decimal Close { get; set; }
  public decimal Volume { get; set; }
}

public class BusinessQuantUniverseResponse
{
  public BusinessQuantUniverseMetadata? Metadata { get; set; }
  public List<BusinessQuantUniverseRecord>? Data { get; set; }
}

public class BusinessQuantUniverseMetadata
{
  [JsonPropertyName("total_records")]
  public int TotalRecords { get; set; }
}

public class BusinessQuantUniverseRecord
{
  [JsonPropertyName("security_type")]
  public string SecurityType { get; set; } = null!;

  public string Ticker { get; set; } = null!;
  public long? Cik { get; set; }
  public string? Name { get; set; }

  [JsonPropertyName("name_short")]
  public string? NameShort { get; set; }

  public string? Sector { get; set; }
  public string? Industry { get; set; }
  public string? Exchange { get; set; }

  [JsonPropertyName("seriesid")]
  public string? SeriesId { get; set; }

  [JsonPropertyName("contractid")]
  public string? ContractId { get; set; }
}
