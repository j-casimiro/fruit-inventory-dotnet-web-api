using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using FruitInventoryAPI.Models;

namespace FruitInventoryAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FruitController(IDbConnection db) : ControllerBase
    {

        [HttpGet]
        public async Task<IActionResult> GetAllFruits()
        {
            var fruits = new List<Fruit>();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                SELECT f.FruitID, f.FruitName, f.FruitType, i.Price, i.Stock
                FROM Fruits f
                LEFT JOIN Inventory i ON f.FruitID = i.FruitID
            ";
            db.Open();
            await using var reader = await ((OracleCommand)cmd).ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                fruits.Add(new Fruit
                {
                    FruitID = reader.GetInt32(0),
                    FruitName = reader.GetString(1),
                    FruitType = reader.GetString(2),
                    Price = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    Stock = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                });
            }
            db.Close();
            return Ok(fruits);
        }

        [HttpPost]
        public async Task<IActionResult> AddFruit([FromBody] Fruit fruit)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT INTO Fruits (FruitName, FruitType) VALUES (:name, :type) RETURNING FruitID INTO :id";
            cmd.Parameters.Add(new OracleParameter("name", OracleDbType.Varchar2) { Value = fruit.FruitName });
            cmd.Parameters.Add(new OracleParameter("type", OracleDbType.Varchar2) { Value = (object)fruit.FruitType ?? DBNull.Value });
            var idParam = new OracleParameter("id", OracleDbType.Int32) { Value = fruit.FruitID };
            db.Open();
            await ((OracleCommand)cmd).ExecuteNonQueryAsync();
            db.Close();
            return Ok(new {fruitId = idParam.Value});
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFruit(int id, [FromBody] Fruit fruit)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE Fruits SET FruitName = :name, FruitType = :type WHERE FruitID = :id";
            cmd.Parameters.Add(new OracleParameter("name", OracleDbType.Varchar2) { Value = fruit.FruitName });
            cmd.Parameters.Add(new OracleParameter("type", OracleDbType.Varchar2) { Value = (object)fruit.FruitType ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32) { Value = id });
            db.Open();
            var rows = await ((OracleCommand)cmd).ExecuteNonQueryAsync();
            db.Close();
            return rows > 0 ? Ok("Fruit updated.") : NotFound("Fruit not found.");
        }
        
        [HttpPost("add-with-inventory")]
        public async Task<IActionResult> AddFruitWithInventory([FromBody] Fruit fruit)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "BEGIN FruitInventory_Pkg.AddFruit(:name, :type); END;";
            cmd.Parameters.Add(new OracleParameter("name", OracleDbType.Varchar2) { Value = fruit.FruitName });
            cmd.Parameters.Add(new OracleParameter("type", OracleDbType.Varchar2) { Value = (object)fruit.FruitType ?? DBNull.Value });

            db.Open();
            await ((OracleCommand)cmd).ExecuteNonQueryAsync();

            // Get the FruitID you just added
            using var getIdCmd = db.CreateCommand();
            getIdCmd.CommandText = "SELECT FruitID FROM Fruits WHERE FruitName = :name AND ROWNUM = 1";
            getIdCmd.Parameters.Add(new OracleParameter("name", OracleDbType.Varchar2) { Value = fruit.FruitName });
            var fruitId = Convert.ToInt32(await ((OracleCommand)getIdCmd).ExecuteScalarAsync());

            // Upsert inventory
            using var invCmd = db.CreateCommand();
            invCmd.CommandText = "BEGIN FruitInventory_Pkg.UpsertInventory(:id, :price, :stock); END;";
            invCmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32) { Value = fruitId });
            invCmd.Parameters.Add(new OracleParameter("price", OracleDbType.Decimal) { Value = fruit.Price });
            invCmd.Parameters.Add(new OracleParameter("stock", OracleDbType.Int32) { Value = fruit.Stock });
            await ((OracleCommand)invCmd).ExecuteNonQueryAsync();

            db.Close();

            return Ok("Fruit and inventory added successfully.");
        }
        
        [HttpPut("update-with-inventory/{id}")]
        public async Task<IActionResult> UpdateFruitWithInventory(int id, [FromBody] Fruit fruit)
        {
            db.Open();

            // Update fruit table
            using var cmdFruit = db.CreateCommand();
            cmdFruit.CommandText = "UPDATE Fruits SET FruitName = :name, FruitType = :type WHERE FruitID = :id";
            cmdFruit.Parameters.Add(new OracleParameter("name", OracleDbType.Varchar2) { Value = fruit.FruitName });
            cmdFruit.Parameters.Add(new OracleParameter("type", OracleDbType.Varchar2) { Value = (object)fruit.FruitType ?? DBNull.Value });
            cmdFruit.Parameters.Add(new OracleParameter("id", OracleDbType.Int32) { Value = id });
            await ((OracleCommand)cmdFruit).ExecuteNonQueryAsync();

            // Upsert inventory
            using var cmdInv = db.CreateCommand();
            cmdInv.CommandText = "BEGIN FruitInventory_Pkg.UpsertInventory(:id, :price, :stock); END;";
            cmdInv.Parameters.Add(new OracleParameter("id", OracleDbType.Int32) { Value = id });
            cmdInv.Parameters.Add(new OracleParameter("price", OracleDbType.Decimal) { Value = fruit.Price });
            cmdInv.Parameters.Add(new OracleParameter("stock", OracleDbType.Int32) { Value = fruit.Stock });
            await ((OracleCommand)cmdInv).ExecuteNonQueryAsync();

            db.Close();

            return Ok("Fruit and inventory updated successfully.");
        }
    }
}
