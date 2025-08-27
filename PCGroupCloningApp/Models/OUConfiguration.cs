// Models/OUConfiguration.cs
using System.ComponentModel.DataAnnotations;

namespace PCGroupCloningApp.Models
{
    public class OUConfiguration
    {
        public int Id { get; set; }

        [Required]
        public string RetiredComputersOU { get; set; } = string.Empty;

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public string UpdatedBy { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}