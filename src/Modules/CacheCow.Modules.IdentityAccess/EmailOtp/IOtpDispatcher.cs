namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// What the OTP service hands to the delivery adapter. Carries the secret
/// code (redacted-by-default via <see cref="OtpCode"/>) and the account
/// existence flag; <see cref="ToString"/> is redacted so neither the code nor
/// the email address leaks through formatting (SECURITY.md, Logging rule 4).
/// </summary>
public sealed class OtpDispatchRequest
{
    public OtpDispatchRequest(string emailAddress, OtpCode code, bool accountExists, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
        ArgumentNullException.ThrowIfNull(code);

        EmailAddress = emailAddress;
        Code = code;
        AccountExists = accountExists;
        ExpiresAt = expiresAt;
    }

    public string EmailAddress { get; }

    public OtpCode Code { get; }

    /// <summary>
    /// Whether the address maps to an account. The adapter — not the service —
    /// decides what, if anything, to send when this is false, so issuance
    /// responses stay identical for both populations (CC-SEC-016).
    /// </summary>
    public bool AccountExists { get; }

    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Redacted (SECURITY.md, Logging rule 4).</summary>
    public override string ToString() => "OtpDispatchRequest[REDACTED]";
}

/// <summary>
/// Port for delivering an issued code by email. Invoked identically whether
/// or not the address maps to an account — the send decision happens behind
/// this port (enumeration safety, CC-SEC-016). The Azure Communication
/// Services adapter is a later issue (issue 059, Dependencies); the module
/// registers <see cref="NullOtpDispatcher"/> as a provisional default.
/// Transactional OTP mail carries no more PII than necessary and never
/// carries secrets in logged headers/metadata (SECURITY.md, Email and
/// messaging security rule 1).
/// </summary>
public interface IOtpDispatcher
{
    ValueTask DispatchAsync(OtpDispatchRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Provisional default until the Azure Communication Services adapter lands:
/// delivers nothing (codes are simply never received, which fails closed).
/// </summary>
public sealed class NullOtpDispatcher : IOtpDispatcher
{
    public ValueTask DispatchAsync(OtpDispatchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.CompletedTask;
    }
}
