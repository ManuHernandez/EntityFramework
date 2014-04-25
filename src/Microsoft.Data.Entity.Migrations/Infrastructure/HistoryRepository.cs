// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations.Utilities;

namespace Microsoft.Data.Entity.Migrations.Infrastructure
{
    public class HistoryRepository
    {
        private readonly DbContextConfiguration _contextConfiguration;
        private IModel _historyModel;
        private bool _exists;

        public HistoryRepository([NotNull] DbContextConfiguration contextConfiguration)
        {
            Check.NotNull(contextConfiguration, "contextConfiguration");

            _contextConfiguration = contextConfiguration;
        }

        public virtual DbContextConfiguration ContextConfiguration
        {
            get { return _contextConfiguration; }
        }

        public virtual string TableName
        {
            get { return "__MigrationHistory"; }
        }

        public virtual IModel HistoryModel
        {
            get { return _historyModel ?? (_historyModel = CreateHistoryModel()); }
        }

        public virtual DbContext CreateHistoryContext()
        {
            var options = new DbContextOptions().UseModel(HistoryModel);

            // TODO: Revisit and decide whether it is ok to reuse the extension instances
            // from the user context for the history context.
            foreach (var extension in ContextConfiguration.ContextOptions.Extensions)
            {
                options.AddBuildAction(c => c.AddOrUpdateExtension(extension));
            }

            var context = new DbContext(
                ContextConfiguration.Services.ServiceProvider, 
                options.BuildConfiguration());

            if (!_exists && !context.Database.Exists())
            {
                context.Database.Create();
                _exists = true;
            }

            return context;
        }

        public virtual IReadOnlyList<IMigrationMetadata> Migrations
        {
            get
            {
                using (var historyContext = CreateHistoryContext())
                {
                    return historyContext.Set<HistoryRow>()
                        .Where(h => h.ContextKey == CreateContextKey())
                        .Select(h => new MigrationMetadata(h.MigrationName, h.Timestamp))
                        .OrderBy(m => m.Timestamp + m.Name)
                        .ToArray();
                }
            }
        }

        public virtual void AddMigration([NotNull] IMigrationMetadata migration)
        {
            Check.NotNull(migration, "migration");

            using (var historyContext = CreateHistoryContext())
            {
                historyContext.Set<HistoryRow>().Add(
                    new HistoryRow()
                        {
                            MigrationName = migration.Name,
                            Timestamp = migration.Timestamp,
                            ContextKey = CreateContextKey()
                        });

                historyContext.SaveChanges();
            }
        }

        protected virtual IModel CreateHistoryModel()
        {
            var builder = new ModelBuilder();

            builder
                .Entity<HistoryRow>()
                .ToTable(TableName)
                .Properties(
                    ps =>
                    {
                        // TODO: Add column constraints (FixedLength, MaxLength) if needed.
                        ps.Property(e => e.MigrationName);
                        ps.Property(e => e.Timestamp);
                        ps.Property(e => e.ContextKey);
                    })
                // TODO: Key should be {e.Timestamp, e.MigrationName, e.ContextKey} but composite keys are not implemented yet.
                .Key(e => new { e.MigrationName });

            return builder.Model;
        }

        protected virtual string CreateContextKey()
        {
            return ContextConfiguration.Context.GetType().Name;
        }

        private class HistoryRow
        {
            public string MigrationName { get; set; }
            public string Timestamp { get; set; }
            public string ContextKey { get; set; }            
        }
    }
}
