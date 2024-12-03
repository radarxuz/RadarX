using TestX.Data.Entities;
using TestX.Data.IRepositories;
using TestX.Service.Helpers;
using TestX.Service.Interfaces;

namespace TestX.Service.Services;

/// <summary>
/// 
/// </summary>
public class CameraService : ICameraService
{
    private readonly IRepository<Camera> cameraRepository;
    private readonly IRepository<Punishment> punishmentRepository;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cameraRepository"></param>
    public CameraService(IRepository<Camera> cameraRepository, IRepository<Punishment> punishmentRepository)
    {
        this.cameraRepository = cameraRepository;
        this.punishmentRepository = punishmentRepository;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="latitude"></param>
    /// <param name="longitude"></param>
    /// <param name="maxDistance"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    // public async Task<IEnumerable<Camera>> RetrieveAsync(double latitude, double longitude, double maxDistance)
    // {
    //     var allCameras = await cameraRepository.SelectAllAsync();
    //
    //     // Filter cameras by distance
    //     var nearbyCameras = allCameras.Where(camera =>
    //     {
    //         // Calculate the distance between the user's location and each camera
    //         double distance = DistanceHelper.HaversineDistance(latitude, longitude, camera.Latitude, camera.Longitude);
    //         return distance <= maxDistance;
    //     });
    //
    //     return nearbyCameras;
    // }
    //
    public async Task<IEnumerable<object>> RetrieveAsync()
    {
        // Get all cameras
        var allCameras = await cameraRepository.SelectAllAsync();

        // Filter cameras by distance and number of punishments
        var nearbyCameras = allCameras.Where(camera =>
        {
            // Check if the camera has more than 1 punishment
            var punishmentCount = punishmentRepository.SelectAllAsync(p => p.CameraId == camera.Id).Result.Count();

            // Return only cameras within the max distance and having more than 1 punishment
            return punishmentCount >= 5;
        });

        return nearbyCameras;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<Camera>> RetrieveAllAsync()
    {
        return await cameraRepository.SelectAllAsync();
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="latitude"></param>
    /// <param name="longitude"></param>
    /// <returns></returns>
    public async Task<bool> CheckAsync(double latitude, double longitude)
    {
        var allCameras = await cameraRepository.SelectAllAsync();
        if (allCameras.Any(a => a.Longitude == longitude && a.Latitude == latitude))
            return true;

        return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="camera"></param>
    /// <returns></returns>
    public async Task<Camera> InsertAsync(Camera camera)
    {
        var allCameras = await cameraRepository.SelectAllAsync();
        Camera cameraEntity = null;
        if (allCameras.Any(a => a.Longitude == camera.Longitude && a.Latitude == camera.Latitude))
            cameraEntity = await cameraRepository.InsertAsync(camera);

        return cameraEntity;
    }
}