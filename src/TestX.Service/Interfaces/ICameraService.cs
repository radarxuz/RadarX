using TestX.Data.Entities;

namespace TestX.Service.Interfaces;

/// <summary>
/// 
/// </summary>
public interface ICameraService
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="latitude"></param>
    /// <param name="longitude"></param>
    /// <param name="maxDistance"></param>
    /// <returns></returns>
    Task<IEnumerable<object>> RetrieveAsync();
    Task<bool> CheckAsync(double latitude, double longitude);
    Task<Camera> InsertAsync(Camera camera);
    
}