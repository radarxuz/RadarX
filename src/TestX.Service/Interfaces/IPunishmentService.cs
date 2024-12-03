using TestX.Data.Entities;

namespace TestX.Service.Interfaces;

public interface IPunishmentService
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="punishment"></param>
    /// <returns></returns>
    Task<Punishment> InsertAsync(Punishment punishment);
}