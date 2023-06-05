namespace EF_Vs_AdoNet_Benchmark.Controllers
{
    using DAL;
    using Models;
    using Microsoft.AspNetCore.Mvc;
    using System.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Extensions.Configuration;
    using System.Diagnostics;

    [ApiController]
    [Route("api/[controller]")]
    public class BenchmarkController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly EFDbContext _eFDbContext;

        public BenchmarkController(IConfiguration configuration, EFDbContext eFDbContext)
        {
            _configuration = configuration;
            _eFDbContext = eFDbContext;
        }


        [HttpGet("GetAll")]
        public async Task<IActionResult> SkipWarmUpPeriodAndGetAll()
        {
            var allRecords = await _eFDbContext.MyEntities.ToListAsync();
            return Ok(allRecords);
        }

        [HttpGet("GetAllv2")]
        public IActionResult SkipWarmUpPeriodAndGetAllv2()
        {
            Task<List<MyEntity>> getAllTask = Task.Run(() => _eFDbContext.MyEntities.ToListAsync());
            var allRecords = getAllTask.GetAwaiter().GetResult();
            return Ok(allRecords);
        }

        [HttpGet("GetAllADO")]
        public IActionResult SkipWarmUpPeriodAndGetAllADO()
        {
            var records = new List<MyEntity>();

            using (var connection = new SqlConnection(_configuration.GetConnectionString("EFConnection")))
            {
                connection.Open();

                string sql = "SELECT * FROM MyEntities";
                using (var command = new SqlCommand(sql, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var entity = new MyEntity
                        {
                            Id = reader.GetString(reader.GetOrdinal("Id")),
                            Name = reader.GetString(reader.GetOrdinal("Name"))
                        };

                        records.Add(entity);
                    }
                }

                connection.Close();
            }

            return Ok(records);
        }



        // Remove all entities using Entity Framework
        [HttpPost("RemoveAllEF")]
        public async Task<IActionResult> RemoveAllEntitiesEF()
        {
            var stopwatch = Stopwatch.StartNew();

            var allEntities = await _eFDbContext.MyEntities.ToListAsync();
            _eFDbContext.MyEntities.RemoveRange(allEntities);
            await _eFDbContext.SaveChangesAsync();

            stopwatch.Stop();
            return Ok($"Execution time: {stopwatch.ElapsedMilliseconds}ms");
        }


        // Remove all entities using ADO.NET
        [HttpPost("RemoveAllADO")]
        public async Task<IActionResult> RemoveAllEntitiesADO()
        {
            var stopwatch = Stopwatch.StartNew();

            using (var connection = new SqlConnection(_configuration.GetConnectionString("EFConnection")))
            {
                await connection.OpenAsync();

                // Prepare the SQL command
                string sql = "DELETE FROM MyEntities";
                using (var command = new SqlCommand(sql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                await connection.CloseAsync();
            }

            stopwatch.Stop();
            return Ok($"Execution time: {stopwatch.ElapsedMilliseconds}ms");
        }

        // Insert multiple records using Entity Framework
        [HttpPost("EF")]
        public async Task<IActionResult> InsertMultipleRecordsEF()
        {
            var stopwatch = Stopwatch.StartNew();

            List<MyEntity> entities = new List<MyEntity>();

            // Generate and populate the entities
            for (int i = 0; i < 500; i++)
            {
                entities.Add(new MyEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Entity " + i
                });
            }

            // Disable change tracking and auto-detect changes for performance
            _eFDbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            // Add the entities in batches
            int batchSize = 100;
            for (int i = 0; i < entities.Count; i += batchSize)
            {
                var batch = entities.Skip(i).Take(batchSize);
                _eFDbContext.Set<MyEntity>().AddRange(batch);
                await _eFDbContext.SaveChangesAsync();
            }

            stopwatch.Stop();
            return Ok($"Execution time: {stopwatch.ElapsedMilliseconds}ms");
        }

        // Insert multiple records using ADO.NET
        [HttpPost("ADO")]
        public async Task<IActionResult> InsertMultipleRecordsADO()
        {
            var stopwatch = Stopwatch.StartNew();

            using (var connection = new SqlConnection(_configuration.GetConnectionString("EFConnection")))
            {
                await connection.OpenAsync();

                // Prepare the SQL command
                string sql = "INSERT INTO MyEntities (Id, Name) VALUES (@Id, @Name)";
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@Id", SqlDbType.VarChar);
                    command.Parameters.Add("@Name", SqlDbType.VarChar);

                    // Generate and populate the entities
                    for (int i = 0; i < 500; i++)
                    {
                        command.Parameters["@Id"].Value = Guid.NewGuid().ToString();
                        command.Parameters["@Name"].Value = "Entity " + i;

                        await command.ExecuteNonQueryAsync();
                    }
                }

                await connection.CloseAsync();
            }

            stopwatch.Stop();
            return Ok($"Execution time: {stopwatch.ElapsedMilliseconds}ms");
        }

        // Insert multiple records using ADO.NET with SqlBulkCopy
        [HttpPost("ADOBulk")]
        public async Task<IActionResult> InsertMultipleRecordsADOSqlBulkCopy()
        {
            var stopwatch = Stopwatch.StartNew();

            var entities = new List<MyEntity>();

            // Generate and populate the entities
            for (int i = 0; i < 500; i++)
            {
                entities.Add(new MyEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Entity " + i
                });
            }

            using (var connection = new SqlConnection(_configuration.GetConnectionString("EFConnection")))
            {
                await connection.OpenAsync();

                // Create a DataTable to hold the entities
                var dataTable = new DataTable();
                dataTable.Columns.Add("Id", typeof(string));
                dataTable.Columns.Add("Name", typeof(string));

                // Populate the DataTable with the entities
                foreach (var entity in entities)
                {
                    dataTable.Rows.Add(entity.Id, entity.Name);
                }

                // Use SqlBulkCopy to perform bulk insert
                using (var bulkCopy = new SqlBulkCopy(connection))
                {
                    bulkCopy.DestinationTableName = "dbo.MyEntities";
                    bulkCopy.ColumnMappings.Add("Id", "Id");
                    bulkCopy.ColumnMappings.Add("Name", "Name");

                    // Set batch size and other options if needed (higher batchSize means more performance)
                    bulkCopy.BatchSize = 120;  // ideal size for performance but low memmory specs
                    bulkCopy.BulkCopyTimeout = 60;

                    // Perform the bulk insert
                    await bulkCopy.WriteToServerAsync(dataTable);
                }

                await connection.CloseAsync();
            }

            stopwatch.Stop();
            return Ok($"Execution time: {stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
