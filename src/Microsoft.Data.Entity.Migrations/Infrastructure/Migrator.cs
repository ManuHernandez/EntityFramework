// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Migrations.Model;
using Microsoft.Data.Entity.Migrations.Utilities;
using Microsoft.Data.Entity.Relational;
using System.Globalization;

namespace Microsoft.Data.Entity.Migrations.Infrastructure
{
    public class Migrator
    {
        private readonly DbContextConfiguration _contextConfiguration;
        private readonly HistoryRepository _historyRepository;
        private readonly MigrationAssembly _migrationAssembly;
        private readonly ModelDiffer _modelDiffer;
        private readonly DatabaseBuilder _databaseBuilder;
        private readonly MigrationOperationSqlGenerator _sqlGenerator;
        private readonly SqlStatementExecutor _sqlExecutor;

        public Migrator(
            [NotNull] DbContextConfiguration contextConfiguration,
            [NotNull] HistoryRepository historyRepository,
            [NotNull] MigrationAssembly migrationAssembly,
            [NotNull] ModelDiffer modelDiffer,
            [NotNull] DatabaseBuilder databaseBuilder,
            [NotNull] MigrationOperationSqlGenerator sqlGenerator,
            [NotNull] SqlStatementExecutor sqlExecutor)
        {
            Check.NotNull(contextConfiguration, "contextConfiguration");
            Check.NotNull(historyRepository, "historyRepository");
            Check.NotNull(migrationAssembly, "migrationAssembly");
            Check.NotNull(modelDiffer, "modelDiffer");
            Check.NotNull(databaseBuilder, "databaseBuilder");
            Check.NotNull(sqlGenerator, "sqlGenerator");
            Check.NotNull(sqlExecutor, "sqlExecutor");

            _contextConfiguration = contextConfiguration;
            _historyRepository = historyRepository;
            _migrationAssembly = migrationAssembly;
            _modelDiffer = modelDiffer;
            _databaseBuilder = databaseBuilder;
            _sqlGenerator = sqlGenerator;
            _sqlExecutor = sqlExecutor;
        }

        public virtual DbContextConfiguration ContextConfiguration
        {
            get { return _contextConfiguration; }
        }

        public virtual HistoryRepository HistoryRepository
        {
            get { return _historyRepository; }
        }

        public virtual MigrationAssembly MigrationAssembly
        {
            get { return _migrationAssembly; }
        }

        public virtual ModelDiffer ModelDiffer
        {
            get { return _modelDiffer; }
        }

        public virtual DatabaseBuilder DatabaseBuilder
        {
            get { return _databaseBuilder; }
        }

        public virtual MigrationOperationSqlGenerator SqlGenerator
        {
            get { return _sqlGenerator; }
        }

        public virtual SqlStatementExecutor SqlExecutor
        {
            get { return _sqlExecutor; }
        }

        public virtual IMigrationMetadata CreateMigration([NotNull] string migrationName)
        {
            Check.NotEmpty(migrationName, "migrationName");

            var sourceModel = MigrationAssembly.Model;
            var targetModel = ContextConfiguration.Model;

            IReadOnlyList<MigrationOperation> upgradeOperations, downgradeOperations;
            if (sourceModel != null)
            {
                upgradeOperations = ModelDiffer.Diff(sourceModel, targetModel);
                downgradeOperations = ModelDiffer.Diff(targetModel, sourceModel);
            }
            else
            {
                upgradeOperations = ModelDiffer.DiffSource(targetModel);
                downgradeOperations = ModelDiffer.DiffTarget(targetModel);                
            }

            return
                new MigrationMetadata(migrationName, CreateMigrationTimestamp())
                    {
                        SourceModel = sourceModel,
                        TargetModel = targetModel,
                        UpgradeOperations = upgradeOperations,
                        DowngradeOperations = downgradeOperations
                    };
        }

        protected virtual string CreateMigrationTimestamp()
        {
            return DateTime.UtcNow.ToString("yyyyMMddHHmmssf", CultureInfo.InvariantCulture);
        }

        public virtual IReadOnlyList<IMigrationMetadata> GetPendingMigrations()
        {
            return MigrationAssembly.Migrations
                .Except(HistoryRepository.Migrations, (x, y) => x.Timestamp == y.Timestamp && x.Name == y.Name)
                .ToArray();
        }

        public virtual IReadOnlyList<IMigrationMetadata> GetMigrationsSince([NotNull] string migrationName)
        {
            Check.NotEmpty(migrationName, "migrationName");

            var migrations = new List<IMigrationMetadata>();
            var found = false;

            foreach (var migration in MigrationAssembly.Migrations)
            {
                if (found)
                {
                    migrations.Add(migration);
                }
                else if (migration.Name == migrationName)
                {
                    found = true;
                }
            }

            return migrations;
        }

        public virtual void UpdateDatabase()
        {
            // TODO: Run the following in a transaction.

            foreach (var migration in GetPendingMigrations())
            {
                UpdateDatabase(migration);

                HistoryRepository.AddMigration(migration);
            }
        }

        protected virtual void UpdateDatabase(IMigrationMetadata migration)
        {
            SqlGenerator.Database = DatabaseBuilder.GetDatabase(migration.TargetModel);

            var statements = SqlGenerator.Generate(migration.UpgradeOperations, generateIdempotentSql: true);
            // TODO: Figure out what needs to be done to avoid the cast below.
            var dbConnection = ((RelationalConnection)_contextConfiguration.Connection).DbConnection;

            _sqlExecutor.ExecuteNonQuery(dbConnection, statements);
        }
    }
}
