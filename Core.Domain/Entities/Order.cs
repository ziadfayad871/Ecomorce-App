using System.ComponentModel.DataAnnotations;

namespace Core.Domain.Entities
{
    public class Order
    {
        public int Id { get; set; }

        [Required, MaxLength(40)]
        public string OrderNumber { get; set; } = "";

        public int? MemberId { get; set; }

        [MaxLength(120)]
        public string CustomerName { get; set; } = "";

        [Required, MaxLength(150)]
        public string CustomerEmail { get; set; } = "";

        [Range(0, 999999999)]
        public decimal TotalAmount { get; set; }

        [Required, MaxLength(30)]
        public string Status { get; set; } = "Pending";

        [MaxLength(200)]
        public string? PaymentSessionId { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAtUtc { get; set; }

        public List<OrderItem> Items { get; set; } = new();
    }
}
