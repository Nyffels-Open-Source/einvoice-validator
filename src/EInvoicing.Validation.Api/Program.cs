using EInvoicing.Validation.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddValidationServices(builder.Configuration);

var app = builder.Build();

app.MapOpenApi("/openapi/v1.json");
app.MapScalarApiReference();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
