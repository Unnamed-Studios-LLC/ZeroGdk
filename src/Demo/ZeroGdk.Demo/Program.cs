using Google.Protobuf.WellKnownTypes;
using ZeroGdk.Demo;
using ZeroGdk.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddZeroGdk();
builder.Services.AddGrpcReflection();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapGrpcReflectionService();
}

app.UseHttpsRedirection();
app.UseZeroGdk();

app.Run();