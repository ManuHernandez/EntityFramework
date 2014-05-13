// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Data.Entity.Metadata;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Xunit;

namespace Microsoft.Data.Entity.AzureTableStorage.FunctionalTests
{
    public class EmulatorEndToEndTests : IDisposable
    {
        private EmulatorContext _context;
        private CloudTable _table;
        private const string _testPartition = "unittests";

        #region setup

        public EmulatorEndToEndTests()
        {
            _context = new EmulatorContext();

            var account = CloudStorageAccount.Parse("UseDevelopmentStorage=true");
            var tableClient = account.CreateCloudTableClient();
            _table = tableClient.GetTableReference("AzureStorageEmulatorEntity");
            _table.CreateIfNotExists();

            var deleteTest = new AzureStorageEmulatorEntity
                {
                    PartitionKey = _testPartition,
                    RowKey = "It_deletes_entity_test",
                };

            _table.Execute(TableOperation.Insert(deleteTest));

            for (var i = 0; i < 2; i++)
            {
                var findTest = new AzureStorageEmulatorEntity
                    {
                        PartitionKey = _testPartition,
                        RowKey = "It_finds_entity_test_" + i
                    };
                _table.Execute(TableOperation.Insert(findTest));
            }
        }

        private class EmulatorContext : DbContext
        {
            public DbSet<AzureStorageEmulatorEntity> BooFars { get; set; }

            protected override void OnConfiguring(DbContextOptions builder)
            {
                builder.UseAzureTableStorge("UseDevelopmentStorage=true;");
            }

            protected override void OnModelCreating(ModelBuilder builder)
            {
                builder.Entity<AzureStorageEmulatorEntity>().Key(s => s.Key);
            }

        }

        private class AzureStorageEmulatorEntity : TableEntity
        {
            public string Key
            {
                get { return PartitionKey + RowKey; }
            }

            public double Cost { get; set; }
            public string Name { get; set; }
            public DateTime Purchased { get; set; }
            public int Count { get; set; }
            public Guid GlobalGuid { get; set; }
            public bool Awesomeness { get; set; }
        }

        #endregion

        [Fact]
        public void It_adds_entity()
        {
            _context.BooFars.Add(new AzureStorageEmulatorEntity
                {
                    PartitionKey = _testPartition,
                    RowKey = "It_adds_entity_test",
                    Name = "Anchorage",
                    GlobalGuid = new Guid(),
                    Cost = 32145.2342,
                    Count = 324234959,
                    Purchased = DateTime.Parse("Tue, 13 May 2014 01:08:13 GMT"),
                    Awesomeness = true,
                });
            var changes = _context.SaveChanges();
            Assert.Equal(1, changes);
        }
        [Fact]
        public void It_finds_entities()
        {
            var rows = _context.BooFars.Where(s => s.PartitionKey == _testPartition && s.RowKey.StartsWith("It_finds_entity_test_"));
            Assert.Equal(2, rows.Count());
        }

        [Fact]
        public void It_deletes_entity()
        {
            var tableRow = _context.BooFars.First(s => s.PartitionKey == _testPartition && s.RowKey == "It_deletes_entity_test");
            _context.Delete(tableRow);
            var changes = _context.SaveChanges();
            Assert.Equal(1,changes);
        }

        public void Dispose()
        {
            _table.DeleteIfExists();
        }
    }
}