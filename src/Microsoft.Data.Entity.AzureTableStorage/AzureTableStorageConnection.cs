// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.AzureTableStorage.Interfaces;
using Microsoft.Data.Entity.AzureTableStorage.Wrappers;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Storage;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Linq;

namespace Microsoft.Data.Entity.AzureTableStorage
{
    public class AzureTableStorageConnection : DataStoreConnection
    {
        private readonly string _connectionString;
        private readonly CloudStorageAccountWrapper _account;

        /// <summary>
        /// For testing
        /// </summary>
        protected AzureTableStorageConnection() { }
        public AzureTableStorageConnection(DbContextConfiguration configuration)
        {
            var storeConfig = configuration
                .ContextOptions
                .Extensions
                .OfType<AzureTableStorageConfigurationExtension>()
                .Single();

            _connectionString = storeConfig.ConnectionString;

            _account = new CloudStorageAccountWrapper(_connectionString);
        }

        public CloudStorageAccountWrapper Account
        {
            get { return _account; }
        }

        public virtual ICloudTable GetTableReference(string tableName)
        {
            return new CloudTableWrapper(_account.CreateCloudTableClient().GetTableReference(tableName));
        }
    }
}
