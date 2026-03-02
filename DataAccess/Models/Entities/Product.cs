using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace DataAccess.Models.Entities
{
    public class Product
    {
        public int Id { get; set; }

        [Required, MaxLength(160)]
        public string Name { get; set; } = "";

        [Range(0, 999999)]
        public decimal Price { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public List<ProductImage> Images { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
