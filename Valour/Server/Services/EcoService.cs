﻿using System.Collections.Concurrent;
using Valour.Server.Database;
using Valour.Server.Workers.Economy;
using Valour.Shared;
using Valour.Shared.Models;
using Valour.Shared.Models.Economy;

namespace Valour.Server.Services;

/// <summary>
/// The EcoService handles economic transactions and trading for platform-wide
/// and planet economies
/// </summary>
public class EcoService
{
    private readonly ValourDB _db;
    private readonly ILogger<EcoService> _logger;
    private readonly CoreHubService _coreHub;

    /// <summary>
    /// Cache for currency definitions
    /// </summary>
    private readonly ConcurrentDictionary<long, Currency> _currencyCache = new();

    public EcoService(ValourDB db, ILogger<EcoService> logger, CoreHubService coreHub)
    {
        _db = db;
        _logger = logger;
        _coreHub = coreHub;
    }

    ////////////////
    // Currencies //
    ////////////////

    public async ValueTask<Currency> GetPlanetCurrencyAsync(long planetId) => 
        (await _db.Currencies.FirstOrDefaultAsync(x => x.PlanetId == planetId)).ToModel();

    /// <summary>
    /// Returns the currency with the given id
    /// </summary>
    public async ValueTask<Currency> GetCurrencyAsync(long id) {

        _currencyCache.TryGetValue(id, out var currency);

        if (currency is null)
        {
           currency = (await _db.Currencies.FindAsync(id)).ToModel();

            if (currency is not null)
                _currencyCache.TryAdd(id, currency);
        }

        return currency;
    }

    /// <summary>
    /// Creates the given currency
    /// </summary>
    public async Task<TaskResult<Currency>> CreateCurrencyAsync(Currency newCurrency)
    {
        if (newCurrency is null)
            return new TaskResult<Currency>(false, "Given value is null");

        var exists = await _db.Currencies.AnyAsync(x => x.PlanetId == newCurrency.PlanetId);

        if (exists)
            return new TaskResult<Currency>(false, "Planet already has a currency");

        var planetExists = await _db.Planets.AnyAsync(x => x.Id == newCurrency.PlanetId);
        if (!planetExists)
            return new TaskResult<Currency>(false, "Planet does not exist");

        var validation = ValidateCurrency(newCurrency);
        if (!validation.Success)
            return new TaskResult<Currency>(false, validation.Message);

        newCurrency.Id = IdManager.Generate();
        newCurrency.Issued = 0; // Issuance is managed via other means
        var dbCurrency = newCurrency.ToDatabase();

        var tran = await _db.Database.BeginTransactionAsync();
        
        try
        {
            await _db.Currencies.AddAsync(dbCurrency);
            await _db.SaveChangesAsync();
            await tran.CommitAsync();
        }
        catch (Exception e)
        {
            await tran.RollbackAsync();
            _logger.LogError("Error adding currency: {Message}", e.Message);
            return new TaskResult<Currency>(false, "Error adding currency");
        }

        _coreHub.NotifyCurrencyChange(newCurrency);

        return new TaskResult<Currency>(true, "Currency added successfully", newCurrency);
    }

    /// <summary>
    /// Updates the given currency
    /// </summary>
    public async Task<TaskResult<Currency>> UpdateCurrencyAsync(Currency updated)
    {
        var validation = ValidateCurrency(updated);
        if (!validation.Success)
            return new TaskResult<Currency>(false, validation.Message);

        var old = await _db.Currencies.FindAsync(updated.Id);
        if (old is null)
            return new TaskResult<Currency>(false, "Currency not found");

        if (updated.PlanetId != old.PlanetId)
            return new TaskResult<Currency>(false, "Planet Id cannot be changed");

        if (updated.DecimalPlaces < old.DecimalPlaces)
            return new TaskResult<Currency>(false, "You cannot remove decimals");

        if (updated.Issued != old.Issued)
            return new TaskResult<Currency>(false, "You cannot directly edit the issued amount");

        await using var tran = await _db.Database.BeginTransactionAsync();

        try
        {
            _db.Entry(old).CurrentValues.SetValues(updated);
            _db.Currencies.Update(old);
            await _db.SaveChangesAsync();
            await tran.CommitAsync();
        }
        catch (Exception e)
        {
            await tran.RollbackAsync();
            _logger.LogError(e, "Error updating currency: {Message}", e.Message);
            return new TaskResult<Currency>(false, "Error updating currency");
        }

        _currencyCache[updated.Id] = updated;

        _coreHub.NotifyCurrencyChange(updated);

        return new TaskResult<Currency>(true, "Currency updated successfully", updated);
    }

