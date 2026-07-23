using SQLite;
using Tirki.Models;

namespace Tirki.Services;

public class LocalDatabaseService
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly Task _initialization;

    public LocalDatabaseService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "tirki.db3");
        _connection = new SQLiteAsyncConnection(dbPath);
        _initialization = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _connection.CreateTableAsync<Transaction>();
        await _connection.CreateTableAsync<Category>();
    }

    private async Task EnsureInitializedAsync() => await _initialization;

    public async Task<List<Transaction>> GetTransactionsAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<Transaction>()
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    public async Task<List<Transaction>> GetTransactionsAsync(DateTime from, DateTime to)
    {
        await EnsureInitializedAsync();
        return await _connection.Table<Transaction>()
            .Where(t => !t.IsDeleted && t.Date >= from && t.Date <= to)
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<Category>()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Transaction?> GetTransactionAsync(Guid id)
    {
        await EnsureInitializedAsync();
        return await _connection.Table<Transaction>().Where(t => t.Id == id).FirstOrDefaultAsync();
    }

    public async Task<decimal> GetBalanceAsync()
    {
        var transactions = await GetTransactionsAsync();
        return transactions.Sum(t => t.Amount);
    }

    public async Task SaveTransactionAsync(Transaction transaction)
    {
        await EnsureInitializedAsync();
        transaction.UpdatedAt = DateTime.UtcNow;
        await _connection.InsertOrReplaceAsync(transaction);
    }

    public async Task DeleteTransactionAsync(Transaction transaction)
    {
        transaction.IsDeleted = true;
        await SaveTransactionAsync(transaction);
    }

    public async Task SaveCategoryAsync(Category category)
    {
        await EnsureInitializedAsync();
        category.UpdatedAt = DateTime.UtcNow;
        await _connection.InsertOrReplaceAsync(category);
    }

    public async Task DeleteCategoryAsync(Category category)
    {
        category.IsDeleted = true;
        await SaveCategoryAsync(category);
    }

    /// <summary>Tutte le transazioni, incluse quelle cancellate (soft-delete) — per il sync con Drive.</summary>
    public async Task<List<Transaction>> GetAllTransactionsRawAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<Transaction>().ToListAsync();
    }

    /// <summary>Tutte le categorie, incluse quelle cancellate (soft-delete) — per il sync con Drive.</summary>
    public async Task<List<Category>> GetAllCategoriesRawAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<Category>().ToListAsync();
    }

    /// <summary>Scrive una transazione così com'è, senza toccare UpdatedAt — per scrivere il risultato di un merge sync.</summary>
    public async Task SaveTransactionRawAsync(Transaction transaction)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(transaction);
    }

    /// <summary>Scrive una categoria così com'è, senza toccare UpdatedAt — per scrivere il risultato di un merge sync.</summary>
    public async Task SaveCategoryRawAsync(Category category)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(category);
    }
}
