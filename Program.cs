using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace IFP3;

public static class Program
{
    public static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine(">>>Shared Reference Bug Demonstration");
        RunBaselineDemo();
        Console.WriteLine();

        Console.WriteLine(">>>TASK 1 Immutable Record & Custom Equality by ISBN");
        RunTask1Demo();
        Console.WriteLine();

        Console.WriteLine(">>>TASK 2 Price Ordering and Controlled State Changes");
        RunTask2Demo();
        Console.WriteLine();

        Console.WriteLine(">>>TASK 3Catalogue Report, Where(!Equals) Trap & ReferenceEquals Fix");
        RunTask3Demo();
        Console.WriteLine();

        Console.WriteLine(">>>VERIFICATION SUITE Automated Invariant and Boundary Checks");
        RunVerificationSuite();
        Console.WriteLine();
    }

    private static void RunBaselineDemo()
    {
        var legacyCatalog = new List<LegacyBook>
        {
            new() { Title = "Refactoring", Isbn = "111", Price = 45.00m, StockCount = 4 },
            new() { Title = "Clean Code", Isbn = "222", Price = 35.50m, StockCount = 2 },
            new() { Title = "The Pragmatic Programmer", Isbn = "333", Price = 40.00m, StockCount = 6 },
        };

        LegacyBook featured = legacyCatalog[0];
        LegacyBook display = featured;

        display.StockCount = 0;

        Console.WriteLine("display.StockCount set to 0.");
        Console.WriteLine($"featured.StockCount: {featured.StockCount} (Prints 0 because featured and display reference the exact same object!)");
        Console.WriteLine($"ReferenceEquals(featured, display): {ReferenceEquals(featured, display)}");
    }

    private static void RunTask1Demo()
    {
        var catalog = GetInitialCatalogue();

        var defaultA = new DefaultBook("Refactoring", "111", 45.00m, 4);
        var defaultB = new DefaultBook("Refactoring (2nd Edition)", "111", 55.00m, 10);
        bool defaultEqualityResult = defaultA.Equals(defaultB);

        Console.WriteLine("1. Compiler-generated default record equality comparison:");
        Console.WriteLine($"   Book A: Title='{defaultA.Title}', ISBN='{defaultA.Isbn}', Price={defaultA.Price}, Stock={defaultA.StockCount}");
        Console.WriteLine($"   Book B: Title='{defaultB.Title}', ISBN='{defaultB.Isbn}', Price={defaultB.Price}, Stock={defaultB.StockCount}");
        Console.WriteLine("   Prediction: Compiler compares all fields -> FALSE");
        Console.WriteLine($"   Actual defaultA.Equals(defaultB): {defaultEqualityResult}");

        var bookA = catalog[0];
        var bookB = new Book("Refactoring (Revised)", "111", 99.99m, 50);
        var bookC = new Book("Clean Architecture", "999", 45.00m, 4);

        Console.WriteLine("\n2. Custom Book equality override (comparing ONLY ISBN):");
        Console.WriteLine($"   bookA (ISBN {bookA.Isbn}) == bookB (ISBN {bookB.Isbn}, different title/price/stock): {bookA.Equals(bookB)} (Equal Pair)");
        Console.WriteLine($"   bookA (ISBN {bookA.Isbn}) == bookC (ISBN {bookC.Isbn}, same price/stock): {bookA.Equals(bookC)} (Unequal Pair)");
        Console.WriteLine($"   Operator == syntax: (bookA == bookB) -> {bookA == bookB}");
        Console.WriteLine($"   bookA.GetHashCode() == bookB.GetHashCode(): {bookA.GetHashCode() == bookB.GetHashCode()}");
    }

    private static void RunTask2Demo()
    {
        var catalog = GetInitialCatalogue();

        Console.WriteLine("1. Price-sorted view via LINQ OrderBy (creates new sequence):");
        var priceSorted = catalog.OrderBy(b => b).ToList();
        PrintCatalogue(priceSorted);

        Console.WriteLine("\n2. Verification that original catalogue order is completely preserved:");
        PrintCatalogue(catalog);

        Console.WriteLine("\n3. Alternative sorting approach: in-place sort on a defensive copy:");
        var defensiveCopy = new List<Book>(catalog);
        defensiveCopy.Sort();
        Console.WriteLine($"   Defensive copy sorted count: {defensiveCopy.Count}");
        Console.WriteLine($"   Cheapest in copy: {defensiveCopy[0].Title} ({defensiveCopy[0].Price:F2})");
        Console.WriteLine($"   Original catalog[0] still: {catalog[0].Title} ({catalog[0].Price:F2})");

        Console.WriteLine("\n4. Controlled state transitions with private init and with expressions:");
        Book original = catalog[0];
        Console.WriteLine($"   Initial book: {original.Title} | Stock: {original.StockCount}");

        Book restocked = original.Restock(3);
        Console.WriteLine($"   After Restock(3): New Book Stock = {restocked.StockCount} | Original Book Stock = {original.StockCount}");

        Book sold = restocked.Sell(5);
        Console.WriteLine($"   After Sell(5):    New Book Stock = {sold.StockCount} | Previous Book Stock = {restocked.StockCount}");

        Console.WriteLine("\n5. Boundary enforcement (Oversell rejection):");
        try
        {
            Console.WriteLine($"   Attempting to sell 3 copies when current stock is {sold.StockCount}...");
            _ = sold.Sell(3);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"   [SUCCESSFULLY REJECTED] {ex.Message}");
        }

        Console.WriteLine("\n6. Discarding the return value of Sell/Restock:");
        _ = sold.Sell(1);
        Console.WriteLine($"   Called 'sold.Sell(1)' but discarded result. Current sold.StockCount is still: {sold.StockCount}");
    }

    private static void RunTask3Demo()
    {
        var catalog = GetInitialCatalogue();

        Console.WriteLine("1. Price-sorted catalogue view:");
        var priceSorted = catalog.OrderBy(b => b).ToList();
        PrintCatalogue(priceSorted);

        Console.WriteLine("\n2. Adding duplicate ISBN entry to a working copy:");
        var duplicate = new Book("Refactoring (Accidental Duplicate Entry)", "111", 5.00m, 100);
        var catalogueWithDuplicate = new List<Book>(catalog) { duplicate };

        bool duplicateDetected = catalog.Contains(duplicate);
        Console.WriteLine($"   Working copy count: {catalogueWithDuplicate.Count}");
        Console.WriteLine($"   Duplicate detected via equality override: {duplicateDetected}");

        Console.WriteLine("\n3. Naive exclusion: Where(b => !b.Equals(duplicate)):");
        var naiveResult = catalogueWithDuplicate.Where(b => !b.Equals(duplicate)).ToList();
        Console.WriteLine($"   Count after naive exclusion: {naiveResult.Count} (LOST 2 ITEMS!)");
        Console.WriteLine("   Surviving ISBNs: " + string.Join(", ", naiveResult.Select(b => $"{b.Isbn} ({b.Title})")));
        Console.WriteLine("   Explanation: Because Equals compares ONLY by ISBN, both the duplicate AND the legitimate");
        Console.WriteLine("   original book with ISBN 111 match, so !Equals excludes BOTH!");

        Console.WriteLine("\n4. Corrected exclusion: Where(b => !ReferenceEquals(b, duplicate)):");
        var correctedResult = catalogueWithDuplicate.Where(b => !ReferenceEquals(b, duplicate)).ToList();
        Console.WriteLine($"   Count after ReferenceEquals exclusion: {correctedResult.Count} (Dropped by exactly 1!)");
        Book survivingOriginal = correctedResult.Single(b => b.Isbn == "111");
        Console.WriteLine($"   Surviving original book: {survivingOriginal.Title} (ISBN {survivingOriginal.Isbn})");
        Console.WriteLine($"   Original price unchanged: {survivingOriginal.Price:F2} (expected 45.00)");
        Console.WriteLine($"   Original stock unchanged: {survivingOriginal.StockCount} (expected 4)");

        Console.WriteLine("\n5. Stock change simulation on original book:");
        Book book = catalog[0];
        Console.WriteLine($"   Initial stock:        {book.StockCount}");
        Book afterRestock = book.Restock(3);
        Console.WriteLine($"   After Restock(+3):    {afterRestock.StockCount} (original book still: {book.StockCount})");
        Book afterSale = afterRestock.Sell(5);
        Console.WriteLine($"   After Sell(-5):       {afterSale.StockCount} (restocked book still: {afterRestock.StockCount})");

        try
        {
            Console.WriteLine($"   Attempting oversell: Sell(3) with stock 2...");
            _ = afterSale.Sell(3);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"   [Oversell Rejected]: {ex.Message}");
        }
    }

    private static void RunVerificationSuite()
    {
        var runner = new SimpleTestRunner();

        var legacy = new LegacyBook { Title = "X", Isbn = "1", Price = 10m, StockCount = 5 };
        var legacyRef = legacy;
        legacyRef.StockCount = 0;
        runner.AssertEqual(0, legacy.StockCount, "Baseline: LegacyBook mutates across shared reference");

        runner.AssertThrows<ArgumentException>(() => new Book("", "123", 10m, 5), "Constructor rejects empty title");
        runner.AssertThrows<ArgumentException>(() => new Book("Title", "", 10m, 5), "Constructor rejects empty ISBN");
        runner.AssertThrows<ArgumentOutOfRangeException>(() => new Book("Title", "123", -1m, 5), "Constructor rejects negative price");
        runner.AssertThrows<ArgumentOutOfRangeException>(() => new Book("Title", "123", 10m, -1), "Constructor rejects negative stock");

        var b1 = new Book("Refactoring", "111", 45m, 4);
        var b2 = new Book("Different Title", "111", 99m, 99);
        var b3 = new Book("Refactoring", "222", 45m, 4);
        runner.AssertTrue(b1.Equals(b2), "Custom equality: same ISBN implies equal");
        runner.AssertTrue(b1 == b2, "Operator == reflects custom equality");
        runner.AssertEqual(b1.GetHashCode(), b2.GetHashCode(), "GetHashCode is consistent for equal ISBNs");
        runner.AssertFalse(b1.Equals(b3), "Custom equality: different ISBN implies not equal");
        runner.AssertFalse(b1 == b3, "Operator != reflects different ISBN");

        var def1 = new DefaultBook("Refactoring", "111", 45m, 4);
        var def2 = new DefaultBook("Different Title", "111", 99m, 99);
        runner.AssertFalse(def1.Equals(def2), "Default record equality fails when title/price/stock differ");

        runner.AssertTrue(b1.CompareTo(null) > 0, "CompareTo null returns positive integer");
        runner.AssertTrue(b1.CompareTo(b1) == 0, "CompareTo self returns zero");
        var cheapBook = new Book("Cheap", "777", 10m, 1);
        var expensiveBook = new Book("Expensive", "888", 100m, 1);
        runner.AssertTrue(cheapBook.CompareTo(expensiveBook) < 0, "CompareTo orders cheaper books before expensive");

        var baseBook = new Book("T", "123", 20m, 10);
        var restocked = baseBook.Restock(5);
        runner.AssertEqual(10, baseBook.StockCount, "Original book stock remains 10 after Restock");
        runner.AssertEqual(15, restocked.StockCount, "New book stock is 15 after Restock");

        var sold = restocked.Sell(7);
        runner.AssertEqual(15, restocked.StockCount, "Restocked book stock remains 15 after Sell");
        runner.AssertEqual(8, sold.StockCount, "New book stock is 8 after Sell");

        runner.AssertThrows<ArgumentOutOfRangeException>(() => sold.Restock(0), "Restock rejects quantity 0");
        runner.AssertThrows<ArgumentOutOfRangeException>(() => sold.Restock(-2), "Restock rejects negative quantity");
        runner.AssertThrows<ArgumentOutOfRangeException>(() => sold.Sell(0), "Sell rejects quantity 0");
        runner.AssertThrows<ArgumentOutOfRangeException>(() => sold.Sell(-2), "Sell rejects negative quantity");
        runner.AssertThrows<InvalidOperationException>(() => sold.Sell(9), "Sell rejects overselling (8 available, 9 requested)");

        _ = sold.Sell(2);
        runner.AssertEqual(8, sold.StockCount, "Discarding Sell(2) result preserves sold.StockCount at 8");

        var rawCatalog = GetInitialCatalogue();
        var sortedSeq = rawCatalog.OrderBy(b => b).ToList();
        runner.AssertEqual("Clean Code", sortedSeq[0].Title, "Sorted first is Clean Code (35.50)");
        runner.AssertEqual("The Pragmatic Programmer", sortedSeq[1].Title, "Sorted second is Pragmatic Programmer (40.00)");
        runner.AssertEqual("Refactoring", sortedSeq[2].Title, "Sorted third is Refactoring (45.00)");
        runner.AssertEqual("Refactoring", rawCatalog[0].Title, "Original catalogue[0] remains Refactoring");

        var dupBook = new Book("Dup", "111", 1m, 1);
        var withDup = new List<Book>(rawCatalog) { dupBook };
        var naiveExclusion = withDup.Where(b => !b.Equals(dupBook)).ToList();
        runner.AssertEqual(2, naiveExclusion.Count, "Naive Where(!Equals) excludes ALL matching ISBNs (leaves 2)");

        var exactExclusion = withDup.Where(b => !ReferenceEquals(b, dupBook)).ToList();
        runner.AssertEqual(3, exactExclusion.Count, "ReferenceEquals exclusion leaves exactly 3 items");
        var originalRefactoring = exactExclusion.Single(b => b.Isbn == "111");
        runner.AssertEqual("Refactoring", originalRefactoring.Title, "Original Refactoring preserved");
        runner.AssertEqual(45.00m, originalRefactoring.Price, "Original price preserved");
        runner.AssertEqual(4, originalRefactoring.StockCount, "Original stock preserved");

        runner.PrintSummary();
    }

    public static List<Book> GetInitialCatalogue() => new()
    {
        new Book("Refactoring", "111", 45.00m, 4),
        new Book("Clean Code", "222", 35.50m, 2),
        new Book("The Pragmatic Programmer", "333", 40.00m, 6)
    };

    private static void PrintCatalogue(IEnumerable<Book> books)
    {
        foreach (var book in books)
        {
            Console.WriteLine($"   {book.Isbn} | {book.Title,-26} | ${book.Price.ToString("F2", CultureInfo.InvariantCulture),6} | Stock: {book.StockCount}");
        }
    }
}