    /// <summary>
    /// Validates the state of a currency
    /// </summary>
    public static TaskResult ValidateCurrency(Currency currency)
    {
        if (currency.Name.Length > 20)
            return TaskResult.FromError("Max name length is 20 characters");

        if (string.IsNullOrEmpty(currency.Name))
            return TaskResult.FromError("Currency must have a name");

        if (currency.PluralName.Length > 20)
            return TaskResult.FromError("Max name plural length is 20 characters");

        if (string.IsNullOrEmpty(currency.PluralName))
            return TaskResult.FromError("Currency must have a plural name");

        if (currency.ShortCode.Length > 5)
            return TaskResult.FromError("Max shortcode length is 5 characters");

        if (string.IsNullOrEmpty(currency.ShortCode))
            return TaskResult.FromError("Currency must have a shortcode");

        if (currency.Symbol.Length > 5)
            return TaskResult.FromError("Max symbol length is 5 characters");

        if (string.IsNullOrEmpty(currency.Symbol))
            return TaskResult.FromError("Currency must have a symbol");

        if (currency.DecimalPlaces > 8)
            return TaskResult.FromError("Currency can have max 8 decimals");

        if (currency.DecimalPlaces < 0)
            return TaskResult.FromError("Negative decimals are not allowed");

        return TaskResult.SuccessResult;
    }

    //////////////
    // Accounts //
    //////////////

    /// <summary>
    /// Returns the account with the given id
    /// </summary>
    public async ValueTask<EcoAccount> GetAccountAsync(long id) =>
        (await _db.EcoAccounts.FindAsync(id)).ToModel();

    /// <summary>
    /// Returns the user account with the given user and planet ids
    /// </summary>
    public async ValueTask<EcoAccount> GetUserAccountAsync(long userId, long planetId) =>
        (await _db.EcoAccounts.FirstOrDefaultAsync(x => x.UserId == userId && x.PlanetId == planetId && x.AccountType == AccountType.User)).ToModel();
    
    /// <summary>
    /// Returns the planet accounts for the given planet id
    /// </summary>
    public async ValueTask<List<EcoAccount>> GetPlanetPlanetAccountsAsync(long planetId) =>
        await _db.EcoAccounts.Where(x => x.AccountType == AccountType.Planet && x.PlanetId == planetId).Select(x => x.ToModel()).ToListAsync();
    
    /// <summary>
    /// Returns the user accounts for the given planet id
    /// </summary>
    public async ValueTask<List<EcoAccount>> GetPlanetUserAccountsAsync(long planetId) =>
        await _db.EcoAccounts.Where(x => x.AccountType == AccountType.User && x.PlanetId == planetId).Select(x => x.ToModel()).ToListAsync();
    
    /// <summary>
    /// Returns all accounts associated with a user id
    /// </summary>
    public async ValueTask<List<EcoAccount>> GetAccountsAsync(long userId) =>
        await _db.EcoAccounts.Where(x => x.UserId == userId).Select(x => x.ToModel()).ToListAsync();
    
    public async ValueTask<TaskResult<EcoAccount>> CreateEcoAccountAsync(EcoAccount account)
    {
        if (account is null)
            return new TaskResult<EcoAccount>(false, "Account is null");

        var planet = await _db.Planets.FindAsync(account.PlanetId);
        if (planet is null)
            return new TaskResult<EcoAccount>(false, "Planet not found");
        
        if (!await _db.PlanetMembers.AnyAsync(x => x.UserId == account.UserId && x.PlanetId == account.PlanetId))
            return new TaskResult<EcoAccount>(false, "User is not a member of the planet");

        if (string.IsNullOrWhiteSpace(account.Name))
        {
            account.Name = $"{planet.Name.Truncate(15)}-{Guid.NewGuid().ToString().Truncate(5)}";
        }
        
        if (account.Name.Length > 20)
            return new TaskResult<EcoAccount>(false, "Max account name length is 20");
        
        if (account.BalanceValue > 0)
            return new TaskResult<EcoAccount>(false, "Initial balance must be zero");

        if (account.AccountType == AccountType.User)
        {
            if (await _db.EcoAccounts.AnyAsync(x => x.UserId == account.UserId && x.PlanetId == account.PlanetId && x.AccountType == AccountType.User))
                return new TaskResult<EcoAccount>(false, "User already has an account on this planet");
        }

        if (!await _db.Currencies.AnyAsync(x => x.PlanetId == account.PlanetId && x.Id == account.CurrencyId))
            return new TaskResult<EcoAccount>(false, "Currency is invalid for given planet");

        account.Id = IdManager.Generate();
        
        await using var tran = await _db.Database.BeginTransactionAsync();
        
        try
        {
            await _db.EcoAccounts.AddAsync(account.ToDatabase());
            await _db.SaveChangesAsync();
            await tran.CommitAsync();
        }
        catch (Exception e)
        {
            await tran.RollbackAsync();
            _logger.LogError(e, "Error creating account: {Message}", e.Message);
            return new TaskResult<EcoAccount>(false, "Error creating account");
        }
        
        return new TaskResult<EcoAccount>(true, "Account created successfully", account);
    }

    //////////////////
    // Transactions //
    //////////////////

