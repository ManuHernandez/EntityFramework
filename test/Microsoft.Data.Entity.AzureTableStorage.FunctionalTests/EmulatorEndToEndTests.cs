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
        private AzureStorageEmulatorEntity _sampleEntity;
        private const string _testPartition = "unittests";
        private const string _testConnectionString = "UseDevelopmentStorage=true";
        #region setup

        public EmulatorEndToEndTests()
        {
            _context = new EmulatorContext();

            var account = CloudStorageAccount.Parse(_testConnectionString);
            var tableClient = account.CreateCloudTableClient();
            _table = tableClient.GetTableReference("AzureStorageEmulatorEntity");
            _table.CreateIfNotExists();
            var setupBatch = new TableBatchOperation();
            var deleteTest = new AzureStorageEmulatorEntity
                {
                    PartitionKey = _testPartition,
                    RowKey = "It_deletes_entity_test",
                    Purchased = DateTime.Now,
                };

            setupBatch.Add(TableOperation.Insert(deleteTest));

            for (var i = 0; i < 20; i++)
            {
                var findTest = new AzureStorageEmulatorEntity
                    {
                        PartitionKey = _testPartition,
                        RowKey = "It_finds_entity_test_" + i,
                        Purchased = DateTime.Now,
                    };
                setupBatch.Add(TableOperation.Insert(findTest));
            }

            _sampleEntity = new AzureStorageEmulatorEntity
                {
                    PartitionKey = _testPartition,
                    RowKey = "Sample_entity",
                    Name = "Sample",
                    GlobalGuid = new Guid(),
                    Cost = -234.543,
                    Count = 359,
                    Purchased = DateTime.Parse("Tue, 1 Jan 2013 22:11:20 GMT"),
                    Awesomeness = true,
                };
            setupBatch.Add(TableOperation.Insert(_sampleEntity));
            _table.ExecuteBatch(setupBatch);

        }

        private class EmulatorContext : DbContext
        {
            public DbSet<AzureStorageEmulatorEntity> BooFars { get; set; }

            protected override void OnConfiguring(DbContextOptions builder)
            {
                builder.UseAzureTableStorge(_testConnectionString);
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
            // override object.Equals
            public override bool Equals(object obj)
            {
                var other = obj as AzureStorageEmulatorEntity;
                if (other == null)
                    return false;
                else if (Key != other.Key)
                    return false;
                else if (Cost != other.Cost)
                    return false;
                else if (Name != other.Name)
                    return false;
                else if (Count != other.Count)
                    return false;
                else if (!GlobalGuid.Equals(other.GlobalGuid))
                    return false;
                else if (Awesomeness != other.Awesomeness)
                    return false;
                return Purchased.ToUniversalTime().Equals(other.Purchased.ToUniversalTime());
            }
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
            Assert.Equal(20, rows.Count());
        }

        [Fact(Skip = "Emulator does not force exceptions, but production servers do")]
        public void It_handles_out_of_range_dates()
        {
            var lowDate = new AzureStorageEmulatorEntity
                {
                    PartitionKey = "unittests",
                    RowKey = "DateOutOfRange",
                    Purchased = DateTime.Parse("Dec 31, 1600 23:59:00 GMT")
                };
            _context.BooFars.Add(lowDate);
            Assert.Throws<AggregateException>(() =>
                {
                    _context.SaveChanges();
                });
        }

        [Fact]
        public void It_materializes_entity()
        {
            var actual = _context.BooFars.First(s => s.RowKey == "Sample_entity");
            Assert.True(_sampleEntity.Equals(actual));
        }


        [Fact]
        public void It_deletes_entity()
        {
            var tableRow = _context.BooFars.First(s => s.PartitionKey == _testPartition && s.RowKey == "It_deletes_entity_test");
            _context.Delete(tableRow);
            var changes = _context.SaveChanges();
            Assert.Equal(1, changes);
        }

        public void Dispose()
        {
            _table.DeleteIfExists();
        }
    }
}