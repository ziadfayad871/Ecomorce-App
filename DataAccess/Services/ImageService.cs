using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Services
{
    public class ImageService
    {
        private readonly IWebHostEnvironment _env;
        public ImageService(IWebHostEnvironment env) => _env = env;

        public async Task<string> SaveProductImageAsync(IFormFile file)
        {
            var root = Path.Combine(_env.WebRootPath, "uploads", "cards");
            Directory.CreateDirectory(root);

            var ext = Path.GetExtension(file.FileName);
            var name = $"{Guid.NewGuid()}{ext}";
            var full = Path.Combine(root, name);

            using var fs = new FileStream(full, FileMode.Create);
            await file.CopyToAsync(fs);

            return $"/uploads/cards/{name}";
        }
    }
}
