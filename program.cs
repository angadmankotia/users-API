
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ----- Configuration (for demo keep here) -----
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ThisIsADemoSecretKey_change_in_prod";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "UsersApiDemo";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "UsersApiClients";

// Add services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=users.db"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    // Seed sample data
    if (!db.Users.Any())
    {
        db.Users.AddRange(new User { Name = "Alice", Email = "alice@example.com", Age = 28 },
                          new User { Name = "Bob", Email = "bob@example.com", Age = 35 });
        db.SaveChanges();
    }
}

// Middleware: Request logging
app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var method = context.Request.Method;
    var path = context.Request.Path + context.Request.QueryString;
    Console.WriteLine($"[Request] {method} {path} - Start");

    await next();

    sw.Stop();
    var status = context.Response.StatusCode;
    Console.WriteLine($"[Request] {method} {path} - Completed {status} in {sw.ElapsedMilliseconds}ms");
});

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// ----- Auth for demo: /login returns JWT for username/password -----
app.MapPost("/login", async (LoginRequest login, AppDbContext db) =>
{
    // WARNING: This is demo-only. Do not store plain-text passwords in prod.
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == login.Email);
    if (user == null) return Results.Unauthorized();

    // For demo accept any password if the user exists. Replace with real password check.
    var token = GenerateJwt(login.Email);
    return Results.Ok(new { token });
});

// ----- CRUD Endpoints for Users -----
app.MapGet("/users", async (AppDbContext db) =>
{
    var users = await db.Users.AsNoTracking().ToListAsync();
    return Results.Ok(users);
}).WithName("GetAllUsers");

app.MapGet("/users/{id:int}", async (int id, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user == null) return Results.NotFound();
    return Results.Ok(user);
}).WithName("GetUserById");

// Create user - requires authentication
app.MapPost("/users", async (UserCreateDto dto, AppDbContext db) =>
{
    var validationErrors = ValidateUserCreateDto(dto);
    if (validationErrors.Any()) return Results.BadRequest(new { errors = validationErrors });

    var exists = await db.Users.AnyAsync(u => u.Email == dto.Email);
    if (exists) return Results.Conflict(new { message = "Email already in use" });

    var user = new User { Name = dto.Name.Trim(), Email = dto.Email.Trim().ToLowerInvariant(), Age = dto.Age };
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/users/{user.Id}", user);
}).RequireAuthorization();

// Update user - requires authentication
app.MapPut("/users/{id:int}", async (int id, UserUpdateDto dto, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user == null) return Results.NotFound();

    var validationErrors = ValidateUserUpdateDto(dto);
    if (validationErrors.Any()) return Results.BadRequest(new { errors = validationErrors });

    if (!string.IsNullOrWhiteSpace(dto.Name)) user.Name = dto.Name.Trim();
    if (!string.IsNullOrWhiteSpace(dto.Email)) user.Email = dto.Email.Trim().ToLowerInvariant();
    if (dto.Age.HasValue) user.Age = dto.Age.Value;

    await db.SaveChangesAsync();
    return Results.Ok(user);
}).RequireAuthorization();

// Delete user - requires authentication
app.MapDelete("/users/{id:int}", async (int id, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user == null) return Results.NotFound();
    db.Users.Remove(user);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.Run();


string GenerateJwt(string email)
{
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, email),
        new Claim(JwtRegisteredClaimNames.Email, email),
        new Claim("role", "user")
    };
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(
        issuer: jwtIssuer,
        audience: jwtAudience,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(6),
        signingCredentials: creds);
    return new JwtSecurityTokenHandler().WriteToken(token);
}

List<string> ValidateUserCreateDto(UserCreateDto dto)
{
    var errors = new List<string>();
    if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length < 2) errors.Add("Name must be at least 2 characters.");
    if (string.IsNullOrWhiteSpace(dto.Email) || !new EmailAddressAttribute().IsValid(dto.Email)) errors.Add("A valid email is required.");
    if (dto.Age < 0 || dto.Age > 150) errors.Add("Age must be between 0 and 150.");
    return errors;
}

List<string> ValidateUserUpdateDto(UserUpdateDto dto)
{
    var errors = new List<string>();
    if (dto.Name != null && dto.Name.Trim().Length > 0 && dto.Name.Trim().Length < 2) errors.Add("Name must be at least 2 characters.");
    if (dto.Email != null && !new EmailAddressAttribute().IsValid(dto.Email)) errors.Add("If supplied, email must be valid.");
    if (dto.Age.HasValue && (dto.Age < 0 || dto.Age > 150)) errors.Add("If supplied, age must be between 0 and 150.");
    return errors;
}

public class LoginRequest
{
    [Required]
    public string Email { get; set; } = default!;
    [Required]
    public string Password { get; set; } = default!; // demo only
}

public class UserCreateDto
{
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public int Age { get; set; }
}

public class UserUpdateDto
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public int? Age { get; set; }
}

public class User
{
    [Key]
    public int Id { get; set; }
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;
    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = default!;
    public int Age { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users { get; set; } = default!;
}

