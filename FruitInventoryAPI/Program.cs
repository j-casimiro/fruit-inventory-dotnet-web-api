using Oracle.ManagedDataAccess.Client;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IDbConnection>(sp =>
    new OracleConnection(builder.Configuration.GetConnectionString("OracleDb")));

builder.Services.AddControllers();

// this is just to allow running on the local server, this should be disabled in the production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("AllowReactApp");

app.MapControllers();

app.Run();