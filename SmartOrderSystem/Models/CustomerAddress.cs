using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("customer_addresses")]
    public class CustomerAddress
    {
        [Key]
        [Column("address_id")]
        public int address_id { get; set; }

        [Column("customer_id")]
        public int customer_id { get; set; }

        [Required]
        [Column("recipient_name")]
        public string recipient_name { get; set; } = string.Empty;

        [Required]
        [Column("contact_number")]
        public string contact_number { get; set; } = string.Empty;

        [Required]
        [Column("province")]
        public string province { get; set; } = string.Empty;

        [Required]
        [Column("city")]
        public string city { get; set; } = string.Empty;

        [Required]
        [Column("barangay")]
        public string barangay { get; set; } = string.Empty;

        [Required]
        [Column("street_address")]
        public string street_address { get; set; } = string.Empty;

        [Column("postal_code")]
        public string? postal_code { get; set; }

        [Column("landmark")]
        public string? landmark { get; set; }

        [Column("address_type")]
        public string address_type { get; set; } = "Home";

        [Column("is_default")]
        public bool is_default { get; set; }

        [Column("created_at")]
        public DateTime created_at { get; set; }

        [Column("updated_at")]
        public DateTime updated_at { get; set; }

        [ForeignKey(nameof(customer_id))]
        public Customer? Customer { get; set; }
    }
}