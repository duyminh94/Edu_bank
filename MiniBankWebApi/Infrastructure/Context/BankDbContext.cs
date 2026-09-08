using MiniBankWebApi.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MiniBankWebApi.Infrastructure.Context
{
    public class BankDbContext : DbContext
    {
        public BankDbContext(DbContextOptions<BankDbContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Transfer> Transfers => Set<Transfer>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Account>(entity =>
            {
                entity.HasIndex(a => a.AccountNumber).IsUnique();
                entity.HasIndex(a => a.IdentityNumber).IsUnique();
                entity.Property(a => a.Balance).HasPrecision(18, 2).IsConcurrencyToken();
            });

            modelBuilder.Entity<Transfer>(entity =>
            {
                entity.Property(t => t.Amount).HasPrecision(18, 2);

                entity.HasOne(t => t.FromAccount)
                      .WithMany()
                      .HasForeignKey(t => t.FromAccountId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.ToAccount)
                      .WithMany()
                      .HasForeignKey(t => t.ToAccountId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(r => r.Token);

                entity.HasOne(r => r.Account)
                      .WithMany()
                      .HasForeignKey(r => r.AccountId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Account>().HasData(
                new Account
                {
                    Id = 1,
                    AccountNumber = "1001",
                    FullName = "Nguyen Van A",
                    PasswordHash = "AQAAAAIAAYagAAAAEPYwH/iKW5GgLySpI2/onbP/0cJ1RowNKyvMKmtfK+dWhfJrXYpBi1bVvKLhGyISsQ==",
                    Email = "doi-thanh-mail-cua-ban@gmail.com",
                    PhoneNumber = "0900000001",
                    DateOfBirth = new DateTime(1990, 1, 15),
                    IdentityNumber = "079090000001",
                    Address = "12 Le Loi, Quan 1, TP HCM",
                    Balance = 50000000m
                },
                new Account
                {
                    Id = 2,
                    AccountNumber = "1002",
                    FullName = "Tran Thi B",
                    PasswordHash = "AQAAAAIAAYagAAAAECMPGYknCrkGyNB9eJCrxUyjd5QBddNJnL129v2HV13ItjgYQUfzrGtY8kFN9kLBKg==",
                    Email = "doi-thanh-mail-thu-hai@gmail.com",
                    PhoneNumber = "0900000002",
                    DateOfBirth = new DateTime(1992, 5, 20),
                    IdentityNumber = "079092000002",
                    Address = "45 Nguyen Hue, Quan 1, TP HCM",
                    Balance = 20000000m
                },
                new Account
                {
                    Id = 3,
                    AccountNumber = "1003",
                    FullName = "Le Van C",
                    PasswordHash = "AQAAAAIAAYagAAAAELgZE1F2f1855C2hspjAOubCWc7zdTQ9BJjk6LXP6T+uCK3KtQnLyO0Cr/5j73Oj9w==",
                    Email = "doi-thanh-mail-thu-ba@gmail.com",
                    PhoneNumber = "0900000003",
                    DateOfBirth = new DateTime(1988, 11, 2),
                    IdentityNumber = "079088000003",
                    Address = "9 Tran Hung Dao, Quan 5, TP HCM",
                    Balance = 30000000m
                },
                new Account
                {
                    Id = 4,
                    AccountNumber = "1004",
                    FullName = "Pham Thi D",
                    PasswordHash = "AQAAAAIAAYagAAAAEPv2TqJtK60jilbg5uHkN0g4LKHxojo8gYoQM7CyRsGUh/u/dpBB/K9r46ypJOLrBw==",
                    Email = "doi-thanh-mail-thu-tu@gmail.com",
                    PhoneNumber = "0900000004",
                    DateOfBirth = new DateTime(1995, 7, 8),
                    IdentityNumber = "079095000004",
                    Address = "78 Vo Van Tan, Quan 3, TP HCM",
                    Balance = 15000000m
                }
            );
        }
    }
}
