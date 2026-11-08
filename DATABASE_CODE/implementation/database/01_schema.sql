
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF DB_ID(N'glowme_db') IS NULL
    CREATE DATABASE glowme_db;
GO
USE glowme_db;
GO
IF SCHEMA_ID(N'glowme') IS NULL
    EXEC (N'CREATE SCHEMA glowme AUTHORIZATION dbo');
GO

/* ---------------------------------------------------------------------
   1. Admin
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Admin (
    AdminID       INT IDENTITY(1,1) NOT NULL,
    FullName      NVARCHAR(100)     NOT NULL,
    Email         VARCHAR(150)      NOT NULL,
    PasswordHash  VARCHAR(255)      NOT NULL,
    AdminRole     VARCHAR(20)       NOT NULL CONSTRAINT DF_Admin_Role DEFAULT ('Moderator'),
    CreatedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Admin_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Admin PRIMARY KEY (AdminID),
    CONSTRAINT UQ_Admin_Email UNIQUE (Email),
    CONSTRAINT CK_Admin_Role CHECK (AdminRole IN ('Super admin', 'Moderator'))
);
GO

/* ---------------------------------------------------------------------
   2. Category — managed by an admin (Makeup, Hair Styling, Nails, Eyelashes, Eyebrows)
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Category (
    CategoryID    INT IDENTITY(1,1) NOT NULL,
    AdminID       INT               NOT NULL,
    CategoryName  NVARCHAR(50)      NOT NULL,
    Description   NVARCHAR(300)     NULL,
    CONSTRAINT PK_Category PRIMARY KEY (CategoryID),
    CONSTRAINT UQ_Category_Name UNIQUE (CategoryName),
    CONSTRAINT FK_Category_Admin FOREIGN KEY (AdminID) REFERENCES glowme.Admin (AdminID)
);
GO

/* ---------------------------------------------------------------------
   3. Customer
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Customer (
    CustomerID       INT IDENTITY(1,1) NOT NULL,
    FullName         NVARCHAR(100)     NOT NULL,
    Email            VARCHAR(150)      NOT NULL,
    PasswordHash     VARCHAR(255)      NOT NULL,
    PhoneNumber      VARCHAR(10)       NOT NULL,          -- stored 05XXXXXXXX
    City             NVARCHAR(50)      NOT NULL,
    Address          NVARCHAR(250)     NULL,
    Latitude         DECIMAL(9,6)      NULL,
    Longitude        DECIMAL(9,6)      NULL,
    AccountStatus    VARCHAR(10)       NOT NULL CONSTRAINT DF_Customer_Status DEFAULT ('Pending'),
    CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_Customer_Created DEFAULT (SYSDATETIME()),
    ProfilePhotoURL  NVARCHAR(500)     NULL,              -- [UI] round photo on every customer screen; NULL = initials
    CONSTRAINT PK_Customer PRIMARY KEY (CustomerID),
    CONSTRAINT UQ_Customer_Email UNIQUE (Email),
    CONSTRAINT UQ_Customer_Phone UNIQUE (PhoneNumber),
    CONSTRAINT CK_Customer_Phone CHECK (PhoneNumber LIKE '05[03-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
    CONSTRAINT CK_Customer_Status CHECK (AccountStatus IN ('Pending', 'Active', 'Suspended')),
    CONSTRAINT CK_Customer_Geo CHECK ((Latitude IS NULL AND Longitude IS NULL)
                                   OR (Latitude BETWEEN 16 AND 33 AND Longitude BETWEEN 34 AND 56))
);
GO

/* ---------------------------------------------------------------------
   4. ServiceProvider
   --------------------------------------------------------------------- */
