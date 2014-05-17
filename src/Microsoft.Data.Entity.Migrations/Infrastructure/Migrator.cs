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
        private readonly IMigrationOperationSqlGeneratorFactory _sqlGeneratorFactory;
        private readonly SqlStatementExecutor _sqlExecutor;

        public Migrator(
            [NotNull] DbContextConfiguration contextConfiguration,
            [NotNull] HistoryRepository historyRepository,
            [NotNull] MigrationAssembly migrationAssembly,
            [NotNull] ModelDiffer modelDiffer,
            [NotNull] DatabaseBuilder databaseBuilder,
            [NotNull] IMigrationOperationSqlGeneratorFactory sqlGeneratorFactory,
            [NotNull] SqlStatementExecutor sqlExecutor)
        {
            Check.NotNull(contextConfiguration, "contextConfiguration");
            Check.NotNull(historyRepository, "historyRepository");
            Check.NotNull(migrationAssembly, "migrationAssembly");
            Check.NotNull(modelDiffer, "modelDiffer");
            Check.NotNull(databaseBuilder, "databaseBuilder");
            Check.NotNull(sqlGeneratorFactory, "sqlGeneratorFactory");
            Check.NotNull(sqlExecutor, "sqlExecutor");

            _contextConfiguration = contextConfiguration;
            _historyRepository = historyRepository;
            _migrationAssembly = migrationAssembly;
            _modelDiffer = modelDiffer;
            _databaseBuilder = databaseBuilder;
            _sqlGeneratorFactory = sqlGeneratorFactory;
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

        public virtual IMigrationOperationSqlGeneratorFactory SqlGeneratorFactory
        {
            get { return _sqlGeneratorFactory; }
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
            var sqlStatements = GenerateSqlStatements(GetPendingMigrations());
            // TODO: Figure out what needs to be done to avoid the cast below.
            var dbConnection = ((RelationalConnection)_contextConfiguration.Connection).DbConnection;

            _sqlExecutor.ExecuteNonQuery(dbConnection, sqlStatements);
        }

        public virtual IReadOnlyList<SqlStatement> GenerateSqlStatements(
            IReadOnlyList<IMigrationMetadata> migrations, bool downgrade = false)
        {
            var sqlStatements = new List<SqlStatement>();

            foreach (var migration in migrations)
            {
                var database = DatabaseBuilder.GetDatabase(migration.TargetModel);
                var sqlGenerator = SqlGeneratorFactory.Create(database);

                sqlStatements.AddRange(sqlGenerator.Generate(
                    downgrade ? migration.DowngradeOperations: migration.UpgradeOperations, 
                    generateIdempotentSql: true));
            }

            return sqlStatements;
        }
    }
}
