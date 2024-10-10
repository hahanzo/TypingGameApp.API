using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TypingGameApp.API.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using TypingGameApp.API.Controllers;
using TypingGameApp.API.Service;
using TypingGameApp.API.Hubs;

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddIdentityApiEndpoints<AppUser>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddSignalR();

builder.Services.Configure<IdentityOptions>(optins =>
{
    optins.Password.RequireDigit = false;
    optins.Password.RequireLowercase = false;
    optins.Password.RequireUppercase = false;
    optins.User.RequireUniqueEmail = true;
});

builder.Services.AddDbContextPool<AppDbContext>(
      options => options.UseMySql(builder.Configuration.GetConnectionString("DevDB"), new MySqlServerVersion(new Version(8,0,39))));
builder.Services.AddTransient<TextService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins("http://localhost:4200")
                            .AllowCredentials()
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                      });
});

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthorization();

app.MapControllers();
app.MapGroup("/api")
   .MapIdentityApi<AppUser>();
app.MapGroup("/api")
   .MapIdentityUserEndpoints();

app.MapHub<GameHub>("/gamehub");
app.Run();
