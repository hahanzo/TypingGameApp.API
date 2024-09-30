using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TypingGameApp.API.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddIdentityApiEndpoints<AppUser>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.Configure<IdentityOptions>(optins =>
{
    optins.User.RequireUniqueEmail = true;
});

builder.Services.AddDbContextPool<AppDbContext>(
      options => options.UseMySql(builder.Configuration.GetConnectionString("DevDB"), new MySqlServerVersion(new Version(8,0,39))));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app
    .MapGroup("/api")
    .MapIdentityApi<AppUser>();

app.MapPost("api/signup", async (
    UserManager<AppUser> userManger,
    [FromBody] UserRegistrationModel userRegistrationModel
    ) =>
    {
        AppUser user = new AppUser()
        {
            UserName = userRegistrationModel.UserName,
            Email = userRegistrationModel.Email,
            RegisteredOn = userRegistrationModel.RegisteredOn,
        };
        var result = await userManger.CreateAsync(
            user,
            userRegistrationModel.Password);
        if (result.Succeeded)
            return Results.Ok(result);
        else
            return Results.BadRequest(result);
    });

app.Run();

public class UserRegistrationModel
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public DateTime RegisteredOn { get; set; }
}
