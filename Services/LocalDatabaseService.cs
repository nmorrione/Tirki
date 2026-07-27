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

    /// <summary>
    /// Categorie precompilate al primo avvio, derivate analizzando le descrizioni ricorrenti nello
    /// storico reale di spese dell'utente (spesa/ristoranti/trasporti/casa/ecc.), così chi installa
    /// l'app da zero parte con categorie sensate invece che vuote.
    /// </summary>
    private static readonly (string Name, string ColorHex)[] DefaultCategories =
    [
        ("Spesa", "#4CAF50"),
        ("Ristoranti e bar", "#FF7043"),
        ("Trasporti", "#2196F3"),
        ("Casa e bollette", "#795548"),
        ("Salute e benessere", "#E53935"),
        ("Svago e tempo libero", "#FFB300"),
        ("Abbonamenti", "#8E24AA"),
        ("Regali", "#EC407A"),
        ("Shopping", "#26A69A"),
        ("Stipendio", "#009688"),
        ("Investimenti", "#3949AB"),
        ("Altro", "#9E9E9E"),
    ];

    private async Task InitializeAsync()
    {
        await _connection.CreateTableAsync<Transaction>();
        await _connection.CreateTableAsync<Category>();
        await _connection.CreateTableAsync<TransactionPreset>();
        await SeedDefaultCategoriesIfNeededAsync();
    }

    /// <summary>
    /// Una tantum: se non è mai stato fatto, precompila le categorie di default. Il flag (non il
    /// semplice "tabella vuota") evita che ricompaiano se l'utente le cancella tutte di proposito.
    /// </summary>
    private async Task SeedDefaultCategoriesIfNeededAsync()
    {
        if (Preferences.Default.Get(AppPreferenceKeys.CategoriesSeeded, false)) return;

        foreach (var (name, colorHex) in DefaultCategories)
        {
            await _connection.InsertOrReplaceAsync(new Category
            {
                Name = name,
                ColorHex = colorHex,
            });
        }

        Preferences.Default.Set(AppPreferenceKeys.CategoriesSeeded, true);
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

    public async Task<List<TransactionPreset>> GetPresetsAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<TransactionPreset>()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Description)
            .ToListAsync();
    }

    public async Task SavePresetAsync(TransactionPreset preset)
    {
        await EnsureInitializedAsync();
        preset.UpdatedAt = DateTime.UtcNow;
        await _connection.InsertOrReplaceAsync(preset);
    }

    public async Task DeletePresetAsync(TransactionPreset preset)
    {
        preset.IsDeleted = true;
        await SavePresetAsync(preset);
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

    /// <summary>Tutti i preset, inclusi quelli cancellati (soft-delete) — per il sync con Drive.</summary>
    public async Task<List<TransactionPreset>> GetAllPresetsRawAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<TransactionPreset>().ToListAsync();
    }

    /// <summary>Scrive un preset così com'è, senza toccare UpdatedAt — per scrivere il risultato di un merge sync.</summary>
    public async Task SavePresetRawAsync(TransactionPreset preset)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(preset);
    }
}
