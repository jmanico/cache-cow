namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// Port answering whether an email address maps to a consumer account. The
/// answer NEVER changes the shape, status, or content of any response — it is
/// recorded on the OTP record and forwarded to the dispatch port only
/// (enumeration safety, CC-SEC-016). The real implementation is an adapter
/// over the consumer identity provider (Microsoft Entra External ID,
/// issue 058 — out of scope here); the module registers
/// <see cref="NullAccountDirectory"/> as a provisional default.
/// </summary>
public interface IAccountDirectory
{
    ValueTask<bool> AccountExistsAsync(string emailAddress, CancellationToken cancellationToken);
}

/// <summary>
/// Provisional default until the Entra External ID adapter (issue 058)
/// lands: no address maps to an account, so no code can ever verify — a
/// fail-closed stand-in, never a bypass (SECURITY.md, Logging rule 2).
/// </summary>
public sealed class NullAccountDirectory : IAccountDirectory
{
    public ValueTask<bool> AccountExistsAsync(string emailAddress, CancellationToken cancellationToken) =>
        ValueTask.FromResult(false);
}
