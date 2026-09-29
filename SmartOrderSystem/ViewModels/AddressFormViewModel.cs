using System.ComponentModel.DataAnnotations;

namespace SmartOrderSystem.ViewModels
{
    public class AddressFormViewModel
    {
        public int AddressId { get; set; } // 0 = new address, >0 = editing existing

        [Required]
        public string AddressType { get; set; } = "Home";

        [Required(ErrorMessage = "Full name is required.")]
        public string RecipientName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact number is required.")]
        public string ContactNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Province is required.")]
        public string Province { get; set; } = string.Empty;

        [Required(ErrorMessage = "City/Municipality is required.")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Barangay is required.")]
        public string Barangay { get; set; } = string.Empty;

        [Required(ErrorMessage = "Street address is required.")]
        public string StreetAddress { get; set; } = string.Empty;

        public string? PostalCode { get; set; }
        public string? Landmark { get; set; }
    }
}