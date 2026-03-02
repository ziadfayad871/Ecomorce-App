using System.ComponentModel.DataAnnotations;

namespace Task.Areas.Admin.ViewModels
{
    public class ProductFormVm
    {
        public int Id { get; set; }

        [Required, MaxLength(160)]
        public string Name { get; set; } = "";

        [Range(0, 999999)]
        public decimal Price { get; set; }

        [Required]
        public int CategoryId { get; set; }

        public IFormFile? Image { get; set; }
    }
}
