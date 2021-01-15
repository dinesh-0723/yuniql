﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Shouldly;
using System;
using Yuniql.Core;
using Yuniql.Extensibility;
using System.Collections.Generic;
using System.Linq;
using Yuniql.PlatformTests.Interfaces;
using Yuniql.PlatformTests.Setup;
using IMigrationServiceFactory = Yuniql.PlatformTests.Interfaces.IMigrationServiceFactory;
using MigrationServiceFactory = Yuniql.PlatformTests.Setup.MigrationServiceFactory;

namespace Yuniql.PlatformTests.Core
{
    [TestClass]
    public class BulkImportServiceTests : TestClassBase
    {
        private ITestDataService _testDataService;
        private ITraceService _traceService;
        private IMigrationServiceFactory _migrationServiceFactory;
        private TestConfiguration _testConfiguration;

        [TestInitialize]
        public void Setup()
        {
            _testConfiguration = ConfigureWithEmptyWorkspace();

            //create test data service provider
            var testDataServiceFactory = new TestDataServiceFactory();
            _testDataService = testDataServiceFactory.Create(_testConfiguration.Platform);

            //create data service factory for migration proper
            _traceService = new FileTraceService { IsDebugEnabled = true };
            _migrationServiceFactory = new MigrationServiceFactory(_traceService);
        }

        [TestCleanup]
        public void Cleanup()
        {
            //drop the test directory
            try
            {
                if (Directory.Exists(_testConfiguration.WorkspacePath))
                    Directory.Delete(_testConfiguration.WorkspacePath, true);
            }
            catch (Exception) { /*swallow exceptions*/ }

            //drop test database
            try
            {
                _testDataService.DropDatabase(_testConfiguration.ConnectionString);
            }
            catch (Exception) { /*swallow exceptions*/ }
        }

        [TestMethod]
        public void Test_Bulk_Import_With_Default_Separator()
        {
            //arrange - prepare bulk destination table
            var directoryService = new DirectoryService();
            var fileService = new FileService();
            var workspaceService = new WorkspaceService(_traceService, directoryService, fileService);
            workspaceService.Init(_testConfiguration.WorkspacePath);

            workspaceService.IncrementMajorVersion(_testConfiguration.WorkspacePath, null);
            string v100Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.00");
            _testDataService.CreateScriptFile(Path.Combine(v100Directory, $"TestCsv.sql"), _testDataService.GetSqlForCreateBulkTable("TestCsv"));

            //act
            var configuration = _testConfiguration.GetFreshConfiguration();
            configuration.TargetVersion = "v1.00";

            var migrationService = _migrationServiceFactory.Create(configuration.Platform);
            migrationService.Run();

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestCsv").ShouldBeTrue();

            //arrange - add new minor version with csv files
            workspaceService.IncrementMinorVersion(_testConfiguration.WorkspacePath, null);
            string v101Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.01");
            File.Copy(Path.Combine(Path.Combine(Environment.CurrentDirectory, "Data"), "TestCsv.csv"), Path.Combine(v101Directory, "TestCsv.csv"));

            //act
            configuration.TargetVersion = "v1.01";
            migrationService.Run();

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestCsv").ShouldBeTrue();

            var results = _testDataService.GetBulkTestData(_testConfiguration.ConnectionString, "TestCsv");
            var testDataRows = new List<BulkTestDataRow>
            {
                new BulkTestDataRow { FirstName="Jack", LastName ="Poole", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Diana", LastName ="Churchill", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Rebecca", LastName ="Lyman", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Sam", LastName ="Macdonald", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Matt", LastName ="Paige", BirthDate = new DateTime(1980,1,1) },
            };

            results.Count.ShouldBe(5);
            testDataRows.All(t => results.Exists(r =>
                t.FirstName == r.FirstName
                && t.LastName == r.LastName
                && t.BirthDate == r.BirthDate
            )).ShouldBeTrue();
        }

