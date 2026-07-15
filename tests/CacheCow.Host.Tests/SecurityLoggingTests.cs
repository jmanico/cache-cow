using CacheCow.Host.Security;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Issue 022 (CC-SEC-010, CC-NFR-003; SECURITY.md, Logging rules 3-5): the
/// structured security-event logger, log-injection sanitization, and PII
/// redaction helpers.
/// </summary>
public sealed class SecurityLoggingTests
{
    [Fact]
    [Requirement("CC-SEC-010")]
    public void Sanitize_neutralizes_crlf_log_injection()
    {
        var hostile = "search term\r\nFAKE ENTRY: AuthenticationSuccess actor admin";

        var sanitized = LogSanitizer.Sanitize(hostile);

        Assert.DoesNotContain("\r", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", sanitized, StringComparison.Ordinal);
        Assert.Contains("search term__FAKE ENTRY", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-010")]
    public void Sanitize_neutralizes_ansi_escape_sequences()
    {
        var hostile = "value\x1b[2J\x1b[31mred";

        var sanitized = LogSanitizer.Sanitize(hostile);

        Assert.DoesNotContain("\x1b", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-010")]
    public void Sanitize_truncates_oversized_values()
    {
        var oversized = new string('a', 10_000);

        var sanitized = LogSanitizer.Sanitize(oversized);

        Assert.True(sanitized.Length < 300);
        Assert.EndsWith("...(truncated)", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-010")]
    public void Sanitize_handles_null_and_empty()
    {
        Assert.Equal(string.Empty, LogSanitizer.Sanitize(null));
        Assert.Equal(string.Empty, LogSanitizer.Sanitize(string.Empty));
    }

    [Fact]
    [Requirement("CC-SEC-010")]
    public void RedactEmail_masks_the_local_part()
    {
        Assert.Equal("***@example.test", PiiRedactor.RedactEmail("jane.doe@example.test"));
        Assert.Equal("[redacted]", PiiRedactor.RedactEmail("not-an-email"));
        Assert.Equal("[redacted]", PiiRedactor.RedactEmail(null));
    }

    [Fact]
    [Requirement("CC-SEC-010")]
    public void Mask_keeps_at_most_one_character()
    {
        Assert.Equal("a***", PiiRedactor.Mask("alice"));
        Assert.Equal("***", PiiRedactor.Mask("a"));
        Assert.Equal("[redacted]", PiiRedactor.Mask(null));
    }

    [Fact]
    [Requirement("CC-SEC-010")]
    [Requirement("CC-NFR-003")]
    public void Security_events_are_structured_with_named_fields_and_sanitized_values()
    {
        var logger = new CapturingLogger();
        var events = new SecurityEventLogger(logger);

        events.ValidationRejected("contact-form", "payload\r\ninjected line");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("ValidationRejected", entry.EventId.Name);

        // Structured template: named parameters present as state, hostile
        // input inert as field data (SECURITY.md, Logging rules 4-5).
        var state = Assert.IsAssignableFrom<IReadOnlyList<KeyValuePair<string, object?>>>(entry.State);
        var subject = Assert.Single(state, pair => pair.Key == "Subject").Value as string;
        Assert.NotNull(subject);
        Assert.DoesNotContain("\n", subject, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", entry.Message, StringComparison.Ordinal);
        Assert.Contains("payload__injected line", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-010")]
    public void Every_event_class_of_logging_rule_3_emits_an_event()
    {
        var logger = new CapturingLogger();
        var events = new SecurityEventLogger(logger);

        events.AuthenticationSuccess("alice");
        events.AuthenticationFailure("mallory", "invalid-code");
        events.AuthorizationDenied("mallory", "orders/42", "requirements-not-met");
        events.ValidationRejected("schema", "field");
        events.AdminAction("hr-admin-1", "employee-export", "employee/7");
        events.RateLimitRejected("user:mallory", "/v1/orders");

        Assert.Equal(6, logger.Entries.Count);
        string?[] expected = ["AuthenticationSuccess", "AuthenticationFailure", "AuthorizationDenied", "ValidationRejected", "AdminAction", "RateLimitRejected"];
        Assert.Equal(expected, logger.Entries.Select(entry => entry.EventId.Name).ToArray());
    }

    private sealed class CapturingLogger : ILogger<SecurityEventLogger>
    {
        public List<(EventId EventId, string Message, object? State)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((eventId, formatter(state, exception), state));
    }
}
