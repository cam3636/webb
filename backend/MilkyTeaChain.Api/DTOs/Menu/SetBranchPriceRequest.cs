namespace MilkyTeaChain.Api.DTOs.Menu;

public class SetBranchPriceRequest
{
    public decimal? Price { get; set; }
    public bool UseDefaultPrice { get; set; } = false;
}
