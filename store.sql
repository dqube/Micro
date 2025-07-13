/******************** DATABASE INITIALIZATION ********************/
CREATE SCHEMA shared;
CREATE SCHEMA messaging;
CREATE SCHEMA identity;
CREATE SCHEMA contact;
CREATE SCHEMA store;
CREATE SCHEMA employee;
CREATE SCHEMA catalog;
CREATE SCHEMA inventory;
CREATE SCHEMA supplier;
CREATE SCHEMA promotion;
CREATE SCHEMA customer;
CREATE SCHEMA transaction;
CREATE SCHEMA payment;
CREATE SCHEMA reporting;

/******************** SHARED FOUNDATIONAL SCHEMAS ********************/
-- Countries and currencies must be created first as they're referenced by many other tables
CREATE TABLE shared.Countries (
    CountryCode CHAR(2) PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    CurrencyCode CHAR(3) NOT NULL
);

CREATE TABLE shared.Currencies (
    CurrencyCode CHAR(3) PRIMARY KEY,
    Name VARCHAR(50) NOT NULL,
    Symbol VARCHAR(5) NOT NULL
);

/******************** IDENTITY & ACCESS SERVICE ********************/
CREATE TABLE identity.Roles (
    RoleId INT PRIMARY KEY,
    Name VARCHAR(20) NOT NULL UNIQUE,
    Description VARCHAR(255) NULL
);

CREATE TABLE identity.Users (
    UserId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Username VARCHAR(50) NOT NULL UNIQUE,
    Email VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash BYTEA NOT NULL,
    PasswordSalt BYTEA NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FailedLoginAttempts INT NOT NULL DEFAULT 0,
    LockoutEnd TIMESTAMP NULL
);

