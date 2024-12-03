using TestX.Data.Entities;
using TestX.Data.IRepositories;
using TestX.Service.Interfaces;

namespace TestX.Service.Services;

public class PunishmentService: IPunishmentService
{
    private readonly IRepository<Punishment> punishmentRepository;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="punishmentRepository"></param>
    public PunishmentService(IRepository<Punishment> punishmentRepository)
    {
        this.punishmentRepository = punishmentRepository;
    }

    public async Task<Punishment> InsertAsync(Punishment punishment)
    {
        var punishments = await punishmentRepository.SelectAllAsync();
        Punishment punishmentEntity = null;
        if (punishments.Any(a => a.Uid == punishment.Uid))
            punishmentEntity = await punishmentRepository.InsertAsync(punishment);

        return punishmentEntity;
    }
}