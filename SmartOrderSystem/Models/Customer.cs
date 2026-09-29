using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("customers")]
    public class Customer
    {
        [Key]
        [Column("customer_id")]
        public int customer_id { get; set; }

        [Required]
        [Column("full_name")]
        public string full_name { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        public string email { get; set; } = string.Empty;

        [Required]
        [Column("contact_number")]
        public string contact_number { get; set; } = string.Empty;

        [Required]
        [Column("password_hash")]
        public string password_hash { get; set; } = string.Empty;

        [Column("reset_answer_failed_attempts")]
        public int reset_answer_failed_attempts { get; set; }

        [Column("reset_answer_locked_until")]
        public DateTime? reset_answer_locked_until { get; set; }

        // I-comment out o alisin ang ibang properties kung wala sila sa DB
    }
}
