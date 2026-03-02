using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Models.Entities
{
    public class ProductImage
    {
        public int Id { get; set; }
        public string Path { get; set; } = "";

        public int ProductId { get; set; }
        public Product? Product { get; set; }
    }
}