        [TestMethod]
        public void Test_Bulk_Import_With_Pipe_Separator()
        {
            //arrange - pre-create destination bulk tables
            var directoryService = new DirectoryService();
            var fileService = new FileService();
            var workspaceService = new WorkspaceService(_traceService, directoryService, fileService);
            workspaceService.Init(_testConfiguration.WorkspacePath);

            workspaceService.IncrementMajorVersion(_testConfiguration.WorkspacePath, null);
            string v100Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.00");
            _testDataService.CreateScriptFile(Path.Combine(v100Directory, $"TestCsvPipeSeparated.sql"), _testDataService.GetSqlForCreateBulkTable("TestCsvPipeSeparated"));

            //act
            var configuration = _testConfiguration.GetFreshConfiguration();
            configuration.TargetVersion = "v1.00";

            var migrationService = _migrationServiceFactory.Create(configuration.Platform);
            migrationService.Run();

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestCsvPipeSeparated").ShouldBeTrue();

            //arrange - add new version with csv files
            workspaceService.IncrementMinorVersion(_testConfiguration.WorkspacePath, null);
            string v101Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.01");
            File.Copy(Path.Combine(Path.Combine(Environment.CurrentDirectory, "Data"), "TestCsvPipeSeparated.csv"), Path.Combine(v101Directory, "TestCsvPipeSeparated.csv"));

            //act - bulk load csv files
            migrationService.Run(_testConfiguration.WorkspacePath, "v1.01", isAutoCreateDatabase: true, bulkSeparator: "|");

            //assert
            var results = _testDataService.GetBulkTestData(_testConfiguration.ConnectionString, "TestCsvPipeSeparated");
            var testDataRows = new List<BulkTestDataRow>
            {
                new BulkTestDataRow { FirstName="Jack", LastName ="Poole", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Diana", LastName ="Churchill", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Rebecca", LastName ="Lyman", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Sam", LastName ="Macdonald", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Matt", LastName ="Paige", BirthDate = new DateTime(1980,1,1) },
            };

            results.Count.ShouldBe(5);
            testDataRows.All(t => results.Exists(r =>
                t.FirstName == r.FirstName
                && t.LastName == r.LastName
                && t.BirthDate == r.BirthDate
            )).ShouldBeTrue();
        }

        [TestMethod]
        public void Test_Bulk_Import_With_Utf8_Encoded_File()
        {
            //arrange - pre-create destination bulk tables
            var directoryService = new DirectoryService();
            var fileService = new FileService();
            var workspaceService = new WorkspaceService(_traceService, directoryService, fileService);
            workspaceService.Init(_testConfiguration.WorkspacePath);

            workspaceService.IncrementMajorVersion(_testConfiguration.WorkspacePath, null);
            string v100Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.00");
            _testDataService.CreateScriptFile(Path.Combine(v100Directory, $"TestCsvUtf8.sql"), _testDataService.GetSqlForCreateBulkTable("TestCsvUtf8"));

            //act
            var configuration = _testConfiguration.GetFreshConfiguration();
            configuration.TargetVersion = "v1.00";

            var migrationService = _migrationServiceFactory.Create(configuration.Platform);
            migrationService.Run();

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestCsvUtf8").ShouldBeTrue();

            //arrange - add new version with csv files
            workspaceService.IncrementMinorVersion(_testConfiguration.WorkspacePath, null);
            string v101Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.01");
            File.Copy(Path.Combine(Path.Combine(Environment.CurrentDirectory, "Data"), "TestCsvUtf8.csv"), Path.Combine(v101Directory, "TestCsvUtf8.csv"));

            //act - bulk load csv files
            migrationService.Run(_testConfiguration.WorkspacePath, "v1.01", isAutoCreateDatabase: true);

            //assert
            var results = _testDataService.GetBulkTestData(_testConfiguration.ConnectionString, "TestCsvUtf8");
            var testDataRows = new List<BulkTestDataRow>
            {
                new BulkTestDataRow { FirstName="Allan", LastName ="Søgaard", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Martin", LastName ="Bæk", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Gitte", LastName ="Jürgensen", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Maria", LastName ="Østergård", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Peter", LastName ="Langkjær", BirthDate = new DateTime(1980,1,1) },
            };

            results.Count.ShouldBe(5);
            testDataRows.All(t => results.Exists(r =>
                t.FirstName == r.FirstName
                && t.LastName == r.LastName
                && t.BirthDate == r.BirthDate
            )).ShouldBeTrue();
        }

