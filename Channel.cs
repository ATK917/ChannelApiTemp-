using System.ComponentModel.DataAnnotations;

namespace ChannelApiTemp.Models
{
    public class Channel
    {
        [Key]
        public int Id { get; set; }  // Primary key

        public string Name { get; set; }
        public string Url { get; set; }
        public int Subscribers { get; set; }
        public string Category { get; set; }
    }
}
