namespace Core.Application.Common.Persistence;

public interface IDbInitializer
{
    System.Threading.Tasks.Task InitializeAsync();
}
