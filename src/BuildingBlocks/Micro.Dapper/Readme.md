Unified Usage Examples
1. Stored Procedure with Automatic Command Type Detection
csharp
// Automatically detected as stored procedure (no spaces in command text)
var products = connection.Query<Product>("usp_GetProductsByCategory", new { CategoryId = 5 });

// Explicit command type (optional)
var users = connection.Query<User>("usp_GetActiveUsers", commandType: CommandType.StoredProcedure);
2. SQL Query with Parameters
csharp
// Automatically detected as SQL text (contains spaces)
var orders = connection.Query<Order>(
    "SELECT * FROM Orders WHERE CustomerId = @CustomerId AND OrderDate > @MinDate", 
    new { CustomerId = 123, MinDate = DateTime.Today.AddDays(-30) });
3. Stored Procedure with Output Parameters
csharp
// Create command for stored procedure
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "usp_CalculateOrderTotal";
    cmd.CommandType = CommandType.StoredProcedure;
    
    // Add parameters
    cmd.AddParameter("@OrderId", 12345);
    cmd.AddParameter("@Output_TotalAmount", null); // Output parameter
    
    // Execute
    cmd.ExecuteNonQuery();
    
    // Get output parameter
    decimal totalAmount = cmd.GetOutputParameter<decimal>("@Output_TotalAmount");
    Console.WriteLine($"Order total: {totalAmount}");
}
4. Unified Async Execution
csharp
// Async stored procedure call
var products = await connection.QueryAsync<Product>("usp_GetFeaturedProducts");

// Async SQL query
var users = await connection.QueryAsync<User>(
    "SELECT * FROM Users WHERE IsActive = @IsActive", 
    new { IsActive = true });

// Async execute stored procedure with output
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "usp_GenerateReport";
    cmd.CommandType = CommandType.StoredProcedure;
    
    cmd.AddParameter("@ReportType", "Monthly");
    cmd.AddParameter("@Output_RecordCount", null);
    
    await cmd.ExecuteNonQueryAsync();
    
    int count = cmd.GetOutputParameter<int>("@Output_RecordCount");
    Console.WriteLine($"Report generated with {count} records");
}
5. Automatic Parameter Handling
csharp
// Different parameter styles all work
connection.Execute("usp_UpdateProduct", new { 
    Id = 123, 
    Name = "New Name", 
    Output_RowsAffected = (int?)null 
});

// Dictionary parameters
var parameters = new Dictionary<string, object> {
    { "@ProductId", 123 },
    { "@NewPrice", 19.99m },
    { "@Output_Message", null }
};
connection.Execute("usp_UpdateProductPrice", parameters);

// Get all output parameters
var outputValues = cmd.GetAllOutputParameters();
Key Features
Unified Method Approach:

Single set of methods (Query, Execute) works for both SQL and stored procedures

Automatic command type detection (based on SQL text)

Optional explicit command type specification

Comprehensive Stored Procedure Support:

Output parameter handling with GetOutputParameter and GetAllOutputParameters

Automatic parameter naming conventions (adds @ prefix when needed)

Proper parameter direction handling

Advanced Auto-Mapping:

Case-insensitive property matching

Underscore to PascalCase conversion

Nullable type support

Enum handling

Proper DBNull conversion

Flexible Parameter Input:

Anonymous objects

Dictionaries

Explicit parameter objects

Async Support:

All operations have async counterparts

Proper connection state management

This implementation provides a clean, unified API that maintains all the convenience of Dapper while adding robust stored procedure support and advanced mapping capabilities.