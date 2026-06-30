using System.Net.Http.Json;
using System.Text.Json;
using GeFinsight.Core.Interfaces;
using GeFinsight.Core.Rules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GeFinsight.Infrastructure.Services;

public class LocalInsightService : IInsightService
{
    public Task<string> GenerateInsightAsync(
        InsightContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var report = context.Report;
        var topCategory = report.ByCategory.OrderByDescending(kv => kv.Value).FirstOrDefault();
        var breached = context.RuleResults.FirstOrDefault(r => r.Severity == RuleSeverity.Breached);
        var warning = context.RuleResults.FirstOrDefault(r => r.Severity == RuleSeverity.Warning);

        if (breached is not null)
            return Task.FromResult($"{breached.CategoryName} needs attention: {breached.Message} Net position is £{report.NetAmount:N2} this month.");

        if (warning is not null)
            return Task.FromResult($"{warning.CategoryName} is close to its budget: {warning.Message} Current net position is £{report.NetAmount:N2}.");

        if (!string.IsNullOrWhiteSpace(topCategory.Key))
            return Task.FromResult($"{topCategory.Key} is the largest expense category this month at £{topCategory.Value:N2}. Overall net position is £{report.NetAmount:N2}, with all active budget rules currently on track.");

        return Task.FromResult($"No spending has been recorded this month yet. Current net position is £{report.NetAmount:N2}.");
    }
}

public class ClaudeInsightService : IInsightService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model   = "claude-opus-4-6";

    public ClaudeInsightService(HttpClient http, IConfiguration config)
    {
        _http   = http;
        _apiKey = config["Anthropic:ApiKey"] ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _http.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }
    }

    public async Task<string> GenerateInsightAsync(
        InsightContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Anthropic API key is not configured.");

        var prompt = BuildPrompt(context.Summary, context.RuleResults);

        var requestBody = new
        {
            model      = Model,
            max_tokens = 300,
            messages   = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var response = await _http.PostAsJsonAsync(ApiUrl, requestBody, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Claude insight request failed with HTTP {(int)response.StatusCode}.");

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var text = json
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
            throw new JsonException("Claude insight response did not contain text.");

        return text;
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

public class FallbackInsightService : IInsightService
{
    private readonly ClaudeInsightService _claude;
    private readonly LocalInsightService _local;
    private readonly ILogger<FallbackInsightService> _logger;

    public FallbackInsightService(
        ClaudeInsightService claude,
        LocalInsightService local,
        ILogger<FallbackInsightService> logger)
    {
        _claude = claude;
        _local = local;
        _logger = logger;
    }

    public async Task<string> GenerateInsightAsync(
        InsightContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _claude.GenerateInsightAsync(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Claude insight generation failed. Local insight generation will be used.");
            return await _local.GenerateInsightAsync(context, cancellationToken);
        }
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
