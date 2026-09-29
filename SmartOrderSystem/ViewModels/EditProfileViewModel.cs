using System.ComponentModel.DataAnnotations;

namespace SmartOrderSystem.ViewModels
{
    public class EditProfileViewModel
    {
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Enter a valid phone number.")]
        public string ContactNumber { get; set; } = string.Empty;

        public string? IsVerified { get; set; } // pwede mong palitan pag may verified column ka na sa Customer
    }
}