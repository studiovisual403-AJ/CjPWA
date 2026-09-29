using System.ComponentModel.DataAnnotations;

namespace SmartOrderSystem.ViewModels
{
    public class SecurityQuestionViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string Question1Text { get; set; } = string.Empty;
        public string Question2Text { get; set; } = string.Empty;

        [Required(ErrorMessage = "Answer to question 1 is required.")]
        public string Answer1 { get; set; } = string.Empty;

        [Required(ErrorMessage = "Answer to question 2 is required.")]
        public string Answer2 { get; set; } = string.Empty;
    }
}