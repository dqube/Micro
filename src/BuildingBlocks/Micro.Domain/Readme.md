// Product.cs
public class Product : AggregateRoot<ProductId>
{
    public string Name { get; private set; }
    public Money Price { get; private set; }
    public int StockQuantity { get; private set; }

    public Product(ProductId id, string name, Money price, int stockQuantity) : base(id)
    {
        Name = name;
        Price = price;
        StockQuantity = stockQuantity;
        
        Validate();
    }

    private void Validate()
    {
        var validation = new ValidationResult();
        
        if (string.IsNullOrWhiteSpace(Name))
            validation.AddError(nameof(Name), "Product name cannot be empty");
        
        if (Price.Amount <= 0)
            validation.AddError(nameof(Price), "Price must be greater than zero");
        
        if (StockQuantity < 0)
            validation.AddError(nameof(StockQuantity), "Stock quantity cannot be negative");
            
        base.Validate(validation);
    }
}

// Money.cs (Value Object)
public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}


// ProductLowStockEvent.cs
public class ProductLowStockEvent : DomainEvent
{
    public ProductId ProductId { get; }
    public string ProductName { get; }
    public int CurrentStock { get; }

    public ProductLowStockEvent(Product product)
    {
        ProductId = product.Id;
        ProductName = product.Name;
        CurrentStock = product.StockQuantity;
    }
}

// In Product class
public void ReduceStock(int quantity)
{
    StockQuantity -= quantity;
    
    if (StockQuantity < 5) // Threshold for low stock
    {
        AddDomainEvent(new ProductLowStockEvent(this));
    }
}

<!-- YourCompany.DDD.Core.csproj -->
<ItemGroup>
  <PackageReference Include="System.ComponentModel.Annotations" Version="5.0.0" />
</ItemGroup>

<!-- YourCompany.DDD.Validation.csproj -->
<ItemGroup>
  <PackageReference Include="FluentValidation" Version="11.0.0" />
</ItemGroup>

// ProductId.cs
public record ProductId(int Value) : Identity<int>(Value);

// Product.cs
public class Product : AggregateRoot<ProductId, int>
{
    public string Name { get; private set; }

    public Product(ProductId id, string name) : base(id)
    {
        Name = name;
    }
}