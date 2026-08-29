// Verification harness for the SQL Server -> PostgreSQL code transformation.
// Exercises the converted data layer against a real PostgreSQL database:
//   1) opens an Npgsql-backed ApplicationDbContext
//   2) EnsureCreated() -> generates the schema from the EF model + inserts HasData seed rows
//   3) reads seeded data back (counts + sample rows)
//   4) performs a runtime INSERT/SELECT/DELETE (CRUD) on Customer
using System;
using System.Linq;
using Bookstore.Data;
using Bookstore.Domain.Customers;
using Microsoft.EntityFrameworkCore;

var conn = Environment.GetEnvironmentVariable("PGCONN")
    ?? "Host=localhost;Port=5432;Database=BobsUsedBookStore;Username=postgres;Password=postgres";

Console.WriteLine("=== Bob's Used Bookstore - PostgreSQL verification ===");
Console.WriteLine("Connection: " + conn.Replace("Password=postgres", "Password=***"));

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(conn)
    .Options;

using var ctx = new ApplicationDbContext(options);

Console.WriteLine("Provider: " + ctx.Database.ProviderName);
Console.WriteLine("CanConnect: " + ctx.Database.CanConnect());

Console.WriteLine("Running EnsureCreated() (create schema from EF model + seed HasData)...");
bool created = ctx.Database.EnsureCreated();
Console.WriteLine("EnsureCreated returned: " + created);

int refCount = ctx.ReferenceData.Count();
int bookCount = ctx.Book.Count();
Console.WriteLine("ReferenceData rows: " + refCount + " (expected 24)");
Console.WriteLine("Book rows: " + bookCount + " (expected 8)");

Console.WriteLine("Sample books:");
foreach (var b in ctx.Book.OrderBy(x => x.Id).Take(3))
    Console.WriteLine("  #" + b.Id + " " + b.Name + " / " + b.Author + " / price=" + b.Price);

// Runtime CRUD on Customer (no HasData seed -> fresh identity, no sequence conflict)
var sub = "verify-" + Guid.NewGuid().ToString("N").Substring(0, 8);
var cust = new Customer(sub) { FirstName = "Ver", LastName = "Ify", Email = "v@example.com" };
ctx.Customer.Add(cust);
ctx.SaveChanges();
Console.WriteLine("Inserted Customer id=" + cust.Id + " sub=" + cust.Sub + " createdOn=" + cust.CreatedOn.ToString("o"));

var readBack = ctx.Customer.Single(c => c.Sub == sub);
Console.WriteLine("Read back Customer id=" + readBack.Id + " fullName=" + readBack.FullName);

ctx.Customer.Remove(readBack);
ctx.SaveChanges();
Console.WriteLine("Deleted test customer.");

bool ok = refCount == 24 && bookCount == 8 && cust.Id > 0;
Console.WriteLine(ok ? "RESULT: VERIFICATION_OK" : "RESULT: VERIFICATION_FAILED");
Environment.Exit(ok ? 0 : 1);
