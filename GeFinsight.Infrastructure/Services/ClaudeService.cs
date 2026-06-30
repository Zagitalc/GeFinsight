using System.Net.Http.Json;
using System.Text.Json;
using GeFinsight.Core.Interfaces;
using GeFinsight.Core.Rules;
using Microsoft.Extensions.Configuration;

namespace GeFinsight.Infrastructure.Services;

// ─────────────────────────────────────────────
// ClaudeService — sends spending data to the
// Anthropic API and returns a plain-English insight.
// Injected via IClaudeService wherever needed.
// ─────────────────────────────────────────────

public class ClaudeService : IClaudeService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model   = "claude-opus-4-6";

    public ClaudeService(HttpClient http, IConfiguration config)
    {
        _http   = http;
        _apiKey = config["Anthropic:ApiKey"] ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _http.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }
    }

    public async Task<string> GetSpendingInsightAsync(SpendingSummary summary, IEnumerable<RuleResult> ruleResults)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return string.Empty;

        var prompt = BuildPrompt(summary, ruleResults);

        var requestBody = new
        {
            model      = Model,
            max_tokens = 300,
            messages   = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var response = await _http.PostAsJsonAsync(ApiUrl, requestBody);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()
            ?? "Unable to generate insight at this time.";
    }

    private static string BuildPrompt(SpendingSummary summary, IEnumerable<RuleResult> ruleResults)
    {
        var month = new DateTime(summary.Year, summary.Month, 1).ToString("MMMM yyyy");
        var categoryLines = string.Join("\n", summary.ByCategory
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"  - {kv.Key}: £{kv.Value:F2}"));

        var ruleLines = string.Join("\n", ruleResults.Select(r =>
            $"  - [{r.Severity}] {r.CategoryName} ({r.RuleName}): {r.Message}"));

        return $"""
            You are a helpful personal finance assistant. 
            Analyse the spending summary below and write a single concise paragraph (3-5 sentences) 
            of plain-English insight for the user. Be specific, practical, and encouraging. 
            Do not use bullet points. Do not repeat raw numbers unnecessarily.

            Month: {month}
            Total income:   £{summary.TotalIncome:F2}
            Total expenses: £{summary.TotalExpenses:F2}
            Net:            £{summary.TotalIncome - summary.TotalExpenses:F2}

            Spending by category:
            {categoryLines}

            Budget rule results:
            {ruleLines}
            """;
    }
}

// ─────────────────────────────────────────────
// Export strategies — Strategy pattern
// ─────────────────────────────────────────────

public class CsvExportStrategy : IExportStrategy
{
    public string ContentType    => "text/csv";
    public string FileExtension  => "csv";

    public Task<byte[]> ExportAsync(IEnumerable<Core.Domain.Transaction> transactions)
    {
        var lines = new List<string> { "Date,Category,Type,Amount,Note" };
        lines.AddRange(transactions.Select(t =>
            $"{t.Date:yyyy-MM-dd},{t.CategoryName},{t.Type},{t.Amount:F2},{EscapeCsv(t.Note)}"));

        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines));
        return Task.FromResult(bytes);
    }

    private static string EscapeCsv(string value)
        => value.Contains(',') ? $"\"{value}\"" : value;
}