        [TestMethod]
        public void Test_Bulk_Import_With_Null_Columns()
        {
            //arrange - pre-create destination bulk tables
            var directoryService = new DirectoryService();
            var fileService = new FileService();
            var workspaceService = new WorkspaceService(_traceService, directoryService, fileService);
            workspaceService.Init(_testConfiguration.WorkspacePath);

            workspaceService.IncrementMajorVersion(_testConfiguration.WorkspacePath, null);
            string v100Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.00");
            _testDataService.CreateScriptFile(Path.Combine(v100Directory, $"TestCsvNullColumn.sql"), _testDataService.GetSqlForCreateBulkTable("TestCsvNullColumn"));

            //act
            var configuration = _testConfiguration.GetFreshConfiguration();
            configuration.TargetVersion = "v1.00";

            var migrationService = _migrationServiceFactory.Create(configuration.Platform);
            migrationService.Run();

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestCsvNullColumn").ShouldBeTrue();

            //arrange - add new version with csv files
            workspaceService.IncrementMinorVersion(_testConfiguration.WorkspacePath, null);
            string v101Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.01");
            File.Copy(Path.Combine(Path.Combine(Environment.CurrentDirectory, "Data"), "TestCsvNullColumn.csv"), Path.Combine(v101Directory, "TestCsvNullColumn.csv"));

            //act - bulk load csv files
            migrationService.Run(_testConfiguration.WorkspacePath, "v1.01", isAutoCreateDatabase: true);

            var results = _testDataService.GetBulkTestData(_testConfiguration.ConnectionString, "TestCsvNullColumn");
            var testDataRows = new List<BulkTestDataRow>
            {
                new BulkTestDataRow { FirstName="Jack", LastName ="Poole"},
                new BulkTestDataRow { FirstName="Diana", LastName ="Churchill"},
                new BulkTestDataRow { FirstName="Rebecca", LastName ="Lyman" },
                new BulkTestDataRow { FirstName="Sam", LastName ="Macdonald"},
                new BulkTestDataRow { FirstName="Matt", LastName ="Paige"},
            };

            results.Count.ShouldBe(5);
            testDataRows.All(t => results.Exists(r =>
                t.FirstName == r.FirstName
                && t.LastName == r.LastName
                && !r.BirthDate.HasValue
            )).ShouldBeTrue();
        }

