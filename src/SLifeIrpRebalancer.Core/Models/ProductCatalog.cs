namespace SLifeIrpRebalancer.Core.Models;

public sealed record ProductCatalog(
    IReadOnlyList<PrincipalGuaranteedProduct> PrincipalGuaranteed,
    IReadOnlyList<FundProduct> Funds,
    IReadOnlyList<ReturnPeriod> FundReturnPeriods);
