using System.ComponentModel.DataAnnotations;

namespace Core.Domain.Entities
{
    public class Offer
    {
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Title { get; set; } = "";

        [Required, MaxLength(40)]
        public string Code { get; set; } = "";

        [Range(0, 100)]
        public decimal DiscountPercent { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? StartsAtUtc { get; set; }
        public DateTime? EndsAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
