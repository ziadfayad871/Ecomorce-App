using Core.Application.Common.Files;
using Microsoft.AspNetCore.Hosting;

namespace DataAccess.Services
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _env;
        public ImageService(IWebHostEnvironment env) => _env = env;

        public async Task<string> SaveProductImageAsync(Stream content, string fileName)
        {
            var root = Path.Combine(_env.WebRootPath, "uploads", "cards");
            Directory.CreateDirectory(root);

            var ext = Path.GetExtension(fileName);
            var name = $"{Guid.NewGuid()}{ext}";
            var full = Path.Combine(root, name);

            using var fs = new FileStream(full, FileMode.Create);
            await content.CopyToAsync(fs);

            return $"/uploads/cards/{name}";
        }
    }
}
