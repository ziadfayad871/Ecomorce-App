using Core.Domain.Entities;

namespace Task.Areas.Admin.ViewModels
{
    public class CategoryProductsVm
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public List<Product> Products { get; set; } = new();
    }
}
