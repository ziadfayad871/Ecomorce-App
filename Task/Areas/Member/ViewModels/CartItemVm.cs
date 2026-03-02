namespace Task.Areas.Member.ViewModels
{
    public class CartItemVm
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string? ImagePath { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal Total => UnitPrice * Quantity;
    }
}
