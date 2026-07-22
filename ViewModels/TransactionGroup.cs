using Tirki.Models;

namespace Tirki.ViewModels;

public class TransactionGroup : List<Transaction>
{
    public string Name { get; }

    public decimal Total { get; }

    public TransactionGroup(string name, List<Transaction> transactions) : base(transactions)
    {
        Name = name;
        Total = transactions.Sum(t => t.Amount);
    }
}
