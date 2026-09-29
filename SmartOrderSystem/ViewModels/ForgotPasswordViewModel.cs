using System.ComponentModel.DataAnnotations;

namespace SmartOrderSystem.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;
    }
}