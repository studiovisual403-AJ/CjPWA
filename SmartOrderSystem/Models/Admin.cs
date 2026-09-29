using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("admins")]
    public class Admin
    {
        [Key]
        [Column("admin_id")]
        public int admin_id { get; set; } 

        [Column("full_name")]
        public string full_name { get; set; }

        [Column("email")]
        public string email { get; set; }

        [Column("password_hash")]
        public string password_hash { get; set; }
    }
}