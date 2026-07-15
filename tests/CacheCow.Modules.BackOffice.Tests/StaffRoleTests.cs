using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issue 080: the five minimum dashboard roles exist as a closed set —
/// nothing outside it is representable or resolvable (CC-DSH-002).
/// </summary>
public sealed class StaffRoleTests
{
    [Fact]
    [Requirement("CC-DSH-002")]
    public void Role_set_is_exactly_the_five_minimum_roles()
    {
        var names = StaffRole.All.Select(r => r.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(
            ["admin", "finance", "hr-admin", "ops-agent", "sales-viewer"],
            names);
    }

    [Theory]
    [Requirement("CC-DSH-002")]
    [InlineData("sales-viewer")]
    [InlineData("ops-agent")]
    [InlineData("finance")]
    [InlineData("hr-admin")]
    [InlineData("admin")]
    public void Every_minimum_role_resolves_to_its_singleton(string name)
    {
        Assert.True(StaffRole.TryResolve(name, out var role));
        Assert.Equal(name, role!.Name);
        Assert.Same(role, StaffRole.All.Single(r => r.Name == name));
    }

    [Theory]
    [Requirement("CC-DSH-002")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("superuser")]
    [InlineData("Admin")] // case mismatch: exact ordinal only, fail closed
    [InlineData(" admin")]
    [InlineData("admin ")]
    public void Unknown_or_malformed_role_names_do_not_resolve(string? name)
    {
        Assert.False(StaffRole.TryResolve(name, out var role));
        Assert.Null(role);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Role_type_is_closed_no_public_construction_path()
    {
        var constructors = typeof(StaffRole).GetConstructors();

        Assert.Empty(constructors); // no public ctor: the set cannot grow at runtime
    }
}