        [TestMethod]
        public void Test_Bulk_Import_With_Unquoted()
        {
            //arrange - pre-create destination bulk tables
            var directoryService = new DirectoryService();
            var fileService = new FileService();
            var workspaceService = new WorkspaceService(_traceService, directoryService, fileService);
            workspaceService.Init(_testConfiguration.WorkspacePath);

            workspaceService.IncrementMajorVersion(_testConfiguration.WorkspacePath, null);
            string v100Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.00");
            _testDataService.CreateScriptFile(Path.Combine(v100Directory, $"TestCsvUnquoted.sql"), _testDataService.GetSqlForCreateBulkTable("TestCsvUnquoted"));

            //act
            var configuration = _testConfiguration.GetFreshConfiguration();
            configuration.TargetVersion = "v1.00";

            var migrationService = _migrationServiceFactory.Create(configuration.Platform);
            migrationService.Run();

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestCsvUnquoted").ShouldBeTrue();

            //arrange - add new version with csv files
            workspaceService.IncrementMinorVersion(_testConfiguration.WorkspacePath, null);
            string v101Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.01");
            File.Copy(Path.Combine(Path.Combine(Environment.CurrentDirectory, "Data"), "TestCsvUnquoted.csv"), Path.Combine(v101Directory, "TestCsvUnquoted.csv"));

            //act - bulk load csv files
            migrationService.Run(_testConfiguration.WorkspacePath, "v1.01", isAutoCreateDatabase: true);

            //assert
            var results = _testDataService.GetBulkTestData(_testConfiguration.ConnectionString, "TestCsvUnquoted");
            var testDataRows = new List<BulkTestDataRow>
            {
                new BulkTestDataRow { FirstName="Jack", LastName ="Poole", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Diana", LastName ="Churchill", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Rebecca", LastName ="Lyman", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Sam", LastName ="Macdonald", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Matt", LastName ="Paige", BirthDate = new DateTime(1980,1,1) },
            };

            results.Count.ShouldBe(5);
            testDataRows.All(t => results.Exists(r =>
                t.FirstName == r.FirstName
                && t.LastName == r.LastName
                && t.BirthDate == r.BirthDate
            )).ShouldBeTrue();
        }

        [TestMethod]
        public void Test_Bulk_Import_Destination_Table_Does_Not_Exist_Throws_Error()
        {
            //arrange
            var directoryService = new DirectoryService();
            var fileService = new FileService();
            var workspaceService = new WorkspaceService(_traceService, directoryService, fileService);
            workspaceService.Init(_testConfiguration.WorkspacePath);

            //we simulate a importing data into TestCsvBulkTable that doesnt exist
            string v000Directory = Path.Combine(_testConfiguration.WorkspacePath, "v0.00");
            File.Copy(Path.Combine(Path.Combine(Environment.CurrentDirectory, "Data"), "TestCsv.csv"), Path.Combine(v000Directory, "TestCsv.csv"));

            //act - bulk load csv files
            try
            {
                var configuration = _testConfiguration.GetFreshConfiguration();
                configuration.TargetVersion = "v1.00";

                var migrationService = _migrationServiceFactory.Create(configuration.Platform);
                migrationService.Run();
            }
            catch (Exception ex)
            {
                //assert
                ex.Message.ShouldNotBeEmpty();
            }
        }

        [TestMethod]
        public void Test_Bulk_Import_Mismatch_Columns_But_Nullable()
        {
            //arrange - pre-create destination bulk tables
            var directoryService = new DirectoryService();
            var fileService = new FileService();
            var workspaceService = new WorkspaceService(_traceService, directoryService, fileService);
            workspaceService.Init(_testConfiguration.WorkspacePath);

            workspaceService.IncrementMajorVersion(_testConfiguration.WorkspacePath, null);
            string v100Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.00");
            _testDataService.CreateScriptFile(Path.Combine(v100Directory, $"TestCsvMismatchColumn.sql"), _testDataService.GetSqlForCreateBulkTable("TestCsvMismatchColumn"));

            //act
            var configuration = _testConfiguration.GetFreshConfiguration();
            configuration.TargetVersion = "v1.00";

            var migrationService = _migrationServiceFactory.Create(configuration.Platform);
            migrationService.Run();

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestCsvMismatchColumn").ShouldBeTrue();

            //arrange - add new version with csv files
            workspaceService.IncrementMinorVersion(_testConfiguration.WorkspacePath, null);
            string v101Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.01");
            File.Copy(Path.Combine(Path.Combine(Environment.CurrentDirectory, "Data"), "TestCsvMismatchColumn.csv"), Path.Combine(v101Directory, "TestCsvMismatchColumn.csv"));

            //act - bulk load csv files
            migrationService.Run(_testConfiguration.WorkspacePath, "v1.01", isAutoCreateDatabase: true);

            var results = _testDataService.GetBulkTestData(_testConfiguration.ConnectionString, "TestCsvMismatchColumn");
            var testDataRows = new List<BulkTestDataRow>
            {
                new BulkTestDataRow { FirstName="Jack", LastName ="Poole"},
                new BulkTestDataRow { FirstName="Diana", LastName ="Churchill"},
                new BulkTestDataRow { FirstName="Rebecca", LastName ="Lyman" },
                new BulkTestDataRow { FirstName="Sam", LastName ="Macdonald"},
                new BulkTestDataRow { FirstName="Matt", LastName ="Paige"},
            };

            results.Count.ShouldBe(5);
            testDataRows.All(t => results.Exists(r =>
                t.FirstName == r.FirstName
                && t.LastName == r.LastName
                && !r.BirthDate.HasValue
            )).ShouldBeTrue();
        }