    /// <summary>
    /// Returns the transaction with the given id
    /// </summary>
    public async ValueTask<Transaction> GetTransactionAsync(long id) =>
        (await _db.Transactions.FindAsync(id)).ToModel();

    /// <summary>
    /// Returns the last (count) transactions for the given account id
    /// </summary>
    public async Task<List<Transaction>> GetLastTransactionsAsync(long accountId, int count = 50)
    {
        // Get transactions in either direction ordered by time
        return await _db.Transactions
            .Where(x => x.AccountFromId == accountId || x.AccountToId == accountId)
            .OrderByDescending(x => x.TimeStamp)
            .Select(x => x.ToModel())
            .ToListAsync();
    }

    public async ValueTask<TaskResult<Transaction>> CreateTransactionAsync(Transaction transaction)
    {
        if (transaction is null)
            return new TaskResult<Transaction>(false, "Null transaction");
        
        if (transaction.AccountFromId == transaction.AccountToId)
            return new TaskResult<Transaction>(false, "Cannot send to self");
        
        if (transaction.Amount <= 0)
            return new TaskResult<Transaction>(false, "Amount must be positive");

        var fromAccount = await _db.EcoAccounts.FindAsync(transaction.AccountFromId);
        if (fromAccount is null)
            return new TaskResult<Transaction>(false, "Could not find from account");
        
        var currency = await GetCurrencyAsync(fromAccount.CurrencyId);
        if (currency is null)
            return new TaskResult<Transaction>(false, "Critical error: Currency not found");
        
        // Round per the currency's decimal places
        transaction.Amount = Math.Round(transaction.Amount, currency.DecimalPlaces);
        
        if (fromAccount.BalanceValue < transaction.Amount)
            return new TaskResult<Transaction>(false, "Insufficient funds");
        
        var toAccount = await _db.EcoAccounts.FindAsync(transaction.AccountToId);
        if (toAccount is null)
            return new TaskResult<Transaction>(false, "Could not find to account");

        if (fromAccount.CurrencyId != toAccount.CurrencyId)
            return new TaskResult<Transaction>(false, "Account currencies do not match. Use Exchange API instead.");

        // Set id before processing
        transaction.Id = Guid.NewGuid().ToString();

        // Post transaction to queue
        TransactionWorker.AddToQueue(transaction);

        return new TaskResult<Transaction>(true, "Transaction has been queued.", transaction);
    }

    /// <summary>
    /// This method should really only be called by the TransactionWorker within nodes.
    /// Manually calling this can break transaction ordering!
    /// </summary>
    public async Task<TaskResult> ProcessTransactionAsync(Transaction transaction, CoreHubService injectedHub)
    {
        // Fun case for those who wish to break the system
        // Throwbacks to SV1
        if (transaction.Amount < 0)
            return new TaskResult(false, "Amount must be positive");

        // Global Valour Credits transaction
        var isGlobal = (transaction.PlanetId == ISharedPlanet.ValourCentralId);

        var fromAcc = await GetAccountAsync(transaction.AccountFromId);
        if (fromAcc is null)
            return new TaskResult(false, "Could not find sending account");
        
        var toAcc = await GetAccountAsync(transaction.AccountToId);
        if (toAcc is null)
            return new TaskResult(false, "Could not find receiving account");

        if (fromAcc.CurrencyId != toAcc.CurrencyId)
            return new TaskResult(false, "Currency mismatch");

        // Get currency from sending account. Both should be the same anyways.
        var currency = await GetCurrencyAsync(fromAcc.CurrencyId);

        // Get amount rounded to decimals allowed by currency
        transaction.Amount = Math.Round(transaction.Amount, currency.DecimalPlaces);

        // Make sure sender isn't too broke
        if (transaction.Amount > fromAcc.BalanceValue)
            return new TaskResult(false, "Sender cannot afford this transaction");

        await using var trans = await _db.Database.BeginTransactionAsync();

        if (string.IsNullOrWhiteSpace(transaction.Id))
        {
            transaction.Id = Guid.NewGuid().ToString();
        }

        fromAcc.BalanceValue -= transaction.Amount;
        toAcc.BalanceValue += transaction.Amount;

        try
        {
            // Build transaction for database
            var dbTrans = transaction.ToDatabase();
            _db.Transactions.Add(dbTrans);

            await _db.SaveChangesAsync();
            await trans.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await trans.RollbackAsync();

            return new TaskResult(false, "Another transaction modified your account before processing finished. Please try again.");
        }
        catch(Exception)
        {
            await trans.RollbackAsync();
            return new TaskResult(false, "An unexpected error occured. Try again?");
        }

        if (isGlobal)
        {
            injectedHub.NotifyGlobalTransactionProcessed(transaction);
        }
        else
        {
            injectedHub.NotifyPlanetTransactionProcessed(transaction);
        }

        return TaskResult.SuccessResult;
    }
}
