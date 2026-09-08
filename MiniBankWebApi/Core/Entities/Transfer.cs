using System.ComponentModel.DataAnnotations;

namespace MiniBankWebApi.Core.Entities
{
    public class Transfer
    {
        [Key]
        public int Id { get; set; }

        public int FromAccountId { get; set; }

        public int ToAccountId { get; set; }

        public decimal Amount { get; set; }

        public DateTime TransferDate { get; set; }

        public Account FromAccount { get; set; } = null!;

        public Account ToAccount { get; set; } = null!;
    }
}