CREATE TABLE identity.UserRoles (
    UserId UUID REFERENCES identity.Users(UserId),
    RoleId INT REFERENCES identity.Roles(RoleId),
    PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE identity.RegistrationTokens (
    TokenId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId UUID NOT NULL REFERENCES identity.Users(UserId),
    TokenType VARCHAR(20) NOT NULL CHECK (TokenType IN ('EmailVerification','PasswordReset')),
    Expiration TIMESTAMP NOT NULL DEFAULT (CURRENT_TIMESTAMP + INTERVAL '24 hours'),
    IsUsed BOOLEAN NOT NULL DEFAULT FALSE
);

/******************** CONTACT MANAGEMENT CORE ********************/
CREATE TABLE contact.ContactNumberTypes (
    ContactNumberTypeId INT PRIMARY KEY,
    Name VARCHAR(50) NOT NULL UNIQUE,
    Description VARCHAR(255) NULL
);

CREATE TABLE contact.AddressTypes (
    AddressTypeId INT PRIMARY KEY,
    Name VARCHAR(50) NOT NULL UNIQUE,
    Description VARCHAR(255) NULL
);

/******************** STORE OPERATIONS SERVICE ********************/
CREATE TABLE store.Stores (
    StoreId SERIAL PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    LocationId INT NOT NULL,
    Address VARCHAR(255) NOT NULL,
    Phone VARCHAR(20) NOT NULL,
    OpeningHours VARCHAR(100) NOT NULL,
    Status VARCHAR(20) NOT NULL CHECK (Status IN ('Active','Maintenance','Closed')) DEFAULT 'Active'
);

CREATE TABLE store.Registers (
    RegisterId SERIAL PRIMARY KEY,
    StoreId INT NOT NULL REFERENCES store.Stores(StoreId),
    Name VARCHAR(50) NOT NULL,
    CurrentBalance DECIMAL(19,4) NOT NULL DEFAULT 0,
    Status VARCHAR(20) NOT NULL CHECK (Status IN ('Open','Closed')) DEFAULT 'Closed',
    LastOpen TIMESTAMP NULL,
    LastClose TIMESTAMP NULL
);

CREATE TABLE store.Shifts (
    ShiftId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    EmployeeId UUID NOT NULL,
    RegisterId INT NOT NULL REFERENCES store.Registers(RegisterId),
    StartTime TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    EndTime TIMESTAMP NULL,
    StartingCash DECIMAL(19,4) NOT NULL,
    EndingCash DECIMAL(19,4) NULL
);

CREATE TABLE store.CashDrawerMovements (
    MovementId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    RegisterId INT NOT NULL REFERENCES store.Registers(RegisterId),
    EmployeeId UUID NOT NULL,
    MovementType VARCHAR(20) NOT NULL CHECK (MovementType IN ('Open','Close','CashIn','CashOut')),
    Amount DECIMAL(19,4) NOT NULL,
    MovementTime TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Note VARCHAR(255) NULL
);

/******************** EMPLOYEE MANAGEMENT ********************/
CREATE TABLE employee.Employees (
    EmployeeId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId UUID NOT NULL UNIQUE REFERENCES identity.Users(UserId),
    StoreId INT NOT NULL REFERENCES store.Stores(StoreId),
    EmployeeNumber VARCHAR(20) NOT NULL UNIQUE,
    HireDate DATE NOT NULL DEFAULT CURRENT_DATE,
    TerminationDate DATE NULL,
    Position VARCHAR(50) NOT NULL,
    AuthLevel INT NOT NULL DEFAULT 1
);

-- Employee contact numbers (using shared contact types)
CREATE TABLE employee.EmployeeContactNumbers (
    ContactNumberId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    EmployeeId UUID NOT NULL REFERENCES employee.Employees(EmployeeId),
    ContactNumberTypeId INT NOT NULL REFERENCES contact.ContactNumberTypes(ContactNumberTypeId),
    PhoneNumber VARCHAR(20) NOT NULL,
    IsPrimary BOOLEAN NOT NULL DEFAULT FALSE,
    Verified BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Employee addresses (using shared address types)
CREATE TABLE employee.EmployeeAddresses (
    AddressId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    EmployeeId UUID NOT NULL REFERENCES employee.Employees(EmployeeId),
    AddressTypeId INT NOT NULL REFERENCES contact.AddressTypes(AddressTypeId),
    Line1 VARCHAR(100) NOT NULL,
    Line2 VARCHAR(100) NULL,
    City VARCHAR(50) NOT NULL,
    State VARCHAR(50) NULL,
    PostalCode VARCHAR(20) NOT NULL,
    CountryCode CHAR(2) NOT NULL REFERENCES shared.Countries(CountryCode),
    IsPrimary BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ModifiedDate TIMESTAMP NULL
);

/******************** PRODUCT CATALOG SERVICE ********************/
CREATE TABLE catalog.ProductCategories (
    CategoryId SERIAL PRIMARY KEY,
    Name VARCHAR(100) NOT NULL UNIQUE,
    ParentCategoryId INT NULL REFERENCES catalog.ProductCategories(CategoryId)
);

CREATE TABLE catalog.Products (
    ProductId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SKU VARCHAR(50) NOT NULL UNIQUE,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    CategoryId INT NOT NULL REFERENCES catalog.ProductCategories(CategoryId),
    BasePrice DECIMAL(19,4) NOT NULL CHECK (BasePrice >= 0),
    CostPrice DECIMAL(19,4) NOT NULL CHECK (CostPrice >= 0),
    IsTaxable BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE catalog.ProductBarcodes (
    BarcodeId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ProductId UUID NOT NULL REFERENCES catalog.Products(ProductId),
    BarcodeValue VARCHAR(50) NOT NULL UNIQUE,
    BarcodeType VARCHAR(20) NOT NULL DEFAULT 'UPC-A'
);

CREATE TABLE catalog.CountryPricing (
    PricingId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ProductId UUID NOT NULL REFERENCES catalog.Products(ProductId),
    CountryCode CHAR(2) NOT NULL REFERENCES shared.Countries(CountryCode),
    Price DECIMAL(19,4) NOT NULL CHECK (Price >= 0),
    EffectiveDate DATE NOT NULL DEFAULT CURRENT_DATE,
    UNIQUE (ProductId, CountryCode)
);

CREATE TABLE catalog.TaxConfigurations (
    TaxConfigId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    LocationId INT NOT NULL,
    CategoryId INT NULL REFERENCES catalog.ProductCategories(CategoryId),
    TaxRate DECIMAL(5,2) NOT NULL CHECK (TaxRate >= 0)
);

/******************** INVENTORY MANAGEMENT SERVICE ********************/
CREATE TABLE inventory.InventoryItems (
    InventoryItemId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    StoreId INT NOT NULL,
    ProductId UUID NOT NULL REFERENCES catalog.Products(ProductId),
    Quantity INT NOT NULL CHECK (Quantity >= 0) DEFAULT 0,
    ReorderLevel INT NOT NULL DEFAULT 10,
    LastRestockDate TIMESTAMP NULL,
    UNIQUE (StoreId, ProductId)
);

CREATE TABLE inventory.StockMovements (
    MovementId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ProductId UUID NOT NULL REFERENCES catalog.Products(ProductId),
    StoreId INT NOT NULL,
    QuantityChange INT NOT NULL,
    MovementType VARCHAR(20) NOT NULL CHECK (MovementType IN ('Purchase','Return','Adjustment','Damage','Transfer')),
    MovementDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    EmployeeId UUID NOT NULL,
    ReferenceId UUID NULL
);

/******************** SUPPLIER MANAGEMENT ********************/
CREATE TABLE supplier.Suppliers (
    SupplierId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Name VARCHAR(100) NOT NULL,
    TaxIdentificationNumber VARCHAR(50) NULL,
    Website VARCHAR(255) NULL,
    Notes TEXT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ModifiedDate TIMESTAMP NULL
);

-- Supplier contact persons
CREATE TABLE supplier.SupplierContacts (
    ContactId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SupplierId UUID NOT NULL REFERENCES supplier.Suppliers(SupplierId),
    FirstName VARCHAR(50) NOT NULL,
    LastName VARCHAR(50) NOT NULL,
    Email VARCHAR(100) NULL,
    Position VARCHAR(100) NULL,
    IsPrimary BOOLEAN NOT NULL DEFAULT FALSE,
    Notes TEXT NULL
);

-- Supplier contact numbers (using shared contact types)
CREATE TABLE supplier.SupplierContactNumbers (
    ContactNumberId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ContactId UUID NOT NULL REFERENCES supplier.SupplierContacts(ContactId),
    ContactNumberTypeId INT NOT NULL REFERENCES contact.ContactNumberTypes(ContactNumberTypeId),
    PhoneNumber VARCHAR(20) NOT NULL,
    IsPrimary BOOLEAN NOT NULL DEFAULT FALSE,
    Notes VARCHAR(255) NULL
);

-- Supplier addresses (using shared address types)
CREATE TABLE supplier.SupplierAddresses (
    AddressId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SupplierId UUID NOT NULL REFERENCES supplier.Suppliers(SupplierId),
    AddressTypeId INT NOT NULL REFERENCES contact.AddressTypes(AddressTypeId),
    Line1 VARCHAR(100) NOT NULL,
    Line2 VARCHAR(100) NULL,
    City VARCHAR(50) NOT NULL,
    State VARCHAR(50) NULL,
    PostalCode VARCHAR(20) NOT NULL,
    CountryCode CHAR(2) NOT NULL REFERENCES shared.Countries(CountryCode),
    IsPrimary BOOLEAN NOT NULL DEFAULT FALSE,
    IsShipping BOOLEAN NOT NULL DEFAULT FALSE,
    IsBilling BOOLEAN NOT NULL DEFAULT FALSE,
    Notes TEXT NULL
);

CREATE TABLE supplier.PurchaseOrders (
    OrderId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SupplierId UUID NOT NULL REFERENCES supplier.Suppliers(SupplierId),
    StoreId INT NOT NULL,
    OrderDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ExpectedDate DATE NULL,
    Status VARCHAR(20) NOT NULL CHECK (Status IN ('Draft','Ordered','Received','Cancelled')) DEFAULT 'Draft',
    TotalAmount DECIMAL(19,4) NOT NULL DEFAULT 0,
    ShippingAddressId UUID NULL REFERENCES supplier.SupplierAddresses(AddressId),
    ContactPersonId UUID NULL REFERENCES supplier.SupplierContacts(ContactId)
);

CREATE TABLE supplier.PurchaseOrderDetails (
    OrderDetailId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    OrderId UUID NOT NULL REFERENCES supplier.PurchaseOrders(OrderId),
    ProductId UUID NOT NULL REFERENCES catalog.Products(ProductId),
    Quantity INT NOT NULL CHECK (Quantity > 0),
    UnitCost DECIMAL(19,4) NOT NULL CHECK (UnitCost >= 0),
    ReceivedQuantity INT NULL
);

/******************** PROMOTIONS ENGINE SERVICE ********************/
CREATE TABLE promotion.DiscountTypes (
    DiscountTypeId INT PRIMARY KEY,
    Name VARCHAR(50) NOT NULL UNIQUE,
    Description VARCHAR(255) NULL
);

CREATE TABLE promotion.DiscountCampaigns (
    CampaignId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Name VARCHAR(100) NOT NULL,
    Description VARCHAR(255) NULL,
    StartDate TIMESTAMP NOT NULL,
    EndDate TIMESTAMP NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    MaxUsesPerCustomer INT NULL,
    CHECK (EndDate > StartDate)
);

CREATE TABLE promotion.DiscountRules (
    RuleId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    CampaignId UUID NOT NULL REFERENCES promotion.DiscountCampaigns(CampaignId),
    RuleType VARCHAR(20) NOT NULL CHECK (RuleType IN ('Category','Product','TotalAmount','BuyXGetY')),
    ProductId UUID NULL,
    CategoryId INT NULL,
    MinQuantity INT NULL,
    MinAmount DECIMAL(19,4) NULL,
    DiscountValue DECIMAL(19,4) NOT NULL,
    DiscountType VARCHAR(10) NOT NULL CHECK (DiscountType IN ('Percent','Fixed','FreeItem')) DEFAULT 'Percent',
    FreeProductId UUID NULL
);

CREATE TABLE promotion.Promotions (
    PromotionId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Name VARCHAR(100) NOT NULL,
    Description TEXT NULL,
    StartDate TIMESTAMP NOT NULL,
    EndDate TIMESTAMP NOT NULL,
    IsCombinable BOOLEAN NOT NULL DEFAULT FALSE,
    MaxRedemptions INT NULL,
    CHECK (EndDate > StartDate)
);

CREATE TABLE promotion.PromotionProducts (
    PromotionProductId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PromotionId UUID NOT NULL REFERENCES promotion.Promotions(PromotionId),
    ProductId UUID NULL,
    CategoryId INT NULL,
    MinQuantity INT NOT NULL DEFAULT 1,
    DiscountPercent DECIMAL(5,2) NULL,
    BundlePrice DECIMAL(19,4) NULL
);

/******************** CUSTOMER MANAGEMENT SERVICE ********************/
CREATE TABLE customer.Customers (
    CustomerId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId UUID NULL REFERENCES identity.Users(UserId),
    FirstName VARCHAR(50) NOT NULL,
    LastName VARCHAR(50) NOT NULL,
    Email VARCHAR(100) UNIQUE,
    MembershipNumber VARCHAR(50) UNIQUE,
    JoinDate DATE NOT NULL DEFAULT CURRENT_DATE,
    ExpiryDate DATE NOT NULL,
    CountryCode CHAR(2) NOT NULL REFERENCES shared.Countries(CountryCode),
    LoyaltyPoints INT NOT NULL DEFAULT 0,
    PreferredContactMethod INT NULL REFERENCES contact.ContactNumberTypes(ContactNumberTypeId),
    PreferredAddressType INT NULL REFERENCES contact.AddressTypes(AddressTypeId)
);

-- Customer contact numbers (using shared contact types)
CREATE TABLE customer.CustomerContactNumbers (
    ContactNumberId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    CustomerId UUID NOT NULL REFERENCES customer.Customers(CustomerId),
    ContactNumberTypeId INT NOT NULL REFERENCES contact.ContactNumberTypes(ContactNumberTypeId),
    PhoneNumber VARCHAR(20) NOT NULL,
    IsPrimary BOOLEAN NOT NULL DEFAULT FALSE,
    Verified BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Customer addresses (using shared address types)
CREATE TABLE customer.CustomerAddresses (
    AddressId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    CustomerId UUID NOT NULL REFERENCES customer.Customers(CustomerId),
    AddressTypeId INT NOT NULL REFERENCES contact.AddressTypes(AddressTypeId),
    Line1 VARCHAR(100) NOT NULL,
    Line2 VARCHAR(100) NULL,
    City VARCHAR(50) NOT NULL,
    State VARCHAR(50) NULL,
    PostalCode VARCHAR(20) NOT NULL,
    CountryCode CHAR(2) NOT NULL REFERENCES shared.Countries(CountryCode),
    IsPrimary BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ModifiedDate TIMESTAMP NULL
);

CREATE TABLE customer.LoyaltyPrograms (
    ProgramId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Name VARCHAR(100) NOT NULL,
    PointsPerDollar DECIMAL(5,2) NOT NULL DEFAULT 1.0,
    SignupBonus INT NOT NULL DEFAULT 0,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE customer.LoyaltyTiers (
    TierId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ProgramId UUID NOT NULL REFERENCES customer.LoyaltyPrograms(ProgramId),
    Name VARCHAR(50) NOT NULL,
    MinPoints INT NOT NULL,
    DiscountPercent DECIMAL(5,2) NOT NULL
);

CREATE TABLE customer.GiftCards (
    GiftCardId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    CardNumber VARCHAR(20) NOT NULL UNIQUE,
    InitialBalance DECIMAL(19,4) NOT NULL,
    CurrentBalance DECIMAL(19,4) NOT NULL,
    IssueDate DATE NOT NULL DEFAULT CURRENT_DATE,
    ExpiryDate DATE NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE customer.LoyaltyPointLedger (
    LedgerId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    CustomerId UUID NOT NULL REFERENCES customer.Customers(CustomerId),
    TransactionDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PointsEarned INT NOT NULL DEFAULT 0,
    PointsRedeemed INT NOT NULL DEFAULT 0,
    SaleId UUID NULL
);

/******************** TRANSACTION PROCESSING SERVICE ********************/
CREATE TABLE transaction.Sales (
    SaleId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    StoreId INT NOT NULL,
    EmployeeId UUID NOT NULL,
    CustomerId UUID NULL REFERENCES customer.Customers(CustomerId),
    RegisterId INT NOT NULL,
    TransactionTime TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    SubTotal DECIMAL(19,4) NOT NULL CHECK (SubTotal >= 0),
    DiscountTotal DECIMAL(19,4) NOT NULL DEFAULT 0 CHECK (DiscountTotal >= 0),
    TaxAmount DECIMAL(19,4) NOT NULL CHECK (TaxAmount >= 0),
    TotalAmount DECIMAL(19,4) NOT NULL CHECK (TotalAmount >= 0),
    ReceiptNumber VARCHAR(20) NOT NULL UNIQUE
);

CREATE TABLE transaction.SaleDetails (
    SaleDetailId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SaleId UUID NOT NULL REFERENCES transaction.Sales(SaleId),
    ProductId UUID NOT NULL,
    Quantity INT NOT NULL CHECK (Quantity > 0),
    UnitPrice DECIMAL(19,4) NOT NULL CHECK (UnitPrice >= 0),
    AppliedDiscount DECIMAL(19,4) NOT NULL DEFAULT 0,
    TaxApplied DECIMAL(19,4) NOT NULL CHECK (TaxApplied >= 0),
    LineTotal DECIMAL(19,4) NOT NULL CHECK (LineTotal >= 0)
);

CREATE TABLE transaction.AppliedDiscounts (
    AppliedDiscountId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SaleDetailId UUID NULL REFERENCES transaction.SaleDetails(SaleDetailId),
    SaleId UUID NULL REFERENCES transaction.Sales(SaleId),
    CampaignId UUID NOT NULL,
    RuleId UUID NOT NULL,
    DiscountAmount DECIMAL(19,4) NOT NULL CHECK (DiscountAmount >= 0),
    CHECK (
        (SaleDetailId IS NOT NULL AND SaleId IS NULL) OR 
        (SaleDetailId IS NULL AND SaleId IS NOT NULL)
    )
);

CREATE TABLE transaction.Returns (
    ReturnId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SaleId UUID NOT NULL REFERENCES transaction.Sales(SaleId),
    ReturnDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    EmployeeId UUID NOT NULL,
    CustomerId UUID NULL REFERENCES customer.Customers(CustomerId),
    TotalRefund DECIMAL(19,4) NOT NULL
);

CREATE TABLE transaction.ReturnDetails (
    ReturnDetailId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ReturnId UUID NOT NULL REFERENCES transaction.Returns(ReturnId),
    ProductId UUID NOT NULL,
    Quantity INT NOT NULL,
    Reason VARCHAR(50) NOT NULL CHECK (Reason IN ('Defective','WrongItem','CustomerChange','Other')),
    Restock BOOLEAN NOT NULL DEFAULT TRUE
);

/******************** PAYMENT GATEWAY SERVICE ********************/
CREATE TABLE payment.PaymentTypes (
    PaymentTypeId INT PRIMARY KEY,
    Name VARCHAR(50) NOT NULL UNIQUE,
    Description VARCHAR(255) NULL
);

CREATE TABLE payment.PaymentProcessors (
    ProcessorId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Name VARCHAR(50) NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CommissionRate DECIMAL(5,2) NOT NULL DEFAULT 0
);

CREATE TABLE payment.PaymentMethods (
    MethodId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PaymentTypeId INT NOT NULL REFERENCES payment.PaymentTypes(PaymentTypeId),
    ProcessorId UUID NULL REFERENCES payment.PaymentProcessors(ProcessorId),
    Name VARCHAR(50) NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE payment.SalePayments (
    PaymentId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SaleId UUID NOT NULL,
    MethodId UUID NOT NULL REFERENCES payment.PaymentMethods(MethodId),
    Amount DECIMAL(19,4) NOT NULL CHECK (Amount > 0),
    TransactionCode VARCHAR(100) NULL,
    ApprovalCode VARCHAR(50) NULL,
    ProcessedTime TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE payment.PaymentDetails (
    PaymentDetailId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SalePaymentId UUID NOT NULL REFERENCES payment.SalePayments(PaymentId),
    CardLastFour CHAR(4) NULL,
    CardType VARCHAR(20) NULL,
    AuthorizationCode VARCHAR(50) NULL,
    ProcessorResponse TEXT NULL,
    IsSettled BOOLEAN NOT NULL DEFAULT FALSE,
    SettlementDate TIMESTAMP NULL
);

CREATE TABLE payment.GiftCardTransactions (
    TransactionId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    GiftCardId UUID NOT NULL REFERENCES customer.GiftCards(GiftCardId),
    SalePaymentId UUID NULL REFERENCES payment.SalePayments(PaymentId),
    Amount DECIMAL(19,4) NOT NULL,
    TransactionType VARCHAR(20) NOT NULL CHECK (TransactionType IN ('Issuance','Redemption','Reload')),
    TransactionDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

/******************** REPORTING & ANALYTICS SERVICE ********************/
CREATE TABLE reporting.SalesSnapshots (
    SnapshotId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    SaleId UUID NOT NULL,
    StoreId INT NOT NULL,
    SaleDate DATE NOT NULL,
    TotalAmount DECIMAL(19,4) NOT NULL,
    CustomerId UUID NULL
);

CREATE TABLE reporting.InventorySnapshots (
    SnapshotId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ProductId UUID NOT NULL,
    StoreId INT NOT NULL,
    Quantity INT NOT NULL,
    SnapshotDate DATE NOT NULL DEFAULT CURRENT_DATE
);

CREATE TABLE reporting.PromotionEffectiveness (
    EffectivenessId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PromotionId UUID NOT NULL,
    RedemptionCount INT NOT NULL DEFAULT 0,
    RevenueImpact DECIMAL(19,4) NOT NULL,
    AnalysisDate DATE NOT NULL DEFAULT CURRENT_DATE
);

/******************** EVENT MESSAGING ********************/
CREATE TABLE messaging.Outbox (
    MessageId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    EventType VARCHAR(100) NOT NULL,
    Payload TEXT NOT NULL,
    CreatedDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ProcessedDate TIMESTAMP NULL
);

CREATE TABLE messaging.Inbox (
    MessageId UUID PRIMARY KEY,
    EventType VARCHAR(100) NOT NULL,
    Payload TEXT NOT NULL,
    ReceivedDate TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ProcessedDate TIMESTAMP NULL
);

/******************** REFERENCE DATA POPULATION ********************/
-- Currencies (must be first as referenced by countries)
INSERT INTO shared.Currencies (CurrencyCode, Name, Symbol) VALUES
('USD', 'US Dollar', '$'),
('EUR', 'Euro', '€'),
('GBP', 'British Pound', '£'),
('JPY', 'Japanese Yen', '¥'),
('CAD', 'Canadian Dollar', '$'),
('AUD', 'Australian Dollar', '$');

-- Countries (referenced by addresses)
INSERT INTO shared.Countries (CountryCode, Name, CurrencyCode) VALUES
('US', 'United States', 'USD'),
('GB', 'United Kingdom', 'GBP'),
('DE', 'Germany', 'EUR'),
('FR', 'France', 'EUR'),
('CA', 'Canada', 'CAD'),
('AU', 'Australia', 'AUD'),
('JP', 'Japan', 'JPY');

-- Contact number types (used by multiple domains)
INSERT INTO contact.ContactNumberTypes (ContactNumberTypeId, Name, Description) VALUES
(1, 'Mobile', 'Primary mobile number'),
(2, 'Home', 'Home landline number'),
(3, 'Work', 'Work contact number'),
(4, 'Emergency', 'Emergency contact number'),
(5, 'Fax', 'Facsimile number'),
(6, 'Other', 'Other contact number');

-- Address types (used by multiple domains)
INSERT INTO contact.AddressTypes (AddressTypeId, Name, Description) VALUES
(1, 'Home', 'Primary residential address'),
(2, 'Work', 'Business/work address'),
(3, 'Billing', 'Billing and statements address'),
(4, 'Shipping', 'Default shipping address'),
(5, 'Warehouse', 'Supplier warehouse location'),
(6, 'Headquarters', 'Company headquarters');

-- Roles (for identity service)
INSERT INTO identity.Roles (RoleId, Name, Description) VALUES
(1, 'Cashier', 'Can process sales and returns'),
(2, 'Supervisor', 'Can override transactions and manage registers'),
(3, 'Manager', 'Full store operations access'),
(4, 'Admin', 'System administration access'),
(5, 'Inventory', 'Inventory management access'),
(6, 'Reporting', 'Reporting and analytics access');

-- Payment types (for payment service)
INSERT INTO payment.PaymentTypes (PaymentTypeId, Name, Description) VALUES
(1, 'Cash', 'Physical currency payment'),
(2, 'Credit Card', 'Payment via credit card'),
(3, 'Debit Card', 'Payment via debit card'),
(4, 'Mobile Payment', 'Payment via mobile wallet'),
(5, 'Gift Card', 'Payment via store gift card'),
(6, 'Store Credit', 'Payment using customer store credit'),
(7, 'Bank Transfer', 'Direct bank transfer payment'),
(8, 'Crypto', 'Cryptocurrency payment');

-- Discount types (for promotions service)
INSERT INTO promotion.DiscountTypes (DiscountTypeId, Name, Description) VALUES
(1, 'Percentage', 'Percentage discount'),
(2, 'Fixed Amount', 'Fixed monetary amount discount'),
(3, 'BOGO', 'Buy One Get One free/discounted'),
(4, 'Bundle', 'Product bundle discount'),
(5, 'Loyalty', 'Loyalty program discount'),
(6, 'Seasonal', 'Seasonal promotion discount');

/******************** INDEX CREATION ********************/
-- Identity Service
CREATE INDEX IX_Users_Email ON identity.Users(Email);
CREATE INDEX IX_RegistrationTokens_Expiration ON identity.RegistrationTokens(Expiration) WHERE IsUsed = FALSE;

-- Store Service
CREATE INDEX IX_Registers_Store ON store.Registers(StoreId);
CREATE INDEX IX_Shifts_Employee ON store.Shifts(EmployeeId);

-- Product Service
CREATE INDEX IX_Products_SKU ON catalog.Products(SKU);
CREATE INDEX IX_Products_Category ON catalog.Products(CategoryId);
CREATE INDEX IX_CountryPricing_Product ON catalog.CountryPricing(ProductId);

-- Inventory Service
CREATE INDEX IX_InventoryItems_StoreProduct ON inventory.InventoryItems(StoreId, ProductId);
CREATE INDEX IX_StockMovements_Date ON inventory.StockMovements(MovementDate);

-- Supplier Service
CREATE INDEX IX_SupplierContacts_Supplier ON supplier.SupplierContacts(SupplierId);
CREATE INDEX IX_SupplierAddresses_Supplier ON supplier.SupplierAddresses(SupplierId);

-- Promotion Service
CREATE INDEX IX_DiscountCampaigns_Active ON promotion.DiscountCampaigns(IsActive) WHERE IsActive = TRUE;
CREATE INDEX IX_Promotions_DateRange ON promotion.Promotions(StartDate, EndDate);

-- Customer Service
CREATE INDEX IX_Customers_Membership ON customer.Customers(MembershipNumber);
CREATE INDEX IX_CustomerContactNumbers_Customer ON customer.CustomerContactNumbers(CustomerId);
CREATE INDEX IX_CustomerAddresses_Customer ON customer.CustomerAddresses(CustomerId);
CREATE INDEX IX_GiftCards_Active ON customer.GiftCards(IsActive) WHERE IsActive = TRUE;

-- Transaction Service
CREATE INDEX IX_Sales_DateStore ON transaction.Sales(TransactionTime, StoreId);
CREATE INDEX IX_SaleDetails_Sale ON transaction.SaleDetails(SaleId);

-- Payment Service
CREATE INDEX IX_SalePayments_Sale ON payment.SalePayments(SaleId);
CREATE INDEX IX_PaymentDetails_Unsettled ON payment.PaymentDetails(IsSettled) WHERE IsSettled = FALSE;

-- Reporting Service
CREATE INDEX IX_SalesSnapshots_Date ON reporting.SalesSnapshots(SaleDate);
CREATE INDEX IX_InventorySnapshots_Date ON reporting.InventorySnapshots(SnapshotDate);
CREATE INDEX IX_InventorySnapshots_Product ON reporting.InventorySnapshots(ProductId);

DO $$
BEGIN
    RAISE NOTICE 'Database schema created successfully with all domains and enhanced contact management';
END $$;