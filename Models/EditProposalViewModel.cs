using System.ComponentModel.DataAnnotations;

namespace MusicCatalogWebApplication.Models
{
    public class EditProposalViewModel
    {
        public int ID { get; set; }

        [Required]
        public string UserLogin { get; set; }

        [Required]
        [StringLength(32)]
        public string TableName { get; set; }

        public int Record_ID { get; set; }

        [Required]
        [StringLength(128)]
        public string ProposedChange { get; set; }

        [Required]
        [StringLength(16)]
        public string Status { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }
    }
}