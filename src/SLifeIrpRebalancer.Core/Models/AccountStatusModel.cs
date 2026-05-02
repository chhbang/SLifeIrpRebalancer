namespace SLifeIrpRebalancer.Core.Models;

public sealed class AccountStatusModel
{
    public decimal TotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? ProfitAmount { get; set; }
    public RebalanceTiming RebalanceTiming { get; set; } = RebalanceTiming.Immediate;

    /// <summary>
    /// The planned execution date when <see cref="RebalanceTiming"/> is
    /// <see cref="RebalanceTiming.MaturityReservation"/>. Typically the maturity date of a product
    /// that triggers the rebalance. Required only when timing is MaturityReservation; null for immediate.
    /// </summary>
    public DateOnly? ExecutionDate { get; set; }

    public List<OwnedProductModel> OwnedItems { get; set; } = [];
}
