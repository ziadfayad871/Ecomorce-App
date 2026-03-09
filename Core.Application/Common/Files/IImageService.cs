
namespace Core.Application.Common.Files;

public interface IImageService
{
    Task<string> SaveProductImageAsync(Stream content, string fileName);
}