public class LegacyBook
{
    public string Title { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockCount { get; set; }
}

public sealed record DefaultBook(string Title, string Isbn, decimal Price, int StockCount);

public sealed record Book : IComparable<Book>, IComparable, IEquatable<Book>
{
    public string Title { get; init; }
    public string Isbn { get; init; }
    public decimal Price { get; init; }
    public int StockCount { get; private init; }

    public Book(string title, string isbn, decimal price, int stockCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn, nameof(isbn));
        ArgumentOutOfRangeException.ThrowIfNegative(price, nameof(price));
        ArgumentOutOfRangeException.ThrowIfNegative(stockCount, nameof(stockCount));

        Title = title;
        Isbn = isbn;
        Price = price;
        StockCount = stockCount;
    }

    public bool Equals(Book? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        return StringComparer.Ordinal.Equals(Isbn, other.Isbn);
    }

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Isbn);

    public int CompareTo(Book? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (other is null)
        {
            return 1;
        }

        return Price.CompareTo(other.Price);
    }

    int IComparable.CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is not Book other)
        {
            throw new ArgumentException($"Object must be of type {nameof(Book)}", nameof(obj));
        }

        return CompareTo(other);
    }

    public Book Restock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Restock quantity must be positive.");
        }

        return this with { StockCount = checked(StockCount + quantity) };
    }

    public Book Sell(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Sale quantity must be positive.");
        }

        if (quantity > StockCount)
        {
            throw new InvalidOperationException($"Cannot sell {quantity} book(s): only {StockCount} in stock.");
        }

        return this with { StockCount = StockCount - quantity };
    }

    public override string ToString() =>
        $"{Isbn} | {Title} | ${Price.ToString("F2", CultureInfo.InvariantCulture)} | Stock: {StockCount}";
}

