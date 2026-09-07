var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AuthService>();
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowReact");
app.UseAuthorization();

app.MapControllers();

app.Run();