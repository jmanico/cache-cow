using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Inventory;
using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issue 084: inventory-by-cold-store gating and read-only behavior
/// (CC-DSH-003, CC-CAT-002, CC-CAT-003, CC-QA-005). Grants come from the TEST
/// matrix (<see cref="DashboardTestMatrix"/>) — which role SHOULD hold
/// inventory access is unresolved (issue 084, Open Questions).
/// </summary>
public sealed class DashboardInventoryServiceTests
{
    private readonly FakeInventoryReader reader = new();

    public DashboardInventoryServiceTests()
    {
        reader.Add(DashboardTestData.Inventory());
        reader.Add(DashboardTestData.Inventory(coldStoreId: "cold-store-in-1", sku: "SKU-PANEER", market: Market.IN));
    }

    private DashboardInventoryService Service(IRolePermissionMatrixProvider? matrixProvider = null) =>
        new(
            new DashboardAuthorizationService(
                matrixProvider ?? new ConfiguredRolePermissionMatrixProvider(DashboardTestMatrix.Create()),
                new ConfiguredStepUpPolicyProvider(StepUpPolicy.Create(TimeSpan.FromMinutes(5))),
                new FixedTimeProvider(DashboardTestData.Now)),
            reader);

    [Fact]
    [Requirement("CC-DSH-003")]
    [Requirement("CC-CAT-002")]
    public void Search_WithGrantedRole_ReturnsPerSkuPerColdStoreRows()
    {
        var result = Service().Search(
            DashboardTestData.Staff("ops-agent"), DashboardInventoryQuery.Create(), "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
        Assert.Equal(2, result.Value!.Items.Count);
    }

    [Fact]
    [Requirement("CC-DSH-003")]
    public void Search_FiltersByColdStoreMarketAndSku()
    {
        // AC-02: filtering by cold store, market, and SKU is available.
        var result = Service().Search(
            DashboardTestData.Staff("ops-agent"),
            DashboardInventoryQuery.Create(coldStoreId: "cold-store-in-1", market: Market.IN, sku: SkuId.Parse("SKU-PANEER")),
            "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
        var row = Assert.Single(result.Value!.Items);
        Assert.Equal("cold-store-in-1", row.ColdStoreId);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Search_WithRoleLackingGrant_DeniesAndNeverReadsInventory()
    {
        // AC-04: a role without inventory access gets nothing (rendered 404).
        var result = Service().Search(
            DashboardTestData.Staff("finance"), DashboardInventoryQuery.Create(), "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.PermissionNotGranted, result.DenialReason);
        Assert.Equal(0, reader.SearchCalls);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Search_WithNoMatrixConfigured_Denies()
    {
        var result = Service(new UnconfiguredRolePermissionMatrixProvider()).Search(
            DashboardTestData.Staff("ops-agent"), DashboardInventoryQuery.Create(), "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.MatrixNotConfigured, result.DenialReason);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Search_WhenTheAuthorizationPathFaults_FailsClosed()
    {
        var result = Service(new ThrowingMatrixProvider()).Search(
            DashboardTestData.Staff("ops-agent"), DashboardInventoryQuery.Create(), "corr-1");

        // An exception in the authorization path is a denial, never a bypass
        // (SECURITY.md, Logging rule 2).
        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.AuthorizationFault, result.DenialReason);
        Assert.Equal(0, reader.SearchCalls);
    }

    [Fact]
    [Requirement("CC-DSH-003")]
    public void Search_WhenReaderFaults_FailsClosedRatherThanShowingEmptyStock()
    {
        reader.Throw = true;

        var result = Service().Search(
            DashboardTestData.Staff("ops-agent"), DashboardInventoryQuery.Create(), "corr-1");

        // Never stale, never fabricated (issue 084, Failure Behavior).
        Assert.Equal(DashboardActionStatus.Unavailable, result.Status);
    }

    // ---- the row model ----------------------------------------------------

    [Fact]
    [Requirement("CC-DSH-006")]
    public void ServiceLevel_IsIntegerBasisPoints_NotFloatingPoint()
    {
        var row = DashboardTestData.Inventory(serviceLevelBasisPoints: 9_950);

        // 99.50%, exactly — no binary floating point anywhere on this surface.
        Assert.Equal(9_950, row.ServiceLevelBasisPoints);
        Assert.Equal(typeof(int?), typeof(DashboardInventoryRow)
            .GetProperty(nameof(DashboardInventoryRow.ServiceLevelBasisPoints))!.PropertyType);
    }

    [Fact]
    [Requirement("CC-DSH-006")]
    public void ServiceLevel_MayBeAbsent_WhenTheSupplyingContextHasNoFigure()
    {
        var row = DashboardTestData.Inventory(serviceLevelBasisPoints: null);
        Assert.Null(row.ServiceLevelBasisPoints);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    [Requirement("CC-DSH-006")]
    public void ServiceLevel_OutsideBasisPointRange_IsRejectedNotClamped(int basisPoints)
    {
        Assert.Throws<DashboardValidationException>(() =>
            DashboardTestData.Inventory(serviceLevelBasisPoints: basisPoints));
    }

    [Fact]
    [Requirement("CC-CAT-002")]
    public void NegativeQuantity_IsRejected()
    {
        Assert.Throws<DashboardValidationException>(() => DashboardTestData.Inventory(quantity: -1));
    }

    [Fact]
    [Requirement("CC-CAT-003")]
    public void StockState_OutsideTheThreeStateSet_IsRejected()
    {
        Assert.Throws<DashboardValidationException>(() =>
            DashboardTestData.Inventory(state: (DashboardStockState)99));
    }

    [Fact]
    [Requirement("CC-CAT-003")]
    public void StockState_HasExactlyTheThreeCcCat003States()
    {
        // CC-CAT-003 maps to exactly three user-facing states; a fourth would
        // be a spec deviation.
        Assert.Equal(3, Enum.GetValues<DashboardStockState>().Length);
    }

    [Fact]
    [Requirement("CC-DSH-003")]
    public void ColdStoreId_WithControlCharacters_IsRejected()
    {
        // Log-injection vector (SECURITY.md, Logging rule 5).
        Assert.Throws<DashboardValidationException>(() =>
            DashboardTestData.Inventory(coldStoreId: "cold\nstore"));
    }

    /// <summary>
    /// Issue 084, Open Questions: CC-DSH-003 authors no inventory WRITE
    /// operation, so this module ships none — unreferenced code paths are
    /// scope creep (REQUIREMENTS.md §17). This test is the guard: adding a
    /// mutation member to the port fails it, forcing the ratification
    /// conversation rather than letting a stock-correction surface appear
    /// quietly.
    /// </summary>
    [Fact]
    [Requirement("CC-DSH-003")]
    public void InventoryPort_ExposesNoMutationSurface()
    {
        var members = typeof(IDashboardInventoryReader).GetMethods().Select(method => method.Name).ToList();

        Assert.Equal([nameof(IDashboardInventoryReader.Search)], members);
    }
}
