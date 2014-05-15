// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Configuration;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.Entity.AzureTableStorage.FunctionalTests
{
    public class BatchTests : IClassFixture<CloudTableFixture>
    {
        private TestContext _context;
        private CloudTable _table;
        private string _testParition;

        public BatchTests(CloudTableFixture fixture)
        {

            var connectionString = ConfigurationManager.AppSettings["TestConnectionString"];
            _context = new TestContext();
            fixture.GetOrCreateTable("AzureStorageBatchEmulatorEntity", connectionString);
            fixture.DeleteOnDispose = true;
            _testParition = "BatchTests-" + DateTime.UtcNow.ToString("O");
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        public void It_creates_many_items(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var item = new AzureStorageBatchEmulatorEntity { Count = i, PartitionKey = _testParition, RowKey = i.ToString() };
                _context.Items.Add(item);
            }
            var changes= _context.SaveChanges();

            //var query = new TableQuery();
            //query.Where("ParitionKey=" + _testParition);
            //var actual = _table.ExecuteQuery(query);
            //actual.
            Assert.Equal(count,changes); 
        }

        private class TestContext : DbContext
        {
            public DbSet<AzureStorageBatchEmulatorEntity> Items { get; set; }
            protected override void OnModelCreating(Metadata.ModelBuilder builder)
            {
                builder.Entity<AzureStorageBatchEmulatorEntity>().Key(s => s.RowKey);
            }

            protected override void OnConfiguring(DbContextOptions builder)
            {
                builder.UseAzureTableStorge(ConfigurationManager.AppSettings["TestConnectionString"]);
            }
        }
        private class AzureStorageBatchEmulatorEntity : TableEntity
        {
            public int Count { get; set; }
        }

    }

}