CREATE TABLE glowme.ServiceProvider (
    ProviderID         INT IDENTITY(1,1) NOT NULL,
    FullName           NVARCHAR(100)     NOT NULL,
    BusinessName       NVARCHAR(100)     NOT NULL,
    Email              VARCHAR(150)      NOT NULL,
    PasswordHash       VARCHAR(255)      NOT NULL,
    PhoneNumber        VARCHAR(10)       NOT NULL,        -- stored 05XXXXXXXX
    Bio                NVARCHAR(600)     NULL,
    City               NVARCHAR(50)      NOT NULL,
    Address            NVARCHAR(250)     NULL,
    Latitude           DECIMAL(9,6)      NULL,
    Longitude          DECIMAL(9,6)      NULL,
    OffersHomeService  BIT               NOT NULL CONSTRAINT DF_Provider_Home DEFAULT (0),
    AverageRating      DECIMAL(2,1)      NULL,            -- average of Visible reviews, kept up to date by the app
    AccountStatus      VARCHAR(10)       NOT NULL CONSTRAINT DF_Provider_Status DEFAULT ('Pending'),
    CreatedAt          DATETIME2(0)      NOT NULL CONSTRAINT DF_Provider_Created DEFAULT (SYSDATETIME()),
    ProfilePhotoURL    NVARCHAR(500)     NULL,            -- [UI] avatar in the sidebar, profile header and cards; NULL = initials
    CONSTRAINT PK_ServiceProvider PRIMARY KEY (ProviderID),
    CONSTRAINT UQ_Provider_Email UNIQUE (Email),
    CONSTRAINT UQ_Provider_Phone UNIQUE (PhoneNumber),
    CONSTRAINT UQ_Provider_Business UNIQUE (BusinessName),
    CONSTRAINT CK_Provider_Phone CHECK (PhoneNumber LIKE '05[03-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
    CONSTRAINT CK_Provider_Status CHECK (AccountStatus IN ('Pending', 'Active', 'Suspended')),
    CONSTRAINT CK_Provider_Rating CHECK (AverageRating BETWEEN 1 AND 5),
    CONSTRAINT CK_Provider_Geo CHECK ((Latitude IS NULL AND Longitude IS NULL)
                                   OR (Latitude BETWEEN 16 AND 33 AND Longitude BETWEEN 34 AND 56))
);
GO

/* ---------------------------------------------------------------------
   5. Service — offered by one provider, in one category
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Service (
    ServiceID        INT IDENTITY(1,1) NOT NULL,
    ProviderID       INT               NOT NULL,
    CategoryID       INT               NOT NULL,
    ServiceName      NVARCHAR(100)     NOT NULL,
    Description      NVARCHAR(500)     NULL,
    Price            DECIMAL(8,2)      NOT NULL,
    DurationMinutes  INT               NOT NULL,
    IsActive         BIT               NOT NULL CONSTRAINT DF_Service_Active DEFAULT (1),
    CONSTRAINT PK_Service PRIMARY KEY (ServiceID),
    CONSTRAINT UQ_Service_Name UNIQUE (ProviderID, ServiceName),
    CONSTRAINT CK_Service_Price CHECK (Price BETWEEN 10 AND 10000),
    CONSTRAINT CK_Service_Duration CHECK (DurationMinutes BETWEEN 15 AND 480 AND DurationMinutes % 15 = 0),
    CONSTRAINT FK_Service_Provider FOREIGN KEY (ProviderID) REFERENCES glowme.ServiceProvider (ProviderID),
    CONSTRAINT FK_Service_Category FOREIGN KEY (CategoryID) REFERENCES glowme.Category (CategoryID)
);
GO

/* ---------------------------------------------------------------------
   6. Booking — one appointment of one customer with one provider
   Shown in the UI as  #BK-{BookingID}
   Pending -> Accepted -> Completed ;  Pending -> Rejected ;  Pending/Accepted -> Cancelled
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Booking (
    BookingID           INT IDENTITY(2030,1) NOT NULL,
    CustomerID          INT               NOT NULL,
    ProviderID          INT               NOT NULL,
    BookingDate         DATE              NOT NULL,
    StartTime           TIME(0)           NOT NULL,
    EndTime             TIME(0)           NOT NULL,
    LocationType        VARCHAR(20)       NOT NULL,
    LocationAddress     NVARCHAR(250)     NOT NULL,
    PaymentOption       VARCHAR(10)       NOT NULL,
    TotalAmount         DECIMAL(9,2)      NOT NULL,
    BookingStatus       VARCHAR(10)       NOT NULL CONSTRAINT DF_Booking_Status DEFAULT ('Pending'),
    Notes               NVARCHAR(500)     NULL,
    CancellationReason  NVARCHAR(300)     NULL,           -- the customer's reason when Cancelled, the provider's reason when Rejected
    CreatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_Booking_Created DEFAULT (SYSDATETIME()),
    UpdatedAt           DATETIME2(0)      NOT NULL CONSTRAINT DF_Booking_Updated DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Booking PRIMARY KEY (BookingID),
    CONSTRAINT CK_Booking_Time CHECK (EndTime > StartTime),
    CONSTRAINT CK_Booking_Location CHECK (LocationType IN ('Customer home', 'Provider studio', 'Other location')),
    CONSTRAINT CK_Booking_Option CHECK (PaymentOption IN ('Deposit', 'Full')),
    CONSTRAINT CK_Booking_Total CHECK (TotalAmount > 0),
    CONSTRAINT CK_Booking_Status CHECK (BookingStatus IN ('Pending', 'Accepted', 'Rejected', 'Completed', 'Cancelled')),
    CONSTRAINT CK_Booking_Reason CHECK ((BookingStatus IN ('Rejected', 'Cancelled') AND CancellationReason IS NOT NULL)
                                     OR (BookingStatus NOT IN ('Rejected', 'Cancelled') AND CancellationReason IS NULL)),
    CONSTRAINT CK_Booking_Updated CHECK (UpdatedAt >= CreatedAt),
    CONSTRAINT FK_Booking_Customer FOREIGN KEY (CustomerID) REFERENCES glowme.Customer (CustomerID),
    CONSTRAINT FK_Booking_Provider FOREIGN KEY (ProviderID) REFERENCES glowme.ServiceProvider (ProviderID)
);
CREATE INDEX IX_Booking_ProviderDay ON glowme.Booking (ProviderID, BookingDate, StartTime) INCLUDE (EndTime, BookingStatus);
CREATE INDEX IX_Booking_Customer ON glowme.Booking (CustomerID, BookingDate DESC);
GO

/* ---------------------------------------------------------------------
   7. BookingService — services of a booking, price copied on the day of booking
   --------------------------------------------------------------------- */
CREATE TABLE glowme.BookingService (
    BookingID       INT           NOT NULL,
    ServiceID       INT           NOT NULL,
    Quantity        INT           NOT NULL CONSTRAINT DF_BookingService_Qty DEFAULT (1),
    PriceAtBooking  DECIMAL(8,2)  NOT NULL,
    CONSTRAINT PK_BookingService PRIMARY KEY (BookingID, ServiceID),
    CONSTRAINT CK_BookingService_Qty CHECK (Quantity BETWEEN 1 AND 5),
    CONSTRAINT CK_BookingService_Price CHECK (PriceAtBooking > 0),
    CONSTRAINT FK_BookingService_Booking FOREIGN KEY (BookingID) REFERENCES glowme.Booking (BookingID),
    CONSTRAINT FK_BookingService_Service FOREIGN KEY (ServiceID) REFERENCES glowme.Service (ServiceID)
);
GO

/* ---------------------------------------------------------------------
   8. SubscriptionPlan
   --------------------------------------------------------------------- */
CREATE TABLE glowme.SubscriptionPlan (
    PlanID        INT IDENTITY(1,1) NOT NULL,
    PlanName      NVARCHAR(60)      NOT NULL,
    Description   NVARCHAR(300)     NULL,
    Price         DECIMAL(8,2)      NOT NULL,
    DurationDays  INT               NOT NULL,
    IsActive      BIT               NOT NULL CONSTRAINT DF_Plan_Active DEFAULT (1),
    CONSTRAINT PK_SubscriptionPlan PRIMARY KEY (PlanID),
    CONSTRAINT UQ_Plan_Name UNIQUE (PlanName),
    CONSTRAINT CK_Plan_Price CHECK (Price >= 0),
    CONSTRAINT CK_Plan_Days CHECK (DurationDays BETWEEN 1 AND 366)
);
GO

/* ---------------------------------------------------------------------
   9. Subscription — a provider's plan period
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Subscription (
    SubscriptionID  INT IDENTITY(1,1) NOT NULL,
    ProviderID      INT               NOT NULL,
    PlanID          INT               NOT NULL,
    StartDate       DATE              NOT NULL,
    EndDate         DATE              NOT NULL,
    Status          VARCHAR(10)       NOT NULL CONSTRAINT DF_Subscription_Status DEFAULT ('Active'),
    CreatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_Subscription_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Subscription PRIMARY KEY (SubscriptionID),
    CONSTRAINT CK_Subscription_Dates CHECK (EndDate > StartDate),
    CONSTRAINT CK_Subscription_Status CHECK (Status IN ('Active', 'Expired', 'Cancelled')),
    CONSTRAINT FK_Subscription_Provider FOREIGN KEY (ProviderID) REFERENCES glowme.ServiceProvider (ProviderID),
    CONSTRAINT FK_Subscription_Plan FOREIGN KEY (PlanID) REFERENCES glowme.SubscriptionPlan (PlanID)
);
-- one Active subscription per provider at a time
CREATE UNIQUE INDEX UX_Subscription_Active ON glowme.Subscription (ProviderID) WHERE Status = 'Active';
GO

/* ---------------------------------------------------------------------
   10. Payment — for a booking (deposit / full / remaining) OR for a subscription, never both
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Payment (
    PaymentID             INT IDENTITY(1,1) NOT NULL,
    BookingID             INT               NULL,
    Amount                DECIMAL(9,2)      NOT NULL,
    PaymentType           VARCHAR(20)       NOT NULL,
    PaymentMethod         VARCHAR(15)       NOT NULL,
    PaymentStatus         VARCHAR(10)       NOT NULL CONSTRAINT DF_Payment_Status DEFAULT ('Pending'),
    CommissionAmount      DECIMAL(9,2)      NULL,         -- 10 % of a booking payment; NULL for subscriptions
    ProviderAmount        DECIMAL(9,2)      NULL,         -- Amount - CommissionAmount; NULL for subscriptions
    TransactionReference  VARCHAR(40)       NULL,         -- gateway reference; 'CASH-…' / 'TRF-…' for offline remaining payments
    PaidAt                DATETIME2(0)      NULL,
    SubscriptionID        INT               NULL,
    CONSTRAINT PK_Payment PRIMARY KEY (PaymentID),
    CONSTRAINT CK_Payment_Amount CHECK (Amount > 0),
    CONSTRAINT CK_Payment_Type CHECK (PaymentType IN ('Deposit', 'Full payment', 'Remaining balance', 'Subscription')),
    CONSTRAINT CK_Payment_Method CHECK (PaymentMethod IN ('Mada', 'Credit card', 'Apple Pay', 'STC Pay', 'Cash', 'Bank transfer')),
    CONSTRAINT CK_Payment_Status CHECK (PaymentStatus IN ('Pending', 'Paid', 'Failed', 'Refunded')),
    CONSTRAINT CK_Payment_Target CHECK (
        (BookingID IS NOT NULL AND SubscriptionID IS NULL AND PaymentType <> 'Subscription'
            AND CommissionAmount = ROUND(Amount * 0.10, 2) AND ProviderAmount = Amount - CommissionAmount)
     OR (SubscriptionID IS NOT NULL AND BookingID IS NULL AND PaymentType = 'Subscription'
            AND CommissionAmount IS NULL AND ProviderAmount IS NULL AND PaymentMethod NOT IN ('Cash'))),
    CONSTRAINT CK_Payment_Offline CHECK (PaymentMethod NOT IN ('Cash', 'Bank transfer') OR PaymentType IN ('Remaining balance', 'Subscription')),
    CONSTRAINT CK_Payment_Paid CHECK ((PaymentStatus IN ('Paid', 'Refunded') AND PaidAt IS NOT NULL AND TransactionReference IS NOT NULL)
                                   OR (PaymentStatus IN ('Pending', 'Failed') AND PaidAt IS NULL)),
    CONSTRAINT FK_Payment_Booking FOREIGN KEY (BookingID) REFERENCES glowme.Booking (BookingID),
    CONSTRAINT FK_Payment_Subscription FOREIGN KEY (SubscriptionID) REFERENCES glowme.Subscription (SubscriptionID)
);
CREATE INDEX IX_Payment_Booking ON glowme.Payment (BookingID) WHERE BookingID IS NOT NULL;
CREATE INDEX IX_Payment_Subscription ON glowme.Payment (SubscriptionID) WHERE SubscriptionID IS NOT NULL;
CREATE UNIQUE INDEX UX_Payment_Reference ON glowme.Payment (TransactionReference) WHERE TransactionReference IS NOT NULL;
GO

/* ---------------------------------------------------------------------
   11. Availability — working windows (Open) and blocked windows (Blocked) per date
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Availability (
    AvailabilityID  INT IDENTITY(1,1) NOT NULL,
    ProviderID      INT               NOT NULL,
    AvailableDate   DATE              NOT NULL,
    StartTime       TIME(0)           NOT NULL,
    EndTime         TIME(0)           NOT NULL,
    SlotStatus      VARCHAR(10)       NOT NULL CONSTRAINT DF_Availability_Status DEFAULT ('Open'),
    CONSTRAINT PK_Availability PRIMARY KEY (AvailabilityID),
    CONSTRAINT CK_Availability_Time CHECK (EndTime > StartTime),
    CONSTRAINT CK_Availability_Status CHECK (SlotStatus IN ('Open', 'Blocked')),
    CONSTRAINT FK_Availability_Provider FOREIGN KEY (ProviderID) REFERENCES glowme.ServiceProvider (ProviderID)
);
CREATE INDEX IX_Availability_ProviderDay ON glowme.Availability (ProviderID, AvailableDate, StartTime);
GO

/* ---------------------------------------------------------------------
   12. Portfolio — photos of previous work
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Portfolio (
    PortfolioID  INT IDENTITY(1,1) NOT NULL,
    ProviderID   INT               NOT NULL,
    PhotoURL     NVARCHAR(500)     NOT NULL,              -- storage path (portfolio/{ProviderID}/…) or URL
    Caption      NVARCHAR(120)     NULL,
    UploadedAt   DATETIME2(0)      NOT NULL CONSTRAINT DF_Portfolio_Uploaded DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Portfolio PRIMARY KEY (PortfolioID),
    CONSTRAINT FK_Portfolio_Provider FOREIGN KEY (ProviderID) REFERENCES glowme.ServiceProvider (ProviderID)
);
CREATE INDEX IX_Portfolio_Provider ON glowme.Portfolio (ProviderID, UploadedAt DESC);
GO

/* ---------------------------------------------------------------------
   13. Favorite — PK (CustomerID, ProviderID) so a provider is saved once
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Favorite (
    CustomerID  INT          NOT NULL,
    ProviderID  INT          NOT NULL,
    AddedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_Favorite_Added DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Favorite PRIMARY KEY (CustomerID, ProviderID),
    CONSTRAINT FK_Favorite_Customer FOREIGN KEY (CustomerID) REFERENCES glowme.Customer (CustomerID),
    CONSTRAINT FK_Favorite_Provider FOREIGN KEY (ProviderID) REFERENCES glowme.ServiceProvider (ProviderID)
);
GO

/* ---------------------------------------------------------------------
   14. Review — 0..1 per completed booking, moderated by an admin
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Review (
    ReviewID      INT IDENTITY(1,1) NOT NULL,
    BookingID     INT               NOT NULL,
    AdminID       INT               NULL,                 -- the admin who hid / removed it
    Rating        INT               NOT NULL,
    Comment       NVARCHAR(800)     NULL,
    ReviewStatus  VARCHAR(10)       NOT NULL CONSTRAINT DF_Review_Status DEFAULT ('Visible'),
    CreatedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Review_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Review PRIMARY KEY (ReviewID),
    CONSTRAINT UQ_Review_Booking UNIQUE (BookingID),
    CONSTRAINT CK_Review_Rating CHECK (Rating BETWEEN 1 AND 5),
    CONSTRAINT CK_Review_Status CHECK (ReviewStatus IN ('Visible', 'Hidden', 'Removed')),
    CONSTRAINT CK_Review_Admin CHECK ((ReviewStatus = 'Visible' AND AdminID IS NULL) OR (ReviewStatus <> 'Visible' AND AdminID IS NOT NULL)),
    CONSTRAINT FK_Review_Booking FOREIGN KEY (BookingID) REFERENCES glowme.Booking (BookingID),
    CONSTRAINT FK_Review_Admin FOREIGN KEY (AdminID) REFERENCES glowme.Admin (AdminID)
);
GO

/* ---------------------------------------------------------------------
   15. Notification — addressed to one customer OR one provider
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Notification (
    NotificationID    INT IDENTITY(1,1) NOT NULL,
    CustomerID        INT               NULL,
    ProviderID        INT               NULL,
    NotificationType  VARCHAR(25)       NOT NULL,
    Message           NVARCHAR(400)     NOT NULL,
    IsRead            BIT               NOT NULL CONSTRAINT DF_Notification_Read DEFAULT (0),
    CreatedAt         DATETIME2(0)      NOT NULL CONSTRAINT DF_Notification_Created DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Notification PRIMARY KEY (NotificationID),
    CONSTRAINT CK_Notification_Target CHECK ((CustomerID IS NOT NULL AND ProviderID IS NULL) OR (CustomerID IS NULL AND ProviderID IS NOT NULL)),
    CONSTRAINT CK_Notification_Type CHECK (NotificationType IN ('Booking request', 'Booking accepted', 'Booking rejected', 'Booking rescheduled',
        'Booking cancelled', 'Booking completed', 'Payment', 'Message', 'Review', 'Subscription')),
    CONSTRAINT FK_Notification_Customer FOREIGN KEY (CustomerID) REFERENCES glowme.Customer (CustomerID),
    CONSTRAINT FK_Notification_Provider FOREIGN KEY (ProviderID) REFERENCES glowme.ServiceProvider (ProviderID)
);
CREATE INDEX IX_Notification_Customer ON glowme.Notification (CustomerID, IsRead, CreatedAt DESC) WHERE CustomerID IS NOT NULL;
CREATE INDEX IX_Notification_Provider ON glowme.Notification (ProviderID, IsRead, CreatedAt DESC) WHERE ProviderID IS NOT NULL;
GO

/* ---------------------------------------------------------------------
   16. Message — conversation between one customer and one provider
   --------------------------------------------------------------------- */
CREATE TABLE glowme.Message (
    MessageID    INT IDENTITY(1,1) NOT NULL,
    CustomerID   INT               NOT NULL,
    ProviderID   INT               NOT NULL,
    SenderType   VARCHAR(10)       NOT NULL,
    MessageText  NVARCHAR(1000)    NOT NULL,
    IsRead       BIT               NOT NULL CONSTRAINT DF_Message_Read DEFAULT (0),
    SentAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_Message_Sent DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_Message PRIMARY KEY (MessageID),
    CONSTRAINT CK_Message_Sender CHECK (SenderType IN ('Customer', 'Provider')),
    CONSTRAINT FK_Message_Customer FOREIGN KEY (CustomerID) REFERENCES glowme.Customer (CustomerID),
    CONSTRAINT FK_Message_Provider FOREIGN KEY (ProviderID) REFERENCES glowme.ServiceProvider (ProviderID)
);
CREATE INDEX IX_Message_Thread ON glowme.Message (CustomerID, ProviderID, SentAt);
GO

PRINT 'glowme_db: 16 tables created in schema glowme.';
GO
