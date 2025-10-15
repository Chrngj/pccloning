// Models/AuditLog.cs
using System.ComponentModel.DataAnnotations;

namespace PCGroupCloningApp.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;  // <-- Tilføj = DateTime.Now her

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Operation { get; set; } = string.Empty;

        [Required]
        public string SourceComputer { get; set; } = string.Empty;

        [Required]
        public string TargetComputer { get; set; } = string.Empty;

        public string GroupsCloned { get; set; } = string.Empty;

        public string AdditionalGroups { get; set; } = string.Empty;

        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public string Details { get; set; } = string.Empty;

        public string? SourceComputerOU { get; set; }
        public string? SourceComputerOUDescription { get; set; }
        public string? TargetComputerOU { get; set; }
        public string? TargetComputerOUDescription { get; set; }
        public string? SourceComputerDescription { get; set; }
        public string? TargetComputerDescription { get; set; }
    }
}