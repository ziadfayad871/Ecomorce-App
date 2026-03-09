using System.ComponentModel.DataAnnotations;

namespace Core.Domain.Entities
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int ProductId { get; set; }

        [Required, MaxLength(160)]
        public string ProductName { get; set; } = "";

        [Range(0, 999999)]
        public decimal UnitPrice { get; set; }

        [Range(1, 9999)]
        public int Quantity { get; set; }
    }
}
