// Models/ServiceAccount.cs
using System.ComponentModel.DataAnnotations;

namespace PCGroupCloningApp.Models
{
    public class ServiceAccount
    {
        public int Id { get; set; }

        [Required]
        public string Domain { get; set; } = "IBK.lan";

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string EncryptedPassword { get; set; } = string.Empty;

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public string UpdatedBy { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}