public sealed class SimpleTestRunner
{
    private int _passed;
    private int _failed;
    private readonly List<string> _failures = new();

    public void AssertEqual<T>(T expected, T actual, string description)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
        {
            _passed++;
            Console.WriteLine($"   [PASS] {description}");
        }
        else
        {
            _failed++;
            string msg = $"{description} -> Expected: <{expected}>, Actual: <{actual}>";
            _failures.Add(msg);
            Console.WriteLine($"   [FAIL] {msg}");
        }
    }

    public void AssertTrue(bool condition, string description)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"   [PASS] {description}");
        }
        else
        {
            _failed++;
            string msg = $"{description} -> Expected TRUE, but got FALSE";
            _failures.Add(msg);
            Console.WriteLine($"   [FAIL] {msg}");
        }
    }

    public void AssertFalse(bool condition, string description)
    {
        if (!condition)
        {
            _passed++;
            Console.WriteLine($"   [PASS] {description}");
        }
        else
        {
            _failed++;
            string msg = $"{description} -> Expected FALSE, but got TRUE";
            _failures.Add(msg);
            Console.WriteLine($"   [FAIL] {msg}");
        }
    }

    public void AssertThrows<TException>(Action action, string description) where TException : Exception
    {
        try
        {
            action();
            _failed++;
            string msg = $"{description} -> Expected exception {typeof(TException).Name} was NOT thrown.";
            _failures.Add(msg);
            Console.WriteLine($"   [FAIL] {msg}");
        }
        catch (TException)
        {
            _passed++;
            Console.WriteLine($"   [PASS] {description}");
        }
        catch (Exception wrongEx)
        {
            _failed++;
            string msg = $"{description} -> Expected {typeof(TException).Name}, but got {wrongEx.GetType().Name}: {wrongEx.Message}";
            _failures.Add(msg);
            Console.WriteLine($"   [FAIL] {msg}");
        }
    }

    public void PrintSummary()
    {
        Console.WriteLine($"Verification Summary: Passed = {_passed}, Failed = {_failed}, Total = {_passed + _failed}");
        if (_failed == 0)
        {
            Console.WriteLine("All invariant and domain assertions passed successfully!");
        }
        else
        {
            Console.WriteLine("Failures detected:");
            foreach (var f in _failures)
            {
                Console.WriteLine($" - {f}");
            }
        }
    }
}