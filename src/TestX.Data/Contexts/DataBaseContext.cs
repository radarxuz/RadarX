using Microsoft.EntityFrameworkCore;
using TestX.Data.Entities;

namespace TestX.Data.Contexts;

/// <summary>
/// 
/// </summary>
public class DataBaseContext : DbContext
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="options"></param>
    public DataBaseContext(DbContextOptions<DataBaseContext> options)
        : base(options)
    {
    }

    public DbSet<Camera> Cameras { get; set; }
    public DbSet<Punishment> Punishments { get; set; }
    public DbSet<Entities.Data> Datas { get; set; }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var navigation in entityType.GetNavigations())
            {
                navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
            }
        }
    }

}
