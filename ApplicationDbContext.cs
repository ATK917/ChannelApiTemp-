using Microsoft.EntityFrameworkCore;
using ChannelApiTemp.Models;
namespace ChannelApiTemp.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Channel> Channels { get; set; }
    }
}
