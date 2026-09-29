using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SmartOrderSystem.ViewModels
{
    public class AddProductViewModel
    {
        [Required(ErrorMessage = "Image is required.")]
        public IFormFile? ProductImage { get; set; }
    
        [Required(ErrorMessage = "Brand is required.")]
        public string Brand { get; set; }

        [Required(ErrorMessage = "Model name is required.")]
        public string ModelName { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Color is required.")]
        public string Color { get; set; }

        [Required(ErrorMessage = "Price is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal DefaultPrice { get; set; }

        public string Status { get; set; } = "Active";

        // Gagamitin natin itong Dictionary para sa automatic mapping ng Size at Stock sa Form
        public Dictionary<int, int> SizesWithStocks { get; set; } = new Dictionary<int, int>();
    }
}