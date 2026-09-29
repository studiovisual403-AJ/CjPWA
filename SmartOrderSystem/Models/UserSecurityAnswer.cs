using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("user_security_answers")]
    public class UserSecurityAnswer
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Customer")]
        public int customer_id { get; set; }

        [Required]
        public string question_1 { get; set; } = string.Empty;

        [Required]
        [Column("answer_1_hash")]
        public string answer_1_hash { get; set; } = string.Empty;

        [Required]
        public string question_2 { get; set; } = string.Empty;

        [Required]
        [Column("answer_2_hash")]
        public string answer_2_hash { get; set; } = string.Empty;
    }

}