        [TestMethodEx(Requires = "IsTransactionalDdlSupported")]
        public void Test_Bulk_Import_Mismatch_Columns_But_Not_Nullable()
        {
            //arrange
            var directoryService = new DirectoryService();
            var fileService = new FileService();
            var workspaceService = new WorkspaceService(_traceService, directoryService, fileService);
            workspaceService.Init(_testConfiguration.WorkspacePath);

            string v000Directory = Path.Combine(_testConfiguration.WorkspacePath, "v0.00");
            _testDataService.CreateScriptFile(Path.Combine(v000Directory, $"test_v0_00.sql"), _testDataService.GetSqlForCreateDbObject($"test_v0_00"));
            _testDataService.CreateScriptFile(Path.Combine(v000Directory, $"test_v0_00_TestCsvMismatchColumnNotNullable.sql"), _testDataService.GetSqlForCreateBulkTable("test_v0_00_TestCsvMismatchColumnNotNullable"));
            File.Copy(Path.Combine(Path.Combine(Environment.CurrentDirectory, "Data"), "TestCsvMismatchColumnNotNullable.csv"), Path.Combine(v000Directory, "test_v0_00_TestCsvMismatchColumnNotNullable.csv"));

            //act - bulk load csv files
            try
            {
                var configuration = _testConfiguration.GetFreshConfiguration();
                configuration.TargetVersion = "v1.00";

                var migrationService = _migrationServiceFactory.Create(configuration.Platform);
                migrationService.Run();
            }
            catch (Exception ex)
            {
                ex.Message.ShouldNotBeEmpty();

                //asset all changes were rolled back
                _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "test_v0_00").ShouldBeFalse();
                _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "test_v0_00_TestCsvMismatchColumnNotNullable").ShouldBeFalse();
            }
        }

        [TestMethodEx(Requires = "IsSchemaSupported")]
        public void Test_Bulk_Import_With_NonDefault_Schema_Destination_Table()
        {
            //arrange - pre-create destination bulk tables
            var directoryService = new DirectoryService();
            var fileService = new FileService();
            var workspaceService = new WorkspaceService(_traceService, directoryService, fileService);
            workspaceService.Init(_testConfiguration.WorkspacePath);

            workspaceService.IncrementMajorVersion(_testConfiguration.WorkspacePath, null);
            string v100Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.00");
            _testDataService.CreateScriptFile(Path.Combine(v100Directory, $"__CreateSchema.sql"), _testDataService.GetSqlForCreateDbSchema("TestSchema"));
            _testDataService.CreateScriptFile(Path.Combine(v100Directory, $"TestCsvWithSchema.sql"), _testDataService.GetSqlForCreateBulkTable("TestSchema.TestCsv"));

            //act
            var configuration = _testConfiguration.GetFreshConfiguration();
            configuration.TargetVersion = "v1.00";

            var migrationService = _migrationServiceFactory.Create(configuration.Platform);
            migrationService.Run();

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestSchema.TestCsv").ShouldBeTrue();

            //arrange - add new version with csv files
            workspaceService.IncrementMinorVersion(_testConfiguration.WorkspacePath, null);
            string v101Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.01");
            File.Copy(Path.Combine(Path.Combine(Environment.CurrentDirectory, "Data"), "TestCsv.csv"), Path.Combine(v101Directory, "TestSchema.TestCsv.csv"));

            //act - bulk load csv files
            migrationService.Run(_testConfiguration.WorkspacePath, "v1.01", isAutoCreateDatabase: true);

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestSchema.TestCsv").ShouldBeTrue();

            var results = _testDataService.GetBulkTestData(_testConfiguration.ConnectionString, "TestSchema.TestCsv");
            var testDataRows = new List<BulkTestDataRow>
            {
                new BulkTestDataRow { FirstName="Jack", LastName ="Poole", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Diana", LastName ="Churchill", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Rebecca", LastName ="Lyman", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Sam", LastName ="Macdonald", BirthDate = new DateTime(1980,1,1) },
                new BulkTestDataRow { FirstName="Matt", LastName ="Paige", BirthDate = new DateTime(1980,1,1) },
            };

            results.Count.ShouldBe(5);
            testDataRows.All(t => results.Exists(r =>
                t.FirstName == r.FirstName
                && t.LastName == r.LastName
                && t.BirthDate == r.BirthDate
            )).ShouldBeTrue();
        }


        [TestMethod]
        public void Test_Bulk_Import_With_Null_Word_Value()
        {
            //arrange - prepare bulk destination table
            var directoryService = new DirectoryService();
            var fileService = new FileService();
            var workspaceService = new WorkspaceService(_traceService, directoryService, fileService);
            workspaceService.Init(_testConfiguration.WorkspacePath);

            workspaceService.IncrementMajorVersion(_testConfiguration.WorkspacePath, null);
            string v100Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.00");
            _testDataService.CreateScriptFile(Path.Combine(v100Directory, $"TestCsvNullWordValue.sql"), _testDataService.GetSqlForCreateBulkTable("TestCsvNullWordValue"));

            //act
            var configuration = _testConfiguration.GetFreshConfiguration();
            configuration.TargetVersion = "v1.00";

            var migrationService = _migrationServiceFactory.Create(configuration.Platform);
            migrationService.Run();

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestCsvNullWordValue").ShouldBeTrue();

            //arrange - add new minor version with csv files
            workspaceService.IncrementMinorVersion(_testConfiguration.WorkspacePath, null);
            string v101Directory = Path.Combine(_testConfiguration.WorkspacePath, "v1.01");
            File.Copy(Path.Combine(Path.Combine(Environment.CurrentDirectory, "Data"), "TestCsvNullWordValue.csv"), Path.Combine(v101Directory, "TestCsvNullWordValue.csv"));

            //act
            migrationService.Run(_testConfiguration.WorkspacePath, "v1.01", isAutoCreateDatabase: true);

            //assert
            _testDataService.CheckIfDbObjectExist(_testConfiguration.ConnectionString, "TestCsvNullWordValue").ShouldBeTrue();

            var results = _testDataService.GetBulkTestData(_testConfiguration.ConnectionString, "TestCsvNullWordValue");
            var testDataRows = new List<BulkTestDataRow>
            {
                new BulkTestDataRow { FirstName="Jack", LastName ="Poole", BirthDate = null },
                new BulkTestDataRow { FirstName="Diana", LastName ="Churchill", BirthDate = null },
                new BulkTestDataRow { FirstName="Rebecca", LastName ="Lyman", BirthDate = null },
                new BulkTestDataRow { FirstName="Sam", LastName ="Macdonald", BirthDate = null },
                new BulkTestDataRow { FirstName="Matt", LastName ="Paige", BirthDate = null },
            };

            results.Count.ShouldBe(5);
            testDataRows.All(t => results.Exists(r =>
                t.FirstName == r.FirstName
                && t.LastName == r.LastName
                && t.BirthDate == r.BirthDate
            )).ShouldBeTrue();
        }
    }
}
