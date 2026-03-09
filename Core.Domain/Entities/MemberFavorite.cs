using System.ComponentModel.DataAnnotations;

namespace Core.Domain.Entities
{
    public class MemberFavorite
    {
        public int Id { get; set; }

        public int MemberId { get; set; }
        public Member? Member { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
