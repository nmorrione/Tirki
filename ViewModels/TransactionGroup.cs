using System.ComponentModel;
using System.Runtime.CompilerServices;
using Tirki.Models;

namespace Tirki.ViewModels;

/// <summary>
/// List&lt;Transaction&gt; così CollectionView (IsGrouped="True") può enumerarne gli item
/// direttamente; implementa INotifyPropertyChanged a mano per il flag IsCurrentSection,
/// dato che non può ereditare anche da ObservableObject.
/// </summary>
public class TransactionGroup : List<Transaction>, INotifyPropertyChanged
{
    public string Name { get; }

    public decimal Total { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isCurrentSection;

    /// <summary>True quando questo è il gruppo mostrato nella barra "sticky" sotto il saldo:
    /// in quel caso il proprio header inline si nasconde per non duplicare l'informazione.</summary>
    public bool IsCurrentSection
    {
        get => _isCurrentSection;
        set
        {
            if (_isCurrentSection == value) return;
            _isCurrentSection = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrentSection)));
        }
    }

    public TransactionGroup(string name, List<Transaction> transactions) : base(transactions)
    {
        Name = name;
        Total = transactions.Sum(t => t.Amount);
    }
}
