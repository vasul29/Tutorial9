using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Tutorial9.Model;

namespace Tutorial9.Services;

public class DbService : IDbService
{
    private readonly IConfiguration _configuration;

    public DbService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task DoSomethingAsync()
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = new SqlCommand();

        command.Connection = connection;
        await connection.OpenAsync();

        DbTransaction transaction = await connection.BeginTransactionAsync();
        command.Transaction = transaction as SqlTransaction;

        // BEGIN TRANSACTION
        try
        {
            command.CommandText = "INSERT INTO Animal VALUES (@IdAnimal, @Name);";
            command.Parameters.AddWithValue("@IdAnimal", 1);
            command.Parameters.AddWithValue("@Name", "Animal1");

            await command.ExecuteNonQueryAsync();

            command.Parameters.Clear();
            command.CommandText = "INSERT INTO Animal VALUES (@IdAnimal, @Name);";
            command.Parameters.AddWithValue("@IdAnimal", 2);
            command.Parameters.AddWithValue("@Name", "Animal2");

            await command.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
        // END TRANSACTION
    }

    public async Task<int> ProcedureAsync(ProductWarehouseRequestDTO requestDto)
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = new SqlCommand("AddProductToWarehouse", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@IdProduct", requestDto.IdProduct);
        command.Parameters.AddWithValue("@IdWarehouse", requestDto.IdWarehouse);
        command.Parameters.AddWithValue("@Amount", requestDto.Amount);
        command.Parameters.AddWithValue("@CreatedAt", requestDto.CreatedAt);

        try
        {
            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                throw new Exception("Procedure executed, but no ID returned.");

            return Convert.ToInt32(result);
        }
        catch
        {
            throw;
        }
    }

    public async Task<int> AddProductToWarehouseAsync(ProductWarehouseRequestDTO requestDto)
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await connection.OpenAsync();

        await using SqlCommand command = new SqlCommand();
        command.Connection = connection;

        DbTransaction transaction = await connection.BeginTransactionAsync();
        command.Transaction = transaction as SqlTransaction;

        try
        {
            // 1. Check if Product exists
            command.CommandText = "SELECT 1 FROM Product WHERE IdProduct = @IdProduct";
            command.Parameters.AddWithValue("@IdProduct", requestDto.IdProduct);
            var productExists = await command.ExecuteScalarAsync() != null;
            if (!productExists)
                throw new Exception("Product not found");

            command.Parameters.Clear();

            // 2. Check if Warehouse exists
            command.CommandText = "SELECT 1 FROM Warehouse WHERE IdWarehouse = @IdWarehouse";
            command.Parameters.AddWithValue("@IdWarehouse", requestDto.IdWarehouse);
            var warehouseExists = await command.ExecuteScalarAsync() != null;
            if (!warehouseExists)
                throw new Exception("Warehouse not found");

            command.Parameters.Clear();

            // 3. Check Order
            command.CommandText = @"SELECT TOP 1 IdOrder, Amount FROM [Order]
                                WHERE IdProduct = @IdProduct AND Amount = @Amount AND CreatedAt < @CreatedAt";
            command.Parameters.AddWithValue("@IdProduct", requestDto.IdProduct);
            command.Parameters.AddWithValue("@Amount", requestDto.Amount);
            command.Parameters.AddWithValue("@CreatedAt", requestDto.CreatedAt);

            int? idOrder = null;
            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    idOrder = reader.GetInt32(0);
                }
            }

            if (idOrder == null)
                throw new Exception("Matching order not found");

            command.Parameters.Clear();

            // 4. Check if already fulfilled
            command.CommandText = "SELECT 1 FROM Product_Warehouse WHERE IdOrder = @IdOrder";
            command.Parameters.AddWithValue("@IdOrder", idOrder);
            var alreadyFulfilled = await command.ExecuteScalarAsync() != null;
            if (alreadyFulfilled)
                throw new Exception("Order already fulfilled");

            command.Parameters.Clear();

            // 5. Update FulfilledAt
            command.CommandText = "UPDATE [Order] SET FulfilledAt = @Now WHERE IdOrder = @IdOrder";
            command.Parameters.AddWithValue("@Now", DateTime.Now);
            command.Parameters.AddWithValue("@IdOrder", idOrder);
            await command.ExecuteNonQueryAsync();
            command.Parameters.Clear();

            // 6. Get Price
            command.CommandText = "SELECT Price FROM Product WHERE IdProduct = @IdProduct";
            command.Parameters.AddWithValue("@IdProduct", requestDto.IdProduct);
            var price = (decimal)(await command.ExecuteScalarAsync() ?? throw new Exception("Price not found"));
            command.Parameters.Clear();

            decimal totalPrice = price * requestDto.Amount;

            // 7. Insert into Product_Warehouse
            command.CommandText = @"INSERT INTO Product_Warehouse 
                (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, @CreatedAt);
                SELECT SCOPE_IDENTITY();";
            command.Parameters.AddWithValue("@IdWarehouse", requestDto.IdWarehouse);
            command.Parameters.AddWithValue("@IdProduct", requestDto.IdProduct);
            command.Parameters.AddWithValue("@IdOrder", idOrder);
            command.Parameters.AddWithValue("@Amount", requestDto.Amount);
            command.Parameters.AddWithValue("@Price", totalPrice);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

            var result = await command.ExecuteScalarAsync();
            await transaction.CommitAsync();

            return Convert.ToInt32(result);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}